using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TakobotoApp
{
    public partial class SearchView : UserControl
    {
        readonly ObservableCollection<WordItem> items = new ObservableCollection<WordItem>();
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        int generation;
        bool auto;   // true while the code (not the user) selects a row

        public SearchView()
        {
            InitializeComponent();
            List.ItemsSource = items;
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
            if (q.Length == 0)
            {
                items.Clear();
                Detail.Clear();
                Hint.Visibility = Visibility.Visible;
                NothingFound.Visibility = Visibility.Collapsed;
                return;
            }
            Hint.Visibility = Visibility.Collapsed;
            SearchOutcome r = null;
            try { r = await Task.Run(() => Engine.Search(q)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != generation || r == null) return;   // a newer search is already running

            items.Clear();
            foreach (var t in r.Exact.Concat(r.Partial)) items.Add(WordItem.From(t));
            NothingFound.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (items.Count == 0) Detail.Clear();
            else { auto = true; List.SelectedIndex = 0; auto = false; }   // show the best match right away
            List.ScrollIntoView(items.FirstOrDefault());
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
