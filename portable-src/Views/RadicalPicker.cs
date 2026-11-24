using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TakobotoApp
{
    /// <summary>"Search by radical" (the puzzle button of the search page). It fills the whole results area:
    ///   top    - a plain band with the radicals picked so far (no input box, no buttons),
    ///   then   - the kanji that contain all of them: a click puts the kanji into the search box at once,
    ///   below  - the radicals, by stroke count.
    /// Same logic as the original app: the selected radicals' kanji lists are intersected; radicals that would give an
    /// empty result are disabled.</summary>
    public class RadicalPicker : UserControl
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

        public event Action<string> KanjiChosen;   // a click on a kanji: it goes into the search box at once
        public event Action DoneRequested;         // DONE: close the panel, show the results

        readonly List<Tile> tiles = new List<Tile>();
        readonly WrapPanel kanjiPanel = new WrapPanel();
        readonly StackPanel strip = new StackPanel { Orientation = Orientation.Horizontal };   // the picked radicals, one row
        readonly TextBlock kanjiInfo;
        int token;
        string candidates;   // kanji that contain every selected radical (null = nothing selected)

        public RadicalPicker()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                                          // the picked radicals
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 120 });    // the kanji
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 140 });    // the radicals

            // row 0: the bar - the radicals picked so far (one row, a click on one takes it off again) | X (clear) | done
            var bar = new ModeBar(strip);
            bar.ClearPressed += () => ClearSelection();   // the round X: take all the picked radicals off
            bar.DonePressed += () => { if (DoneRequested != null) DoneRequested(); };
            root.Children.Add(bar);

            // row 1: the kanji that contain the picked radicals; a click on one puts it into the search box
            var results = new Grid { Margin = new Thickness(16, 8, 10, 0) };
            results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            results.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            kanjiInfo = Ui.Text("The kanji will appear here. Click one to put it into the search box.", 12.5, "FgFaint", null, new Thickness(0, 0, 0, 4));
            results.Children.Add(kanjiInfo);
            var kanjiScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = kanjiPanel };
            Grid.SetRow(kanjiScroll, 1);
            results.Children.Add(kanjiScroll);
            Grid.SetRow(results, 1);
            root.Children.Add(results);

            // row 2: the radicals
            var radicalBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 2, 10, 0) };
            radicalBorder.SetResourceReference(Border.BorderBrushProperty, "Line");
            radicalBorder.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = BuildRadicals() };
            Grid.SetRow(radicalBorder, 2);
            root.Children.Add(radicalBorder);

            Content = root;
        }

        // the band at the top: one small tile per picked radical
        void ShowStrip(KanjiRadical[] picked)
        {
            strip.Children.Clear();
            foreach (var r in picked)
            {
                var radical = r;
                var label = new TextBlock { Text = radical.Radical.ToString(), FontSize = 26, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                label.SetResourceReference(TextBlock.ForegroundProperty, "AccentFg");
                var box = new Border { Width = 44, Height = 44, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Child = label, ToolTip = "Click to take this radical off" };
                box.SetResourceReference(Border.BackgroundProperty, "AccentFill");
                box.MouseLeftButtonUp += (s, e) => { var t = tiles.FirstOrDefault(x => x.Radical == radical); if (t != null && t.Selected) { t.Selected = false; Recalculate(); } };
                strip.Children.Add(box);
            }
        }

        // ---- the radical grid -----------------------------------------------------------------------------------
        UIElement BuildRadicals()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 6, 8) };
            WrapPanel group = null;
            int strokes = -1;
            foreach (var r in KanjiRadicals.Radicals)
            {
                if (r.StrokeCount != strokes)
                {
                    strokes = r.StrokeCount;
                    stack.Children.Add(Ui.Text(strokes + (strokes == 1 ? " stroke" : " strokes"), 11.5, "FgFaint", FontWeights.SemiBold, new Thickness(2, 8, 0, 3)));
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
                Width = 40, Height = 40, Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = text + "  (" + r.StrokeCount + " strokes)",
            };
            if (text == "个")
            {
                // this glyph is missing from most fonts: the original shows an image instead
                // (a white glyph on transparent: used as a mask and painted in the text colour of the theme)
                t.Icon = new System.Windows.Shapes.Rectangle { Margin = new Thickness(9), OpacityMask = new ImageBrush(Ui.Resource("RadicalA.png")) };
                t.Box.Child = t.Icon;
            }
            else
            {
                t.Label = new TextBlock { Text = text, FontSize = 22, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
            ShowStrip(tiles.Where(x => x.Selected).Select(x => x.Radical).ToArray());
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
            ShowStrip(new KanjiRadical[0]);
            UpdateResults();
        }

        // ---- the kanji that contain the selected radicals ----------------------------------------------------------
        async void UpdateResults()
        {
            int my = ++token;
            kanjiPanel.Children.Clear();
            if (string.IsNullOrEmpty(candidates))
            {
                kanjiInfo.Text = candidates == null ? "The kanji will appear here. Click one to put it into the search box." : "No kanji contain all these radicals.";
                return;
            }
            kanjiInfo.Text = "Looking up…";
            List<WordKanji> kanji = null;
            string chars = candidates;
            try { kanji = await Task.Run(() => Engine.OrderedKanji(chars, 240)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token || kanji == null) return;

            kanjiInfo.Text = chars.Length + " kanji" + (chars.Length > kanji.Count ? " (the " + kanji.Count + " most common)" : "") + " - click one to put it into the search box";
            foreach (var k in kanji) kanjiPanel.Children.Add(KanjiTile(k));
        }

        UIElement KanjiTile(WordKanji k)
        {
            var label = new TextBlock { Text = k.Kanji, FontSize = 28, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, k.IsSecondary ? "FgMuted" : "Fg");
            var box = new Border
            {
                Width = 48, Height = 48, Margin = new Thickness(0, 0, 5, 5), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, Child = label, ToolTip = (k.AllMeanings ?? ""),
            };
            box.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            box.SetResourceReference(Border.BorderBrushProperty, "Line");
            box.MouseEnter += (s, e) => box.SetResourceReference(Border.BackgroundProperty, "HoverBg");
            box.MouseLeave += (s, e) => box.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            string ch = k.Kanji;
            box.MouseLeftButtonUp += (s, e) => { if (KanjiChosen != null) KanjiChosen(ch); };
            return box;
        }
    }
}
