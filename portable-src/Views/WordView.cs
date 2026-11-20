using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Takoboto.Database;
using DbLanguage = Takoboto.Database.Language;

namespace TakobotoApp
{
    /// <summary>The detail card of one word: writing, reading, meanings, forms, kanji with stroke order, phrases.</summary>
    public class WordView : UserControl
    {
        public static double TestScroll;   // test switch: scroll the detail by this many pixels after loading
        public static string TestTab;      // test switch: open this tab first (overview | forms | kanji)
        static string preferredTab = "overview";   // the tab the user chose last; kept while browsing other words

        readonly Grid shell;
        readonly Border tabBar;
        readonly StackPanel tabs;
        readonly StackPanel actions;
        readonly Button navBack, navForward;
        readonly StackPanel navPanel;
        bool navShown;   // the search page uses back / forward arrows; the other pages do not

        /// <summary>The arrows at the left of the tab strip were pressed (only the search page listens).</summary>
        public event Action NavBack, NavForward;
        readonly ScrollViewer scroll;
        readonly StackPanel root;
        readonly string group = Guid.NewGuid().ToString("N");   // radio group of this view's tabs (several views exist)
        Dictionary<string, Func<UIElement>> builders = new Dictionary<string, Func<UIElement>>();
        Dictionary<string, UIElement> built = new Dictionary<string, UIElement>();
        bool programmatic;
        WordTag current;
        int token;

        static readonly Dictionary<string, string> FormLabels = new Dictionary<string, string>
        {
            { "Nonpast", "Present" }, { "NonpastNegative", "Present, negative" }, { "Past", "Past" }, { "PastNegative", "Past, negative" },
            { "TeForm", "Te-form" }, { "TeFormNegative", "Te-form, negative" }, { "TeFormNegative2", "Te-form, negative (2)" },
            { "Progressive", "Progressive" }, { "ProgressiveNegative", "Progressive, negative" },
            { "Volitional", "Volitional" }, { "VolitionalNegative", "Volitional, negative" },
            { "Imperative", "Imperative" }, { "ImperativeNegative", "Imperative, negative" },
            { "Request", "Request" }, { "RequestNegative", "Request, negative" },
            { "Provisional", "Provisional (-eba)" }, { "ProvisionalNegative", "Provisional, negative" },
            { "Conditional", "Conditional (-tara)" }, { "ConditionalNegative", "Conditional, negative" },
            { "Potential", "Potential" }, { "PotentialNegative", "Potential, negative" },
            { "Passive", "Passive" }, { "PassiveNegative", "Passive, negative" },
            { "Causative", "Causative" }, { "CausativeNegative", "Causative, negative" },
            { "CausativePassive", "Causative passive" }, { "CausativePassiveNegative", "Causative passive, negative" },
            { "Adverbial", "Adverbial" }, { "Noun", "Noun form" },
        };

        public WordView()
        {
            // the content is as wide as the pane up to 880 px and always sits at the LEFT edge (next to the result list);
            // a one-column grid with a MaxWidth does that, a centred StackPanel with MaxWidth would float in the middle
            root = new StackPanel();
            var holder = new Grid { Margin = new Thickness(14, 12, 14, 14), HorizontalAlignment = HorizontalAlignment.Stretch };
            holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 960 });
            holder.Children.Add(root);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = holder };

            // tab strip (top left); the favourite and add-to-list buttons stand right after the last tab
            tabs = new StackPanel { Orientation = Orientation.Horizontal };
            actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var bar = new Grid { Margin = new Thickness(12, 0, 16, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // back / forward
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // tabs
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // favourite, add to list
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            navBack = NavButton(Icons.ArrowLeft, "Back", "Back to the previous card (Alt+Left)", () => { if (NavBack != null) NavBack(); });
            navForward = NavButton(Icons.ArrowRight, "Forward", "Forward (Alt+Right)", () => { if (NavForward != null) NavForward(); });
            nav.Children.Add(navBack);
            nav.Children.Add(navForward);
            nav.Visibility = Visibility.Collapsed;
            navPanel = nav;
            bar.Children.Add(nav);
            Grid.SetColumn(tabs, 1);
            bar.Children.Add(tabs);
            Grid.SetColumn(actions, 2);
            bar.Children.Add(actions);
            tabBar = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = bar, Visibility = Visibility.Collapsed };
            tabBar.SetResourceReference(Border.BorderBrushProperty, "Line");

            shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.Children.Add(tabBar);
            Grid.SetRow(scroll, 1);
            shell.Children.Add(scroll);
            Content = shell;
            Clear();
        }

        public WordTag Current { get { return current; } }

        static Button NavButton(Geometry icon, string name, string tip, Action click)
        {
            var b = new Button { Style = (Style)Application.Current.FindResource("IconButton"), Content = Icons.Make(icon, 20, null), ToolTip = tip };
            AutomationProperties.SetName(b, name);
            b.Click += (s, e) => click();
            return b;
        }

        /// <summary>Show the back / forward arrows at the left of the tab strip (search page) and say which of them can be used.</summary>
        public void SetNav(bool canBack, bool canForward)
        {
            navShown = true;
            navPanel.Visibility = Visibility.Visible;
            if (tabBar.Visibility == Visibility.Collapsed) tabBar.Visibility = Visibility.Visible;
            navBack.IsEnabled = canBack; navBack.Opacity = canBack ? 1 : 0.3;
            navForward.IsEnabled = canForward; navForward.Opacity = canForward ? 1 : 0.3;
        }

        public void Clear()
        {
            token++;
            current = null;
            tabs.Children.Clear();
            actions.Children.Clear();
            tabBar.Visibility = navShown ? Visibility.Visible : Visibility.Collapsed;   // the arrows stay: "back" works from an empty page too
            root.Children.Clear();
            root.Children.Add(Ui.SText("Select a word to see its details.", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
        }

        public async void Show(WordTag tag)
        {
            if (tag == null) { Clear(); return; }
            int my = ++token;
            current = tag;
            tabs.Visibility = Visibility.Hidden;      // keep the space, avoid a jump (the arrows stay)
            actions.Visibility = Visibility.Hidden;
            root.Children.Clear();
            root.Children.Add(Ui.SText("Loading…", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
            Word w = null;
            try { w = await Task.Run(() => Engine.LoadWord(tag)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token) return;   // another word was selected meanwhile
            root.Children.Clear();
            if (w == null)
            {
                tabs.Children.Clear();
                actions.Children.Clear();
                tabBar.Visibility = navShown ? Visibility.Visible : Visibility.Collapsed;
                root.Children.Add(Ui.SText("This word could not be loaded.", 16, "FgMuted"));
                return;
            }
            Voice.Warm();   // the voice takes some seconds to load: start now, so that the first click does not wait (nothing happens without the voice folder)
            Build(w, tag);
            scroll.ScrollToTop();
            if (TestScroll > 0) { var off = TestScroll; Dispatcher.BeginInvoke(new Action(() => scroll.ScrollToVerticalOffset(off)), System.Windows.Threading.DispatcherPriority.Background); }
        }

        // ------------------------------------------------------------------------------------------------------
        // Tabs:  Overview (writing, reading, meanings, phrases) | Forms | Kanji
        // A tab exists only when the word has that data. The tab the user picked last is kept for the next word.
        void Build(Word w, WordTag tag)
        {
            tabs.Children.Clear();
            actions.Children.Clear();
            builders = new Dictionary<string, Func<UIElement>>();
            built = new Dictionary<string, UIElement>();
            var order = new List<KeyValuePair<string, string>>();

            builders["overview"] = () => BuildOverview(w);
            order.Add(new KeyValuePair<string, string>("overview", "Overview"));
            if (w.HasForms && w.Forms != null)
            {
                builders["forms"] = () => BuildForms(w);
                order.Add(new KeyValuePair<string, string>("forms", "Forms"));
            }
            if (w.HasKanjis && w.Kanjis != null && w.Kanjis.Count > 0)
            {
                builders["kanji"] = () => BuildKanji(w);
                order.Add(new KeyValuePair<string, string>("kanji", "Kanji"));
            }
            BuildActions(w, tag);

            string first = TestTab != null && builders.ContainsKey(TestTab) ? TestTab
                         : builders.ContainsKey(preferredTab) ? preferredTab : "overview";
            RadioButton firstButton = null;
            foreach (var kv in order)
            {
                string key = kv.Key;
                var rb = new RadioButton { Style = (Style)FindResource("TabButton"), Content = kv.Value, GroupName = group };
                rb.Checked += (s, e) => { if (!programmatic) preferredTab = key; SelectTab(key); };
                tabs.Children.Add(rb);
                if (key == first) firstButton = rb;
            }
            tabs.Visibility = Visibility.Visible;
            actions.Visibility = Visibility.Visible;
            tabBar.Visibility = Visibility.Visible;
            programmatic = true;
            firstButton.IsChecked = true;
            programmatic = false;
        }

        void SelectTab(string key)
        {
            UIElement content;
            if (!built.TryGetValue(key, out content)) { content = builders[key](); built[key] = content; }
            root.Children.Clear();
            root.Children.Add(content);
            scroll.ScrollToTop();
        }

        UIElement BuildOverview(Word w)
        {
            var panel = new StackPanel();
            panel.Children.Add(BuildHeader(w));
            if (w.HasReadings && w.Readings != null && w.Readings.Count > 0) panel.Children.Add(BuildReadings(w));
            if (w.Meanings != null && w.Meanings.Count > 0) panel.Children.Add(BuildMeanings(w));
            panel.Children.Add(BuildPhrases(w));
            return panel;
        }

        // ---- writing ----------------------------------------------------------------------------------------------
        UIElement BuildHeader(Word w)
        {
            var stack = new StackPanel();
            bool first = true;
            foreach (var p in w.MainEntries ?? new List<WordPart>())
                { stack.Children.Add(PartBlock(p, first ? 36 : 26)); first = false; }
            return CardWithCopy(stack, () => BlockText(w.MainEntries), "Copy writing");
        }

        // ---- favourite / add to list (shown in the tab strip, so they work on every tab) --------------------
        void BuildActions(Word w, WordTag tag)
        {
            var star = new Button { Style = (Style)FindResource("IconButton") };
            Action refresh = () =>
            {
                bool fav = Store.IsFavorite(tag.WordId);
                star.Content = fav ? "" : "";
                star.ToolTip = fav ? "Remove from favorites" : "Add to favorites";
                AutomationProperties.SetName(star, fav ? "Remove from favorites" : "Add to favorites");
                if (fav) star.SetResourceReference(Control.ForegroundProperty, "Accent"); else star.SetResourceReference(Control.ForegroundProperty, "FgMuted");
            };
            refresh();
            star.Click += (s, e) => { Store.ToggleFavorite(tag); refresh(); };
            actions.Children.Add(star);

            var add = new Button { Style = (Style)FindResource("IconButton"), Content = "", ToolTip = "Add to a list" };
            AutomationProperties.SetName(add, "Add to a list");
            add.Click += (s, e) => ShowListMenu(add, tag);
            actions.Children.Add(add);

        }

        void ShowListMenu(FrameworkElement anchor, WordTag tag)
        {
            var items = new List<KeyValuePair<string, Action>>();
            foreach (var l in Store.Lists.Lists.ToList())
            {
                var list = l;
                items.Add(new KeyValuePair<string, Action>(list.Name, () => Store.AddToList(list, tag)));
            }
            items.Add(new KeyValuePair<string, Action>("＋ New list…", () =>
            {
                var created = Store.CreateList("New list");
                if (created != null) { Store.AddToList(created, tag); MainWindow.Instance.OpenList(created); }
            }));
            Ui.ShowMenu(anchor, items);
        }

        // ---- a copy button for ONE block (the writings, the readings): it copies just that block's Japanese text ------------------
        static string BlockText(IEnumerable<WordPart> parts)
        {
            var all = (parts ?? new List<WordPart>()).ToList();
            var main = all.Where(p => p.IsPrimary).ToList();   // the usual forms; the rare ones can still be selected by hand
            return string.Join("、", (main.Count > 0 ? main : all).Select(p => p.Text));
        }

        /// <summary>A card with a small copy button in its top right corner. For a moment after the click the button shows a check mark.</summary>
        static UIElement CardWithCopy(UIElement content, Func<string> text, string tip)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(content);

            var btn = new Button
            {
                Style = (Style)Application.Current.FindResource("IconButton"), Content = "", FontSize = 14, Width = 30, Height = 30,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -6, -6, 0), ToolTip = tip,
            };
            AutomationProperties.SetName(btn, tip);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
            timer.Tick += (s, e) => { timer.Stop(); btn.Content = ""; btn.ClearValue(Control.ForegroundProperty); };
            btn.Click += (s, e) =>
            {
                try { Clipboard.SetText(text()); }
                catch (Exception ex) { App.Log(ex); return; }   // the clipboard can be busy: then nothing is copied and no check mark is shown
                btn.Content = "";
                btn.SetResourceReference(Control.ForegroundProperty, "Accent");
                timer.Stop(); timer.Start();
            };
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);
            return Ui.Card(grid);
        }

        // ---- a written form (kanji / kana) as in the original app ---------------------------------------------
        UIElement PartBlock(WordPart p, double size)
        {
            var tb = new SelText { FontFamily = Ui.JpFont, Margin = new Thickness(0, 0, 0, 3) };
            string brush = p.HasInfoMarkedSecondary ? "FgFaint" : (p.IsSecondary ? "FgMuted" : "Fg");
            tb.Inlines.Add(Ui.Run(p.Text, brush, size));
            if (!Ui.HasKanji(p.Text))
            {
                string r = Ui.Romaji(p.Text);
                if (!string.IsNullOrEmpty(r)) { tb.Inlines.Add(new LineBreak()); tb.Inlines.Add(Ui.Run(r, "FgFaint", 16)); }
            }
            if (p.HasInfo)
                foreach (var info in p.Infos)
                {
                    tb.Inlines.Add(new LineBreak());
                    tb.Inlines.Add(Ui.Run(info.Text, "FgFaint", 14));
                }
            return tb;
        }

        UIElement BuildReadings(Word w)
        {
            var stack = new StackPanel();
            stack.Children.Add(Ui.Title("Reading"));
            foreach (var p in w.Readings)
            {
                // the loudspeaker stands right after the reading: over the text block, at the place where the kana end
                // (the block is a RichTextBox, it cannot be put in a row by its content width)
                var line = new Grid();
                line.Children.Add(PartBlock(p, 28));
                var ft = new FormattedText(p.Text ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(Ui.JpFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 28, Brushes.Black, null, TextFormattingMode.Display, 1.0);
                var say = Ui.SpeakButton(() => p.Text, 30);
                say.HorizontalAlignment = HorizontalAlignment.Left;
                say.VerticalAlignment = VerticalAlignment.Top;
                say.Margin = new Thickness(ft.Width + 12, 3, 0, 0);
                line.Children.Add(say);
                stack.Children.Add(line);
            }
            return CardWithCopy(stack, () => BlockText(w.Readings), "Copy reading");
        }

        // ---- meanings ------------------------------------------------------------------------------------------
        UIElement BuildMeanings(Word w)
        {
            var stack = new StackPanel();
            stack.Children.Add(Ui.Title(w.Meanings.Count > 1 ? "Meanings" : "Meaning"));

            int n = 0;
            foreach (var m in w.Meanings)
            {
                n++;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w.Meanings.Count > 1 ? 34 : 0) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (w.Meanings.Count > 1)
                    row.Children.Add(Ui.SText(n + ".", 16, "FgMuted", FontWeights.SemiBold, new Thickness(0, 1, 0, 0)));

                var body = new StackPanel();
                if (m.HasInfos)
                    body.Children.Add(Ui.SText(string.Join("\n", m.Infos.Select(i => i.Text)), 13.5, "FgMuted", null, new Thickness(0, 0, 0, 4)));
                // only the languages the user wants (all there are, if this meaning has none of them), side by side
                var shown = Langs.Pick(m.Parts, p => p.Language);
                body.Children.Add(MeaningColumns(shown.Select(p => new TLine { Lang = p.Language, Text = p.Text, Primary = p.IsPrimary })));
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                stack.Children.Add(row);
            }
            return Ui.Card(stack);
        }

        static readonly DbLanguage[] ColumnOrder = { DbLanguage.English, DbLanguage.French, DbLanguage.German, DbLanguage.Russian };
        static readonly Typeface MeaningFont = new Typeface(new FontFamily("Segoe UI, Yu Gothic UI, Meiryo UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        /// <summary>A thin, quiet horizontal line: it separates the entries of one language (use / service / role are three
        /// entries, not one text in three rows).</summary>
        static UIElement Hairline()
        {
            var line = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2), Opacity = 0.85, IsHitTestVisible = false };
            line.SetResourceReference(Border.BackgroundProperty, "Line");
            return line;
        }

        /// <summary>Width in pixels of the widest line of a language (or of its longest word), bold 16 px as it is drawn.</summary>
        const double TextBoxSlack = 20;   // a text box wraps a few pixels earlier than the bare text width

        static double TextWidth(IEnumerable<string> pieces)
        {
            double w = 0;
            foreach (var t in pieces)
            {
                var ft = new FormattedText(t ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, MeaningFont, 16, Brushes.Black, null, TextFormattingMode.Display, 1.0);   // text boxes lay their text out in Display mode (wider than Ideal)
                w = Math.Max(w, ft.Width);
            }
            return w;
        }

        /// <summary>The translations of ONE meaning in invisible side-by-side columns, always in the order English, French,
        /// German, Russian (only the languages this meaning has); see <see cref="LangColumns"/> for how the widths are chosen.
        /// Each language has its flag at the top left of its column and its lines under each other.</summary>
        static UIElement MeaningColumns(IEnumerable<TLine> lines)
        {
            var panel = new LangColumns();
            foreach (var group in lines.GroupBy(x => x.Lang).OrderBy(g => Array.IndexOf(ColumnOrder, g.Key)))
            {
                var cell = new Grid();
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var flag = Ui.Flag(group.Key, 16);
                flag.VerticalAlignment = VerticalAlignment.Top;
                flag.Margin = new Thickness(0, 4, 8, 0);
                cell.Children.Add(flag);
                var texts = new StackPanel();
                foreach (var l in group)
                {
                    if (texts.Children.Count > 0) texts.Children.Add(Hairline());   // a thin line between the separate entries of a language
                    texts.Children.Add(Ui.SText(l.Text, 16, l.Primary ? "Fg" : "FgMuted", l.Primary ? FontWeights.SemiBold : FontWeights.Normal, new Thickness(0, 1, 0, 1)));
                }
                Grid.SetColumn(texts, 1);
                cell.Children.Add(texts);
                // the narrowest this column may get: the flag and its longest word
                var words = group.SelectMany(l => (l.Text ?? "").Split(new[] { ' ', '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                cell.Tag = new ColumnInfo { Natural = 24 + TextWidth(group.Select(l => l.Text)) + TextBoxSlack, Minimum = 24 + TextWidth(words) + TextBoxSlack };
                panel.Children.Add(cell);
            }
            return panel;
        }

        // ---- translations: ONE flag per language, at the top left of that language's lines ---------------------------
        class TLine { public Language Lang; public string Text; public bool Primary = true; }

        /// <summary>The translations grouped by language (in the order the languages first appear): a flag at the top left
        /// and that language's lines stacked to its right. <paramref name="meaning"/>: the main lines of a meaning are bold,
        /// the others muted; otherwise (example phrases) all lines are muted.</summary>
        static UIElement TranslationBlock(IEnumerable<TLine> lines, double flagSize, double fontSize, bool meaning)
        {
            var panel = new StackPanel();
            foreach (var group in lines.GroupBy(l => l.Lang))
            {
                var row = new Grid { Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 4, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var flag = Ui.Flag(group.Key, flagSize);
                flag.VerticalAlignment = VerticalAlignment.Top;
                flag.Margin = new Thickness(0, 3, 10, 0);
                row.Children.Add(flag);

                var texts = new StackPanel();
                foreach (var l in group)
                {
                    if (texts.Children.Count > 0) texts.Children.Add(Hairline());
                    var t = meaning
                        ? Ui.SText(l.Text, fontSize, l.Primary ? "Fg" : "FgMuted", l.Primary ? FontWeights.SemiBold : FontWeights.Normal, new Thickness(0, 1, 0, 1))
                        : Ui.SText(l.Text, fontSize, "FgMuted", null, new Thickness(0, 1, 0, 1));
                    texts.Children.Add(t);
                }
                Grid.SetColumn(texts, 1);
                row.Children.Add(texts);
                panel.Children.Add(row);
            }
            return panel;
        }

        // ---- forms (own tab, always fully shown) --------------------------------------------------------------------
        UIElement BuildForms(Word w)
        {
            var panel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var f in typeof(WordForms).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var form = f.GetValue(w.Forms) as WordForm;
                if (form == null || string.IsNullOrEmpty(form.Plain)) continue;
                string label;
                if (!FormLabels.TryGetValue(f.Name, out label)) label = f.Name;
                panel.Children.Add(FormBlock(label, form));
            }
            // as many columns as fit (each at least ~340 px wide)
            panel.SizeChanged += (s, e) =>
            {
                double width = panel.ActualWidth;
                if (width <= 0) return;
                int cols = Math.Max(1, (int)(width / 340));
                foreach (FrameworkElement c in panel.Children) c.Width = Math.Max(120, Math.Floor(width / cols) - 14);
            };
            return Ui.Card(panel);
        }

        UIElement FormBlock(string label, WordForm f)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 12, 10) };
            box.Children.Add(Ui.SText(label, 12.5, "FgMuted", FontWeights.SemiBold));
            var tb = new SelText { FontFamily = Ui.JpFont };
            tb.Inlines.Add(Ui.Run(f.Plain, "Fg", 22));
            if (f.HasAdditionalPlain) tb.Inlines.Add(Ui.Run(", " + f.AdditionalPlain, "Fg", 22));
            if (f.HasPolite) tb.Inlines.Add(Ui.Run(", " + f.Polite, "FgMuted", 22));
            string romaji = !Ui.HasKanji(f.Plain) ? Ui.Romaji(f.Plain) : null;
            if (!string.IsNullOrEmpty(romaji)) { tb.Inlines.Add(new LineBreak()); tb.Inlines.Add(Ui.Run(romaji, "FgFaint", 14)); }
            box.Children.Add(tb);
            if (f.HasExplanation) box.Children.Add(Ui.SText(f.Explanation, 13, "FgMuted"));
            return box;
        }

        // ---- kanji -------------------------------------------------------------------------------------------------
        UIElement BuildKanji(Word w)
        {
            var stack = new StackPanel();
            for (int i = 0; i < w.Kanjis.Count; i++)
            {
                var k = w.Kanjis[i];
                var block = new Grid { Margin = new Thickness(0, 0, 0, i == w.Kanjis.Count - 1 ? 0 : 12), Opacity = k.Opacity };
                block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
                block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var big = Ui.SText(k.Kanji, 54, "Fg", null, new Thickness(0, -6, 0, 0), true);
                big.VerticalAlignment = VerticalAlignment.Top;
                block.Children.Add(big);

                var info = new StackPanel();
                var line = new SelText { FontFamily = Ui.JpFont, FontSize = 17 };
                if (k.HasOnReading) { line.Inlines.Add(Ui.Run("ON: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.OnReading, "Fg")); }
                if (k.HasOnReading && k.HasKunReading) line.Inlines.Add(new Run("   "));
                if (k.HasKunReading) { line.Inlines.Add(Ui.Run("KUN: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.KunReading, "Fg")); }
                if (line.Inlines.Count > 0) info.Children.Add(line);
                if (!string.IsNullOrEmpty(k.AllMeanings)) info.Children.Add(Ui.SText(k.AllMeanings, 15, "Fg", null, new Thickness(0, 4, 0, 0)));
                var more = new Button { Style = (Style)FindResource("LinkButton"), Content = "Words with " + k.Kanji, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 14, Margin = new Thickness(0, 6, 0, 0) };
                string kanjiChar = k.Kanji;
                more.Click += (s, e) => MainWindow.Instance.OpenSearch(kanjiChar);
                info.Children.Add(more);
                Grid.SetColumn(info, 1);
                block.Children.Add(info);

                if (k.KanjiStrokes != null && k.KanjiStrokes.Count > 0)
                {
                    var steps = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
                    foreach (var step in k.KanjiStrokes) steps.Children.Add(StrokeStep(step));
                    Grid.SetRow(steps, 1);
                    Grid.SetColumn(steps, 0);
                    Grid.SetColumnSpan(steps, 2);
                    block.Children.Add(steps);
                }
                stack.Children.Add(block);
            }
            return Ui.Card(stack);
        }

        static UIElement StrokeStep(KanjiStrokes step)
        {
            var canvas = new Canvas { Width = 109, Height = 109 };
            foreach (var sp in step.Paths)
            {
                Geometry g;
                try { g = Geometry.Parse(sp.Data); } catch { continue; }
                var path = new Path
                {
                    Data = g,
                    StrokeThickness = 3.4,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent,
                };
                path.SetResourceReference(Shape.StrokeProperty, sp.IsLast ? "Accent" : "FgFaint");
                canvas.Children.Add(path);
            }
            var cell = new Border { Width = 66, Height = 66, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Padding = new Thickness(3), Child = new Viewbox { Child = canvas } };
            cell.SetResourceReference(Border.BorderBrushProperty, "Line");
            return cell;
        }

        // ---- phrases -----------------------------------------------------------------------------------------------
        // The example phrases come from the dictionary file itself (Tatoeba sentences inside takoboto.dat) - nothing is
        // fetched from the internet. The few "recommended" ones are shown at once; all the others are looked up in the
        // background right away (no button) and added as you scroll down, PhraseBatch at a time, so that a word with
        // hundreds of phrases does not freeze the window.
        const int PhraseBatch = 25;

        UIElement BuildPhrases(Word w)
        {
            var outer = new StackPanel();
            var recommended = w.RecommendedSentences ?? new List<Sentence>();
            var stack = new StackPanel();
            var title = Ui.Title("Example sentences");
            stack.Children.Add(title);
            foreach (var s in recommended) stack.Children.Add(SentenceBlock(s));

            var moreHost = new StackPanel();
            var status = Ui.Text("Looking for example sentences…", 13, "FgMuted", null, new Thickness(0, 6, 0, 0));
            stack.Children.Add(moreHost);
            stack.Children.Add(status);
            outer.Children.Add(Ui.Card(stack));

            int my = token;
            var pending = new List<Sentence>();
            int next = 0;
            bool loaded = false;
            ScrollChangedEventHandler onScroll = null;
            Action fill = null;

            // is the end of the list within ~700 px of the visible area?
            Func<bool> nearEnd = () =>
            {
                if (!moreHost.IsVisible || scroll.ViewportHeight <= 0) return false;
                try { return moreHost.TransformToAncestor(scroll).Transform(new Point(0, moreHost.ActualHeight)).Y < scroll.ViewportHeight + 700; }
                catch (InvalidOperationException) { return false; }
            };

            fill = () =>
            {
                if (my != token) { scroll.ScrollChanged -= onScroll; return; }
                if (!loaded) return;
                while (next < pending.Count && nearEnd())
                {
                    for (int i = 0; i < PhraseBatch && next < pending.Count; i++) moreHost.Children.Add(SentenceBlock(pending[next++]));
                    moreHost.UpdateLayout();
                }
                if (next >= pending.Count)
                {
                    if (recommended.Count == 0 && pending.Count == 0)   // nothing at all: say so, so that it is clear the dictionary has none
                    {
                        status.Visibility = Visibility.Visible;
                        status.Text = "No example sentences for this word - the dictionary has none.";
                    }
                    else status.Visibility = Visibility.Collapsed;
                }
                else { status.Visibility = Visibility.Visible; status.Text = "Showing " + (recommended.Count + next) + " of " + (recommended.Count + pending.Count) + " sentences - the rest loads as you scroll"; }
            };
            onScroll = (s, e) => fill();
            scroll.ScrollChanged += onScroll;
            moreHost.IsVisibleChanged += (s, e) => { if (moreHost.IsVisible) Dispatcher.BeginInvoke(fill, System.Windows.Threading.DispatcherPriority.Loaded); };

            Func<Task> load = async () =>
            {
                List<Sentence> found = null;
                try { found = await Task.Run(() => Engine.MoreSentences(w)); } catch (Exception ex) { App.Log(ex); }
                if (my != token) return;
                var known = new HashSet<string>(recommended.Select(r => r.MainText));
                pending.AddRange((found ?? new List<Sentence>()).Where(x => !known.Contains(x.MainText)));
                loaded = true;
                // how many there are: the card used to scroll on with no visible end
                int total = recommended.Count + pending.Count;
                if (total > 0) title.Text = "Example sentences: " + total + (found != null && found.Count >= Engine.SentenceCap ? "+" : "");
                fill();
            };
            var ignore = load();   // runs in the background; errors are logged inside
            return outer;
        }

        // The words of a sentence are found by SentenceRuby (the engine does not say where they stand in the text) and become
        // links: click -> preview pop-up. With furigana on, the kanji of those words get their reading above them.
        static readonly Dictionary<char, string> onReadingCache = new Dictionary<char, string>();

        static readonly Dictionary<char, string> kunReadingCache = new Dictionary<char, string>();

        static string KunReadingOf(char c)
        {
            string r;
            if (kunReadingCache.TryGetValue(c, out r)) return r;
            try { var k = Engine.Kanji(c); r = k != null ? (k.KunReading ?? "") : ""; } catch { r = ""; }
            kunReadingCache[c] = r;
            return r;
        }

        static string OnReadingOf(char c)
        {
            string r;
            if (onReadingCache.TryGetValue(c, out r)) return r;
            try { var k = Engine.Kanji(c); r = k != null ? (k.OnReading ?? "") : ""; } catch { r = ""; }
            onReadingCache[c] = r;
            return r;
        }

        UIElement SentenceText(Sentence s)
        {
            var box = new SelText { FontSize = 20, FontFamily = Ui.JpFont };
            string text = s.MainText ?? "";
            bool furigana = Store.Settings.Furigana;
            int cursor = 0;
            foreach (var m in SentenceRuby.Analyze(text, s.Parts, OnReadingOf, KunReadingOf))
            {
                if (m.Start > cursor) box.Inlines.Add(new Run(text.Substring(cursor, m.Start - cursor)));
                var run = (Run)WordLink(text.Substring(m.Start, m.Length), m.Part);
                box.Inlines.Add(run);
                if (furigana)
                    foreach (var piece in m.Ruby)
                        box.Ruby.Add(new RubyItem { Run = run, Offset = piece.Offset - m.Start, Length = piece.Length, Reading = piece.Reading });
                cursor = m.Start + m.Length;
            }
            if (cursor < text.Length) box.Inlines.Add(new Run(text.Substring(cursor)));
            if (box.Ruby.Count == 0) return box;

            // the readings are painted over the text; the lines get a little more room above them
            box.MakeRoomForFurigana(FuriganaRoom);
            var host = new Grid();
            host.Children.Add(box);
            host.Children.Add(new RubyLayer(box));
            return host;
        }

        // How much taller a line gets for the furigana. The sentence cell has 6 px of padding above its first line that used to be
        // empty, so the first line costs nothing more than this room minus that padding (see SentenceBlock).
        const double FuriganaRoom = 8;

        // A word of the sentence: dotted underline, turns green under the mouse. It is a plain Run (not a Hyperlink: a
        // Hyperlink swallows the mouse-down, so a text selection could never START on a word). SelText sees the click
        // and runs the action stored in Tag.
        static Inline WordLink(string shown, SentencePart part)
        {
            string writing = part.Writing, reading = part.Reading;
            var run = new Run(shown) { Cursor = System.Windows.Input.Cursors.Hand, Tag = new Action(() => Preview.ShowWord(writing, reading)) };
            run.TextDecorations = new TextDecorationCollection
            {
                // the recipe from the WPF documentation: a pen in "font recommended" units, offset 0 = the normal underline position
                new TextDecoration(TextDecorationLocation.Underline, new Pen(Brushes.Gray, 1) { DashStyle = DashStyles.Dot }, 0,
                    TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended),
            };
            run.MouseEnter += (s, e) => run.SetResourceReference(TextElement.ForegroundProperty, "Accent");
            run.MouseLeave += (s, e) => run.ClearValue(TextElement.ForegroundProperty);
            return run;
        }

        UIElement SentenceBlock(Sentence s)
        {
            // like a bilingual book: the Japanese text on the left, its translations on the right, a line down the middle
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var jp = SentenceText(s);
            bool hasRuby = jp is Grid;   // a sentence with furigana: its lines carry their own room above, so less padding on top
            // the loudspeaker stands after the sentence (top right of the Japanese column)
            var jpRow = new Grid();
            jpRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            jpRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            jpRow.Children.Add(jp);
            var say = Ui.SpeakButton(() => s.MainText, 26);
            say.VerticalAlignment = VerticalAlignment.Top;
            say.Margin = new Thickness(4, hasRuby ? 6 : 3, 0, 0);
            Grid.SetColumn(say, 1);
            jpRow.Children.Add(say);
            var left = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, hasRuby ? 0 : 6, 12, 6), Child = jpRow };
            left.SetResourceReference(Border.BorderBrushProperty, "Line");

            var right = new StackPanel { Margin = new Thickness(12, 6, 0, 6) };
            var shown = Langs.Pick(s.Translations, t => t.Language);   // the wanted languages; everything if there is none of them
            right.Children.Add(TranslationBlock(shown.Select(t => new TLine { Lang = t.Language, Text = t.Text }), 16, 15, false));
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);

            var row = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };   // a hairline between the phrases
            row.SetResourceReference(Border.BorderBrushProperty, "Line");
            return row;
        }
    }
}
