using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TakobotoApp
{
    /// <summary>Conjugation of verbs and adjectives. The rules are DATA in the database (table conjugation: for every word type a stem + suffix
    /// for each form; they were learnt from the old dictionary and reproduce all its 12,311 verbs and adjectives exactly), so a new
    /// dictionary can be added without a line of code.</summary>
    public static class Conjugator
    {
        class Row { public string Form, Plain, Polite, Expl, AddPlain, AddExpl; }

        public class Candidate { public string Dictionary; public WordType Type; public string FormName; }

        static readonly Dictionary<string, List<Row>> templates = new Dictionary<string, List<Row>>();
        static readonly string[] formNames = typeof(WordForms).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
        static readonly Dictionary<WordType, int> cut = new Dictionary<WordType, int>
        {
            { WordType.IAdjective, 1 }, { WordType.VerbIchidan, 1 }, { WordType.VerbKu, 1 }, { WordType.VerbGu, 1 }, { WordType.VerbSu, 1 }, { WordType.VerbTsu, 1 },
            { WordType.VerbNu, 1 }, { WordType.VerbBu, 1 }, { WordType.VerbMu, 1 }, { WordType.VerbRu, 1 }, { WordType.VerbU, 1 }, { WordType.VerbIku, 1 },
            { WordType.VerbKuru, 2 }, { WordType.VerbSuru, 2 }, { WordType.VerbIrassharuAndOthers, 1 }, { WordType.VerbAru, 1 }, { WordType.VerbGozaru, 1 }
        };

        public static void Load(Sqlite sql)
        {
            templates.Clear();
            sql.Each("SELECT type, variant, form, plain, polite, expl, add_plain, add_expl FROM conjugation", r =>
            {
                string key = r.Str(0) + "|" + r.Str(1);
                List<Row> l;
                if (!templates.TryGetValue(key, out l)) templates[key] = l = new List<Row>();
                l.Add(new Row { Form = r.Str(2), Plain = r.Str(3) ?? "", Polite = r.Str(4) ?? "", Expl = r.Str(5) ?? "", AddPlain = r.Str(6) ?? "", AddExpl = r.Str(7) ?? "" });
            });
        }

        static string VariantOf(WordType type, string c)
        {
            if (type == WordType.IAdjective && c == "いい") return "ii";
            if ((type == WordType.VerbKuru || type == WordType.VerbSuru) && c.Length == 2) return "short";
            if (type == WordType.VerbIku && (c == "いく" || c == "行く")) return "iku";
            return "";
        }

        /// <summary>The last character a word of this type ends with in its dictionary form (い, る, く ...).</summary>
        static char EndingOf(WordType type)
        {
            switch (type)
            {
                case WordType.IAdjective: return 'い';
                case WordType.VerbKu: case WordType.VerbIku: return 'く';
                case WordType.VerbGu: return 'ぐ';
                case WordType.VerbSu: return 'す';
                case WordType.VerbTsu: return 'つ';
                case WordType.VerbNu: return 'ぬ';
                case WordType.VerbBu: return 'ぶ';
                case WordType.VerbMu: return 'む';
                case WordType.VerbU: return 'う';
                default: return 'る';
            }
        }

        static string Apply(string suffix, string stem)
        {
            if (string.IsNullOrEmpty(suffix)) return null;
            return suffix[0] == '=' ? suffix.Substring(1) : stem + suffix;
        }

        /// <summary>The forms of a word; null when it has none (not a verb / adjective, or one the dictionary does not conjugate).</summary>
        public static WordForms Make(Word w)
        {
            if (w.MainEntries.Count == 0) return null;
            var e0 = w.MainEntries[0];
            var type = e0.WordType;
            if (type == WordType.NoType || !cut.ContainsKey(type)) return null;
            string c = e0.Text;
            if (e0.IsKanaWord && w.Readings.Count > 0) c = w.Readings[0].Text;
            if (string.IsNullOrEmpty(c) || c.Length < 2 || c[c.Length - 1] != EndingOf(type)) return null;
            string variant = VariantOf(type, c);
            if (type == WordType.IAdjective && variant == "" && c.EndsWith("いい")) return null;
            List<Row> rows;
            if (!templates.TryGetValue(type + "|" + variant, out rows)) return null;
            string stem = variant == "ii" ? "" : c.Substring(0, c.Length - cut[type]);
            var forms = new WordForms();
            foreach (var row in rows)
            {
                var field = typeof(WordForms).GetField(row.Form);
                if (field == null) continue;
                var f = new WordForm(w, Apply(row.Plain, stem), Apply(row.Polite, stem), string.IsNullOrEmpty(row.Expl) ? null : row.Expl);
                if (row.AddPlain.Length > 0) f.AdditionalPlain = Apply(row.AddPlain, stem);
                if (row.AddExpl.Length > 0) f.AdditionalExplanation = row.AddExpl;
                field.SetValue(forms, f);
            }
            return forms;
        }

        // ---- the other way: which words does a conjugated text come from? ----------------------------------------------------------------
        class Suffix { public string Text; public WordType Type; public string Ending; public string Form; }
        static List<Suffix> suffixes;

        static List<Suffix> Suffixes()
        {
            if (suffixes != null) return suffixes;
            var list = new List<Suffix>();
            foreach (var kv in templates)
            {
                var parts = kv.Key.Split('|');
                WordType type;
                if (!Enum.TryParse(parts[0], out type) || parts[1] == "ii") continue;
                var nonpast = kv.Value.FirstOrDefault(r => r.Form == "Nonpast");
                if (nonpast == null || string.IsNullOrEmpty(nonpast.Plain) || nonpast.Plain[0] == '=') continue;
                foreach (var row in kv.Value)
                    foreach (string s in new[] { row.Plain, row.Polite, row.AddPlain })
                        if (!string.IsNullOrEmpty(s) && s[0] != '=' && s != nonpast.Plain)
                            list.Add(new Suffix { Text = s, Type = type, Ending = nonpast.Plain, Form = row.Form });
            }
            suffixes = list.GroupBy(x => x.Text + "|" + x.Type + "|" + x.Ending).Select(g => g.First()).OrderByDescending(x => x.Text.Length).ToList();
            return suffixes;
        }

        /// <summary>The dictionary forms a text can be a conjugation of (食べました -> 食べる).</summary>
        public static List<Candidate> Deconjugate(string text)
        {
            var result = new List<Candidate>();
            if (string.IsNullOrEmpty(text) || text.Length < 2) return result;
            var seen = new HashSet<string>();
            var inputs = new List<string> { text };
            if (text.IndexOf('来') >= 0)   // 来ます 来ない 来て: the old writing of the verb 来る is in kana in these forms
                foreach (string k in new[] { "き", "こ", "く" }) inputs.Add(text.Replace("来", k));
            foreach (string input in inputs)
                foreach (var s in Suffixes())
                {
                    if (input.Length <= s.Text.Length || !input.EndsWith(s.Text, StringComparison.Ordinal)) continue;
                    string dict = input.Substring(0, input.Length - s.Text.Length) + s.Ending;
                    if (dict == input || !seen.Add(dict + "|" + s.Type)) continue;
                    result.Add(new Candidate { Dictionary = dict, Type = s.Type, FormName = s.Form });
                }
            return result;
        }
    }
}
