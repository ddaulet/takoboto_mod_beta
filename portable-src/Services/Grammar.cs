using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TakobotoApp
{
    /// <summary>One example sentence of a grammar construction: the Japanese sentence and its English and Chinese translations.</summary>
    public class GExample
    {
        public string Ja = "";
        public string En = "";
        public string Zh = "";
        public string Tok = "";      // the words and readings of Ja (see WordView.TokenText)
    }

    /// <summary>One meaning / pattern of a construction ("1a Nのあいだ〈空間〉"): its connection patterns, the explanation in Japanese, English and
    /// Chinese, and the examples.</summary>
    public class GSection
    {
        public int Id;
        public string Label = "";
        public string Connection = "";
        public string ExplJa = "";
        public string ExplEn = "";
        public string ExplZh = "";
        public string TokJa = "";    // the words and readings of ExplJa
        public List<GExample> Examples = new List<GExample>();
    }

    public class GEntry
    {
        public int Id;
        public string Title = "";                  // the headword ("あと１")
        public List<GSection> Sections = new List<GSection>();
        public List<GEntry> ReferencedBy = new List<GEntry>();   // the constructions whose texts point to this one ("→【…】")
        public string Key = "";        // the headword without the variant number and the dots, for matching
        public string SearchText = ""; // everything that can be searched, lower case
        public string Subtitle { get; set; }   // a short line under the title in the list (a property: it is bound in the list)
        public string TitleText { get { return Title; } }
        public override string ToString() { return Title; }
    }

    public class GrammarData
    {
        public List<GEntry> Entries = new List<GEntry>();
    }

    /// <summary>The grammar section: the grammar books in our own database (tables grammar_entries / grammar_senses / grammar_examples; an example is
    /// a sentence of the same table as the dictionary's). They are the user's own books and are NOT part of the public database: without them the
    /// program has no Grammar page.</summary>
    public static class Grammar
    {
        static bool? available;
        public static bool Available
        {
            get
            {
                if (available == null)
                {
                    try { available = int.Parse(Db.Sql.Scalar("SELECT COUNT(*) FROM grammar_entries") ?? "0") > 0; }
                    catch { available = false; }
                }
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

        /// <summary>A reference to another construction in a text: →【てあげる】</summary>
        public static readonly Regex RefRe = new Regex(@"→?\s*【([^】]+)】");

        static readonly Regex KeyClean = new Regex(@"[0-9０-９₀-₉…～〜\.．　／/・＋+\s\(\)（）]");   // also the small numbers of the headwords (あと₁)

        public static string KeyOf(string s)
        {
            return KeyClean.Replace(s ?? "", "");
        }

        static void Load()
        {
            try
            {
                var d = new GrammarData();
                var entries = new Dictionary<int, GEntry>();
                var sections = new Dictionary<int, GSection>();
                Db.Sql.Each("SELECT id, title FROM grammar_entries ORDER BY id", r =>
                {
                    var e = new GEntry { Id = r.Int(0), Title = r.Str(1) ?? "" };
                    entries[e.Id] = e; d.Entries.Add(e);
                });
                Db.Sql.Each("SELECT id, entry_id, label, connection, expl_ja, expl_en, expl_zh, tok_ja FROM grammar_senses ORDER BY entry_id, ord", r =>
                {
                    GEntry e;
                    if (!entries.TryGetValue(r.Int(1), out e)) return;
                    var sec = new GSection { Id = r.Int(0), Label = r.Str(2) ?? "", Connection = r.Str(3) ?? "", ExplJa = r.Str(4) ?? "", ExplEn = r.Str(5) ?? "", ExplZh = r.Str(6) ?? "", TokJa = r.Str(7) ?? "" };
                    sections[sec.Id] = sec; e.Sections.Add(sec);
                });
                Db.Sql.Each("SELECT g.sense_id, s.ja, s.tok, en.text, zh.text FROM grammar_examples g JOIN sentences s ON s.id = g.sentence_id " +
                            "LEFT JOIN sentence_tr en ON en.sentence_id = s.id AND en.lang = 'en' LEFT JOIN sentence_tr zh ON zh.sentence_id = s.id AND zh.lang = 'zh' ORDER BY g.sense_id, g.ord", r =>
                {
                    GSection sec;
                    if (sections.TryGetValue(r.Int(0), out sec))
                        sec.Examples.Add(new GExample { Ja = r.Str(1) ?? "", Tok = r.Str(2) ?? "", En = r.Str(3) ?? "", Zh = r.Str(4) ?? "" });
                });
                foreach (var e in d.Entries)
                {
                    e.Key = KeyOf(e.Title);
                    var sb = new System.Text.StringBuilder();
                    sb.Append(e.Title).Append(' ');
                    foreach (var sec in e.Sections)
                        sb.Append(sec.Label).Append(' ').Append(sec.Connection).Append(' ').Append(sec.ExplEn).Append(' ').Append(sec.ExplJa).Append(' ').Append(sec.ExplZh).Append(' ');
                    e.SearchText = sb.ToString().ToLowerInvariant();
                    e.Subtitle = "";   // later: a short explanation of the construction (its own column of the data)
                }
                data = d;
                FindReferences(d);
            }
            catch (Exception ex) { App.Log(ex); data = new GrammarData(); }
        }

        /// <summary>Every construction gets the list of the constructions that refer to it, so that its page can say where it is mentioned.</summary>
        static void FindReferences(GrammarData d)
        {
            foreach (var e in d.Entries)
            {
                var texts = new List<string>();
                foreach (var sec in e.Sections) { texts.Add(sec.Label); texts.Add(sec.Connection); texts.Add(sec.ExplJa); texts.Add(sec.ExplEn); texts.Add(sec.ExplZh); }
                foreach (string t in texts)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    foreach (Match m in RefRe.Matches(t))
                    {
                        var target = FindTitle(m.Groups[1].Value);
                        if (target != null && target != e && !target.ReferencedBy.Contains(e)) target.ReferencedBy.Add(e);
                    }
                }
            }
            foreach (var e in d.Entries) e.ReferencedBy.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        static readonly Regex TagRe = new Regex("〈([^〉]+)〉");

        // the meaning tags of the book ("空間", "時間"), or else the first words of the English explanation
        static string MakeSubtitle(GEntry e)
        {
            var tags = new List<string>();
            foreach (var sec in e.Sections)
                foreach (Match m in TagRe.Matches(sec.Label ?? ""))
                    if (!tags.Contains(m.Groups[1].Value)) tags.Add(m.Groups[1].Value);
            if (tags.Count > 0) return string.Join(" · ", tags);
            foreach (var sec in e.Sections)
            {
                string t = (sec.ExplEn ?? "").Replace("\n", " ").Trim();
                if (t.Length == 0 || t.StartsWith("→")) continue;
                int dot = t.IndexOf(". ", StringComparison.Ordinal);
                if (dot > 8) t = t.Substring(0, dot);
                return t.Length > 70 ? t.Substring(0, 70) + "…" : t;
            }
            return "";
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

        static bool IsHan(char c) { return (c >= '一' && c <= '鿿') || c == '々'; }

        public static string SentenceKey(string s)
        {
            return new string((s ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray());
        }

        /// <summary>The example sentences of the grammar books that contain one of these words (as a word: a kanji writing must not be part of a longer
        /// kanji compound; a kana-only word must have three letters or more). Used by the word card; sentences that are in the data twice come once.</summary>
        public static List<GExample> ExamplesFor(IEnumerable<string> writings, int max = 60)
        {
            var r = new List<GExample>();
            if (data == null) return r;
            var ws = writings.Where(x => !string.IsNullOrEmpty(x) && (x.Any(IsHan) || x.Length >= 3)).Distinct().ToList();
            if (ws.Count == 0) return r;
            var seen = new HashSet<string>();
            foreach (var e in data.Entries)
                foreach (var sec in e.Sections)
                    foreach (var x in sec.Examples)
                    {
                        if (r.Count >= max) return r;
                        string ja = x.Ja;
                        bool hit = false;
                        foreach (var w in ws)
                        {
                            int i = ja.IndexOf(w, StringComparison.Ordinal);
                            while (i >= 0 && !hit)
                            {
                                bool okBefore = !IsHan(w[0]) || i == 0 || !IsHan(ja[i - 1]);
                                int end = i + w.Length;
                                bool okAfter = !IsHan(w[w.Length - 1]) || end >= ja.Length || !IsHan(ja[end]);
                                if (okBefore && okAfter) hit = true;
                                else i = ja.IndexOf(w, i + 1, StringComparison.Ordinal);
                            }
                            if (hit) break;
                        }
                        if (hit && seen.Add(SentenceKey(ja))) r.Add(x);
                    }
            return r;
        }

        public static string NormTitle(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s ?? "")
            {
                if (c >= '０' && c <= '９') sb.Append((char)('0' + (c - '０')));
                else if (c >= '₀' && c <= '₉') sb.Append((char)('0' + (c - '₀')));      // あと₁ = あと1
                else if (!char.IsWhiteSpace(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The construction a reference like 【てあげる】 or 【から1】 points to.</summary>
        public static GEntry FindTitle(string name)
        {
            if (data == null) return null;
            string n = NormTitle(name);
            var e = data.Entries.FirstOrDefault(x => NormTitle(x.Title) == n);
            if (e != null) return e;
            string k = KeyOf(name);
            return k.Length == 0 ? null : data.Entries.FirstOrDefault(x => x.Key == k);
        }

        public static GEntry ById(int id)
        {
            return data == null ? null : data.Entries.FirstOrDefault(e => e.Id == id);
        }
    }
}
