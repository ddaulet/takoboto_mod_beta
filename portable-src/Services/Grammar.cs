using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TakobotoApp
{
    /// <summary>One example sentence of a grammar construction: the Japanese sentence and its English translation.</summary>
    public class GExample
    {
        public string Ja = "";
        public string En = "";
    }

    /// <summary>A meaning / form of a construction: "1 ...", "2a ...". Has its connection patterns, an explanation in Japanese and in
    /// English and the examples.</summary>
    public class GSection
    {
        public int Sense;                          // 1, 2, ... (0: before the first numbered meaning)
        public string Form = "";                   // a, b, c ...
        public string Head = "";
        public List<string> Patterns = new List<string>();
        public string ExplJa = "";
        public string ExplEn = "";
        public List<GExample> Examples = new List<GExample>();
    }

    public class GEntry
    {
        public int Id;
        public string Title = "";                  // the headword, as in the table of contents ("あと１")
        public string Heading = "";
        public List<string> Patterns = new List<string>();
        public List<GSection> Sections = new List<GSection>();
        [XmlIgnore] public string Key = "";        // the headword without the variant number and the dots, for matching
        [XmlIgnore] public string SearchText = ""; // everything that can be searched, lower case
        public override string ToString() { return Title; }
    }

    public class GrammarData
    {
        public List<GEntry> Entries = new List<GEntry>();
    }

    /// <summary>The grammar section: a local data file, the folder "grammar" next to Takoboto.exe (grammar.xml). It is made from the books of
    /// the user for the user's own use and is NOT part of the public package: without the file the program has no Grammar page.</summary>
    public static class Grammar
    {
        public static string Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grammar");
        static string FilePath { get { return Path.Combine(Dir, "grammar.xml"); } }

        static bool? available;
        public static bool Available
        {
            get
            {
                if (available == null) available = File.Exists(FilePath);
                return available.Value;
            }
        }

        static GrammarData data;
        static Task loading;
        static readonly object Gate = new object();

        public static Task EnsureLoaded()
        {
            lock (Gate)
            {
                if (loading == null && Available) loading = Task.Run(() => Load());
                return loading ?? Task.FromResult(0);
            }
        }

        public static List<GEntry> Entries { get { return data == null ? new List<GEntry>() : data.Entries; } }
        public static bool Loaded { get { return data != null; } }

        static readonly Regex KeyClean = new Regex(@"[0-9０-９…～〜\.．　／/・＋+\s\(\)（）]");

        public static string KeyOf(string s)
        {
            return KeyClean.Replace(s ?? "", "");
        }

        static void Load()
        {
            try
            {
                using (var s = File.OpenRead(FilePath))
                {
                    var d = (GrammarData)new XmlSerializer(typeof(GrammarData)).Deserialize(s);
                    foreach (var e in d.Entries)
                    {
                        e.Key = KeyOf(e.Title);
                        var sb = new System.Text.StringBuilder();
                        sb.Append(e.Title).Append(' ').Append(e.Heading).Append(' ');
                        foreach (var p in e.Patterns) sb.Append(p).Append(' ');
                        foreach (var sec in e.Sections)
                        {
                            sb.Append(sec.Head).Append(' ').Append(sec.ExplEn).Append(' ').Append(sec.ExplJa).Append(' ');
                            foreach (var p in sec.Patterns) sb.Append(p).Append(' ');
                        }
                        e.SearchText = sb.ToString().ToLowerInvariant();
                    }
                    data = d;
                }
            }
            catch (Exception ex) { App.Log(ex); data = new GrammarData(); }
        }

        /// <summary>The constructions that are written like this reading / writing (a word card uses it to link to the grammar page).</summary>
        public static List<GEntry> FindByWord(IEnumerable<string> writings)
        {
            var r = new List<GEntry>();
            if (data == null) return r;
            var keys = new HashSet<string>(writings.Select(KeyOf).Where(k => k.Length > 0));
            foreach (var e in data.Entries)
                if (keys.Contains(e.Key)) r.Add(e);
            return r;
        }

        public static IEnumerable<GEntry> Search(string query)
        {
            if (data == null) return Enumerable.Empty<GEntry>();
            query = (query ?? "").Trim().ToLowerInvariant();
            if (query.Length == 0) return data.Entries;
            string key = KeyOf(query);
            var starts = data.Entries.Where(e => key.Length > 0 && e.Key.StartsWith(key));
            var inTitle = data.Entries.Where(e => key.Length > 0 && e.Key.Contains(key) && !e.Key.StartsWith(key));
            var inText = data.Entries.Where(e => e.SearchText.Contains(query) && !(key.Length > 0 && e.Key.Contains(key)));
            return starts.Concat(inTitle).Concat(inText);
        }

        public static GEntry ById(int id)
        {
            return data == null ? null : data.Entries.FirstOrDefault(e => e.Id == id);
        }
    }
}
