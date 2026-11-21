using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace TakobotoApp
{
    /// <summary>The Grammar page: a list of grammar constructions (left) and the card of the chosen one (right): its meanings, patterns,
    /// explanations in Japanese and English, and example sentences with their English translation.</summary>
    public class GrammarView : UserControl
    {
        readonly TextBox box;
        readonly ListBox list;
        readonly TextBlock status;
        readonly StackPanel card;
        readonly ScrollViewer scroll;
        bool filling;
        int pendingId;

        public GrammarView()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 360 });

            // left: search box + list
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
            list.SelectionChanged += (s, e) => { if (!filling && list.SelectedItem is GEntry) ShowEntry((GEntry)list.SelectedItem); };
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

            // right: the card
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
            ShowEntry(e);
        }

        void ShowEntry(GEntry e)
        {
            card.Children.Clear();
            var head = new StackPanel();
            head.Children.Add(Ui.SText(e.Title, 34, "Fg", null, new Thickness(0, 0, 0, 4), true));
            if (e.Patterns.Count > 0) head.Children.Add(Ui.SText(string.Join("   ", e.Patterns.Select(p => "[" + p + "]")), 15, "FgMuted", null, new Thickness(0, 0, 0, 2), true));
            card.Children.Add(Ui.Card(head));

            foreach (var sec in e.Sections)
            {
                if (sec.Examples.Count == 0 && sec.Patterns.Count == 0 && string.IsNullOrWhiteSpace(sec.ExplJa) && string.IsNullOrWhiteSpace(sec.ExplEn) && string.IsNullOrWhiteSpace(sec.Head)) continue;
                var st = new StackPanel();
                string title = (sec.Sense > 0 ? sec.Sense + (sec.Form.Length > 0 ? sec.Form : "") + ".  " : (sec.Form.Length > 0 ? sec.Form + ")  " : "")) + (sec.Head ?? "");
                if (title.Trim().Length > 0) st.Children.Add(Ui.SText(title.Trim(), 18, "Accent", System.Windows.FontWeights.SemiBold, new Thickness(0, 0, 0, 4), true));
                if (sec.Patterns.Count > 0) st.Children.Add(Ui.SText(string.Join("   ", sec.Patterns.Select(p => "[" + p.Trim('[', ']', '［', '］') + "]")), 15, "FgMuted", null, new Thickness(0, 0, 0, 6), true));

                if (sec.Examples.Count > 0)
                {
                    foreach (var ex in sec.Examples)
                    {
                        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var texts = new StackPanel();
                        texts.Children.Add(Ui.SText(ex.Ja, 19, "Fg", null, new Thickness(0, 0, 0, 1), true));
                        if (!string.IsNullOrWhiteSpace(ex.En)) texts.Children.Add(Ui.SText(ex.En, 14.5, "FgMuted"));
                        row.Children.Add(texts);
                        var say = Ui.SpeakButton(() => ex.Ja, 28);
                        say.VerticalAlignment = VerticalAlignment.Top;
                        Grid.SetColumn(say, 1);
                        row.Children.Add(say);
                        st.Children.Add(row);
                    }
                }
                if (!string.IsNullOrWhiteSpace(sec.ExplJa))
                {
                    st.Children.Add(Ui.SText("日本語", 12.5, "FgFaint", null, new Thickness(0, 4, 0, 1), true));
                    st.Children.Add(Ui.SText(sec.ExplJa, 15.5, "Fg", null, new Thickness(0, 0, 0, 6), true));
                }
                if (!string.IsNullOrWhiteSpace(sec.ExplEn))
                {
                    st.Children.Add(Ui.SText("English", 12.5, "FgFaint", null, new Thickness(0, 2, 0, 1)));
                    st.Children.Add(Ui.SText(sec.ExplEn, 15.5, "Fg", null, new Thickness(0, 0, 0, 4)));
                }
                card.Children.Add(Ui.Card(st));
            }
            scroll.ScrollToTop();
        }
    }
}
