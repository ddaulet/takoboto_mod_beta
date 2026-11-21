using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>The Grammar page: a list of constructions (left) and the card of the chosen one (right), built like a word card:
    /// the title, a "Meanings" card (every meaning with its patterns and its explanation in Japanese and in English) and an "Example
    /// sentences" card (the book's examples grouped by meaning, a filter by meaning, then sentences found in the dictionary).</summary>
    public class GrammarView : UserControl
    {
        readonly TextBox box;
        readonly ListBox list;
        readonly TextBlock status;
        readonly StackPanel card;
        readonly ScrollViewer scroll;
        bool filling;
        int pendingId;
        int showToken;
        int filter = -1;                 // -1 all meanings, otherwise the index of the section

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
            box = new TextBox { Height = 44, Margin = new Thickness(16, 16, 16, 10), Padding = new Thickness(14, 0, 14, 0), FontSize = 16, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search constructions: Japanese (kana), or a word of the English explanation" };
            AutomationProperties.SetName(box, "Search grammar");
            box.TextChanged += (s, e) => Populate();
            box.KeyDown += (s, e) => { if (e.Key == Key.Down && list.Items.Count > 0) { list.Focus(); if (list.SelectedIndex < 0) list.SelectedIndex = 0; } };
            left.Children.Add(box);
            list = new ListBox { Style = (Style)FindResource("PlainList") };
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

            var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = System.Windows.Media.Brushes.Transparent, ResizeBehavior = GridResizeBehavior.PreviousAndNext, Cursor = Cursors.SizeWE };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            card = new StackPanel();
            var holder = new Grid { Margin = new Thickness(14, 12, 14, 14) };
            holder.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 960 });
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

        void ShowMessage(string text)
        {
            card.Children.Clear();
            card.Children.Add(Ui.SText(text, 16, "FgMuted", null, new Thickness(0, 40, 0, 0)));
        }

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
            foreach (var e in found.Take(500)) list.Items.Add(e);
            filling = false;
            status.Text = found.Count + (found.Count == 1 ? " construction" : " constructions") + (found.Count > 500 ? " (the first 500 are shown, type more)" : "");
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

        static string Label(GSection s)
        {
            if (s.Sense > 0) return s.Sense + (s.Form ?? "");
            return string.IsNullOrEmpty(s.Form) ? "" : s.Form + ")";
        }

        static bool HasContent(GSection s)
        {
            return s.Examples.Count > 0 || s.Patterns.Count > 0 || !string.IsNullOrWhiteSpace(s.ExplJa) || !string.IsNullOrWhiteSpace(s.ExplEn) || !string.IsNullOrWhiteSpace(s.Head);
        }

        void ShowEntry(GEntry e)
        {
            int my = ++showToken;
            card.Children.Clear();

            var head = new StackPanel();
            head.Children.Add(Ui.SText(e.Title, 34, "Fg", null, new Thickness(0, 0, 0, 0), true));
            card.Children.Add(Ui.Card(head));

            var secs = e.Sections.Where(HasContent).ToList();

            // meanings: number | the form, its patterns, the Japanese explanation | the English explanation
            var texty = secs.Where(s => !string.IsNullOrWhiteSpace(s.Head) || s.Patterns.Count > 0 || !string.IsNullOrWhiteSpace(s.ExplJa) || !string.IsNullOrWhiteSpace(s.ExplEn)).ToList();
            if (texty.Count > 0)
            {
                var m = new StackPanel();
                m.Children.Add(Ui.Title(secs.Count(s => s.Sense > 0) > 1 ? "Meanings" : "Meaning"));
                foreach (var s in texty)
                {
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.Children.Add(Ui.SText(Label(s), 16, "FgMuted", System.Windows.FontWeights.SemiBold, new Thickness(0, 1, 0, 0)));
                    var ja = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
                    if (!string.IsNullOrWhiteSpace(s.Head)) ja.Children.Add(Ui.SText(s.Head, 17, "Fg", System.Windows.FontWeights.SemiBold, new Thickness(0, 0, 0, 2), true));
                    if (s.Patterns.Count > 0) ja.Children.Add(Ui.SText(string.Join("   ", s.Patterns.Select(p => "[" + p.Trim('[', ']', '［', '］') + "]")), 14, "FgMuted", null, new Thickness(0, 0, 0, 3), true));
                    if (!string.IsNullOrWhiteSpace(s.ExplJa)) ja.Children.Add(Ui.SText(s.ExplJa, 15, "Fg", null, new Thickness(0, 0, 0, 0), true));
                    Grid.SetColumn(ja, 1);
                    row.Children.Add(ja);
                    if (!string.IsNullOrWhiteSpace(s.ExplEn))
                    {
                        var en = Ui.SText(s.ExplEn, 15, "FgMuted", null, new Thickness(0, 0, 0, 0));
                        Grid.SetColumn(en, 2);
                        row.Children.Add(en);
                    }
                    m.Children.Add(row);
                }
                card.Children.Add(Ui.Card(m));
            }

            // the examples of the book, grouped by meaning, with a filter
            var withEx = secs.Where(s => s.Examples.Count > 0).ToList();
            var seen = new HashSet<string>();
            if (withEx.Count > 0)
            {
                var ex = new StackPanel();
                int total = withEx.Sum(s => s.Examples.Count);
                ex.Children.Add(Ui.Title("Example sentences: " + total));
                if (withEx.Count > 1)
                {
                    var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                    chips.Children.Add(Chip("All", filter < 0, () => { filter = -1; ShowEntry(e); }));
                    foreach (var s in withEx)
                    {
                        int idx = e.Sections.IndexOf(s);
                        chips.Children.Add(Chip(Label(s) + (string.IsNullOrEmpty(s.Head) ? "" : "  " + Shorten(s.Head)), filter == idx, () => { filter = idx; ShowEntry(e); }));
                    }
                    ex.Children.Add(chips);
                }
                foreach (var s in withEx)
                {
                    foreach (var x in s.Examples) seen.Add(Normalize(x.Ja));
                    if (filter >= 0 && e.Sections.IndexOf(s) != filter) continue;
                    if (withEx.Count > 1)
                        ex.Children.Add(Ui.SText(Label(s) + (string.IsNullOrEmpty(s.Head) ? "" : "  ·  " + s.Head), 13.5, "FgMuted", null, new Thickness(0, 6, 0, 4), true));
                    foreach (var x in s.Examples) ex.Children.Add(ExampleRow(x));
                }
                card.Children.Add(Ui.Card(ex));
            }

            LoadDictionarySentences(e, seen, my);
            scroll.ScrollToTop();
        }

        static string Shorten(string s) { return s.Length > 18 ? s.Substring(0, 18) + "…" : s; }

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

        // one example like in the word card: the Japanese on the left (with the loudspeaker), the English on the right, a line in the middle
        static UIElement ExampleRow(GExample x)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var jaGrid = new Grid();
            jaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            jaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            jaGrid.Children.Add(Ui.SText(x.Ja, 19, "Fg", null, new Thickness(0), true));
            var say = Ui.SpeakButton(() => x.Ja, 26);
            say.VerticalAlignment = VerticalAlignment.Top;
            say.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(say, 1);
            jaGrid.Children.Add(say);
            var left = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 6, 12, 6), Child = jaGrid };
            left.SetResourceReference(Border.BorderBrushProperty, "Line");
            var right = new Border { Padding = new Thickness(12, 6, 0, 6) };
            if (!string.IsNullOrWhiteSpace(x.En)) right.Child = Ui.SText(x.En, 15, "FgMuted");
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
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
            var list2 = found.Where(s => !seen.Contains(Normalize(s.MainText))).Take(25).ToList();
            if (list2.Count == 0) return;
            var st = new StackPanel();
            st.Children.Add(Ui.Title("From the dictionary: " + list2.Count + (found.Count > list2.Count ? "+" : "")));
            st.Children.Add(Ui.SText("Sentences of the dictionary that contain " + e.Title + ". They are found automatically and are not tied to one meaning.", 12.5, "FgFaint", null, new Thickness(0, 0, 0, 6)));
            foreach (var s in list2) st.Children.Add(WordView.SentenceBlock(s));
            card.Children.Add(Ui.Card(st));
        }
    }
}
