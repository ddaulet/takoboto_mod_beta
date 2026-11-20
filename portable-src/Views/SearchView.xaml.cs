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

        public SearchView()
        {
            InitializeComponent();
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
