using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>The reading (furigana) of a run of kanji inside a sentence.</summary>
    public class RubyPiece
    {
        public int Offset;      // position in the sentence text
        public int Length;      // how many kanji
        public string Reading;  // hiragana
    }

    /// <summary>One dictionary word found in a sentence: where it stands in the text and the furigana of its kanji.</summary>
    public class WordMatch
    {
        public int Start, Length;        // the text of the sentence that belongs to the word
        public SentencePart Part;
        public List<RubyPiece> Ruby = new List<RubyPiece>();   // offsets are relative to the sentence text
        public bool Conjugated;          // the word stands in the sentence in a form other than its dictionary form
    }

    /// <summary>Finds the words of a sentence in its text and works out which reading belongs to which kanji.
    ///
    /// The engine tells which dictionary words a sentence contains (dictionary form + reading) but not where they stand
    /// in the text. So each word is looked for in the text: first the whole written form, then (for conjugated words such
    /// as 食べる -> 食べて) its stem, which must contain a kanji so that single kana never match by chance.
    ///
    /// Furigana are only given when they are CERTAIN: the dictionary reading is aligned with the written form (kanji runs
    /// versus the kana between them: 食べる / たべる -> 食 = た), and when there is any doubt - the alignment is not unique,
    /// the word is conjugated in a way that changes the reading of its kanji (来る: く -> き, こ; 良い: いい -> よ), a counter
    /// follows a number (三本: ほん -> ぼん) - NO furigana are shown for that word. Missing is better than wrong.</summary>
    public static class SentenceRuby
    {
        public static bool IsKanji(char c)
        {
            return (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || (c >= '豈' && c <= '﫿') || c == '々' || c == '〆' || c == '〇';
        }

        public static bool HasKanji(string s) { return !string.IsNullOrEmpty(s) && s.Any(IsKanji); }

        static bool IsNumeral(char c)
        {
            return (c >= '0' && c <= '9') || (c >= '０' && c <= '９') || "〇一二三四五六七八九十百千万億兆".IndexOf(c) >= 0;
        }

        static string ToHiragana(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c);
            return sb.ToString();
        }

        // a word that can be conjugated ends like a verb or an i-adjective (食べる 書く 泳ぐ 話す 待つ 死ぬ 遊ぶ 読む 高い)
        static bool CanConjugate(string w)
        {
            return w.Length > 1 && "うくぐすつぬぶむるい".IndexOf(w[w.Length - 1]) >= 0;
        }

        static bool IsHiragana(char c) { return c >= 'ぁ' && c <= 'ゟ'; }

        // After the stem of a conjugated word comes its ending (食べ|て, 行|った): hiragana. A kanji or katakana there means the
        // stem was found inside ANOTHER word (言|葉 is not 言う).
        static bool StemEndsOk(string text, int end)
        {
            return end >= text.Length || !(IsKanji(text[end]) || (text[end] >= '゠' && text[end] <= 'ヿ'));
        }

        /// <summary>The words of the sentence, in text order, with their furigana.
        /// <paramref name="onReadings"/> gives the on-readings of a kanji (katakana, comma separated); it is used to tell a
        /// lone kanji that is the tail of a compound (犯罪|人 にん) from a wrong "word" the dictionary analysis made of it (人 ひと).</summary>
        public static List<WordMatch> Analyze(string text, IList<SentencePart> parts, Func<char, string> onReadings = null)
        {
            var result = new List<WordMatch>();
            text = text ?? "";
            int cursor = 0;
            foreach (var part in parts ?? new List<SentencePart>())
            {
                int at = -1, len = 0;
                string w = part.Writing ?? "";
                for (int l = w.Length; l >= 1 && at < 0; l--)
                {
                    string stem = w.Substring(0, l);
                    if (l < w.Length && (!HasKanji(stem) || !CanConjugate(w))) break;   // kana-only stems are too ambiguous; only verbs / adjectives have stems
                    int i = text.IndexOf(stem, cursor, StringComparison.Ordinal);
                    while (i >= 0)
                    {
                        if (l == w.Length || StemEndsOk(text, i + l)) { at = i; len = l; break; }
                        i = text.IndexOf(stem, i + 1, StringComparison.Ordinal);
                    }
                }
                bool byWriting = at >= 0;
                if (at < 0 && !string.IsNullOrEmpty(part.Reading))        // written in kana in this sentence
                {
                    int i = text.IndexOf(part.Reading, cursor, StringComparison.Ordinal);
                    if (i >= 0) { at = i; len = part.Reading.Length; }
                }
                if (at < 0) continue;

                var m = new WordMatch { Start = at, Length = len, Part = part, Conjugated = byWriting && len < w.Length };
                if (byWriting && HasKanji(w.Substring(0, len)) && !string.IsNullOrEmpty(part.Reading))
                {
                    char before = at > 0 ? text[at - 1] : ' ';
                    string hira = ToHiragana(part.Reading);
                    // a LONE kanji right after another kanji is the tail of a compound (犯罪人, 国際競争力): the dictionary
                    // analysis often gives it its everyday reading (ひと, ちから) instead of the one it has in the compound (にん,
                    // りょく). Only an on-reading of the kanji is trusted there.
                    bool loneAfterKanji = w.Length == 1 && IsKanji(before) && !IsNumeral(before);
                    if (loneAfterKanji && !(onReadings != null && OnReadingMatches(onReadings(w[0]), hira)))
                        { /* no furigana */ }
                    else
                        m.Ruby = Furigana(w, hira, len, m.Conjugated, before, at);
                }
                result.Add(m);
                cursor = at + len;
            }
            return result;
        }

        // ---- furigana of one word ---------------------------------------------------------------------------------------
        class Seg { public int Start, Length; public bool Kanji; }

        static List<RubyPiece> Furigana(string w, string reading, int matched, bool conjugated, char before, int textStart)
        {
            var none = new List<RubyPiece>();
            // readings that change when the word is conjugated: the dictionary reading would be WRONG for the text
            if (conjugated && ChangesWhenConjugated(w, reading)) return none;

            // written form -> runs of kanji / of kana
            var segs = new List<Seg>();
            for (int i = 0; i < w.Length; i++)
            {
                bool k = IsKanji(w[i]);
                if (segs.Count > 0 && segs[segs.Count - 1].Kanji == k) segs[segs.Count - 1].Length++;
                else segs.Add(new Seg { Start = i, Length = 1, Kanji = k });
            }
            if (!segs.Any(s => s.Kanji)) return none;

            // a counter after a number changes its sound (三本 さんぼん, 一人 ひとり): not the dictionary reading
            if (segs.Count == 1 && segs[0].Kanji && IsNumeral(before)) return none;

            // align: every kana run must be found in the reading; what is left between them is the reading of the kanji runs.
            // The split has to be the ONLY possible one.
            var assign = new string[segs.Count];
            var found = new List<string[]>();
            Align(segs, ToHiragana(w), reading, 0, 0, new string[segs.Count], found);
            if (found.Count != 1) return none;
            assign = found[0];

            var pieces = new List<RubyPiece>();
            for (int i = 0; i < segs.Count; i++)
            {
                if (!segs[i].Kanji) continue;
                if (segs[i].Start + segs[i].Length > matched) continue;        // the word is cut inside this kanji run
                pieces.Add(new RubyPiece { Offset = textStart + segs[i].Start, Length = segs[i].Length, Reading = assign[i] });
            }
            return pieces;
        }

        static bool OnReadingMatches(string onReadings, string hiraganaReading)
        {
            if (string.IsNullOrEmpty(onReadings)) return false;
            foreach (var r in onReadings.Split(new[] { ',', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries))
                if (ToHiragana(r) == hiraganaReading) return true;
            return false;
        }

        static bool ChangesWhenConjugated(string w, string reading)
        {
            if (w.EndsWith("来る") && reading.EndsWith("くる")) return true;                    // 来る: く / き / こ
            if (w.EndsWith("為る")) return true;                                                // 為る: し / せ / さ
            if (reading.EndsWith("いい") && (w.EndsWith("良い") || w.EndsWith("好い") || w.EndsWith("善い") || w.EndsWith("宜い") || w.EndsWith("佳い")))
                return true;                                                                    // いい -> よく, よかった
            return false;
        }

        // Recursive: collect every way to split the reading over the segments (stops at 2: "not unique").
        static void Align(List<Seg> segs, string written, string reading, int si, int rp, string[] cur, List<string[]> found)
        {
            if (found.Count > 1) return;
            if (si == segs.Count) { if (rp == reading.Length) found.Add((string[])cur.Clone()); return; }
            var seg = segs[si];
            if (!seg.Kanji)
            {
                string kana = written.Substring(seg.Start, seg.Length);
                if (string.CompareOrdinal(reading, rp, kana, 0, kana.Length) != 0) return;
                cur[si] = null;
                Align(segs, written, reading, si + 1, rp + kana.Length, cur, found);
                return;
            }
            for (int n = 1; rp + n <= reading.Length; n++)
            {
                cur[si] = reading.Substring(rp, n);
                Align(segs, written, reading, si + 1, rp + n, cur, found);
                if (found.Count > 1) return;
            }
        }
    }
}
