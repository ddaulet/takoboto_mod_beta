using System;
using System.Collections.Generic;
using System.Linq;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>Which translation languages the user wants to see (Settings), and what to do when an entry has nothing
    /// in them.</summary>
    public static class Langs
    {
        public static bool Wanted(Language l)
        {
            var s = Store.Settings;
            switch (l)
            {
                case Language.English: return s.English;
                case Language.French: return s.French;
                case Language.German: return s.German;
                case Language.Russian: return s.Russian;
                default: return true;
            }
        }

        /// <summary>The items to show: those written in a wanted language. When there are NONE in the wanted languages
        /// (e.g. only Russian is ticked, but this word has no Russian translation) everything there is is shown instead -
        /// an empty meaning would be worse than a translation in another language. The order is kept (English first).</summary>
        public static List<T> Pick<T>(IEnumerable<T> items, Func<T, Language> language)
        {
            var all = (items ?? Enumerable.Empty<T>()).ToList();
            var wanted = all.Where(i => Wanted(language(i))).ToList();
            return wanted.Count > 0 ? wanted : all;
        }
    }
}
