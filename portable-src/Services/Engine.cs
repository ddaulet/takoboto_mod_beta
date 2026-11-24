using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TakobotoApp
{
    public class SearchOutcome
    {
        public List<WordTag> Exact = new List<WordTag>();
        public List<WordTag> Partial = new List<WordTag>();
        public bool Truncated;   // more partial matches exist than were asked for
    }

    /// <summary>What the screens call to get words: a thin layer over our own database (Data\Db.cs) and the search (Data\WordSearch.cs).</summary>
    public static class Engine
    {
        public static string DataFile { get { return Db.DataFile; } }

        public static void Init(string dataDir) { Db.Open(dataDir); }

        /// <summary>One search. <paramref name="max"/> limits only the PARTIAL matches (words that merely start with / contain the text).</summary>
        public static SearchOutcome SearchRaw(string text, int max) { return WordSearch.Run(text, max); }

        // ---- search as a person types ---------------------------------------------------------------------------
        static string ToKana(string s)
        {
            try { return Romaji.ToHiragana(s, true) ?? ""; } catch { return ""; }
        }

        static bool HasLatin(string s) { foreach (char c in s) if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true; return false; }

        static bool RomajiLike(string s)
        {
            foreach (char c in s) if (!(c < 128 && (char.IsLetter(c) || c == '\'' || c == '-'))) return false;
            return s.Length > 0;
        }

        /// <summary>Romaji with an UNFINISHED last syllable ("nih", "食b"): it cannot be converted, so every
        /// completion is tried instead (nih -> には にひ にふ にへ にほ). Empty when there is nothing to complete.</summary>
        static List<string> Completions(string text)
        {
            var result = new List<string>();
            int cut = text.Length;
            while (cut > 0 && ToKana(text.Substring(0, cut)) == "") cut--;   // longest part that can be converted
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

        /// <summary>Only the words that really START with <paramref name="prefix"/> (a part of a non-existing compound, e.g. 食 and ば for 食ば,
        /// is just noise while a word is being typed).</summary>
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

        /// <summary>The search of the app. Also handles kanji mixed with Latin letters ("食be" -> 食べ), and romaji typed letter by letter
        /// ("nih" already shows 日本...).</summary>
        public static SearchOutcome Search(string text, int max = 32)
        {
            text = (text ?? "").Trim();
            bool japanese = Romaji.ContainsJapanese(text);
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
                    if (WordItem.From(t).Reading.Contains(reading) || (t.JapaneseParts ?? new List<WordPart>()).Any(p => p.Text == reading)) return t;
            return r.Exact[0];
        }

        public static Word LoadWord(WordTag tag) { return Db.LoadWord(tag.WordId); }

        public static WordTag TagById(int id) { return Db.Tag(id); }

        /// <summary>All the entries the text is exactly (a spelling or a reading), at most <paramref name="max"/>.</summary>
        public static List<WordTag> LookupAll(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<WordTag>();
            return SearchRaw(text.Trim(), 200).Exact.Take(max).ToList();
        }

        // ---- random words ------------------------------------------------------------------------------------------
        /// <summary>A word that has MANY example sentences is a word people use, so that is the measure of "common": at least this many.</summary>
        public const int CommonMinSentences = 25;

        static bool IsKatakanaWord(WordTag t)
        {
            string main = WordItem.From(t).Main ?? "";
            return main.Length > 0 && main.All(c => (c >= '゠' && c <= 'ヿ') || c == 'ｰ');
        }

        /// <summary>All words (reading in hiragana, id): for the alphabetical index.</summary>
        public static List<KeyValuePair<string, int>> ListAll() { return Db.ListAll(); }

        /// <summary>A random entry of the whole dictionary. <paramref name="commonOnly"/>: only words with many example
        /// sentences; <paramref name="skipKatakana"/>: not the words written in katakana (foreign words).</summary>
        public static WordTag RandomWord(bool commonOnly = false, bool skipKatakana = false)
        {
            WordTag any = null;
            for (int i = 0; i < 200; i++)
            {
                int id = Db.RandomId(commonOnly ? CommonMinSentences : 0);
                if (id == 0) continue;
                var t = Db.Tag(id);
                if (t == null) continue;
                if (any == null) any = t;
                if (skipKatakana && IsKatakanaWord(t)) continue;
                return t;
            }
            return any;   // nothing matched in a reasonable time: any word is better than none
        }

        /// <summary>Several different random words.</summary>
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

        public static WordKanji Kanji(char c) { return Db.Kanji(c); }

        /// <summary>The strokes of a kanji as SVG paths, one per stroke in writing order (KanjiVG). Null when there is no stroke data.</summary>
        public static List<string> StrokePaths(char c) { return Db.StrokePaths(c); }

        /// <summary>Most common kanji first.</summary>
        public static List<WordKanji> OrderedKanji(string chars, int max)
        {
            var list = new List<WordKanji>();
            foreach (char c in chars.Distinct())
            {
                var k = Db.Kanji(c);
                if (k != null) list.Add(k);
            }
            return list.OrderBy(k => k.OrderValue).Take(max).ToList();
        }

        /// <summary>More sentences than this are not loaded (the card then says 3000+).</summary>
        public const int SentenceCap = 3000;

        /// <summary>The example sentences of a word beyond the few recommended ones.</summary>
        public static List<Sentence> MoreSentences(Word w, int max = SentenceCap) { return Db.SentencesOf(w.WordId, false, max); }
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
            string meaning = "", more = "";
            if (t.TranslationParts != null && t.TranslationParts.Count > 0)
            {
                var shown = Langs.Pick(t.TranslationParts, p => p.Language);
                meaning = string.Join(", ", shown.Where(p => p.IsPrimary).Select(p => p.Text));
                more = string.Join(", ", shown.Where(p => !p.IsPrimary).Select(p => p.Text));
            }
            item.Meaning = more.Length == 0 ? meaning : meaning.Length == 0 ? more : meaning + "; " + more;
            if (t.Conjugated && !string.IsNullOrEmpty(t.ConjugatedText)) item.Conjugation = "Conjugated: " + t.ConjugatedText;
            if (removable) item.RemoveVis = System.Windows.Visibility.Visible;
            return item;
        }
    }
}
