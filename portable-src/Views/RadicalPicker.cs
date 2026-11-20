using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Takoboto.Database;
using Takoboto.Database.Impl;

namespace TakobotoApp
{
    /// <summary>"Search by radical" (the puzzle button of the search page). It fills the whole results area:
    ///   top    - the chosen kanji, a backspace button and DONE (DONE puts the chosen kanji into the search box),
    ///   then   - the picked radicals and the kanji that contain all of them (click a kanji to CHOOSE it),
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

        public event Action<string> Done;           // the chosen kanji (may be empty): put them into the search box
        public event Action BackspaceRequested;     // backspace with nothing chosen: delete in the search box

        readonly List<Tile> tiles = new List<Tile>();
        readonly WrapPanel kanjiPanel = new WrapPanel();
        readonly TextBlock summary;
        readonly TextBlock kanjiInfo;
        readonly Button clearBtn;
        readonly TextBlock chosenText;
        int token;
        string candidates;   // kanji that contain every selected radical (null = nothing selected)
        string chosen = "";  // the kanji clicked so far (waiting for DONE)

        public RadicalPicker()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                                          // chosen kanji | backspace | DONE
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                                          // the picked radicals | Clear
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 120 });    // the kanji
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star), MinHeight = 140 });    // the radicals

            // row 1: the picked radicals
            var top = new Grid { Margin = new Thickness(16, 10, 16, 4) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            summary = Ui.Text("Pick the radicals the kanji contains.", 13.5, "FgMuted", null, null, true);
            summary.VerticalAlignment = VerticalAlignment.Center;
            summary.TextWrapping = TextWrapping.NoWrap;
            summary.TextTrimming = TextTrimming.CharacterEllipsis;
            top.Children.Add(summary);
            clearBtn = new Button { Style = (Style)Application.Current.FindResource("LinkButton"), Content = "Clear", IsEnabled = false, Focusable = false };
            clearBtn.Click += (s, e) => ClearSelection();
            Grid.SetColumn(clearBtn, 1);
            top.Children.Add(clearBtn);
            Grid.SetRow(top, 1);
            root.Children.Add(top);

            // row 2: the kanji that contain the chosen radicals
            var results = new Grid { Margin = new Thickness(16, 0, 10, 0) };
            results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            results.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            kanjiInfo = Ui.Text("The kanji will appear here. Click one to choose it, then press DONE.", 12.5, "FgFaint", null, new Thickness(0, 0, 0, 4));
            results.Children.Add(kanjiInfo);
            var kanjiScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = kanjiPanel };
            Grid.SetRow(kanjiScroll, 1);
            results.Children.Add(kanjiScroll);
            Grid.SetRow(results, 2);
            root.Children.Add(results);

            // row 3: the radicals
            var radicalBorder = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(16, 2, 10, 0) };
            radicalBorder.SetResourceReference(Border.BorderBrushProperty, "Line");
            radicalBorder.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = BuildRadicals() };
            Grid.SetRow(radicalBorder, 3);
            root.Children.Add(radicalBorder);

            // row 0 (right under the mode buttons): chosen kanji | backspace | DONE
            var bar = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 8, 12, 8) };
            bar.SetResourceReference(Border.BorderBrushProperty, "Line");
            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var chosenBox = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Height = 46, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 8, 0) };
            chosenBox.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            chosenBox.SetResourceReference(Border.BorderBrushProperty, "Line");
            chosenText = new TextBlock { FontFamily = Ui.JpFont, FontSize = 26, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            chosenBox.Child = chosenText;
            barGrid.Children.Add(chosenBox);
            var back = new Button { Style = (Style)Application.Current.FindResource("KeyButton"), Content = "⌫", Width = 64, Height = 46, Margin = new Thickness(0, 0, 8, 0), Focusable = false, ToolTip = "Remove the last chosen kanji" };
            back.Click += (s, e) => Backspace();
            Grid.SetColumn(back, 1);
            barGrid.Children.Add(back);
            var done = new Button { Style = (Style)Application.Current.FindResource("AccentButton"), Content = "DONE", Height = 46, MinWidth = 84, Focusable = false, ToolTip = "Put the chosen kanji into the search box" };
            done.Click += (s, e) => Finish();
            Grid.SetColumn(done, 2);
            barGrid.Children.Add(done);
            bar.Child = barGrid;
            Grid.SetRow(bar, 0);
            root.Children.Add(bar);

            Content = root;
            ShowChosen();
        }

        // ---- the chosen kanji ----------------------------------------------------------------------------------------
        void ShowChosen()
        {
            if (chosen.Length == 0) { chosenText.Text = "no kanji chosen"; chosenText.FontSize = 14; chosenText.SetResourceReference(TextBlock.ForegroundProperty, "FgFaint"); chosenText.FontFamily = new FontFamily("Segoe UI"); }
            else { chosenText.Text = chosen; chosenText.FontSize = 26; chosenText.SetResourceReference(TextBlock.ForegroundProperty, "Fg"); chosenText.FontFamily = Ui.JpFont; }
        }

        void Choose(string kanji) { chosen += kanji; ShowChosen(); }

        void Backspace()
        {
            if (chosen.Length > 0) { chosen = chosen.Substring(0, chosen.Length - 1); ShowChosen(); }
            else if (BackspaceRequested != null) BackspaceRequested();
        }

        void Finish()
        {
            string text = chosen;
            chosen = "";
            ClearSelection();
            ShowChosen();
            if (Done != null) Done(text);
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
            var picked = tiles.Where(x => x.Selected).Select(x => x.Radical.Radical).ToArray();
            clearBtn.IsEnabled = picked.Length > 0;
            summary.Text = picked.Length == 0 ? "Pick the radicals the kanji contains." : "Radicals: " + string.Join(" ", picked);
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
            summary.Text = "Pick the radicals the kanji contains.";
            UpdateResults();
        }

        // ---- the kanji that contain the selected radicals ----------------------------------------------------------
        async void UpdateResults()
        {
            int my = ++token;
            kanjiPanel.Children.Clear();
            if (string.IsNullOrEmpty(candidates))
            {
                kanjiInfo.Text = candidates == null ? "The kanji will appear here. Click one to choose it, then press DONE." : "No kanji contain all these radicals.";
                return;
            }
            kanjiInfo.Text = "Looking up…";
            List<WordKanji> kanji = null;
            string chars = candidates;
            try { kanji = await Task.Run(() => Engine.OrderedKanji(chars, 240)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token || kanji == null) return;

            kanjiInfo.Text = chars.Length + " kanji" + (chars.Length > kanji.Count ? " (the " + kanji.Count + " most common)" : "") + " - click one to choose it";
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
            box.MouseLeftButtonUp += (s, e) => Choose(ch);
            return box;
        }
    }
}
