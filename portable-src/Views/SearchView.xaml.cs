using System.Collections.Generic;
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
        // No mode is on at first: the search box is typed into with the keyboard as it is. A click on a mode button switches
        // it on (its panel appears), a click on the same button again switches it off.
        enum Mode { None, Keyboard, Radicals }
        InputKeyboard keyboard;
        RadicalPicker picker;
        Mode mode = Mode.None;
        int lastModeTick;

        public SearchView()
        {
            InitializeComponent();
            keyboard = new InputKeyboard { Layout = Store.Settings.InputLayout };
            keyboard.TextTyped += t => InsertRaw(t);
            keyboard.BackspaceTyped += () => BackspaceKey();
            keyboard.Modify += kind => ModifyLast(kind);
            keyboard.LayoutChanged += () => { Store.Settings.InputLayout = keyboard.Layout; Store.SaveSettings(); };
            PanelHost.Content = keyboard;
            picker = new RadicalPicker();
            picker.Done += text => { InsertRaw(text); CloseModes(); };    // DONE: the chosen kanji go into the search box
            picker.BackspaceRequested += () => BackspaceKey();
            RadicalHost.Child = picker;
            List.ItemsSource = items;
            List.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(List_ScrollChanged));
            debounce.Tick += (s, e) => { debounce.Stop(); RunSearch(); };
            RefreshExamples();
        }

        // ---- the suggestions on the empty page: 6 random common words, different every time ---------------------
        int examplesGeneration;

        async void RefreshExamples()
        {
            int my = ++examplesGeneration;
            List<WordTag> tags = null;
            bool skipKatakana = Store.Settings.RandomSkipKatakana;
            try { tags = await Task.Run(() => Engine.RandomWords(6, true, skipKatakana)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != examplesGeneration || tags == null) return;
            Examples.Children.Clear();
            foreach (var tag in tags)
            {
                var t = tag;
                var item = WordItem.From(t);
                var b = new Button { Style = (Style)FindResource("LinkButton"), Content = item.Main, FontSize = 16, Margin = new Thickness(0, 0, 14, 2) };
                b.FontFamily = Ui.JpFont;
                b.ToolTip = (string.IsNullOrEmpty(item.Reading) ? "" : item.Reading + " - ") + item.Meaning;
                b.Click += (s, e) => OpenWord(t, item.Main);
                Examples.Children.Add(b);
            }
        }

        public void FocusBox() { Box.Focus(); Box.SelectAll(); }

        // ---- modes -------------------------------------------------------------------------------------------------
        // Checked / Unchecked are what UI Automation and the keyboard raise, Click comes with every mouse click (also on the
        // mode that is already on: that click switches it off). A mouse click that switches a mode ON raises Checked and,
        // a moment later in the same click, Click: that Click is ignored.
        void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (keyboard == null) return;   // while the page is being built
            var m = ModeRadicals.IsChecked == true ? Mode.Radicals : ModeKeyboard.IsChecked == true ? Mode.Keyboard : Mode.None;
            if (m != Mode.None && m != mode) lastModeTick = Environment.TickCount;
            mode = m;
            ShowPanel();
        }

        void Mode_Click(object sender, RoutedEventArgs e)
        {
            if (Environment.TickCount - lastModeTick < 50) return;   // the Click of the click that just switched this mode on
            ((RadioButton)sender).IsChecked = false;                  // pressed again: switch it off
        }

        void Dummy_Click(object sender, RoutedEventArgs e) { }   // handwriting, voice and photo: placeholders for now

        void ShowPanel()
        {
            PanelBorder.Visibility = mode == Mode.Keyboard ? Visibility.Visible : Visibility.Collapsed;
            RadicalHost.Visibility = mode == Mode.Radicals ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Switch every mode off (the box is typed into with the keyboard as it is).</summary>
        public void CloseModes()
        {
            ModeKeyboard.IsChecked = false;
            ModeRadicals.IsChecked = false;
            Box.Focus();
        }

        /// <summary>Switch to the radicals (puzzle) mode.</summary>
        public void ShowRadicals(string[] preselect = null)
        {
            ModeRadicals.IsChecked = true;
            if (preselect != null) picker.Preselect(preselect);
        }

        // in the keyboard mode the real keyboard types romaji and kana come out
        bool KanaTyping { get { return mode == Mode.Keyboard; } }

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
            if (!KanaTyping) return;
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
            if (KanaTyping && text.Length == 1 && Kana.IsRomajiChar(text[0])) TypeRomaji(text[0]);
            else InsertRaw(text);
        }

        public void BackspaceFromAnywhere() { BackspaceKey(); }

        /// <summary>For automated tests (--mode-clicks keyboard,keyboard): press the mode buttons like a mouse click does
        /// (the real OnClick of the button), one every 2 s, so that "pressed again switches it off" can be tested.</summary>
        public void TestModeClicks(string[] names)
        {
            var queue = new System.Collections.Generic.Queue<string>(names);
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            t.Tick += (s, e) =>
            {
                if (queue.Count == 0) { t.Stop(); return; }
                var rb = queue.Dequeue() == "radicals" ? ModeRadicals : ModeKeyboard;
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(rb, null);
            };
            t.Start();
        }

        /// <summary>For automated tests (--layout hira|kata|off): switch the keyboard mode on with that layout (or off), without saving.</summary>
        public void TestLayout(string layout)
        {
            if (layout == "hira" || layout == "kata") { keyboard.Layout = layout; ModeKeyboard.IsChecked = true; }
            else CloseModes();
        }

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
            bool common = Store.Settings.RandomCommon, skipKatakana = Store.Settings.RandomSkipKatakana;
            try { tag = await Task.Run(() => Engine.RandomWord(common, skipKatakana)); }
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
                if (Hint.Visibility != Visibility.Visible) RefreshExamples();   // the empty page is shown again: new suggestions
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
            if ((e.Key == Key.Enter || e.Key == Key.Space) && KanaTyping) CommitPendingN();
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
