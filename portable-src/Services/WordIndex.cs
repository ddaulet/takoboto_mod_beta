using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakobotoApp
{
    /// <summary>An index of all the words of the dictionary in the order of their reading (a-i-u-e-o). The engine has no list of
    /// all words; the database has them, and the list is sorted in a fraction of a second.</summary>
    public static class WordIndex
    {
        public static int[] Ids = new int[0];
        public static string[] Keys = new string[0];
        static Dictionary<int, int> position;
        static bool ready;
        static Task loading;
        static readonly object Gate = new object();

        public static bool Ready { get { return ready; } }
        public static int Count { get { return Ids.Length; } }

                /// <summary>Start making / loading the index (once). The task ends when it is ready.</summary>
        public static Task EnsureLoaded()
        {
            lock (Gate)
            {
                if (loading == null) loading = Task.Run(() => Build());
                return loading;
            }
        }

        static void Build()
        {
            try
            {
                var all = Engine.ListAll();   // one query: the database has the reading of every word
                all.Sort((a, b) => { int c = string.CompareOrdinal(a.Key, b.Key); return c != 0 ? c : a.Value.CompareTo(b.Value); });
                Publish(all.Select(x => x.Value).ToArray(), all.Select(x => x.Key).ToArray());
            }
            catch (Exception ex) { App.Log(ex); lock (Gate) loading = null; }
        }

        static void Publish(int[] ids, string[] keys)
        {
            var pos = new Dictionary<int, int>(ids.Length);
            for (int i = 0; i < ids.Length; i++) pos[ids[i]] = i;
            Ids = ids; Keys = keys; position = pos;
            ready = true;
        }

        /// <summary>The position of the first word whose reading is not before <paramref name="kana"/>.</summary>
        public static int FirstAtOrAfter(string kana)
        {
            int lo = 0, hi = Keys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (string.CompareOrdinal(Keys[mid], kana) < 0) lo = mid + 1; else hi = mid;
            }
            return Math.Min(lo, Math.Max(0, Keys.Length - 1));
        }

        public static int PositionOf(int wordId)
        {
            int p;
            return position != null && position.TryGetValue(wordId, out p) ? p : -1;
        }
    }

    /// <summary>The index as a list for a ListBox: the rows are made only when they are shown (hundreds of thousands of words).</summary>
    public class VirtualWordList : IList
    {
        readonly Dictionary<int, WordItem> cache = new Dictionary<int, WordItem>();

        public int Count { get { return WordIndex.Count; } }

        public object this[int index]
        {
            get
            {
                if (index < 0 || index >= WordIndex.Count) return null;
                WordItem it;
                if (cache.TryGetValue(index, out it)) return it;
                var tag = Engine.TagById(WordIndex.Ids[index]);
                if (tag == null) return null;
                it = WordItem.From(tag);
                if (cache.Count > 4000) cache.Clear();
                cache[index] = it;
                return it;
            }
            set { throw new NotSupportedException(); }
        }

        public int IndexOf(object value)
        {
            var it = value as WordItem;
            return it == null || it.Tag == null ? -1 : WordIndex.PositionOf(it.Tag.WordId);
        }

        public bool Contains(object value) { return IndexOf(value) >= 0; }
        public IEnumerator GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        public bool IsReadOnly { get { return true; } }
        public bool IsFixedSize { get { return true; } }
        public bool IsSynchronized { get { return false; } }
        public object SyncRoot { get { return this; } }
        public int Add(object value) { throw new NotSupportedException(); }
        public void Clear() { throw new NotSupportedException(); }
        public void Insert(int index, object value) { throw new NotSupportedException(); }
        public void Remove(object value) { throw new NotSupportedException(); }
        public void RemoveAt(int index) { throw new NotSupportedException(); }
        public void CopyTo(Array array, int index) { for (int i = 0; i < Count; i++) array.SetValue(this[i], index + i); }
    }
}
