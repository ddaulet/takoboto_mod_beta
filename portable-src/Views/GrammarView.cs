using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TakobotoApp
{
    /// <summary>The Grammar page: a list of constructions (left) and the card of the chosen one (right), built like a word card:
    /// the title, a "Meanings" card (every meaning with its connection patterns and its explanation in Japanese, English and - if switched on
    /// in Settings - Chinese) and an "Example sentences" card (the book's examples grouped by meaning with a filter, each with its translations),
    /// then sentences found in the dictionary.</summary>
    public class GrammarView : UserControl
    {
        static readonly FontFamily ZhFont = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
        const double ExplSize = 19;       // the explanations are as big as the Japanese examples

        readonly TextBox box;
        readonly ListBox list;
        readonly TextBlock status;
        readonly StackPanel card;
        readonly ScrollViewer scroll;
        bool filling;
        int pendingId;
        int showToken;
        int filter = -1;                 // -1 all meanings, otherwise the index of the section
        GEntry shown;

        public GrammarView()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 360 });

            var left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            box = new TextBox { Height = 44, Margin = new Thickness(16, 16, 16, 10), Padding = new Thickness(14, 0, 14, 0), FontSize = 16, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search constructions: Japanese (kana), or a word of the explanations" };
            AutomationProperties.SetName(box, "Search grammar");
            box.TextChanged += (s, e) => Populate();
            box.KeyDown += (s, e) => { if (e.Key == Key.Down && list.Items.Count > 0) { list.Focus(); if (list.SelectedIndex < 0) list.SelectedIndex = 0; } };
            left.Children.Add(box);
            list = new ListBox { Style = (Style)FindResource("PlainList"), ItemTemplate = EntryTemplate() };
            list.SelectionChanged += (s, e) => { if (!filling && list.SelectedItem is GEntry) { filter = -1; ShowEntry((GEntry)list.SelectedItem); } };
            Grid.SetRow(list, 1);
            left.Children.Add(list);
            status = new TextBlock { FontSize = 12.5, Margin = new Thickness(16, 6, 16, 6) };
            status.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            var statusBar = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = status };
            statusBar.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetRow(statusBar, 2);
            left.Children.Add(statusBar);
            grid.Children.Add(left);

            var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Transparent, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Cursor = Cursors.SizeWE };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            card = new StackPanel();
            var holder = new Grid { Margin = new Thickness(14, 12, 14, 14) };
            holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 1100 });
            holder.Children.Add(card);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = holder };
            var right = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Child = scroll };
            right.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);

            Content = grid;
            ShowMessage("Select a construction to see its meanings, explanation and examples.");
            Loaded += (s, e) => EnsureData();
        }

        // a row of the list: the construction and, under it, a short line (the meaning tags)
        static DataTemplate EntryTemplate()
        {
            var tpl = new DataTemplate(typeof(GEntry));
            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.MarginProperty, new Thickness(2, 5, 2, 5));
            var t1 = new FrameworkElementFactory(typeof(TextBlock));
            t1.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("TitleText"));
            t1.SetValue(TextBlock.FontSizeProperty, 17.0);
            t1.SetValue(TextBlock.FontFamilyProperty, Ui.JpFont);
            t1.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
            sp.AppendChild(t1);
            var t2 = new FrameworkElementFactory(typeof(TextBlock));
            t2.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Subtitle"));
            t2.SetValue(TextBlock.FontSizeProperty, 13.0);
            t2.SetValue(TextBlock.FontFamilyProperty, Ui.JpFont);
            t2.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            t2.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            sp.AppendChild(t2);
            tpl.VisualTree = sp;
            return tpl;
        }

        void ShowMessage(string text)
        {
            card.Children.Clear();
            card.Children.Add(Ui.SText(text, 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
        }

        /// <summary>The Chinese switch in Settings was changed: show the open card again.</summary>
        public void Refresh() { if (shown != null) ShowEntry(shown); }

        async void EnsureData()
        {
            if (Grammar.Loaded) { if (list.Items.Count == 0) Populate(); return; }
            status.Text = "Loading…";
            await Grammar.EnsureLoaded();
            Populate();
            if (pendingId > 0) { var e = Grammar.ById(pendingId); pendingId = 0; if (e != null) Select(e); }
        }

        void Populate()
        {
            if (!Grammar.Loaded) return;
            var found = Grammar.Search(box.Text).ToList();
            filling = true;
            list.Items.Clear();
            foreach (var e in found.Take(600)) list.Items.Add(e);
            filling = false;
            status.Text = found.Count + (found.Count == 1 ? " construction" : " constructions") + (found.Count > 600 ? " (the first 600 are shown, type more)" : "");
        }

        /// <summary>Open the card of a construction (also from a word card).</summary>
        public void OpenEntry(int id)
        {
            if (!Grammar.Loaded) { pendingId = id; EnsureData(); return; }
            var e = Grammar.ById(id);
            if (e != null) Select(e);
        }

        void Select(GEntry e)
        {
            if (!list.Items.Contains(e)) { filling = true; box.Text = ""; filling = false; Populate(); }
            filling = true;
            list.SelectedItem = e;
            filling = false;
            list.ScrollIntoView(e);
            filter = -1;
            ShowEntry(e);
        }

        static readonly Regex LeadRe = new Regex(@"^(\d+[a-zA-Z]?)\s+(.*)$");

        /// <summary>"1a Nのあいだ〈空間〉" -> number "1a" and the rest.</summary>
        static void SplitLabel(string label, out string lead, out string rest)
        {
            var m = LeadRe.Match(label ?? "");
            if (m.Success) { lead = m.Groups[1].Value; rest = m.Groups[2].Value; }
            else { lead = ""; rest = label ?? ""; }
        }

        static bool HasContent(GSection s)
        {
            return s.Examples.Count > 0 || !string.IsNullOrWhiteSpace(s.Connection) || !string.IsNullOrWhiteSpace(s.ExplJa) || !string.IsNullOrWhiteSpace(s.ExplEn) || !string.IsNullOrWhiteSpace(s.ExplZh) || !string.IsNullOrWhiteSpace(s.Label);
        }

        // a text that is nothing but references to other constructions ("→【てあげる】")
        static bool IsRefOnly(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(RefRe.Replace(text, ""));
        }

        static UIElement WithFlag(string flagFile, UIElement text)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var flag = Ui.FlagImage(flagFile, 18);
            flag.Margin = new Thickness(0, 4, 8, 0);
            g.Children.Add(flag);
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            return g;
        }

        static readonly Regex RefRe = Grammar.RefRe;

        /// <summary>The names of the constructions a text refers to (normalised).</summary>
        static HashSet<string> RefNames(string text)
        {
            var set = new HashSet<string>();
            foreach (Match m in RefRe.Matches(text ?? "")) set.Add(Grammar.NormTitle(m.Groups[1].Value));
            return set;
        }

        // a text in which every reference like →【てあげる】 is a link that opens that construction
        UIElement Linked(string text, double size, string brush, System.Windows.FontWeight? weight, Thickness margin, bool jp, bool zhFont = false)
        {
            if (!RefRe.IsMatch(text))
            {
                var plain = Ui.SText(text, size, brush, weight, margin, jp);
                if (zhFont) plain.FontFamily = ZhFont;
                return plain;
            }
            var t = new SelText { FontSize = size, Margin = margin };
            t.SetResourceReference(Control.ForegroundProperty, brush);
            if (weight.HasValue) t.FontWeight = weight.Value;
            if (jp) t.FontFamily = Ui.JpFont;
            if (zhFont) t.FontFamily = ZhFont;
            int pos = 0;
            foreach (Match mt in RefRe.Matches(text))
            {
                if (mt.Index > pos) t.Inlines.Add(new Run(text.Substring(pos, mt.Index - pos)));
                string name = mt.Groups[1].Value;
                var run = new Run(mt.Value) { Cursor = Cursors.Hand, Tag = new Action(() => OpenByName(name)) };
                run.TextDecorations = System.Windows.TextDecorations.Underline;
                run.MouseEnter += (sn, ev) => run.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                run.MouseLeave += (sn, ev) => run.ClearValue(TextElement.ForegroundProperty);
                t.Inlines.Add(run);
                pos = mt.Index + mt.Length;
            }
            if (pos < text.Length) t.Inlines.Add(new Run(text.Substring(pos)));
            return t;
        }

        /// <summary>"Referred to from: ..." - the constructions whose texts point to this one, each a link to its page.</summary>
        UIElement BackLinks(GEntry e)
        {
            var t = new SelText { FontSize = 16, FontFamily = Ui.JpFont, Margin = new Thickness(0) };
            t.SetResourceReference(Control.ForegroundProperty, "FgMuted");
            t.Inlines.Add(new Run("Referred to from:  "));
            bool first = true;
            foreach (var r in e.ReferencedBy)
            {
                if (!first) t.Inlines.Add(new Run("   "));
                first = false;
                var target = r;
                var run = new Run(r.Title) { Cursor = Cursors.Hand, Tag = new Action(() => Select(target)) };
                run.TextDecorations = System.Windows.TextDecorations.Underline;
                run.SetResourceReference(TextElement.ForegroundProperty, "Fg");
                run.MouseEnter += (sn, ev) => run.SetResourceReference(TextElement.ForegroundProperty, "Accent");
                run.MouseLeave += (sn, ev) => run.SetResourceReference(TextElement.ForegroundProperty, "Fg");
                t.Inlines.Add(run);
            }
            return t;
        }

        /// <summary>For the render test: open a construction by its name.</summary>
        public void OpenTitle(string name) { OpenByName(name); }

        void OpenByName(string name)
        {
            var e = Grammar.FindTitle(name);
            if (e != null) Select(e);
        }

        static UIElement Zh(string text, double size, bool muted)
        {
            var t = Ui.SText(text, size, muted ? "FgMuted" : "Fg");
            t.FontFamily = ZhFont;
            return t;
        }

        void ShowEntry(GEntry e)
        {
            shown = e;
            bool zh = Store.Settings.GrammarChinese;
            int my = ++showToken;
            card.Children.Clear();

            var head = new StackPanel();
            head.Children.Add(Ui.SText(e.Title, 34, "Fg", null, new Thickness(0), true));
            card.Children.Add(Ui.Card(head));

            var secs = e.Sections.Where(HasContent).ToList();

            // meanings: number | the form, its patterns, the Japanese explanation | the English explanation | the Chinese explanation
            var m = new StackPanel();
            // the title on the left, the name of the book in the top right corner of the card
            var titleRow = new Grid();
            titleRow.Children.Add(Ui.Title("Explanation"));
            var bookName = Ui.Text("日本語文型辞典", 12.5, "FgFaint", null, new Thickness(0, 2, 0, 0), true);   // a plain text block: it can stand at the right edge
            bookName.HorizontalAlignment = HorizontalAlignment.Right;
            bookName.VerticalAlignment = VerticalAlignment.Top;
            titleRow.Children.Add(bookName);
            m.Children.Add(titleRow);
            foreach (var s in secs)
            {
                string lead, rest;
                SplitLabel(s.Label, out lead, out rest);
                var row = new Grid { Margin = new Thickness(0, 0, 0, 18) };
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(lead.Length > 0 ? 46 : 0) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (zh) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (lead.Length > 0) row.Children.Add(Ui.SText(lead, 17, "FgMuted", System.Windows.FontWeights.SemiBold, new Thickness(0, 2, 0, 0)));
                // the construction (green) and its connection patterns stand over all the columns, the explanations start below them on one line
                var headBox = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                var explRefs = new HashSet<string>(new[] { s.ExplJa, s.ExplEn, zh ? s.ExplZh : null }.Where(t => IsRefOnly(t)).SelectMany(t => RefNames(t)));
                bool headIsSameRef = IsRefOnly(rest) && RefNames(rest).Count > 0 && RefNames(rest).IsSubsetOf(explRefs);
                if (!string.IsNullOrWhiteSpace(rest) && !headIsSameRef) headBox.Children.Add(Linked(rest, 19, "Accent", System.Windows.FontWeights.SemiBold, new Thickness(0, 0, 0, 3), true));
                if (!string.IsNullOrWhiteSpace(s.Connection)) headBox.Children.Add(Linked(s.Connection, 15, "FgMuted", null, new Thickness(0), true));
                if (headBox.Children.Count > 0)
                {
                    Grid.SetColumn(headBox, 1);
                    Grid.SetColumnSpan(headBox, zh ? 3 : 2);
                    row.Children.Add(headBox);
                }
                // when the explanations are nothing but a reference to another construction, ONE white link is shown (not the same link in every language)
                var explTexts = new[] { s.ExplJa, s.ExplEn, zh ? s.ExplZh : null }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                bool refOnly = explTexts.Count > 0 && explTexts.All(IsRefOnly);
                if (refOnly)
                {
                    var refBox = new StackPanel();
                    var distinct = explTexts.GroupBy(t => string.Join("|", RefNames(t).OrderBy(x => x))).Select(g => g.First());
                    foreach (var r in distinct) refBox.Children.Add(Linked(r, ExplSize, "Fg", null, new Thickness(0, 0, 0, 2), true));
                    Grid.SetColumn(refBox, 1); Grid.SetRow(refBox, 1); Grid.SetColumnSpan(refBox, zh ? 3 : 2);
                    row.Children.Add(refBox);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(s.ExplJa))
                    {
                        var ja = WordView.TokenText(s.ExplJa, s.TokJa, ExplSize, "Fg", true, new Thickness(0, 0, 16, 0), null, OpenByName);
                        Grid.SetColumn(ja, 1); Grid.SetRow(ja, 1);
                        row.Children.Add(ja);
                    }
                    if (!string.IsNullOrWhiteSpace(s.ExplEn))
                    {
                        var en = WithFlag("FlagEn.png", Linked(s.ExplEn, ExplSize, "Fg", null, new Thickness(0, 0, 16, 0), false));
                        Grid.SetColumn(en, 2); Grid.SetRow(en, 1);
                        row.Children.Add(en);
                    }
                    if (zh && !string.IsNullOrWhiteSpace(s.ExplZh))
                    {
                        var z = WithFlag("FlagZh.png", Linked(s.ExplZh, ExplSize, "Fg", null, new Thickness(0), false, true));
                        Grid.SetColumn(z, 3); Grid.SetRow(z, 1);
                        row.Children.Add(z);
                    }
                }
                m.Children.Add(row);
            }
            if (e.ReferencedBy.Count > 0)
            {
                var line = new System.Windows.Shapes.Rectangle { Height = 1, Margin = new Thickness(0, 0, 0, 8) };
                line.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Line");
                m.Children.Add(line);
                m.Children.Add(BackLinks(e));
            }
            card.Children.Add(Ui.Card(m));

            // the examples of the book, grouped by meaning, with a filter by meaning
            var withEx = secs.Where(s => s.Examples.Count > 0).ToList();
            var seen = new HashSet<string>();
            if (withEx.Count > 0)
            {
                var ex = new StackPanel();
                ex.Children.Add(Ui.Title("Example sentences: " + withEx.Sum(s => s.Examples.Count)));
                if (withEx.Count > 1)
                {
                    var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                    chips.Children.Add(Chip("All", filter < 0, () => { filter = -1; ShowEntry(e); }));
                    foreach (var s in withEx)
                    {
                        int idx = e.Sections.IndexOf(s);
                        string lead, rest;
                        SplitLabel(s.Label, out lead, out rest);
                        chips.Children.Add(Chip((lead + "  " + Shorten(rest)).Trim(), filter == idx, () => { filter = idx; ShowEntry(e); }));
                    }
                    ex.Children.Add(chips);
                }
                var makers = new List<Func<UIElement>>();
                foreach (var s in withEx)
                {
                    foreach (var x in s.Examples) seen.Add(Normalize(x.Ja));
                    if (filter >= 0 && e.Sections.IndexOf(s) != filter) continue;
                    var sec = s;
                    if (withEx.Count > 1 && !string.IsNullOrWhiteSpace(sec.Label))
                        makers.Add(() => Ui.SText(sec.Label, 14, "FgMuted", null, new Thickness(0, 8, 0, 4), true));
                    foreach (var x in sec.Examples) { var xx = x; makers.Add(() => ExampleRow(xx, zh)); }
                }
                card.Children.Add(Ui.Card(ex));
                AddInBatches(ex, makers, my);       // every example is a heavy control (text with furigana): the first ones at once, the rest in the background
            }

            LoadDictionarySentences(e, seen, my);
            scroll.ScrollToTop();
        }

        // adds the rows a few at a time: the window stays responsive while a long card is built
        void AddInBatches(Panel host, List<Func<UIElement>> makers, int my)
        {
            int i = 0;
            Action step = null;
            step = () =>
            {
                if (my != showToken) return;               // another construction was chosen meanwhile
                int end = Math.Min(makers.Count, i + 4);
                for (; i < end; i++) host.Children.Add(makers[i]());
                if (i < makers.Count) Dispatcher.BeginInvoke(step, DispatcherPriority.Background);
            };
            step();
        }

        static string Shorten(string s) { return (s ?? "").Length > 18 ? s.Substring(0, 18) + "…" : (s ?? ""); }

        static string Normalize(string s)
        {
            return new string((s ?? "").Where(c => char.IsLetterOrDigit(c)).ToArray());
        }

        Button Chip(string text, bool selected, Action click)
        {
            var b = new Button { Style = (Style)FindResource(selected ? "AccentButton" : "PillButton"), Content = text, Margin = new Thickness(0, 0, 6, 6), FontFamily = Ui.JpFont, FontSize = 13 };
            b.Click += (s, ev) => click();
            return b;
        }

        // one example like in the word card: the Japanese on the left (with the loudspeaker), the translations on the right, a line in the middle
        static UIElement ExampleRow(GExample x, bool zh)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var jaGrid = new Grid();
            jaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            jaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            jaGrid.Children.Add(WordView.TokenText(x.Ja, x.Tok, ExplSize, "Fg", true, new Thickness(0), null, null));
            jaGrid.VerticalAlignment = VerticalAlignment.Center;       // the Japanese and the translations stand in the middle of the row, against each other
            var say = Ui.SpeakButton(() => x.Ja, 26);
            say.VerticalAlignment = VerticalAlignment.Center;
            say.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(say, 1);
            jaGrid.Children.Add(say);
            var left = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 6, 12, 6), Child = jaGrid };
            left.SetResourceReference(Border.BorderBrushProperty, "Line");
            var tr = new StackPanel { Margin = new Thickness(12, 6, 0, 6), VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrWhiteSpace(x.En)) tr.Children.Add(WithFlag("FlagEn.png", Ui.SText(x.En, 16, "FgMuted", null, new Thickness(0, 0, 0, 2))));
            if (zh && !string.IsNullOrWhiteSpace(x.Zh)) tr.Children.Add(WithFlag("FlagZh.png", Zh(x.Zh, 16, true)));
            Grid.SetColumn(tr, 1);
            grid.Children.Add(left);
            grid.Children.Add(tr);
            var row = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
            row.SetResourceReference(Border.BorderBrushProperty, "Line");
            return row;
        }

        // sentences of the dictionary that contain the headword of the construction (found by the engine), added under the book's examples
        async void LoadDictionarySentences(GEntry e, HashSet<string> seen, int my)
        {
            string key = e.Key;
            if (key.Length < 2 || e.Title.Contains("…") || e.Title.Contains("...") || e.Title.Contains("．")) return;
            List<Sentence> found = null;
            try
            {
                found = await System.Threading.Tasks.Task.Run(() =>
                {
                    var r = Engine.Search(key, 20);
                    var tag = r.Exact.FirstOrDefault();
                    if (tag == null) return null;
                    var w = Engine.LoadWord(tag);
                    return w == null ? null : Engine.MoreSentences(w, 60);
                });
            }
            catch (Exception ex) { App.Log(ex); }
            if (my != showToken || found == null || found.Count == 0) return;
            var list2 = found.Where(s => !seen.Contains(Normalize(s.MainText))).GroupBy(s => Normalize(s.MainText)).Select(g => g.First()).Take(25).ToList();   // the book's copy wins, and every sentence only once
            if (list2.Count == 0) return;
            var st = new StackPanel();
            st.Children.Add(Ui.Title("From the dictionary: " + list2.Count + (found.Count > list2.Count ? "+" : "")));
            st.Children.Add(Ui.SText("Sentences of the dictionary that contain " + e.Title + ". They are found automatically and are not tied to one meaning.", 12.5, "FgFaint", null, new Thickness(0, 0, 0, 6)));
            card.Children.Add(Ui.Card(st));
            AddInBatches(st, list2.Select(x => (Func<UIElement>)(() => WordView.SentenceBlock(x))).ToList(), my);
        }
    }
}
