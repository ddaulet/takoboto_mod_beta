using System;
using System.Collections.Generic;
using System.Text;

namespace TakobotoApp
{
    /// <summary>Romaji and kana: typing "nihon" gives にほん; kana read aloud in Latin letters; the kinds of Japanese characters.</summary>
    public static class Romaji
    {
        static readonly Dictionary<string, string> Table = new Dictionary<string, string>();
        static readonly Dictionary<string, string> Back = new Dictionary<string, string>();   // hiragana -> romaji (Hepburn)

        static void Add(string romaji, string kana) { Table[romaji] = kana; }

        static Romaji()
        {
            string[] rows =
            {
                "a あ", "i い", "u う", "e え", "o お",
                "ka か", "ki き", "ku く", "ke け", "ko こ", "kya きゃ", "kyu きゅ", "kyo きょ", "kye きぇ",
                "ga が", "gi ぎ", "gu ぐ", "ge げ", "go ご", "gya ぎゃ", "gyu ぎゅ", "gyo ぎょ",
                "sa さ", "si し", "shi し", "su す", "se せ", "so そ", "sha しゃ", "shu しゅ", "sho しょ", "she しぇ", "sya しゃ", "syu しゅ", "syo しょ",
                "za ざ", "zi じ", "ji じ", "zu ず", "ze ぜ", "zo ぞ", "ja じゃ", "ju じゅ", "jo じょ", "je じぇ", "jya じゃ", "jyu じゅ", "jyo じょ", "zya じゃ", "zyu じゅ", "zyo じょ",
                "ta た", "ti ち", "chi ち", "tu つ", "tsu つ", "te て", "to と", "cha ちゃ", "chu ちゅ", "cho ちょ", "che ちぇ", "tya ちゃ", "tyu ちゅ", "tyo ちょ", "tsa つぁ", "tsi つぃ", "tse つぇ", "tso つぉ", "tha てゃ", "thi てぃ", "thu てゅ", "tho てょ",
                "da だ", "di ぢ", "du づ", "de で", "do ど", "dya ぢゃ", "dyu ぢゅ", "dyo ぢょ", "dha でゃ", "dhi でぃ", "dhu でゅ", "dho でょ",
                "na な", "ni に", "nu ぬ", "ne ね", "no の", "nya にゃ", "nyu にゅ", "nyo にょ",
                "ha は", "hi ひ", "hu ふ", "fu ふ", "he へ", "ho ほ", "hya ひゃ", "hyu ひゅ", "hyo ひょ", "fa ふぁ", "fi ふぃ", "fe ふぇ", "fo ふぉ", "fya ふゃ", "fyu ふゅ", "fyo ふょ",
                "ba ば", "bi び", "bu ぶ", "be べ", "bo ぼ", "bya びゃ", "byu びゅ", "byo びょ",
                "pa ぱ", "pi ぴ", "pu ぷ", "pe ぺ", "po ぽ", "pya ぴゃ", "pyu ぴゅ", "pyo ぴょ",
                "ma ま", "mi み", "mu む", "me め", "mo も", "mya みゃ", "myu みゅ", "myo みょ",
                "ya や", "yu ゆ", "yo よ", "ye いぇ",
                "ra ら", "ri り", "ru る", "re れ", "ro ろ", "rya りゃ", "ryu りゅ", "ryo りょ", "la ら", "li り", "lu る", "le れ", "lo ろ",
                "wa わ", "wi うぃ", "wu う", "we うぇ", "wo を", "va ゔぁ", "vi ゔぃ", "vu ゔ", "ve ゔぇ", "vo ゔぉ",
                "xa ぁ", "xi ぃ", "xu ぅ", "xe ぇ", "xo ぉ", "xya ゃ", "xyu ゅ", "xyo ょ", "xwa ゎ", "xtu っ", "xtsu っ", "xka ゕ", "xke ゖ",
                "ltu っ", "ltsu っ", "lya ゃ", "lyu ゅ", "lyo ょ", "la ぁ" /* replaced below */,
                "qa くぁ", "qi くぃ", "qu く", "qe くぇ", "qo くぉ", "kwa くぁ", "gwa ぐぁ"
            };
            foreach (var r in rows)
            {
                int sp = r.IndexOf(' ');
                Table[r.Substring(0, sp)] = r.Substring(sp + 1);
            }
            Table["la"] = "ら";   // "la" is ら (as in the old dictionary); the small kana are typed with x
            Table["-"] = "ー";

            // kana -> Hepburn
            string[] back =
            {
                "あ a", "い i", "う u", "え e", "お o", "か ka", "き ki", "く ku", "け ke", "こ ko", "が ga", "ぎ gi", "ぐ gu", "げ ge", "ご go",
                "さ sa", "し shi", "す su", "せ se", "そ so", "ざ za", "じ ji", "ず zu", "ぜ ze", "ぞ zo", "た ta", "ち chi", "つ tsu", "て te", "と to",
                "だ da", "ぢ ji", "づ zu", "で de", "ど do", "な na", "に ni", "ぬ nu", "ね ne", "の no", "は ha", "ひ hi", "ふ fu", "へ he", "ほ ho",
                "ば ba", "び bi", "ぶ bu", "べ be", "ぼ bo", "ぱ pa", "ぴ pi", "ぷ pu", "ぺ pe", "ぽ po", "ま ma", "み mi", "む mu", "め me", "も mo",
                "や ya", "ゆ yu", "よ yo", "ら ra", "り ri", "る ru", "れ re", "ろ ro", "わ wa", "ゐ i", "ゑ e", "を o", "ん n", "ゔ vu",
                "ぁ a", "ぃ i", "ぅ u", "ぇ e", "ぉ o", "ゎ wa",
                "きゃ kya", "きゅ kyu", "きょ kyo", "ぎゃ gya", "ぎゅ gyu", "ぎょ gyo", "しゃ sha", "しゅ shu", "しょ sho", "じゃ ja", "じゅ ju", "じょ jo",
                "ちゃ cha", "ちゅ chu", "ちょ cho", "ぢゃ ja", "ぢゅ ju", "ぢょ jo", "にゃ nya", "にゅ nyu", "にょ nyo", "ひゃ hya", "ひゅ hyu", "ひょ hyo",
                "びゃ bya", "びゅ byu", "びょ byo", "ぴゃ pya", "ぴゅ pyu", "ぴょ pyo", "みゃ mya", "みゅ myu", "みょ myo", "りゃ rya", "りゅ ryu", "りょ ryo",
                "しぇ she", "じぇ je", "ちぇ che", "ふぁ fa", "ふぃ fi", "ふぇ fe", "ふぉ fo", "てぃ ti", "でぃ di", "とぅ tu", "どぅ du", "うぃ wi", "うぇ we", "うぉ wo",
                "ゔぁ va", "ゔぃ vi", "ゔぇ ve", "ゔぉ vo", "つぁ tsa", "つぃ tsi", "つぇ tse", "つぉ tso", "いぇ ye", "くぁ kwa", "ぐぁ gwa", "ふゅ fyu", "てゅ tyu", "でゅ dyu"
            };
            foreach (var r in back)
            {
                int sp = r.IndexOf(' ');
                Back[r.Substring(0, sp)] = r.Substring(sp + 1);
            }
        }

        static bool IsVowel(char c) { return c == 'a' || c == 'i' || c == 'u' || c == 'e' || c == 'o'; }
        static bool IsLatinLetter(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); }

        /// <summary>Romaji to hiragana. Null when something is left that is not a whole syllable ("nih", "water").
        /// Japanese characters in the text stay as they are when <paramref name="keepJapanese"/> is set.</summary>
        public static string ToHiragana(string text, bool keepJapanese = true)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string s = text.ToLowerInvariant();
            var sb = new StringBuilder();
            int i = 0;
            bool any = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '　') { sb.Append(c); i++; continue; }
                if (!(IsLatinLetter(c) || c == '-' || c == '\''))
                {
                    if (!keepJapanese && IsJapanese(c) == false) return null;
                    sb.Append(c); i++; continue;
                }
                string hit = null; int len = 0;
                for (int n = Math.Min(4, s.Length - i); n >= 1; n--)
                {
                    string part = s.Substring(i, n);
                    if (Table.TryGetValue(part, out hit)) { len = n; break; }
                }
                if (len > 0) { sb.Append(hit); i += len; any = true; continue; }
                if (c == 'n')   // ん: at the end, before a consonant that is not y, or written n' / nn
                {
                    if (i + 1 >= s.Length) { sb.Append('ん'); i++; any = true; continue; }
                    char d = s[i + 1];
                    if (d == '\'') { sb.Append('ん'); i += 2; any = true; continue; }
                    if (d == 'n' || (!IsVowel(d) && d != 'y')) { sb.Append('ん'); i++; any = true; continue; }
                    return null;
                }
                if (i + 1 < s.Length && s[i + 1] == c && c != 'n' && !IsVowel(c) && IsLatinLetter(c)) { sb.Append('っ'); i++; any = true; continue; }   // kk -> っk
                if (c == 't' && i + 2 < s.Length && s[i + 1] == 'c' && s[i + 2] == 'h') { sb.Append('っ'); i++; any = true; continue; }          // tch
                return null;
            }
            return any || sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>The kana for a romaji text: the usual one first, then the ones where an "n" before a vowel is ん ("kenatsu" is けなつ or けんあつ).</summary>
        public static List<string> Variants(string text, int max = 8)
        {
            var result = new List<string>();
            string first = ToHiragana(text, true);
            if (first != null) result.Add(first);
            if (string.IsNullOrEmpty(text)) return result;
            string s = text.ToLowerInvariant();
            var pos = new List<int>();
            for (int i = 1; i + 1 < s.Length; i++)
                if (s[i] == 'n' && s[i - 1] != 'n' && s[i + 1] != 'n' && s[i + 1] != '\'' && (IsVowel(s[i + 1]) || s[i + 1] == 'y') && IsLatinLetter(s[i - 1]) && pos.Count < 3) pos.Add(i);
            for (int mask = 1; mask < (1 << pos.Count); mask++)
            {
                var sb = new StringBuilder(s);
                for (int b = pos.Count - 1; b >= 0; b--) if ((mask & (1 << b)) != 0) sb.Insert(pos[b] + 1, '\'');
                string k = ToHiragana(sb.ToString(), true);
                if (k != null && !result.Contains(k)) result.Add(k);
                if (result.Count >= max) break;
            }
            return result;
        }

        public static string ToKatakana(string text, bool keepJapanese = true)
        {
            string h = ToHiragana(text, keepJapanese);
            return h == null ? null : KanaToKatakana(h);
        }

        /// <summary>Kana in Latin letters, written the way the old dictionary showed them (the long-vowel mark is not written out, っち is "cchi");
        /// other characters stay, the iteration marks go.</summary>
        public static string FromKana(string kana)
        {
            if (string.IsNullOrEmpty(kana)) return "";
            string h = KanaToHiragana(kana);
            var sb = new StringBuilder();
            for (int i = 0; i < h.Length; i++)
            {
                char c = h[i];
                if (c == 'っ')   // っ doubles the next consonant
                {
                    string next = i + 1 < h.Length ? SyllableAt(h, i + 1) : null;
                    if (next != null) sb.Append(Back[next].Substring(0, 1));       // a final っ is not written
                    continue;
                }
                if (c == 'ー' || c == 'ゝ' || c == 'ゞ' || c == '・' || "ぁぃぅぇぉ".IndexOf(c) >= 0) continue;   // ー ゝ ゞ ・ and a lone small vowel
                string syl = SyllableAt(h, i);
                if (syl != null)
                {
                    sb.Append(syl == "を" ? "wo" : syl == "ぢ" ? "di" : syl == "づ" ? "du" : syl == "ゐ" ? "wi" : syl == "ゑ" ? "we" : Back[syl]);
                    i += syl.Length - 1;
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static bool IsVowelKana(char c) { return "あいうえお".IndexOf(c) >= 0; }

        static string SyllableAt(string h, int i)
        {
            if (i + 1 < h.Length) { string two = h.Substring(i, 2); if (Back.ContainsKey(two)) return two; }
            string one = h.Substring(i, 1);
            return Back.ContainsKey(one) ? one : null;
        }

        public static string KanaToHiragana(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c);
            return sb.ToString();
        }

        public static string KanaToKatakana(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'ぁ' && c <= 'ゖ' ? (char)(c + 0x60) : c);
            return sb.ToString();
        }

        // ---- what kind of character ------------------------------------------------------------------------------------------
        public static bool IsKana(char c) { return (c >= 'ぁ' && c <= 'ゟ') || (c >= '゠' && c <= 'ヿ') || c == 'ー' || (c >= 'ｦ' && c <= 'ﾟ'); }
        public static bool IsKanji(char c) { return (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || (c >= '豈' && c <= '﫿') || c == '々' || c == '〆'; }
        public static bool IsJapanese(char c) { return IsKana(c) || IsKanji(c) || c == '、' || c == '。' || c == '・' || c == '〜' || (c >= '！' && c <= '～'); }
        public static bool ContainsJapanese(string s) { if (s != null) foreach (char c in s) if (IsKana(c) || IsKanji(c)) return true; return false; }
        public static bool ContainsKanji(string s) { if (s != null) foreach (char c in s) if (IsKanji(c)) return true; return false; }
        public static bool IsAllKana(string s) { if (string.IsNullOrEmpty(s)) return false; foreach (char c in s) if (!IsKana(c)) return false; return true; }
    }
}
