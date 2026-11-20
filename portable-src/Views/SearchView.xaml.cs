using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Takoboto.Database;

namespace TakobotoApp
{
    public partial class SearchView : UserControl
    {
        const int FirstLimit = 100;    // partial matches asked from the engine at first (fast)
        const int MaxLimit = 5000;     // ... and at most, while the user keeps scrolling

        readonly ObservableCollection<WordItem> items = new ObservableCollection<WordItem>();
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        int generation;
        bool auto;               // true while the code (not the user) selects a row
        int limit = FirstLimit;
        string currentQuery = "";
        bool truncated, loadingMore;
        int pendingSelectId;     // select this word once the next search has filled the list (see OpenWord)

        // ---- the five input modes under the search box ------------------------------------------------------------
        enum Mode { Keyboard, Radicals }
        InputKeyboard keyboard;
        RadicalPicker picker;
        Mode mode = Mode.Keyboard;
        bool radicalsOpen = true;
        int lastModeTick;

        public SearchView()
        {
            InitializeComponent();
            keyboard = new InputKeyboard { Layout = Store.Settings.InputLayout, KeysVisible = Store.Settings.KeyboardOpen };
            keyboard.TextTyped += t => InsertRaw(t);
            keyboard.BackspaceTyped += () => BackspaceKey();
            keyboard.Modify += kind => ModifyLast(kind);
            keyboard.LayoutChanged += () => { Store.Settings.InputLayout = keyboard.Layout; Store.SaveSettings(); };
            keyboard.KeysToggled += () => { Store.Settings.KeyboardOpen = keyboard.KeysVisible; Store.SaveSettings(); };
            picker = new RadicalPicker();
            picker.KanjiChosen += k => InsertRaw(k);
            ShowPanel();
            List.ItemsSource = items;
            List.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(List_ScrollChanged));
            debounce.Tick += (s, e) => { debounce.Stop(); RunSearch(); };
            foreach (var ex in new[] { "食べる", "taberu", "water", "Wasser", "вода", "日本語" })
            {
                string text = ex;
                var b = new Button { Style = (Style)FindResource("LinkButton"), Content = text, FontSize = 15, Margin = new Thickness(0, 0, 14, 0) };
                b.Click += (s, e) => SetQuery(text);
                Examples.Children.Add(b);
            }
        }

        public void FocusBox() { Box.Focus(); Box.SelectAll(); }

        // ---- modes -------------------------------------------------------------------------------------------------
        // Checked is what UI Automation / the keyboard raise, Click comes with every mouse click (also on the mode that is
        // already active, which folds / unfolds its panel). A mouse click raises both: the Click right after Checked is ignored.
        void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (keyboard == null) return;   // while the page is being built
            lastModeTick = Environment.TickCount;
            mode = sender == ModeRadicals ? Mode.Radicals : Mode.Keyboard;
            radicalsOpen = true;
            ShowPanel();
        }

        void Mode_Click(object sender, RoutedEventArgs e)
        {
            if (Environment.TickCount - lastModeTick < 300) return;
            if (mode == Mode.Keyboard) keyboard.KeysVisible = !keyboard.KeysVisible;   // the layout strip stays
            else { radicalsOpen = !radicalsOpen; ShowPanel(); }
        }

        void Dummy_Click(object sender, RoutedEventArgs e) { }   // handwriting, voice and photo: placeholders for now

        void ShowPanel()
        {
            PanelHost.Content = mode == Mode.Keyboard ? (UIElement)keyboard : picker;
            PanelBorder.Visibility = mode == Mode.Keyboard || radicalsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Switch to the radicals (puzzle) mode, or just open its panel when it is already active.</summary>
        public void ShowRadicals(string[] preselect = null)
        {
            if (mode == Mode.Radicals) { radicalsOpen = true; ShowPanel(); }
            else ModeRadicals.IsChecked = true;
            if (preselect != null) picker.Preselect(preselect);
        }

        // ---- typing -------------------------------------------------------------------------------------------------
        /// <summary>Put text into the search box at the caret (the on-screen keyboard and the kanji of the radical search).</summary>
        public void InsertRaw(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!Box.IsKeyboardFocusWithin) { Box.Focus(); Box.Select(Box.Text.Length, 0); }
            int start = Box.SelectionStart;
            Box.Text = Box.Text.Remove(start, Box.SelectionLength).Insert(start, text);
            Box.CaretIndex = start + text.Length;
        }

        void BackspaceKey()
        {
            if (!Box.IsKeyboardFocusWithin) { Box.Focus(); Box.Select(Box.Text.Length, 0); }
            int start = Box.SelectionStart, len = Box.SelectionLength;
            if (len == 0) { if (start == 0) return; start--; len = 1; }
            Box.Text = Box.Text.Remove(start, len);
            Box.CaretIndex = start;
        }

        // the dakuten / handakuten / small keys change the kana before the caret (か -> が)
        void ModifyLast(string kind)
        {
            if (!Box.IsKeyboardFocusWithin) { Box.Focus(); Box.Select(Box.Text.Length, 0); }
            int caret = Box.SelectionStart;
            if (caret == 0) return;
            char c = Box.Text[caret - 1], n = Kana.Modify(c, kind);
            if (n == c) return;
            Box.Text = Box.Text.Remove(caret - 1, 1).Insert(caret - 1, n.ToString());
            Box.CaretIndex = caret;
        }

        /// <summary>In the kana layouts the real keyboard types romaji and kana come out (ka -> か). An unfinished syllable
        /// (k, sh, n) stays as letters until it is complete.</summary>
        void Box_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (keyboard.Layout == "abc") return;
            if (e.Text.Length != 1 || !Kana.IsRomajiChar(e.Text[0])) return;
            e.Handled = true;
            TypeRomaji(e.Text[0]);
        }

        void TypeRomaji(char ch)
        {
            string t = Box.Text;
            int start = Box.SelectionStart, len = Box.SelectionLength;
            if (len > 0) t = t.Remove(start, len);
            // unfinished romaji right before the caret continues with this letter
            int p = start;
            for (int l = Math.Min(3, start); l >= 1; l--)
            {
                string tail = t.Substring(start - l, l);
                if (tail.All(c => c < 128 && char.IsLetter(c)) && Kana.IsPending(tail.ToLowerInvariant())) { p = start - l; break; }
            }
            // "nn" gives ん, but the second n may still start the next syllable: konnichiwa -> こんにちわ, onna -> おんな
            string run = t.Substring(p, start - p) + ch;
            char lc = char.ToLowerInvariant(ch);
            if (p == start && nnCaret == start && start > 0 && (t[start - 1] == 'ん' || t[start - 1] == 'ン') && ("aiueoy".IndexOf(lc) >= 0))
                run = "n" + ch;
            nnCaret = -1;
            string rest, kana = Kana.Feed(run, keyboard.Layout == "kata", out rest);
            if (run.ToLowerInvariant() == "nn") nnCaret = p + kana.Length;
            Box.Text = t.Substring(0, p) + kana + rest + t.Substring(start);
            Box.CaretIndex = p + kana.Length + rest.Length;
        }

        int nnCaret = -1;   // caret position right after a ん that came from "nn"

        // a lone "n" before the caret is waiting for a vowel: Enter / Space settle it as ん
        void CommitPendingN()
        {
            int caret = Box.SelectionStart;
            if (Box.SelectionLength == 0 && caret > 0 && (Box.Text[caret - 1] == 'n' || Box.Text[caret - 1] == 'N'))
            {
                string kana = keyboard.Layout == "kata" ? "ン" : "ん";
                Box.Text = Box.Text.Remove(caret - 1, 1).Insert(caret - 1, kana);
                Box.CaretIndex = caret;
            }
        }

        /// <summary>A key was pressed while the focus was somewhere else on the search page (the result list, the card ...):
        /// it goes into the search box. When the box was not in use, what was typed there before is replaced: it is a new search.</summary>
        public void TypeFromAnywhere(string text)
        {
            if (!Box.IsKeyboardFocusWithin) { Box.Focus(); if (Box.Text.Length > 0) Box.SelectAll(); }
            if (keyboard.Layout != "abc" && text.Length == 1 && Kana.IsRomajiChar(text[0])) TypeRomaji(text[0]);
            else InsertRaw(text);
        }

        public void BackspaceFromAnywhere() { BackspaceKey(); }

        /// <summary>For automated tests (--layout abc|hira|kata): set the keyboard layout without saving it.</summary>
        public void TestLayout(string layout) { keyboard.Layout = layout; }

        public void SetQuery(string text)
        {
            Box.Text = text ?? "";
            Box.CaretIndex = Box.Text.Length;
            debounce.Stop();
            RunSearch();
            Box.Focus();
        }

        /// <summary>Search for <paramref name="queryText"/> and open exactly this entry (used by the word previews).</summary>
        public void OpenWord(WordTag tag, string queryText)
        {
            pendingSelectId = tag.WordId;
            SetQuery(queryText);
        }

        /// <summary>The "Random word" button: a random entry of the whole dictionary, found in the list like any other word.</summary>
        public async void OpenRandom()
        {
            WordTag tag = null;
            try { tag = await Task.Run(() => Engine.RandomWord()); }
            catch (Exception ex) { App.Log(ex); }
            if (tag == null) return;
            string text = WordItem.From(tag).Main;
            if (string.IsNullOrEmpty(text)) return;
            OpenWord(tag, text);
        }

        /// <summary>Re-runs the current search (e.g. after the language options changed).</summary>
        public void Refresh() { if (Box.Text.Trim().Length > 0) RunSearch(); }

        void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            Placeholder.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearBtn.Visibility = Box.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            debounce.Stop();
            debounce.Start();
        }

        async void RunSearch()
        {
            string q = Box.Text.Trim();
            int my = ++generation;
            limit = FirstLimit;
            loadingMore = false;
            if (q.Length == 0)
            {
                items.Clear();
                Detail.Clear();
                StatusBar.Visibility = Visibility.Collapsed;
                Hint.Visibility = Visibility.Visible;
                NothingFound.Visibility = Visibility.Collapsed;
                pendingSelectId = 0;
                return;
            }
            Hint.Visibility = Visibility.Collapsed;
            SearchOutcome r = null;
            try { r = await Task.Run(() => Engine.Search(q, FirstLimit)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != generation || r == null) return;   // a newer search is already running

            currentQuery = q;
            truncated = r.Truncated;
            items.Clear();
            foreach (var t in r.Exact.Concat(r.Partial)) items.Add(WordItem.From(t));
            NothingFound.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateStatus();
            if (items.Count == 0) { Detail.Clear(); pendingSelectId = 0; return; }

            var wanted = pendingSelectId > 0 ? items.FirstOrDefault(i => i.Tag.WordId == pendingSelectId) : null;
            pendingSelectId = 0;
            if (wanted != null) { List.SelectedItem = wanted; List.ScrollIntoView(wanted); }   // chosen by the user: counts for the history
            else { auto = true; List.SelectedIndex = 0; auto = false; List.ScrollIntoView(items[0]); }   // best match shown right away
        }

        // ---- more results while scrolling --------------------------------------------------------------------------
        void List_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0) return;
            if (!truncated || loadingMore || limit >= MaxLimit) return;
            // the list scrolls by whole items: offsets are item numbers
            if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 5) LoadMore();
        }

        async void LoadMore()
        {
            loadingMore = true;
            int my = generation;
            string q = currentQuery;
            int newLimit = Math.Min(MaxLimit, limit * 4);
            StatusText.Text = items.Count + " results - loading more…";
            SearchOutcome r = null;
            try { r = await Task.Run(() => Engine.Search(q, newLimit)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != generation || r == null) { loadingMore = false; return; }

            limit = newLimit;
            truncated = r.Truncated;
            var all = r.Exact.Concat(r.Partial).ToList();
            bool samePrefix = all.Count >= items.Count;
            for (int i = 0; samePrefix && i < items.Count; i++) if (items[i].Tag.WordId != all[i].WordId) samePrefix = false;
            if (samePrefix)
                for (int i = items.Count; i < all.Count; i++) items.Add(WordItem.From(all[i]));   // just append the tail
            else
            {
                var selected = List.SelectedItem as WordItem;
                int keep = selected == null ? -1 : selected.Tag.WordId;
                auto = true;
                items.Clear();
                foreach (var t in all) items.Add(WordItem.From(t));
                var again = keep < 0 ? null : items.FirstOrDefault(i => i.Tag.WordId == keep);
                if (again != null) List.SelectedItem = again;
                auto = false;
            }
            loadingMore = false;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            StatusBar.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (items.Count == 0) return;
            if (!truncated) StatusText.Text = items.Count + (items.Count == 1 ? " result" : " results");
            else if (limit >= MaxLimit) StatusText.Text = "First " + items.Count + " results - type more to narrow it down";
            else StatusText.Text = items.Count + "+ results - more load as you scroll";
        }

        void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var it = List.SelectedItem as WordItem;
            if (it == null) return;
            Detail.Show(it.Tag);
            if (!auto) Store.AddHistory(it.Tag);
        }

        void Clear_Click(object sender, RoutedEventArgs e) { Box.Text = ""; Box.Focus(); }

        void Box_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Enter || e.Key == Key.Space) && keyboard.Layout != "abc") CommitPendingN();
            if (e.Key == Key.Escape) { Box.Text = ""; e.Handled = true; }
            else if (e.Key == Key.Down && items.Count > 0)
            {
                int next = Math.Min(items.Count - 1, Math.Max(0, List.SelectedIndex + 1));
                List.SelectedIndex = next;
                List.ScrollIntoView(items[next]);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && items.Count > 0)
            {
                int prev = Math.Max(0, List.SelectedIndex - 1);
                List.SelectedIndex = prev;
                List.ScrollIntoView(items[prev]);
                e.Handled = true;
            }
        }

        void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { FocusBox(); e.Handled = true; }
        }
    }
}
