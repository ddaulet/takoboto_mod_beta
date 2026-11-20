using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Takoboto.Database;

namespace TakobotoApp
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }

        readonly SearchView search = new SearchView();
        readonly ListsView lists = new ListsView();
        readonly SettingsView settings = new SettingsView();
        readonly AboutView about = new AboutView();
        UIElement[] pages;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            Theme.Changed += UpdateBranding;   // the logo and the window icon follow the colour scheme
            UpdateBranding();

            pages = new UIElement[] { search, lists, settings, about };
            foreach (var p in pages) { p.Visibility = Visibility.Collapsed; Host.Children.Add(p); }

            var s = Store.Settings;
            Width = Math.Max(MinWidth, s.Width); Height = Math.Max(MinHeight, s.Height);
            if (s.Left >= 0 && s.Top >= 0 &&
                s.Left < SystemParameters.VirtualScreenWidth - 80 && s.Top < SystemParameters.VirtualScreenHeight - 80)
            {
                WindowStartupLocation = WindowStartupLocation.Manual; Left = s.Left; Top = s.Top;
            }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (s.Maximized) WindowState = WindowState.Maximized;

            SourceInitialized += (a, b) => Theme.ApplyToWindow(this);
            Closing += (a, b) => SaveWindow();
            Loaded += (a, b) => search.FocusBox();
            PreviewKeyDown += OnKey;
            PreviewTextInput += OnTextInput;
            ready = true;
            Show(search);
        }

        void UpdateBranding()
        {
            try
            {
                Logo.Source = Schemes.Logo(Theme.IsDark);
                var icon = Schemes.WindowIcon();
                if (icon != null) Icon = icon;
            }
            catch (Exception ex) { App.Log(ex); }
        }

        void SaveWindow()
        {
            var s = Store.Settings;
            s.Maximized = WindowState == WindowState.Maximized;
            var r = RestoreBounds;
            s.Width = r.Width; s.Height = r.Height; s.Left = r.Left; s.Top = r.Top;
            Store.SaveSettings();
        }

        void Show(UIElement page)
        {
            foreach (var p in pages) p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;
        }

        bool ready;
        RadioButton lastNav;
        int lastNavTick;

        // Checked fires for keyboard / screen-reader selection, Click for the mouse (also when the item is already
        // checked, e.g. going back from a list to the overview). A mouse click raises both: ignore the duplicate.
        void Nav_Changed(object sender, RoutedEventArgs e) { Navigate((RadioButton)sender); }

        void Navigate(RadioButton rb)
        {
            if (!ready) return;
            int now = Environment.TickCount;
            if (rb == lastNav && now - lastNavTick < 250) return;
            lastNav = rb; lastNavTick = now;
            if (rb == NavSearch) { Show(search); search.FocusBox(); }
            else if (rb == NavHistory) { lists.ShowHistory(); Show(lists); }
            else if (rb == NavFavorites) { lists.ShowFavorites(); Show(lists); }
            else if (rb == NavLists) { lists.ShowOverview(); Show(lists); }
            else if (rb == NavSettings) { Show(settings); }
            else if (rb == NavAbout) { Show(about); }
        }

        /// <summary>Used by other pages: jump to the search page with some text.</summary>
        public void OpenSearch(string text)
        {
            NavSearch.IsChecked = true;
            Show(search);
            search.SetQuery(text);
        }

        public void RefreshSearch() { search.Refresh(); }

        /// <summary>Random word: a click on the button under "Search", or Ctrl+R.</summary>
        void Random_Click(object sender, RoutedEventArgs e) { OpenRandom(); }

        public void OpenRandom()
        {
            NavSearch.IsChecked = true;
            Show(search);
            search.OpenRandom();
        }

        /// <summary>Open one dictionary entry in the search page (used by the word preview pop-up).</summary>
        public void OpenWord(Takoboto.Database.WordTag tag, string queryText)
        {
            NavSearch.IsChecked = true;
            Show(search);
            search.OpenWord(tag, queryText);
        }

        /// <summary>Command line (mainly for testing):  --theme dark|light  --page search|history|favorites|lists|kanji|settings|about
        /// --search text  --radicals "a,b"  --tab overview|forms|kanji  --scroll N</summary>
        public void ApplyArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i], v = i + 1 < args.Length ? args[i + 1] : null;
                if (a == "--theme" && v != null) { Theme.Apply(v); i++; }
                else if (a == "--scheme" && v != null) { i++; Store.Settings.Scheme = v; Theme.Apply(Theme.IsDark ? "dark" : "light"); }
                else if (a == "--tab" && v != null) { i++; WordView.TestTab = v; }
                else if (a == "--scroll" && v != null) { i++; double d; if (double.TryParse(v, out d)) WordView.TestScroll = d; }
                else if (a == "--word-test" && v != null) { i++; WordTest(v); }
                else if (a == "--layout" && v != null) { i++; search.TestLayout(v); }
                else if (a == "--type" && v != null) { i++; TypeTest(v); }
                else if (a == "--page" && v != null)
                {
                    i++;
                    var map = new System.Collections.Generic.Dictionary<string, RadioButton>
                    { { "search", NavSearch }, { "history", NavHistory }, { "favorites", NavFavorites }, { "lists", NavLists }, { "settings", NavSettings }, { "about", NavAbout } };
                    RadioButton rb;
                    if (map.TryGetValue(v, out rb)) { rb.IsChecked = true; Navigate(rb); }
                }
                else if (a == "--search" && v != null) { i++; OpenSearch(v); }
                else if (a == "--radicals" && v != null) { i++; NavSearch.IsChecked = true; Show(search); search.ShowRadicals(v.Split(',')); }
            }
        }

        /// <summary>--word-test click:食事  or  select:食事 : a few seconds after start, click / select that word of a sentence
        /// in the open card (lets the automated tests exercise the preview pop-up without a real mouse).</summary>
        void WordTest(string what)
        {
            bool click = what.StartsWith("click:");
            string word = what.Substring(what.IndexOf(':') + 1);
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                var q = new System.Collections.Generic.Queue<DependencyObject>();
                q.Enqueue(this);
                while (q.Count > 0)
                {
                    var o = q.Dequeue();
                    var st = o as SelText;
                    if (st != null && st.Probe(word, click)) return;
                    for (int i = 0, n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i < n; i++) q.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(o, i));
                }
            };
            t.Start();
        }

        /// <summary>--type text : a moment after start, "type" the text letter by letter as if it came from the keyboard
        /// (goes through the same path as keys pressed anywhere on the search page).</summary>
        void TypeTest(string text)
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                foreach (char c in text) search.TypeFromAnywhere(c.ToString());
            };
            t.Start();
        }

        public void OpenList(UserList l)
        {
            NavLists.IsChecked = true;
            lists.ShowUserList(l);
            Show(lists);
        }

        // ---- on the search page every key goes to the search box, wherever the focus is ------------------------------
        bool SearchPageActive { get { return search.Visibility == Visibility.Visible; } }

        static bool IsTextEntry(IInputElement f)
        {
            return f is System.Windows.Controls.TextBox || f is PasswordBox || (f is ComboBox && ((ComboBox)f).IsEditable);
        }

        void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!SearchPageActive || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
            var f = Keyboard.FocusedElement;
            if (IsTextEntry(f)) return;   // already typing into a box
            // a space presses the focused button / selects the focused row: leave it to them
            if (e.Text.Trim().Length == 0 && (f is System.Windows.Controls.Primitives.ButtonBase || f is ListBoxItem)) return;
            e.Handled = true;
            search.TypeFromAnywhere(e.Text);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && Keyboard.Modifiers == ModifierKeys.None && SearchPageActive && !IsTextEntry(Keyboard.FocusedElement))
            {
                search.BackspaceFromAnywhere();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            switch (e.Key)
            {
                case Key.F: case Key.L: NavSearch.IsChecked = true; Navigate(NavSearch); e.Handled = true; break;
                case Key.D1: NavSearch.IsChecked = true; Navigate(NavSearch); e.Handled = true; break;
                case Key.D2: NavHistory.IsChecked = true; Navigate(NavHistory); e.Handled = true; break;
                case Key.D3: NavFavorites.IsChecked = true; Navigate(NavFavorites); e.Handled = true; break;
                case Key.D4: NavLists.IsChecked = true; Navigate(NavLists); e.Handled = true; break;
                case Key.D5: NavSearch.IsChecked = true; Show(search); search.ShowRadicals(); e.Handled = true; break;   // search by radical
                case Key.R: OpenRandom(); e.Handled = true; break;
            }
        }
    }
}
