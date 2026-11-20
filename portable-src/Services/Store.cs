using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace TakobotoApp
{
    public class AppSettings
    {
        public string Theme = "dark";            // system | light | dark  (dark on first start)
        public string InputLayout = "hira";      // kana keyboard (and what the real keyboard types in the keyboard mode): hira | kata
        public bool RandomCommon = true;         // Random word / suggestions: only common words (many example sentences)
        public bool RandomSkipKatakana = false;  // ... and not the words written in katakana (foreign words)
        public string Scheme = "green";          // colour scheme: green | red | blue | yellow (see Schemes)
        public bool English = true;              // translation languages that are shown (see Langs); French/German/Russian are also searched
        public bool French = true;
        public bool German = true;
        public bool Russian = true;
        public double Width = 1180, Height = 760, Left = -1, Top = -1;
        public bool Maximized;
    }

    public class SavedEntry
    {
        public int Id;
        public string Conj;      // conjugated text, when the word was found by a conjugated form
        public string Mark;      // highlight text
        public long Ticks;
    }

    public class UserList
    {
        public string Name = "";
        public List<SavedEntry> Items = new List<SavedEntry>();
    }

    public class ListsData
    {
        public List<SavedEntry> History = new List<SavedEntry>();
        public List<SavedEntry> Favorites = new List<SavedEntry>();
        public List<UserList> Lists = new List<UserList>();
    }

    /// <summary>Everything the user changes lives in the "userdata" folder next to the exe (portable).
    /// If that folder is not writable it falls back to %LocalAppData%\Takoboto.</summary>
    public static class Store
    {
        public static AppSettings Settings = new AppSettings();
        public static ListsData Lists = new ListsData();
        public static string Dir { get; private set; }
        public static event Action ListsChanged;
        const int MaxHistory = 300;

        public static void Load(string appDir)
        {
            Dir = ResolveDir(appDir);
            Settings = Read<AppSettings>("settings.xml") ?? new AppSettings();
            Lists = Read<ListsData>("lists.xml") ?? new ListsData();
            if (Lists.History == null) Lists.History = new List<SavedEntry>();
            if (Lists.Favorites == null) Lists.Favorites = new List<SavedEntry>();
            if (Lists.Lists == null) Lists.Lists = new List<UserList>();
        }

        static string ResolveDir(string appDir)
        {
            foreach (var candidate in new[] { Path.Combine(appDir, "userdata"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Takoboto") })
            {
                try
                {
                    Directory.CreateDirectory(candidate);
                    string probe = Path.Combine(candidate, ".write-test");
                    File.WriteAllText(probe, "x");
                    File.Delete(probe);
                    return candidate;
                }
                catch { /* try the next location */ }
            }
            return Path.GetTempPath();
        }

        static T Read<T>(string file) where T : class
        {
            try
            {
                string p = Path.Combine(Dir, file);
                if (!File.Exists(p)) return null;
                using (var f = File.OpenRead(p)) return (T)new XmlSerializer(typeof(T)).Deserialize(f);
            }
            catch { return null; }
        }

        static void Write(string file, object data)
        {
            try
            {
                string p = Path.Combine(Dir, file);
                string tmp = p + ".tmp";
                using (var f = File.Create(tmp)) new XmlSerializer(data.GetType()).Serialize(f, data);
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            catch { /* never crash because of a failed save */ }
        }

        public static void SaveSettings() { Write("settings.xml", Settings); }
        public static void SaveLists() { Write("lists.xml", Lists); ListsChanged?.Invoke(); }

        // ---- favourites -----------------------------------------------------------------------------------------
        public static bool IsFavorite(int id) { return Lists.Favorites.Any(e => e.Id == id); }

        public static bool ToggleFavorite(Takoboto.Database.WordTag tag)
        {
            var existing = Lists.Favorites.FirstOrDefault(e => e.Id == tag.WordId);
            bool now;
            if (existing != null) { Lists.Favorites.Remove(existing); now = false; }
            else { Lists.Favorites.Insert(0, Make(tag)); now = true; }
            SaveLists();
            return now;
        }

        // ---- history --------------------------------------------------------------------------------------------
        public static void AddHistory(Takoboto.Database.WordTag tag)
        {
            Lists.History.RemoveAll(e => e.Id == tag.WordId);
            Lists.History.Insert(0, Make(tag));
            if (Lists.History.Count > MaxHistory) Lists.History.RemoveRange(MaxHistory, Lists.History.Count - MaxHistory);
            SaveLists();
        }

        // ---- user lists -----------------------------------------------------------------------------------------
        public static UserList CreateList(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return null;
            var l = new UserList { Name = UniqueName(name) };
            Lists.Lists.Add(l);
            SaveLists();
            return l;
        }

        public static string UniqueName(string name)
        {
            string n = name; int i = 2;
            while (Lists.Lists.Any(l => string.Equals(l.Name, n, StringComparison.OrdinalIgnoreCase))) n = name + " (" + i++ + ")";
            return n;
        }

        public static void RenameList(UserList l, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || l.Name == name) return;
            l.Name = Lists.Lists.Any(o => o != l && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) ? UniqueName(name) : name;
            SaveLists();
        }

        public static void DeleteList(UserList l) { Lists.Lists.Remove(l); SaveLists(); }

        public static bool AddToList(UserList l, Takoboto.Database.WordTag tag)
        {
            if (l.Items.Any(e => e.Id == tag.WordId)) return false;
            l.Items.Insert(0, Make(tag));
            SaveLists();
            return true;
        }

        public static void Remove(List<SavedEntry> list, int id) { list.RemoveAll(e => e.Id == id); SaveLists(); }
        public static void Clear(List<SavedEntry> list) { list.Clear(); SaveLists(); }

        static SavedEntry Make(Takoboto.Database.WordTag t)
        {
            return new SavedEntry { Id = t.WordId, Conj = t.Conjugated ? t.ConjugatedText : null, Mark = t.HighlightText, Ticks = DateTime.UtcNow.Ticks };
        }
    }
}
