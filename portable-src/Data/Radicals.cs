using System;
using System.Collections.Generic;
using System.Text;

namespace TakobotoApp
{
    /// <summary>A radical and the kanji that contain it (the string of kanji is sorted, so two of them can be intersected quickly).</summary>
    public class KanjiRadical
    {
        public char Radical { get; set; }
        public int StrokeCount { get; set; }
        public string Kanjis { get; set; }

        public KanjiRadical(char radical, int strokeCount, string kanjis) { Radical = radical; StrokeCount = strokeCount; Kanjis = kanjis; }

        /// <summary>The kanji that are in both sorted strings.</summary>
        public static string Intersect(string one, string two)
        {
            if (string.IsNullOrEmpty(one) || string.IsNullOrEmpty(two)) return "";
            var sb = new StringBuilder();
            int i = 0, j = 0;
            while (i < one.Length && j < two.Length)
            {
                int c = one[i].CompareTo(two[j]);
                if (c == 0) { sb.Append(one[i]); i++; j++; }
                else if (c < 0) i++; else j++;
            }
            return sb.ToString();
        }

        public static bool HasIntersect(string one, string two)
        {
            if (string.IsNullOrEmpty(one) || string.IsNullOrEmpty(two)) return false;
            int i = 0, j = 0;
            while (i < one.Length && j < two.Length)
            {
                int c = one[i].CompareTo(two[j]);
                if (c == 0) return true;
                if (c < 0) i++; else j++;
            }
            return false;
        }
    }

    public static class KanjiRadicals
    {
        public static IList<KanjiRadical> Radicals { get { return Db.Radicals(); } }
    }
}
