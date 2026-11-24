using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TakobotoApp
{
    /// <summary>Typing Japanese without a Japanese keyboard: romaji -> hiragana / katakana (what an IME does), and the
    /// small helpers the on-screen kana keyboard needs (dakuten, handakuten, small kana).</summary>
    public static class Kana
    {
        static readonly Dictionary<string, string> Map = BuildMap();
        static readonly HashSet<string> Prefixes = BuildPrefixes();
        const string Vowels = "aiueo";

        static Dictionary<string, string> BuildMap()
        {
            var d = new Dictionary<string, string>();
            Action<string, string> row = (cons, kana) => { for (int i = 0; i < 5; i++) d[cons + Vowels[i]] = kana[i].ToString(); };
            row("", "あいうえお");
            row("k", "かきくけこ"); row("g", "がぎぐげご"); row("s", "さしすせそ"); row("z", "ざじずぜぞ");
            row("t", "たちつてと"); row("d", "だぢづでど"); row("n", "なにぬねの"); row("h", "はひふへほ");
            row("b", "ばびぶべぼ"); row("p", "ぱぴぷぺぽ"); row("m", "まみむめも"); row("r", "らりるれろ");
            row("c", "かしくせこ");                       // ca ci cu ce co as in the Windows IME
            d["ya"] = "や"; d["yu"] = "ゆ"; d["yo"] = "よ"; d["ye"] = "いぇ";
            d["wa"] = "わ"; d["wi"] = "うぃ"; d["wu"] = "う"; d["we"] = "うぇ"; d["wo"] = "を";
            d["shi"] = "し"; d["chi"] = "ち"; d["tsu"] = "つ"; d["fu"] = "ふ"; d["ji"] = "じ";
            d["sha"] = "しゃ"; d["shu"] = "しゅ"; d["sho"] = "しょ"; d["she"] = "しぇ";
            d["cha"] = "ちゃ"; d["chu"] = "ちゅ"; d["cho"] = "ちょ"; d["che"] = "ちぇ";
            d["ja"] = "じゃ"; d["ju"] = "じゅ"; d["jo"] = "じょ"; d["je"] = "じぇ";
            d["fa"] = "ふぁ"; d["fi"] = "ふぃ"; d["fe"] = "ふぇ"; d["fo"] = "ふぉ";
            d["va"] = "ゔぁ"; d["vi"] = "ゔぃ"; d["vu"] = "ゔ"; d["ve"] = "ゔぇ"; d["vo"] = "ゔぉ";
            d["tha"] = "てゃ"; d["thi"] = "てぃ"; d["thu"] = "てゅ"; d["tho"] = "てょ";
            d["dha"] = "でゃ"; d["dhi"] = "でぃ"; d["dhu"] = "でゅ"; d["dho"] = "でょ";
            d["twu"] = "とぅ"; d["dwu"] = "どぅ"; d["tsa"] = "つぁ"; d["tsi"] = "つぃ"; d["tse"] = "つぇ"; d["tso"] = "つぉ";
            // kya kyu kyo ... (and kyi kye)
            var yRows = new Dictionary<string, string> { { "k", "き" }, { "g", "ぎ" }, { "n", "に" }, { "h", "ひ" }, { "b", "び" }, { "p", "ぴ" }, { "m", "み" }, { "r", "り" } };
            foreach (var kv in yRows)
            {
                d[kv.Key + "ya"] = kv.Value + "ゃ"; d[kv.Key + "yu"] = kv.Value + "ゅ"; d[kv.Key + "yo"] = kv.Value + "ょ";
                d[kv.Key + "yi"] = kv.Value + "ぃ"; d[kv.Key + "ye"] = kv.Value + "ぇ";
            }
            // other spellings of the same sounds: sya, zya, tya, dya, jya, cya
            foreach (var kv in new Dictionary<string, string> { { "sy", "し" }, { "zy", "じ" }, { "ty", "ち" }, { "dy", "ぢ" }, { "jy", "じ" }, { "cy", "ち" } })
            {
                d[kv.Key + "a"] = kv.Value + "ゃ"; d[kv.Key + "u"] = kv.Value + "ゅ"; d[kv.Key + "o"] = kv.Value + "ょ";
                d[kv.Key + "i"] = kv.Value + "ぃ"; d[kv.Key + "e"] = kv.Value + "ぇ";
            }
            // small kana
            foreach (var p in new[] { "x", "l" })
            {
                row(p, "ぁぃぅぇぉ");
                d[p + "ya"] = "ゃ"; d[p + "yu"] = "ゅ"; d[p + "yo"] = "ょ"; d[p + "wa"] = "ゎ";
                d[p + "tu"] = "っ"; d[p + "tsu"] = "っ";
            }
            d["-"] = "ー";
            return d;
        }

        static HashSet<string> BuildPrefixes()
        {
            var p = new HashSet<string>();
            foreach (var k in Map.Keys) for (int i = 1; i < k.Length; i++) p.Add(k.Substring(0, i));
            p.Add("n");
            return p;
        }

        /// <summary>A letter that is part of typed romaji.</summary>
        public static bool IsRomajiChar(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '\'' || c == '-'; }

        /// <summary>Is <paramref name="s"/> an UNFINISHED romaji syllable (k, sh, ny, n ...)? Such letters stay in the box as
        /// they are until the syllable is complete.</summary>
        public static bool IsPending(string s) { return s.Length > 0 && (Prefixes.Contains(s) || (s.Length == 1 && "bcdfghjklmpqrstvwxyz".IndexOf(s[0]) >= 0)); }

        /// <summary>Convert typed romaji. Returns the kana of the finished syllables; <paramref name="rest"/> is the unfinished
        /// tail (e.g. "k" of "ka"-in-progress, or "n" which may still become "na").</summary>
        public static string Feed(string romaji, bool katakana, out string rest)
        {
            var sb = new StringBuilder();
            string s = romaji.ToLowerInvariant();
            int i = 0;
            rest = "";
            while (i < s.Length)
            {
                char c = s[i];
                if (c == 'n')
                {
                    if (i + 1 >= s.Length) { rest = "n"; break; }
                    char n1 = s[i + 1];
                    if (n1 == 'n' || n1 == '\'') { sb.Append('ん'); i += 2; continue; }       // nn / n'
                    if (Vowels.IndexOf(n1) < 0 && n1 != 'y') { sb.Append('ん'); i += 1; continue; }   // n before a consonant
                }
                else if (i + 1 < s.Length && s[i] == s[i + 1] && "bcdfghjklmpqrstvwxyz".IndexOf(c) >= 0)
                {
                    sb.Append('っ'); i += 1; continue;                                         // kka -> っか
                }
                string hit = null; int len = 0;
                for (int l = Math.Min(3, s.Length - i); l >= 1; l--)
                {
                    string key = s.Substring(i, l);
                    if (Map.TryGetValue(key, out hit)) { len = l; break; }
                }
                if (hit != null) { sb.Append(hit); i += len; continue; }
                string tail = s.Substring(i);
                if (Prefixes.Contains(tail)) { rest = tail; break; }                           // wait for the next letter
                sb.Append(c == 'n' ? "ん" : c.ToString()); i++;                                // not romaji after all
            }
            string kana = sb.ToString();
            return katakana ? ToKatakana(kana) : kana;
        }

        public static string ToKatakana(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c >= 'ぁ' && c <= 'ゖ' ? (char)(c + 0x60) : c);
            return sb.ToString();
        }

        static char ToHiragana(char c) { return c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c; }

        // ---- the modifier keys of the on-screen kana keyboard (they change the LAST typed kana) ------------------------
        static readonly string[] Voiced = "かが きぎ くぐ けげ こご さざ しじ すず せぜ そぞ ただ ちぢ つづ てで とど はば ひび ふぶ へべ ほぼ うゔ".Split(' ');
        static readonly string[] SemiVoiced = "はぱ ひぴ ふぷ へぺ ほぽ".Split(' ');
        static readonly string[] Small = "あぁ いぃ うぅ えぇ おぉ つっ やゃ ゆゅ よょ わゎ".Split(' ');   // big, small

        /// <summary>kind: "dakuten" (゛), "handakuten" (゜) or "small" (小). Returns the changed character, or the same one
        /// when it cannot be changed. Pressing the key again changes it back.</summary>
        public static char Modify(char c, string kind)
        {
            bool kata = c >= 'ァ' && c <= 'ヶ';
            char h = ToHiragana(c);
            var pairs = kind == "dakuten" ? Voiced : kind == "handakuten" ? SemiVoiced : Small;
            foreach (var p in pairs)
            {
                if (p[0] == h) { h = p[1]; return kata ? (char)(h + 0x60) : h; }
                if (p[1] == h) { h = p[0]; return kata ? (char)(h + 0x60) : h; }
            }
            return c;
        }
    }
}
