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

        public static SearchOutcome Search(string text)
        {
            var o = new SearchOutcome();
            lock (Gate)
            {
                var r = db.PrepareSearch(text, o.Exact, o.Partial, Options);
                db.Search(r, 32);
            }
            return o;
        }

        public static Word LoadWord(WordTag tag) { lock (Gate) return db.LoadWord(tag, Options); }

        public static WordTag TagById(int id) { lock (Gate) return db.LoadWordById(id, Options); }

        public static WordKanji Kanji(char c) { lock (Gate) return db.LoadKanji(c, true); }

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

        public static List<Sentence> MoreSentences(Word w)
        {
            var list = new List<Sentence>();
            lock (Gate)
            {
                var s = db.PrepareSentencesSearch(w, list);
                db.SearchOtherSentences(s, 64, Options);
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

            string meaning = (t.TranslationTextPrimary ?? "").Trim();
            string more = (t.TranslationTextSecondary ?? "").Trim().TrimStart(',', ' ');
            item.Meaning = more.Length > 0 ? meaning + "; " + more : meaning;
            if (t.Conjugated && !string.IsNullOrEmpty(t.ConjugatedText)) item.Conjugation = "Conjugated: " + t.ConjugatedText;
            if (removable) item.RemoveVis = System.Windows.Visibility.Visible;
            return item;
        }
    }
}
