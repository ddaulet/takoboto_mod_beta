using System;
using System.Collections.Generic;
using System.Linq;

namespace TakobotoApp
{
    /// <summary>The search over our own database: Japanese (kanji, kana, katakana, romaji), conjugated verbs and adjectives, and the meanings in
    /// the languages the user ticked. Exact matches first, then words that start with the text, then words that contain it; the words
    /// with more example sentences (the commoner ones) come first within each group.</summary>
    public static class WordSearch
    {
        const int ExactCap = 300;

        static string Upper(string prefix) { return prefix + "￿"; }

        static string Hira(string s) { return Romaji.KanaToHiragana(s).ToLowerInvariant(); }

        /// <summary>The ticked languages (5 placeholders: the last one is repeated).</summary>
        static object[] WantedLanguages()
        {
            var l = new List<object>();
            foreach (Language lang in new[] { Language.English, Language.French, Language.German, Language.Russian, Language.Chinese })
                if (Langs.Wanted(lang)) l.Add(Db.CodeOf(lang));
            if (l.Count == 0) l.Add("en");
            while (l.Count < 5) l.Add(l[0]);
            return l.ToArray();
        }

        static List<int> Ids(string sql, params object[] args)
        {
            return Db.Sql.Query(sql, r => r.Int(0), args);
        }

        public static SearchOutcome Run(string raw, int max)
        {
            var o = new SearchOutcome();
            string t = (raw ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return o;
            bool japaneseText = Romaji.ContainsJapanese(t);
            bool latin = t.Any(c => (c >= 'a' && c <= 'z'));

            // ---- Japanese spellings of the text: as typed, and (romaji) as kana
            var ja = new List<string>();
            foreach (string k in Romaji.Variants(t)) if (k.Trim().Length > 0) ja.Add(Hira(k.Trim()));
            if (japaneseText) { string h = Hira(t); if (!ja.Contains(h)) ja.Add(h); }
            ja = ja.Distinct().ToList();

            var exact = new List<int>();
            var seen = new HashSet<int>();
            var marks = new Dictionary<int, string>();
            var conj = new Dictionary<int, string>();
            Action<IEnumerable<int>, string> addExact = (ids, mark) => { foreach (int id in ids) if (seen.Add(id)) { exact.Add(id); marks[id] = mark; } };

            // 0. the text as a meaning comes first when it is Latin letters ("age" = the English word, あげ = the same letters as romaji)
            var lang5 = WantedLanguages();
            Action addEnglish = () =>
            {
                bool phrase = t.All(c => c < 128) && t.Split(' ').Length <= 4 && !t.StartsWith("to ");
                var args = new List<object> { t, phrase ? "to " + t : t };
                args.AddRange(lang5); args.Add(t);
                var ids = Ids("SELECT m.word_id FROM word_meanings m JOIN words w ON w.id = m.word_id WHERE IFNULL(m.text_lc, m.text) IN (?,?) AND m.lang IN (?,?,?,?,?) GROUP BY m.word_id ORDER BY MIN(m.meaning_no), (MAX(IFNULL(m.text_lc, m.text) = ?)) DESC, w.rank DESC, m.word_id LIMIT " + ExactCap, args.ToArray());
                addExact(ids, t);
            };
            bool englishFirst = latin && !japaneseText;
            if (englishFirst) addEnglish();

            // 1. Japanese: the text is a writing or a reading of the word
            if (ja.Count > 0)
            {
                var keys = new List<object>(ja.Take(8).Cast<object>());
                while (keys.Count < 8) keys.Add(keys[0]);
                addExact(Ids("SELECT f.word_id FROM word_forms f JOIN words w ON w.id = f.word_id WHERE IFNULL(f.key, f.text) IN (?,?,?,?,?,?,?,?) GROUP BY f.word_id ORDER BY w.rank DESC, f.word_id LIMIT " + ExactCap, keys.ToArray()), ja[0]);
            }

            // 2. a conjugated verb / adjective
            foreach (string j in ja.Take(2))
            {
                var found = new List<int>();
                foreach (var cand in Conjugator.Deconjugate(j))
                    foreach (int id in Ids("SELECT DISTINCT word_id FROM word_forms WHERE IFNULL(key, text) = ? AND word_type = ?", Hira(cand.Dictionary), (int)cand.Type))
                        if (!seen.Contains(id) && !found.Contains(id)) { found.Add(id); conj[id] = raw.Trim(); }
                if (found.Count > 0)
                {
                    var rank = Ids("SELECT id FROM words WHERE id IN (" + string.Join(",", found) + ") ORDER BY rank DESC, id");
                    addExact(rank, j);
                }
            }

            // 3. the meaning is the text (or "to " + text for a verb)
            if (latin && !englishFirst && !(japaneseText && ja.Count == 0)) addEnglish();

            // ---- partial matches: starts with, then contains
            var partial = new List<int>();
            int want = max + exact.Count;     // a few more than needed: the exact ones are dropped from the list
            Action<IEnumerable<int>, string> addPartial = (ids, mark) =>
            {
                foreach (int id in ids)
                {
                    if (partial.Count >= max) break;
                    if (seen.Add(id)) { partial.Add(id); marks[id] = mark; }
                }
            };

            foreach (string j in ja.Take(2))
                addPartial(Ids("SELECT f.word_id FROM word_forms f JOIN words w ON w.id = f.word_id WHERE IFNULL(f.key, f.text) >= ? AND IFNULL(f.key, f.text) < ? GROUP BY f.word_id ORDER BY w.rank DESC, length(w.kana_key), f.word_id LIMIT ?", j, Upper(j), want), j);

            if (latin && partial.Count < max)
            {
                var args = new List<object> { t, Upper(t) };
                args.AddRange(lang5); args.Add(want);
                addPartial(Ids("SELECT m.word_id FROM word_meanings m JOIN words w ON w.id = m.word_id WHERE IFNULL(m.text_lc, m.text) >= ? AND IFNULL(m.text_lc, m.text) < ? AND m.lang IN (?,?,?,?,?) GROUP BY m.word_id ORDER BY w.rank DESC, m.word_id LIMIT ?", args.ToArray()), t);
            }

            if (partial.Count < max)
                foreach (string j in ja.Take(2))
                    if (j.Length >= 2 || partial.Count == 0)
                        addPartial(Ids("SELECT f.word_id FROM word_forms f JOIN words w ON w.id = f.word_id WHERE instr(IFNULL(f.key, f.text), ?) > 1 GROUP BY f.word_id ORDER BY w.rank DESC, length(w.kana_key), f.word_id LIMIT ?", j, want), j);

            if (latin && t.Length >= 3 && partial.Count < max)
            {
                var args = new List<object> { t };
                args.AddRange(lang5); args.Add(want);
                addPartial(Ids("SELECT m.word_id FROM word_meanings m JOIN words w ON w.id = m.word_id WHERE instr(IFNULL(m.text_lc, m.text), ?) > 1 AND m.lang IN (?,?,?,?,?) GROUP BY m.word_id ORDER BY w.rank DESC, m.word_id LIMIT ?", args.ToArray()), t);
            }

            // ---- the rows
            var all = exact.Concat(partial).ToList();
            var tags = Db.Tags(all).ToDictionary(x => x.WordId);
            var exactSet = new HashSet<int>(exact);
            foreach (int id in all)
            {
                WordTag tag;
                if (!tags.TryGetValue(id, out tag)) continue;
                tag.HighlightText = marks[id];
                string c;
                if (conj.TryGetValue(id, out c)) { tag.Conjugated = true; tag.ConjugatedText = c; }
                if (exactSet.Contains(id)) { tag.IsExactMatch = true; o.Exact.Add(tag); } else o.Partial.Add(tag);
            }
            o.Truncated = partial.Count >= max;
            return o;
        }
    }
}
