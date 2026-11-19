using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Takoboto.Database;
using Takoboto.Database.Impl;

namespace TakobotoApp
{
    /// <summary>"Find kanji by radicals": pick radicals, the kanji that contain all of them are listed.
    /// Same logic as the original: the selected radicals' kanji lists are intersected; radicals that would
    /// give an empty result are disabled.</summary>
    public class RadicalsView : UserControl
    {
        class Tile
        {
            public KanjiRadical Radical;
            public Border Box;
            public TextBlock Label;
            public System.Windows.Shapes.Rectangle Icon;
            public bool Selected;
            public bool Enabled = true;
        }

        readonly List<Tile> tiles = new List<Tile>();
        readonly WrapPanel resultPanel = new WrapPanel();
        readonly TextBlock resultInfo;
        readonly ContentControl detailHost = new ContentControl();
        readonly Button clearBtn;
        readonly TextBlock selectedText;
        int token;
        Border selectedKanjiTile;

        public RadicalsView()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // header
            var header = new Grid { Margin = new Thickness(28, 24, 24, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel();
            titles.Children.Add(Ui.Text("Find kanji by radicals", 26, "Accent", FontWeights.SemiBold));
            selectedText = Ui.Text("Pick the radicals the kanji contains.", 14, "FgMuted", null, new Thickness(0, 4, 0, 0), true);
            titles.Children.Add(selectedText);
            header.Children.Add(titles);
            clearBtn = new Button { Style = (Style)Application.Current.FindResource("PillButton"), Content = "Clear", VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
            clearBtn.Click += (s, e) => ClearSelection();
            Grid.SetColumn(clearBtn, 1);
            header.Children.Add(clearBtn);
            root.Children.Add(header);

            // body: radicals | results
            var body = new Grid();
            Grid.SetRow(body, 1);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star), MinWidth = 380 });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });

            var radicalsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = BuildRadicals() };
            body.Children.Add(radicalsScroll);

            var right = new Border { BorderThickness = new Thickness(1, 0, 0, 0) };
            right.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid.SetColumn(right, 1);
            var rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rightStack = new StackPanel { Margin = new Thickness(24, 6, 20, 16) };
            resultInfo = Ui.Text("", 14, "FgMuted", null, new Thickness(0, 0, 0, 10));
            rightStack.Children.Add(resultInfo);
            rightStack.Children.Add(resultPanel);
            rightScroll.Content = rightStack;
            rightGrid.Children.Add(rightScroll);
            Grid.SetRow(detailHost, 1);
            detailHost.Margin = new Thickness(18, 0, 18, 18);
            rightGrid.Children.Add(detailHost);
            right.Child = rightGrid;
            body.Children.Add(right);

            root.Children.Add(body);
            Content = root;
            UpdateResults();
        }

        // ---- the radical grid -----------------------------------------------------------------------------------
        UIElement BuildRadicals()
        {
            var stack = new StackPanel { Margin = new Thickness(28, 4, 20, 24) };
            WrapPanel group = null;
            int strokes = -1;
            foreach (var r in KanjiRadicals.Radicals)
            {
                if (r.StrokeCount != strokes)
                {
                    strokes = r.StrokeCount;
                    stack.Children.Add(Ui.Text(strokes + (strokes == 1 ? " stroke" : " strokes"), 12.5, "FgFaint", FontWeights.SemiBold, new Thickness(2, 12, 0, 4)));
                    group = new WrapPanel();
                    stack.Children.Add(group);
                }
                group.Children.Add(MakeTile(r).Box);
            }
            return stack;
        }

        Tile MakeTile(KanjiRadical r)
        {
            var t = new Tile { Radical = r };
            string text = r.Radical.ToString();
            t.Box = new Border
            {
                Width = 44, Height = 44, Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = text + "  (" + r.StrokeCount + " strokes)",
            };
            if (text == "个")
            {
                // this glyph is missing from most fonts: the original shows an image instead
                // the picture is a white glyph on transparent: use it as a mask and paint it in the text colour of the theme
                t.Icon = new System.Windows.Shapes.Rectangle { Margin = new Thickness(9), OpacityMask = new ImageBrush(Ui.Resource("RadicalA.png")) };
                t.Box.Child = t.Icon;
            }
            else
            {
                t.Label = new TextBlock { Text = text, FontSize = 24, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                t.Box.Child = t.Label;
            }
            t.Box.MouseLeftButtonUp += (s, e) => { if (t.Enabled) { t.Selected = !t.Selected; Recalculate(); } };
            tiles.Add(t);
            PaintTile(t);
            return t;
        }

        void PaintTile(Tile t)
        {
            // selected = accent fill with light text, disabled = faded, normal = quiet chip
            if (t.Selected)
            {
                t.Box.SetResourceReference(Border.BackgroundProperty, "AccentFill");
                t.Box.SetResourceReference(Border.BorderBrushProperty, "AccentFill");
            }
            else
            {
                t.Box.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
                t.Box.SetResourceReference(Border.BorderBrushProperty, "Line");
            }
            if (t.Label != null) t.Label.SetResourceReference(TextBlock.ForegroundProperty, t.Selected ? "AccentFg" : "Fg");
            if (t.Icon != null) t.Icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, t.Selected ? "AccentFg" : "Fg");
            t.Box.Opacity = t.Enabled ? 1.0 : 0.28;
            t.Box.Cursor = t.Enabled ? Cursors.Hand : Cursors.Arrow;
        }

        // ---- selection logic (as in the original app) ----------------------------------------------------------------
        string candidates;   // kanji that contain every selected radical (null = nothing selected)

        void Recalculate()
        {
            candidates = null;
            foreach (var t in tiles.Where(x => x.Selected))
                candidates = candidates == null ? t.Radical.Kanjis : KanjiRadical.Intersect(candidates, t.Radical.Kanjis);
            foreach (var t in tiles)
            {
                t.Enabled = t.Selected || candidates == null || KanjiRadical.HasIntersect(candidates, t.Radical.Kanjis);
                PaintTile(t);
            }
            var chosen = tiles.Where(x => x.Selected).Select(x => x.Radical.Radical).ToArray();
            clearBtn.IsEnabled = chosen.Length > 0;
            selectedText.Text = chosen.Length == 0 ? "Pick the radicals the kanji contains." : "Selected: " + string.Join("  ", chosen);
            UpdateResults();
        }

        public void Preselect(string[] radicals)
        {
            foreach (var t in tiles) t.Selected = radicals.Contains(t.Radical.Radical.ToString());
            Recalculate();
        }

        void ClearSelection()
        {
            foreach (var t in tiles) { t.Selected = false; t.Enabled = true; PaintTile(t); }
            candidates = null;
            clearBtn.IsEnabled = false;
            selectedText.Text = "Pick the radicals the kanji contains.";
            UpdateResults();
        }

        // ---- results -------------------------------------------------------------------------------------------------
        async void UpdateResults()
        {
            int my = ++token;
            resultPanel.Children.Clear();
            detailHost.Content = null;
            selectedKanjiTile = null;
            if (string.IsNullOrEmpty(candidates))
            {
                resultInfo.Text = candidates == null ? "The kanji will appear here." : "No kanji contain all these radicals.";
                return;
            }
            resultInfo.Text = "Looking up…";
            List<WordKanji> kanji = null;
            string chars = candidates;
            try { kanji = await Task.Run(() => Engine.OrderedKanji(chars, 240)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token || kanji == null) return;

            resultInfo.Text = chars.Length + (chars.Length == 1 ? " kanji" : " kanji") + (chars.Length > kanji.Count ? " (showing the " + kanji.Count + " most common)" : "");
            foreach (var k in kanji) resultPanel.Children.Add(KanjiTile(k));
        }

        UIElement KanjiTile(WordKanji k)
        {
            var box = new Border
            {
                Width = 56, Height = 56, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = k.Kanji, FontSize = 30, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                ToolTip = (k.AllMeanings ?? ""),
            };
            Action<Border, bool> paint = (b, sel) =>
            {
                b.SetResourceReference(Border.BackgroundProperty, sel ? "SelectedBg" : "SurfaceBg");
                b.SetResourceReference(Border.BorderBrushProperty, sel ? "Accent" : "Line");
                ((TextBlock)b.Child).SetResourceReference(TextBlock.ForegroundProperty, k.IsSecondary ? "FgMuted" : "Fg");
            };
            paint(box, false);
            box.MouseEnter += (s, e) => { if (box != selectedKanjiTile) box.SetResourceReference(Border.BackgroundProperty, "HoverBg"); };
            box.MouseLeave += (s, e) => { if (box != selectedKanjiTile) paint(box, false); };
            box.MouseLeftButtonUp += (s, e) =>
            {
                if (selectedKanjiTile != null) paint(selectedKanjiTile, false);
                selectedKanjiTile = box;
                paint(box, true);
                detailHost.Content = KanjiDetail(k);
            };
            return box;
        }

        UIElement KanjiDetail(WordKanji k)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(Ui.Text(k.Kanji, 56, "Fg", null, new Thickness(0, -6, 0, 0), true));

            var info = new StackPanel();
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = Ui.JpFont, FontSize = 16 };
            if (k.HasOnReading) { line.Inlines.Add(Ui.Run("ON: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.OnReading, "Fg")); }
            if (k.HasOnReading && k.HasKunReading) line.Inlines.Add(new System.Windows.Documents.Run("   "));
            if (k.HasKunReading) { line.Inlines.Add(Ui.Run("KUN: ", "FgFaint")); line.Inlines.Add(Ui.Run(k.KunReading, "Fg")); }
            if (line.Inlines.Count > 0) info.Children.Add(line);
            if (!string.IsNullOrEmpty(k.AllMeanings)) info.Children.Add(Ui.Text(k.AllMeanings, 14.5, "Fg", null, new Thickness(0, 4, 0, 0)));
            var go = new Button { Style = (Style)Application.Current.FindResource("AccentButton"), Content = "Words with " + k.Kanji, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(16, 7, 16, 7) };
            string ch = k.Kanji;
            go.Click += (s, e) => MainWindow.Instance.OpenSearch(ch);
            info.Children.Add(go);
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            return Ui.Card(grid, new Thickness(0));
        }
    }
}
