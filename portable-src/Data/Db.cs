using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TakobotoApp
{
    /// <summary>Our own dictionary database (data\takoboto.db): words, kanji, sentences. Everything the screens show is read here.</summary>
    public static class Db
    {
        static Sqlite sql;
        public static string DataFile { get; private set; }
        public static Sqlite Sql { get { return sql; } }

        public static void Open(string dataDir)
        {
            DataFile = Path.Combine(dataDir, "takoboto.db");
            if (!File.Exists(DataFile)) throw new FileNotFoundException("The dictionary database was not found.", DataFile);
            sql = Sqlite.OpenReadOnly(DataFile);
            Conjugator.Load(sql);
        }

        // ---- languages ---------------------------------------------------------------------------------------------------
        public static Language LanguageOf(string code)
        {
            switch (code)
            {
                case "en": return Language.English;
                case "fr": return Language.French;
                case "de": return Language.German;
                case "ru": return Language.Russian;
                case "zh": return Language.Chinese;
                case "ja": return Language.Japanese;
                default: return Language.English;
            }
        }

        public static string CodeOf(Language l)
        {
            switch (l) { case Language.French: return "fr"; case Language.German: return "de"; case Language.Russian: return "ru"; case Language.Chinese: return "zh"; case Language.Japanese: return "ja"; default: return "en"; }
        }

        static WordType TypeOf(int n) { return n >= 0 && n <= (int)WordType.VerbGozaru ? (WordType)n : WordType.NoType; }

        const int Chunk = 64;   // the ids are looked up this many at a time (the statement text stays the same, so it is prepared once)

        static IEnumerable<List<int>> Chunks(IList<int> ids)
        {
            for (int i = 0; i < ids.Count; i += Chunk)
            {
                var part = ids.Skip(i).Take(Chunk).ToList();
                while (part.Count < Chunk) part.Add(part[0]);
                yield return part;
            }
        }

        static readonly string InMarks = Sqlite.Marks(Chunk);

        // ---- result rows -----------------------------------------------------------------------------------------------------
        /// <summary>The words with their writings and meanings (what a result row needs), in the order of <paramref name="ids"/>.</summary>
        public static List<WordTag> Tags(IList<int> ids)
        {
            var forms = new Dictionary<int, List<WordPart>>();
            var meanings = new Dictionary<int, List<WordPart>>();
            foreach (var part in Chunks(ids))
            {
                var args = part.Cast<object>().ToArray();
                sql.Each("SELECT word_id, role, text, kind, is_primary, word_type, kana_word FROM word_forms WHERE word_id IN (" + InMarks + ") ORDER BY word_id, ord", r =>
                {
                    int id = r.Int(0);
                    List<WordPart> l;
                    if (!forms.TryGetValue(id, out l)) forms[id] = l = new List<WordPart>();
                    l.Add(new WordPart { WordId = id, Text = r.Str(2), EntryType = r.Int(3) == 1 ? EntryType.Kanji : EntryType.Kana, IsPrimary = r.Int(4) == 1, WordType = TypeOf(r.Int(5)), IsKanaWord = r.Int(6) == 1, Language = Language.Japanese });
                }, args);
                sql.Each("SELECT word_id, lang, text, is_primary FROM word_meanings WHERE word_id IN (" + InMarks + ") ORDER BY word_id, meaning_no, ord", r =>
                {
                    int id = r.Int(0);
                    List<WordPart> l;
                    if (!meanings.TryGetValue(id, out l)) meanings[id] = l = new List<WordPart>();
                    l.Add(new WordPart { WordId = id, Text = r.Str(2), Language = LanguageOf(r.Str(1)), IsPrimary = r.Int(3) == 1, EntryType = EntryType.Sense });
                }, args);
            }
            var result = new List<WordTag>();
            foreach (int id in ids)
            {
                List<WordPart> f, m;
                if (!forms.TryGetValue(id, out f)) continue;
                meanings.TryGetValue(id, out m);
                result.Add(new WordTag(id, false, null) { JapaneseParts = f, TranslationParts = m ?? new List<WordPart>() });
            }
            return result;
        }

        public static WordTag Tag(int id)
        {
            var l = Tags(new[] { id });
            return l.Count > 0 ? l[0] : null;
        }

        // ---- a whole word ----------------------------------------------------------------------------------------------------
        public static Word LoadWord(int id)
        {
            var w = new Word(id);
            var infos = new Dictionary<int, List<WordInfo>>();
            sql.Each("SELECT f.form_ord, i.code, i.text FROM form_infos f JOIN infos i ON i.id = f.info WHERE f.word_id = ? ORDER BY f.form_ord, f.ord", r =>
            {
                List<WordInfo> l;
                if (!infos.TryGetValue(r.Int(0), out l)) infos[r.Int(0)] = l = new List<WordInfo>();
                l.Add(new WordInfo { Code = r.Str(1), Text = r.Str(2) });
            }, id);
            sql.Each("SELECT ord, role, text, kind, is_primary, word_type, kana_word FROM word_forms WHERE word_id = ? ORDER BY ord", r =>
            {
                var p = new WordPart { WordId = id, Text = r.Str(2), EntryType = r.Int(3) == 1 ? EntryType.Kanji : EntryType.Kana, IsPrimary = r.Int(4) == 1, WordType = TypeOf(r.Int(5)), IsKanaWord = r.Int(6) == 1, Language = Language.Japanese };
                List<WordInfo> l;
                if (infos.TryGetValue(r.Int(0), out l)) p.Infos = l;
                (r.Int(1) == 0 ? w.MainEntries : w.Readings).Add(p);
            }, id);
            if (w.MainEntries.Count == 0 && w.Readings.Count == 0) return null;

            var minfo = new Dictionary<int, List<WordInfo>>();
            sql.Each("SELECT w.meaning_no, i.code, i.text FROM word_infos w JOIN infos i ON i.id = w.info WHERE w.word_id = ? ORDER BY w.meaning_no, w.ord", r =>
            {
                List<WordInfo> l;
                if (!minfo.TryGetValue(r.Int(0), out l)) minfo[r.Int(0)] = l = new List<WordInfo>();
                l.Add(new WordInfo { Code = r.Str(1), Text = r.Str(2) });
            }, id);
            var byNo = new SortedDictionary<int, WordMeaning>();
            sql.Each("SELECT meaning_no, lang, text, is_primary FROM word_meanings WHERE word_id = ? ORDER BY meaning_no, ord", r =>
            {
                WordMeaning m;
                if (!byNo.TryGetValue(r.Int(0), out m)) byNo[r.Int(0)] = m = new WordMeaning();
                m.Parts.Add(new WordPart { WordId = id, Text = r.Str(2), Language = LanguageOf(r.Str(1)), IsPrimary = r.Int(3) == 1, EntryType = EntryType.Sense });
            }, id);
            foreach (var kv in byNo)
            {
                List<WordInfo> l;
                if (minfo.TryGetValue(kv.Key, out l)) kv.Value.Infos = l;
                w.Meanings.Add(kv.Value);
            }

            // the kanji of the writings: the main writing first, then the other ones (a kanji that is only in the other writings is shown paler)
            var primaryChars = new List<char>();
            var otherChars = new List<char>();
            foreach (var p in w.MainEntries.Where(x => x.EntryType == EntryType.Kanji).OrderBy(x => x.IsPrimary ? 0 : 1))
                foreach (char c in p.Text)
                {
                    if (!Romaji.IsKanji(c)) continue;
                    var list = p.IsPrimary ? primaryChars : otherChars;
                    if (!primaryChars.Contains(c) && !otherChars.Contains(c)) list.Add(c);
                }
            foreach (char c in primaryChars.Concat(otherChars))
            {
                var k = Kanji(c);
                if (k == null) continue;
                var copy = new WordKanji { Kanji = k.Kanji, OnReading = k.OnReading, KunReading = k.KunReading, OrderValue = k.OrderValue, Meanings = k.Meanings, KanjiStrokes = k.KanjiStrokes };
                copy.IsSecondary = otherChars.Contains(c);
                copy.Opacity = copy.IsSecondary ? 0.5 : 1;
                w.Kanjis.Add(copy);
            }

            foreach (var s in SentencesOf(id, true, 100)) w.RecommendedSentences.Add(s);
            w.Forms = Conjugator.Make(w);
            return w;
        }

        // ---- kanji -----------------------------------------------------------------------------------------------------------
        static readonly Dictionary<char, WordKanji> kanjiCache = new Dictionary<char, WordKanji>();

        public static WordKanji Kanji(char c)
        {
            lock (kanjiCache)
            {
                WordKanji k;
                if (kanjiCache.TryGetValue(c, out k)) return k;
                var cs = c.ToString();
                string strokes = null;
                sql.Each("SELECT on_reading, kun_reading, order_value, strokes FROM kanji WHERE char = ?", r =>
                {
                    k = new WordKanji { Kanji = cs, OnReading = r.Str(0), KunReading = r.Str(1), OrderValue = (ushort)r.Int(2) };
                    strokes = r.Str(3);
                }, cs);
                if (k != null)
                {
                    sql.Each("SELECT lang, text FROM kanji_meanings WHERE char = ?", r => k.Meanings.Add(new KanjiMeaning { Language = LanguageOf(r.Str(0)), Text = r.Str(1) }), cs);
                    if (!string.IsNullOrEmpty(strokes))
                    {
                        var paths = strokes.Split('|');
                        for (int m = 0; m < paths.Length; m++)       // step m: the strokes 0..m, the last of them is the new one
                        {
                            var step = new KanjiStrokes();
                            for (int n = 0; n <= m; n++) step.Paths.Add(new StrokePath { Data = paths[n], IsLast = n == m });
                            k.KanjiStrokes.Add(step);
                        }
                    }
                }
                kanjiCache[c] = k;
                return k;
            }
        }

        /// <summary>The strokes of a kanji as SVG paths, one per stroke in writing order; null when there is none.</summary>
        public static List<string> StrokePaths(char c)
        {
            var k = Kanji(c);
            if (k == null || k.KanjiStrokes.Count == 0) return null;
            return k.KanjiStrokes.Select(step => step.Paths.FirstOrDefault(p => p.IsLast)).Where(p => p != null).Select(p => p.Data).ToList();
        }

        // ---- sentences -------------------------------------------------------------------------------------------------------
        /// <summary>The sentences with translations and the words in them.</summary>
        public static List<Sentence> Sentences(IList<int> ids)
        {
            var map = new Dictionary<int, Sentence>();
            foreach (var part in Chunks(ids))
            {
                var args = part.Cast<object>().ToArray();
                var pairs = Pairs();
                sql.Each("SELECT id, ja, parts FROM sentences WHERE id IN (" + InMarks + ")", r =>
                {
                    var s = new Sentence(r.Str(1)) { Id = r.Int(0) };
                    if (!r.IsNull(2))
                    {
                        var b = r.Bytes(2);       // the words of the sentence: 2 bytes each, the number of a pair (writing, reading)
                        for (int i = 0; i + 1 < b.Length; i += 2)
                        {
                            int n = (b[i] << 8) | b[i + 1];
                            if (n > 0 && n < pairs.Length) s.Parts.Add(new SentencePart(pairs[n][0], pairs[n][1], 0));
                        }
                    }
                    map[s.Id] = s;
                }, args);
                sql.Each("SELECT sentence_id, lang, text FROM sentence_tr WHERE sentence_id IN (" + InMarks + ") ORDER BY sentence_id, CASE lang WHEN 'en' THEN 0 WHEN 'fr' THEN 1 WHEN 'de' THEN 2 WHEN 'ru' THEN 3 ELSE 4 END", r =>
                {
                    Sentence s;
                    if (map.TryGetValue(r.Int(0), out s)) s.Translations.Add(new SentenceTranslation(LanguageOf(r.Str(1)), r.Str(2)));
                }, args);
            }
            var result = new List<Sentence>();
            foreach (int id in ids) { Sentence s; if (map.TryGetValue(id, out s)) result.Add(s); }
            return result;
        }

        /// <summary>The sentences of a word, the recommended ones first (that is the order they are stored in).</summary>
        public static List<Sentence> SentencesOf(int wordId, bool recommendedOnly, int max)
        {
            var all = SentenceIds(wordId);
            int rec = all.Item1;
            var ids = recommendedOnly ? all.Item2.Take(rec) : all.Item2.Skip(rec).Take(max);
            return Sentences(ids.Take(max).ToList());
        }

        /// <summary>The numbers of the sentences of a word (3 bytes each in one cell): how many of them are recommended, and all of them.</summary>
        static Tuple<int, List<int>> SentenceIds(int wordId)
        {
            int rec = 0;
            var ids = new List<int>();
            sql.Each("SELECT rec, ids FROM word_sentences WHERE word_id = ?", r =>
            {
                rec = r.Int(0);
                var b = r.Bytes(1);
                for (int i = 0; i + 2 < b.Length; i += 3) ids.Add((b[i] << 16) | (b[i + 1] << 8) | b[i + 2]);
            }, wordId);
            return Tuple.Create(rec, ids);
        }

        static string[][] pairs;

        /// <summary>The pairs (writing, reading) the words of the sentences are made of; the number of a pair is its place in the array.</summary>
        static string[][] Pairs()
        {
            lock (kanjiCache)
            {
                if (pairs == null)
                {
                    var l = new List<string[]> { new string[] { "", "" } };
                    sql.Each("SELECT id, writing, reading FROM ja_pairs ORDER BY id", r => { while (l.Count < r.Int(0)) l.Add(new string[] { "", "" }); l.Add(new string[] { r.Str(1) ?? "", r.Str(2) ?? "" }); });
                    pairs = l.ToArray();
                }
                return pairs;
            }
        }

        public static int SentenceCount(int wordId)
        {
            return SentenceIds(wordId).Item2.Count;
        }

        // ---- lists of words ----------------------------------------------------------------------------------------------------
        /// <summary>All words with their reading (hiragana), for the alphabetical index.</summary>
        public static List<KeyValuePair<string, int>> ListAll()
        {
            return sql.Query("SELECT kana_key, id FROM words", r => new KeyValuePair<string, int>(r.Str(0) ?? "", r.Int(1)));
        }

        static readonly Random rng = new Random();

        /// <summary>A random word; <paramref name="minSentences"/>: only words with at least that many example sentences.</summary>
        public static int RandomId(int minSentences)
        {
            string v = sql.Scalar("SELECT id FROM words WHERE rank >= ? AND id >= ? ORDER BY id LIMIT 1", minSentences, 1000000 + rng.Next(0, 1830000));
            if (v == null) v = sql.Scalar("SELECT id FROM words WHERE rank >= ? ORDER BY id LIMIT 1", minSentences);
            return v == null ? 0 : int.Parse(v);
        }

        // ---- radicals ------------------------------------------------------------------------------------------------------------
        static List<KanjiRadical> radicals;

        public static List<KanjiRadical> Radicals()
        {
            if (radicals == null)
                radicals = sql.Query("SELECT radical, strokes, kanji FROM radicals ORDER BY ord", r => new KanjiRadical(r.Str(0)[0], r.Int(1), r.Str(2)));
            return radicals;
        }
    }
}
