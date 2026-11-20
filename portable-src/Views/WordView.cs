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
            holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 960 });
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
            root.Children.Add(Ui.SText("Select a word to see its details.", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
        }

        public async void Show(WordTag tag)
        {
            if (tag == null) { Clear(); return; }
            int my = ++token;
            current = tag;
            if (tabBar.Visibility == Visibility.Visible) tabBar.Visibility = Visibility.Hidden;   // keep the space, avoid a jump
            root.Children.Clear();
            root.Children.Add(Ui.SText("Loading…", 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
            Word w = null;
            try { w = await Task.Run(() => Engine.LoadWord(tag)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token) return;   // another word was selected meanwhile
            root.Children.Clear();
            if (w == null)
            {
                tabBar.Visibility = Visibility.Collapsed;
                root.Children.Add(Ui.SText("This word could not be loaded.", 16, "FgMuted"));
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
                    var shown = Langs.Pick(m.Parts, p => p.Language);   // the first language that is shown
                    var first = shown.Count > 0 ? shown[0].Language : Takoboto.Database.Language.English;
                    var en = shown.Where(p => p.Language == first).Select(p => p.Text);
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
            var tb = new SelText { FontFamily = Ui.JpFont, Margin = new Thickness(0, 0, 0, 6) };
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
            var flagged = new HashSet<Language>();   // the languages whose flag has been drawn already (once per language)
            int n = 0;
            foreach (var m in w.Meanings)
            {
                n++;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w.Meanings.Count > 1 ? 34 : 0) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (w.Meanings.Count > 1)
                    row.Children.Add(Ui.SText(n + ".", 16, "FgMuted", FontWeights.SemiBold, new Thickness(0, 1, 0, 0)));

                var body = new StackPanel();
                if (m.HasInfos)
                    body.Children.Add(Ui.SText(string.Join("\n", m.Infos.Select(i => i.Text)), 13.5, "FgMuted", null, new Thickness(0, 0, 0, 4)));
                // only the languages the user wants (all there are, if this meaning has none of them), one flag per language
                var shown = Langs.Pick(m.Parts, p => p.Language);
                body.Children.Add(TranslationBlock(shown.Select(p => new TLine { Lang = p.Language, Text = p.Text, Primary = p.IsPrimary }), 18, 16, true, flagged));
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                stack.Children.Add(row);
            }
            return Ui.Card(stack);
        }

        // ---- translations: ONE flag per language, at the top left of that language's lines ---------------------------
        class TLine { public Language Lang; public string Text; public bool Primary = true; }

        /// <summary>The translations grouped by language (in the order the languages first appear): a flag at the top left
        /// and that language's lines stacked to its right. <paramref name="meaning"/>: the main lines of a meaning are bold,
        /// the others muted; otherwise (example phrases) all lines are muted.</summary>
        /// <param name="seenFlags">When given (the meanings of a word), a language's flag is drawn only the FIRST time that
        /// language appears: a word with three English meanings has one English flag, not three. The text keeps its column.</param>
        static UIElement TranslationBlock(IEnumerable<TLine> lines, double flagSize, double fontSize, bool meaning, HashSet<Language> seenFlags = null)
        {
            var panel = new StackPanel();
            foreach (var group in lines.GroupBy(l => l.Lang))
            {
                var row = new Grid { Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 6, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                FrameworkElement flag = seenFlags == null || seenFlags.Add(group.Key) ? (FrameworkElement)Ui.Flag(group.Key, flagSize) : new Border { Width = flagSize, Height = 1 };
                flag.VerticalAlignment = VerticalAlignment.Top;
                flag.Margin = new Thickness(0, 3, 10, 0);
                row.Children.Add(flag);

                var texts = new StackPanel();
                foreach (var l in group)
                {
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
            var box = new StackPanel { Margin = new Thickness(0, 0, 14, 14) };
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
                var block = new Grid { Margin = new Thickness(0, 0, 0, i == w.Kanjis.Count - 1 ? 0 : 18), Opacity = k.Opacity };
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
            stack.Children.Add(Ui.Title("Example sentences"));
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
                fill();
            };
            var ignore = load();   // runs in the background; errors are logged inside
            return outer;
        }

        // The engine tells which dictionary words a sentence contains (dictionary form + reading) but not where they
        // stand in the text. So each word is looked for in the text: first the whole written form, then (for conjugated
        // words such as 食べる -> 食べて) its stem, which must contain a kanji so that single kana never match by chance.
        // Found words become links: click -> preview pop-up.
        UIElement SentenceText(Sentence s)
        {
            var box = new SelText { FontSize = 20, FontFamily = Ui.JpFont };
            string text = s.MainText ?? "";
            int cursor = 0;
            foreach (var part in s.Parts ?? new List<SentencePart>())
            {
                int at = -1, len = 0;
                string w = part.Writing ?? "";
                for (int l = w.Length; l >= 1 && at < 0; l--)
                {
                    string stem = w.Substring(0, l);
                    if (l < w.Length && !Ui.HasKanji(stem)) break;       // kana-only stems are too ambiguous
                    int i = text.IndexOf(stem, cursor, StringComparison.Ordinal);
                    if (i >= 0) { at = i; len = l; }
                }
                if (at < 0 && !string.IsNullOrEmpty(part.Reading))        // written in kana in this sentence
                {
                    int i = text.IndexOf(part.Reading, cursor, StringComparison.Ordinal);
                    if (i >= 0) { at = i; len = part.Reading.Length; }
                }
                if (at < 0) continue;
                if (at > cursor) box.Inlines.Add(new Run(text.Substring(cursor, at - cursor)));
                box.Inlines.Add(WordLink(text.Substring(at, len), part));
                cursor = at + len;
            }
            if (cursor < text.Length) box.Inlines.Add(new Run(text.Substring(cursor)));
            return box;
        }

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

            var left = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 11, 16, 11), Child = SentenceText(s) };
            left.SetResourceReference(Border.BorderBrushProperty, "Line");

            var right = new StackPanel { Margin = new Thickness(16, 11, 0, 11) };
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
