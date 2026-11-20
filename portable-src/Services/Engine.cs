using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Takoboto.Database;
using Takoboto.Database.Impl;

namespace TakobotoApp
{
    /// <summary>The engine reads its single data file through this; here it is simply a file next to the exe.</summary>
    sealed class FileStreams : IStreamAccessor
    {
        readonly string dir;
        public FileStreams(string dir) { this.dir = dir; }
        public Stream OpenStream(string filename)
        {
            return new FileStream(Path.Combine(dir, filename), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }

    /// <summary>Runs everything on the calling thread; the app marshals to the UI thread itself.</summary>
    sealed class InlineDispatcher : IDispatcher
    {
        public bool IsNeeded { get { return false; } }
        public void Dispatch(Action action) { action(); }
    }

    public class SearchOutcome
    {
        public List<WordTag> Exact = new List<WordTag>();
        public List<WordTag> Partial = new List<WordTag>();
        public bool Truncated;   // more partial matches exist than were asked for
    }

    /// <summary>Thread-safe facade over the original Takoboto dictionary engine (Takoboto.Database.dll).</summary>
    public static class Engine
    {
        static IDatabase db;
        static readonly object Gate = new object();
        public static string DataFile { get; private set; }

        public static SearchOptions Options
        {
            get
            {
                return new SearchOptions
                {
                    RemoveFrench = !Store.Settings.French,
                    RemoveGerman = !Store.Settings.German,
                    RemoveRussian = !Store.Settings.Russian,
                };
            }
        }

        public static void Init(string dataDir)
        {
            DataFile = Path.Combine(dataDir, "takoboto.dat");
            if (!File.Exists(DataFile)) throw new FileNotFoundException("The dictionary data file was not found.", DataFile);
            db = DatabaseFactory.Load(new InlineDispatcher(), new FileStreams(dataDir));
        }

        /// <summary>One search exactly as the engine does it. <paramref name="max"/> limits only the PARTIAL matches
        /// (words that merely start with / contain the text); the original app used 32.</summary>
        public static SearchOutcome SearchRaw(string text, int max)
        {
            var o = new SearchOutcome();
            lock (Gate)
            {
                var r = db.PrepareSearch(text, o.Exact, o.Partial, Options);
                db.Search(r, max);
            }
            o.Truncated = o.Partial.Count >= max;
            return o;
        }

        // ---- search as a person types ---------------------------------------------------------------------------
        static string ToKana(string s)
        {
            try { return Takoboto.Database.Impl.StringEx.RomajiToHiragana(s, true) ?? ""; } catch { return ""; }
        }

        static bool HasLatin(string s) { foreach (char c in s) if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true; return false; }

        static bool RomajiLike(string s)
        {
            foreach (char c in s) if (!(c < 128 && (char.IsLetter(c) || c == '\'' || c == '-'))) return false;
            return s.Length > 0;
        }

        /// <summary>Romaji with an UNFINISHED last syllable ("nih", "食b"): the engine cannot convert it, so every
        /// completion is tried instead (nih -> には にひ にふ にへ にほ). Empty when there is nothing to complete.</summary>
        static List<string> Completions(string text)
        {
            var result = new List<string>();
            int cut = text.Length;
            while (cut > 0 && ToKana(text.Substring(0, cut)) == "") cut--;   // longest part the engine understands
            if (cut == 0) return result;                                     // nothing converted yet: leave it to the normal search
            string tail = text.Substring(cut);
            if (tail.Length == 0 || tail.Length > 3) return result;
            foreach (char c in tail) if (!char.IsLetter(c)) return result;
            foreach (char vowel in "aiueo")
            {
                string kana = ToKana(text + vowel);
                if (kana != "" && !result.Contains(kana)) result.Add(kana);
            }
            return result;
        }

        /// <summary>Only the words that really START with <paramref name="prefix"/> (the engine also returns the parts of a
        /// non-existing compound, e.g. 食 and ば for 食ば, which is just noise while a word is being typed).</summary>
        static SearchOutcome StartingWith(SearchOutcome o, string prefix)
        {
            Func<WordTag, bool> ok = t => (t.JapaneseParts ?? new List<WordPart>()).Any(p => p.Text != null && p.Text.StartsWith(prefix, StringComparison.Ordinal));
            var r = new SearchOutcome { Truncated = o.Truncated };
            r.Exact.AddRange(o.Exact.Where(ok));
            r.Partial.AddRange(o.Partial.Where(ok));
            return r;
        }

        /// <summary>Exact matches in the order of <paramref name="exactOrder"/>, then partial matches in the order of
        /// <paramref name="partialOrder"/>; every word once.</summary>
        static SearchOutcome Merge(IEnumerable<SearchOutcome> exactOrder, IEnumerable<SearchOutcome> partialOrder)
        {
            var o = new SearchOutcome();
            var seen = new HashSet<int>();
            foreach (var p in exactOrder) foreach (var t in p.Exact) if (seen.Add(t.WordId)) o.Exact.Add(t);
            foreach (var p in partialOrder) foreach (var t in p.Partial) if (seen.Add(t.WordId)) o.Partial.Add(t);
            o.Truncated = exactOrder.Any(p => p.Truncated) || partialOrder.Any(p => p.Truncated);
            return o;
        }

        /// <summary>The search of the app. Handles what the bare engine does not: kanji mixed with Latin letters
        /// ("食be" -> 食べ), and romaji typed letter by letter ("nih" already shows 日本...).</summary>
        public static SearchOutcome Search(string text, int max = 32)
        {
            text = (text ?? "").Trim();
            bool japanese = Takoboto.Database.Impl.StringEx.ContainsJapanese(text);
            bool latin = HasLatin(text);

            if (japanese && latin)   // mixed: the Latin part is turned into kana
            {
                string kana = ToKana(text);
                if (kana != "") return SearchRaw(kana, max);
                var completions = Completions(text);
                if (completions.Count > 0)
                {
                    var found = completions.Select(c => StartingWith(SearchRaw(c, max), c)).ToList();
                    return Merge(found, found);
                }
                int cut = text.Length;
                while (cut > 0 && ToKana(text.Substring(0, cut)) == "") cut--;
                return cut > 0 ? SearchRaw(ToKana(text.Substring(0, cut)), max) : SearchRaw(text, max);
            }

            var main = SearchRaw(text, max);
            if (!japanese && main.Exact.Count == 0 && text.Length <= 12 && RomajiLike(text))
            {
                var completions = Completions(text);
                if (completions.Count > 0)   // Japanese words that start with the completed syllable first, then the English matches
                {
                    var found = completions.Select(c => StartingWith(SearchRaw(c, max), c)).ToList();
                    return Merge(found.Concat(new[] { main }), found.Concat(new[] { main }));
                }
            }
            return main;
        }

        /// <summary>The dictionary entry for a word (used by the pop-up previews): an exact match, the one with the
        /// given reading when there are several.</summary>
        public static WordTag Lookup(string text, string reading = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var r = SearchRaw(text.Trim(), 200);
            if (r.Exact.Count == 0) return null;
            if (!string.IsNullOrEmpty(reading))
                foreach (var t in r.Exact)
                    if ((t.JapaneseTextPrimary ?? "").Contains(reading)) return t;
            return r.Exact[0];
        }

        public static Word LoadWord(WordTag tag) { lock (Gate) return db.LoadWord(tag, Options); }

        public static WordTag TagById(int id) { lock (Gate) return db.LoadWordById(id, Options); }

        /// <summary>All the entries the text is exactly (a spelling or a reading): the engine's own order, at most <paramref name="max"/>.</summary>
        public static List<WordTag> LookupAll(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<WordTag>();
            return SearchRaw(text.Trim(), 200).Exact.Take(max).ToList();
        }

        // ---- random words ------------------------------------------------------------------------------------------
        // The word ids of the dictionary are the JMdict entry numbers: 1,000,000 ... 2,822,600. Only about one in ten
        // numbers is an entry, almost all of them end in 0 or 5, so random multiples of 5 are tried until one exists.
        const int FirstId = 1000000, LastId = 2830000;
        static readonly Random rng = new Random();

        /// <summary>The engine has no frequency or "common word" tag. A word that has MANY example sentences is a word
        /// people use, so that is the measure: at least this many (the sample showed about 2% of all entries do).</summary>
        public const int CommonMinSentences = 25;

        static bool IsKatakanaWord(WordTag t)
        {
            string main = WordItem.From(t).Main ?? "";
            return main.Length > 0 && main.All(c => (c >= '゠' && c <= 'ヿ') || c == 'ｰ');
        }

        // call with the lock held
        static bool IsCommon(WordTag t)
        {
            var w = db.LoadWord(t, Options);
            int n = w.RecommendedSentences != null ? w.RecommendedSentences.Count : 0;
            var list = new List<Sentence>();
            var s = db.PrepareSentencesSearch(w, list);
            db.SearchOtherSentences(s, CommonMinSentences, Options);
            return n + list.Count >= CommonMinSentences;
        }

        /// <summary>A random entry of the whole dictionary. <paramref name="commonOnly"/>: only words with many example
        /// sentences; <paramref name="skipKatakana"/>: not the words written in katakana (foreign words).</summary>
        public static WordTag RandomWord(bool commonOnly = false, bool skipKatakana = false)
        {
            WordTag any = null;
            for (int i = 0; i < 6000; i++)
            {
                lock (Gate)
                {
                    var t = db.LoadWordById(rng.Next(FirstId / 5, LastId / 5 + 1) * 5, Options);
                    if (t == null) continue;
                    if (any == null) any = t;
                    if (skipKatakana && IsKatakanaWord(t)) continue;
                    if (commonOnly && !IsCommon(t)) continue;
                    return t;
                }
            }
            return any;   // nothing matched in a reasonable time: any word is better than none
        }

        /// <summary>Several different random words (the suggestions on the empty search page).</summary>
        public static List<WordTag> RandomWords(int count, bool commonOnly, bool skipKatakana)
        {
            var list = new List<WordTag>();
            for (int guard = 0; list.Count < count && guard < count * 4; guard++)
            {
                var t = RandomWord(commonOnly, skipKatakana);
                if (t != null && list.All(x => x.WordId != t.WordId)) list.Add(t);
            }
            return list;
        }

        public static WordKanji Kanji(char c) { lock (Gate) return db.LoadKanji(c, true); }

        /// <summary>The strokes of a kanji as SVG paths, one per stroke in writing order (the stroke-order data of the dictionary,
        /// KanjiVG). The dictionary hands them out only with a WORD, so one that contains the kanji is looked up. Null when
        /// the kanji has no stroke data (or no word).</summary>
        public static List<string> StrokePaths(char c)
        {
            lock (Gate)
            {
                var exact = new List<WordTag>(); var partial = new List<WordTag>();
                db.Search(db.PrepareSearch(c.ToString(), exact, partial, Options), 60);
                var tag = exact.Concat(partial).FirstOrDefault(t => (t.JapaneseParts ?? new List<WordPart>()).Any(p => p.Text != null && p.Text.IndexOf(c) >= 0));
                if (tag == null) return null;
                var w = db.LoadWord(tag, Options);
                var k = w.Kanjis == null ? null : w.Kanjis.FirstOrDefault(x => x.Kanji == c.ToString());
                if (k == null || k.KanjiStrokes == null || k.KanjiStrokes.Count == 0) return null;
                // every step shows all the strokes so far; the one flagged "last" is the stroke that was added in this step
                var paths = k.KanjiStrokes.Select(step => step.Paths.FirstOrDefault(p => p.IsLast)).Where(p => p != null).Select(p => p.Data).ToList();
                return paths.Count == 0 ? null : paths;
            }
        }

        /// <summary>Most common kanji first.</summary>
        public static List<WordKanji> OrderedKanji(string chars, int max)
        {
            var arr = chars.ToCharArray();
            lock (Gate) db.OrderKanjis(arr);
            var list = new List<WordKanji>();
            foreach (char c in arr.Take(max))
            {
                var k = Kanji(c);
                if (k != null) list.Add(k);
            }
            return list;
        }

        /// <summary>The example phrases of a word beyond the few recommended ones. The original app asked for 64; a common
        /// word has several hundred (食べる 770, 日本 1154) and they load in well under a second, so ask for up to 1000.</summary>
        public const int SentenceCap = 3000;   // more sentences than this are not loaded (the card then says 3000+)

        public static List<Sentence> MoreSentences(Word w, int max = SentenceCap)
        {
            var list = new List<Sentence>();
            lock (Gate)
            {
                var s = db.PrepareSentencesSearch(w, list);
                db.SearchOtherSentences(s, max, Options);
            }
            return list;
        }
    }

    /// <summary>What a result row shows, derived from a WordTag.</summary>
    public class WordItem
    {
        public WordTag Tag { get; private set; }
        public string Main { get; private set; }
        public string Reading { get; private set; }
        public string Others { get; private set; }
        public string Meaning { get; private set; }
        public string Mark { get; private set; }
        public string Conjugation { get; private set; }
        public System.Windows.Visibility OthersVis { get { return string.IsNullOrEmpty(Others) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible; } }
        public System.Windows.Visibility ConjugationVis { get { return string.IsNullOrEmpty(Conjugation) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible; } }
        public System.Windows.Visibility RemoveVis { get; set; }

        public WordItem() { RemoveVis = System.Windows.Visibility.Collapsed; }

        // the name a screen reader announces for a row
        public override string ToString() { return Main + (string.IsNullOrEmpty(Reading) ? "" : " (" + Reading + ")") + " - " + Meaning; }

        public static WordItem From(WordTag t, bool removable = false)
        {
            var parts = t.JapaneseParts ?? new List<WordPart>();
            var kanji = parts.Where(p => p.EntryType == EntryType.Kanji).ToList();
            var kana = parts.Where(p => p.EntryType == EntryType.Kana).ToList();
            var mainPart = kanji.FirstOrDefault(p => p.IsPrimary) ?? kanji.FirstOrDefault();
            var item = new WordItem { Tag = t, Mark = t.HighlightText };

            if (mainPart != null)
            {
                item.Main = mainPart.Text;
                var reads = kana.Where(p => p.IsPrimary).Select(p => p.Text).ToList();
                if (reads.Count == 0 && kana.Count > 0) reads.Add(kana[0].Text);
                item.Reading = string.Join("、", reads);
                var others = kanji.Where(p => p != mainPart).Select(p => p.Text)
                    .Concat(kana.Where(p => !reads.Contains(p.Text)).Select(p => p.Text));
                item.Others = string.Join("、", others);
            }
            else
            {
                var m = kana.FirstOrDefault(p => p.IsPrimary) ?? kana.FirstOrDefault() ?? parts.FirstOrDefault();
                item.Main = m != null ? m.Text : "";
                item.Reading = "";
                item.Others = string.Join("、", kana.Where(p => p != m).Select(p => p.Text));
            }

            // the meaning line, in the languages the user wants (the engine's own text mixes all languages)
            string meaning, more;
            if (t.TranslationParts != null && t.TranslationParts.Count > 0)
            {
                var shown = Langs.Pick(t.TranslationParts, p => p.Language);
                meaning = string.Join(", ", shown.Where(p => p.IsPrimary).Select(p => p.Text));
                more = string.Join(", ", shown.Where(p => !p.IsPrimary).Select(p => p.Text));
            }
            else
            {
                meaning = (t.TranslationTextPrimary ?? "").Trim();
                more = (t.TranslationTextSecondary ?? "").Trim().TrimStart(',', ' ');
            }
            item.Meaning = more.Length == 0 ? meaning : meaning.Length == 0 ? more : meaning + "; " + more;
            if (t.Conjugated && !string.IsNullOrEmpty(t.ConjugatedText)) item.Conjugation = "Conjugated: " + t.ConjugatedText;
            if (removable) item.RemoveVis = System.Windows.Visibility.Visible;
            return item;
        }
    }
}
