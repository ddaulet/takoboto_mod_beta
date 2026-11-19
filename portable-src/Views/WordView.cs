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
            var holder = new Grid { Margin = new Thickness(26, 20, 24, 28), HorizontalAlignment = HorizontalAlignment.Stretch };
            holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 880 });
            holder.Children.Add(root);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = holder };

            // tab strip (top left) with the word actions on the right
            tabs = new StackPanel { Orientation = Orientation.Horizontal };
            actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var bar = new Grid { Margin = new Thickness(12, 0, 16, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        public void Clear()
        {
            token++;
            current = null;
            tabBar.Visibility = Visibility.Collapsed;
            root.Children.Clear();
            root.Children.Add(Ui.Text("Select a word to see its details.", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
        }

        public async void Show(WordTag tag)
        {
            if (tag == null) { Clear(); return; }
            int my = ++token;
            current = tag;
            if (tabBar.Visibility == Visibility.Visible) tabBar.Visibility = Visibility.Hidden;   // keep the space, avoid a jump
            root.Children.Clear();
            root.Children.Add(Ui.Text("Loading…", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
            Word w = null;
            try { w = await Task.Run(() => Engine.LoadWord(tag)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token) return;   // another word was selected meanwhile
            root.Children.Clear();
            if (w == null)
            {
                tabBar.Visibility = Visibility.Collapsed;
                root.Children.Add(Ui.Text("This word could not be loaded.", 16, "FgMuted"));
                return;
            }
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
            return Ui.Card(stack);
        }

        // ---- favourite / add to list / copy (shown in the tab strip, so they work on every tab) --------------------
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

            var copy = new Button { Style = (Style)FindResource("IconButton"), Content = "", ToolTip = "Copy" };
            AutomationProperties.SetName(copy, "Copy");
            copy.Click += (s, e) => CopyWord(w);
            actions.Children.Add(copy);
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

        static void CopyWord(Word w)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(string.Join("、", (w.MainEntries ?? new List<WordPart>()).Where(p => p.IsPrimary).Select(p => p.Text)));
                if (w.Readings != null && w.Readings.Count > 0) sb.Append("  (" + string.Join("、", w.Readings.Where(p => p.IsPrimary).Select(p => p.Text)) + ")");
                sb.AppendLine();
                int n = 1;
                foreach (var m in w.Meanings ?? new List<WordMeaning>())
                {
                    var en = m.Parts.Where(p => p.Language == Takoboto.Database.Language.English).Select(p => p.Text);
                    sb.AppendLine((w.Meanings.Count > 1 ? n + ". " : "") + string.Join("; ", en));
                    n++;
                }
                Clipboard.SetText(sb.ToString().Trim());
            }
            catch { }
        }

        // ---- a written form (kanji / kana) as in the original app ---------------------------------------------
        UIElement PartBlock(WordPart p, double size)
        {
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = Ui.JpFont, Margin = new Thickness(0, 0, 0, 6) };
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
            foreach (var p in w.Readings) stack.Children.Add(PartBlock(p, 28));
            return Ui.Card(stack);
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
                var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w.Meanings.Count > 1 ? 34 : 0) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (w.Meanings.Count > 1)
                    row.Children.Add(Ui.Text(n + ".", 16, "FgMuted", FontWeights.SemiBold, new Thickness(0, 1, 0, 0)));

                var body = new StackPanel();
                if (m.HasInfos)
                    body.Children.Add(Ui.Text(string.Join("\n", m.Infos.Select(i => i.Text)), 13.5, "FgMuted", null, new Thickness(0, 0, 0, 4)));
                foreach (var part in m.Parts)
                {
                    var line = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var flag = Ui.Flag(part.Language, 18);
                    flag.Margin = new Thickness(0, 3, 10, 0);
                    line.Children.Add(flag);
                    var t = Ui.Text(part.Text, 16, "Fg", part.IsPrimary ? FontWeights.SemiBold : FontWeights.Normal);
                    if (!part.IsPrimary) t.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
                    Grid.SetColumn(t, 1);
                    line.Children.Add(t);
                    body.Children.Add(line);
                }
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                stack.Children.Add(row);
            }
            return Ui.Card(stack);
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
            var box = new StackPanel { Margin = new Thickness(0, 0, 14, 14) };
            box.Children.Add(Ui.Text(label, 12.5, "FgMuted", FontWeights.SemiBold));
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = Ui.JpFont };
            tb.Inlines.Add(Ui.Run(f.Plain, "Fg", 22));
            if (f.HasAdditionalPlain) tb.Inlines.Add(Ui.Run(", " + f.AdditionalPlain, "Fg", 22));
            if (f.HasPolite) tb.Inlines.Add(Ui.Run(", " + f.Polite, "FgMuted", 22));
            string romaji = !Ui.HasKanji(f.Plain) ? Ui.Romaji(f.Plain) : null;
            if (!string.IsNullOrEmpty(romaji)) { tb.Inlines.Add(new LineBreak()); tb.Inlines.Add(Ui.Run(romaji, "FgFaint", 14)); }
            box.Children.Add(tb);
            if (f.HasExplanation) box.Children.Add(Ui.Text(f.Explanation, 13, "FgMuted"));
            return box;
        }

        // ---- kanji -------------------------------------------------------------------------------------------------
        UIElement BuildKanji(Word w)
        {
            var stack = new StackPanel();
            for (int i = 0; i < w.Kanjis.Count; i++)
            {
                var k = w.Kanjis[i];
                var block = new Grid { Margin = new Thickness(0, 0, 0, i == w.Kanjis.Count - 1 ? 0 : 18), Opacity = k.Opacity };
                block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
                block.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                block.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var big = Ui.Text(k.Kanji, 54, "Fg", null, new Thickness(0, -6, 0, 0), true);
                big.VerticalAlignment = VerticalAlignment.Top;
                block.Children.Add(big);

                var info = new StackPanel();
                var line = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = Ui.JpFont, FontSize = 17 };
                if (k.HasOnReading) { line.Inlines.Add(Ui.Run("ON: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.OnReading, "Fg")); }
                if (k.HasOnReading && k.HasKunReading) line.Inlines.Add(new Run("   "));
                if (k.HasKunReading) { line.Inlines.Add(Ui.Run("KUN: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.KunReading, "Fg")); }
                if (line.Inlines.Count > 0) info.Children.Add(line);
                if (!string.IsNullOrEmpty(k.AllMeanings)) info.Children.Add(Ui.Text(k.AllMeanings, 15, "Fg", null, new Thickness(0, 4, 0, 0)));
                var more = new Button { Style = (Style)FindResource("LinkButton"), Content = "Words with " + k.Kanji, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 14, Margin = new Thickness(0, 6, 0, 0) };
                string kanjiChar = k.Kanji;
                more.Click += (s, e) => MainWindow.Instance.OpenSearch(kanjiChar);
                info.Children.Add(more);
                Grid.SetColumn(info, 1);
                block.Children.Add(info);

                if (k.KanjiStrokes != null && k.KanjiStrokes.Count > 0)
                {
                    var steps = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
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
        UIElement BuildPhrases(Word w)
        {
            var outer = new StackPanel();
            var recommended = w.RecommendedSentences ?? new List<Sentence>();
            var stack = new StackPanel();
            stack.Children.Add(Ui.Title("Phrases"));
            foreach (var s in recommended) stack.Children.Add(SentenceBlock(s));

            var moreHost = new StackPanel();
            var btn = new Button { Style = (Style)FindResource("LinkButton"), Content = "Search phrases", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, recommended.Count > 0 ? 4 : 0, 0, 0) };
            stack.Children.Add(btn);
            stack.Children.Add(moreHost);
            btn.Click += async (s, e) =>
            {
                int my = token;
                btn.IsEnabled = false;
                btn.Content = "Searching…";
                List<Sentence> found = null;
                try { found = await Task.Run(() => Engine.MoreSentences(w)); } catch (Exception ex) { App.Log(ex); }
                if (my != token) return;
                var known = new HashSet<string>(recommended.Select(r => r.MainText));
                var fresh = (found ?? new List<Sentence>()).Where(x => !known.Contains(x.MainText)).ToList();
                if (fresh.Count == 0) btn.Content = "No more phrases found";
                else
                {
                    btn.Visibility = Visibility.Collapsed;
                    moreHost.Children.Add(Ui.Title(fresh.Count + " more phrases"));
                    foreach (var x in fresh) moreHost.Children.Add(SentenceBlock(x));
                }
            };
            outer.Children.Add(Ui.Card(stack));
            return outer;
        }

        UIElement SentenceBlock(Sentence s)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            box.Children.Add(Ui.Text(s.MainText, 22, "Fg", null, null, true));
            foreach (var t in s.Translations)
            {
                var line = new Grid { Margin = new Thickness(0, 3, 0, 0) };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var flag = Ui.Flag(t.Language, 16);
                flag.Margin = new Thickness(0, 3, 8, 0);
                line.Children.Add(flag);
                var tx = Ui.Text(t.Text, 15, "FgMuted");
                Grid.SetColumn(tx, 1);
                line.Children.Add(tx);
                box.Children.Add(line);
            }
            return box;
        }
    }
}
