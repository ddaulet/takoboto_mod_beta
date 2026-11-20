using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TakobotoApp
{
    /// <summary>Handwriting input (the brush button of the search page). It fills the whole results area:
    ///   top    - the bar: backspace | the candidates for what was written (ONE row, one character each, scrolls sideways) | DONE;
    ///   below  - a round undo button on the right, and under it a square pad to write ONE kanji on with the mouse (or a pen).
    /// A click on a candidate puts it into the search box at once and clears the pad. The recognition is made by
    /// <see cref="Handwriting"/>. Nothing in the panel changes size while you write.</summary>
    public class HandwritingPanel : UserControl
    {
        public event Action<string> KanjiChosen;
        public event Action BackspaceRequested;
        public event Action DoneRequested;

        readonly InkCanvas ink = new InkCanvas();
        readonly StackPanel candidates = new StackPanel { Orientation = Orientation.Horizontal };
        readonly TextBlock loading;
        readonly ModeBar bar;
        readonly Button undo;
        readonly Border pad;
        readonly Grid padHost;
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        int token;

        public HandwritingPanel()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // the bar; the only text that ever shows in it is the short "preparing" notice of the first use
            loading = Ui.Text("Preparing the handwriting data (first time only)…", 13.5, "FgFaint", null, new Thickness(6, 0, 0, 0));
            loading.VerticalAlignment = VerticalAlignment.Center;
            loading.Visibility = Visibility.Collapsed;
            var band = new StackPanel { Orientation = Orientation.Horizontal };
            band.Children.Add(loading);
            band.Children.Add(candidates);
            bar = new ModeBar(band);
            bar.BackspacePressed += () => { if (BackspaceRequested != null) BackspaceRequested(); };
            bar.DonePressed += () => { if (DoneRequested != null) DoneRequested(); };
            root.Children.Add(bar);

            // undo the last stroke: a round button above the pad, on the right (outside the writing area)
            undo = new Button { Style = (Style)Application.Current.FindResource("RoundIconButton"), Content = "", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 16, 0), ToolTip = "Undo the last stroke", IsEnabled = false };
            System.Windows.Automation.AutomationProperties.SetName(undo, "Undo the last stroke");
            undo.Click += (s, e) => { if (ink.Strokes.Count > 0) { ink.Strokes.RemoveAt(ink.Strokes.Count - 1); Schedule(); } };
            Grid.SetRow(undo, 1);
            root.Children.Add(undo);

            // the pad: square, as big as the room allows; nothing is drawn over it
            pad = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Width = 300, Height = 300, ClipToBounds = true };
            pad.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            pad.SetResourceReference(Border.BorderBrushProperty, "Line");
            var padGrid = new Grid();
            padGrid.Children.Add(Guides());

            ink.Background = Brushes.Transparent;
            ink.EditingMode = InkCanvasEditingMode.Ink;
            ink.UseCustomCursor = false;
            ink.Cursor = Cursors.Pen;
            ink.DefaultDrawingAttributes = new DrawingAttributes { Width = 4.5, Height = 4.5, FitToCurve = true, IgnorePressure = true };
            ApplyInkColour();
            ink.StrokeCollected += (s, e) => Schedule();
            ink.Strokes.StrokesChanged += (s, e) => undo.IsEnabled = ink.Strokes.Count > 0;
            ink.PreviewMouseRightButtonUp += (s, e) => { Clear(); e.Handled = true; };   // a right click clears
            padGrid.Children.Add(ink);
            pad.Child = padGrid;

            padHost = new Grid { Margin = new Thickness(16, 8, 16, 14) };
            padHost.Children.Add(pad);
            padHost.SizeChanged += (s, e) =>
            {
                double side = Math.Max(120, Math.Min(padHost.ActualWidth, padHost.ActualHeight));
                pad.Width = side; pad.Height = side;
            };
            Grid.SetRow(padHost, 2);
            root.Children.Add(padHost);
            Content = root;

            debounce.Tick += (s, e) => { debounce.Stop(); Recognise(); };
            Theme.Changed += ApplyInkColour;
            Unloaded += (s, e) => debounce.Stop();
            Loaded += (s, e) => Prepare();
        }

        // ---- the templates ---------------------------------------------------------------------------------------------
        void Prepare()
        {
            loading.Visibility = Handwriting.Ready ? Visibility.Collapsed : Visibility.Visible;
            if (Handwriting.Ready) return;
            Handwriting.EnsureLoaded().ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
            {
                loading.Visibility = Visibility.Collapsed;
                if (ink.Strokes.Count > 0) Recognise();
            })));
        }

        void ApplyInkColour()
        {
            var b = TryFindResource("Fg") as SolidColorBrush;
            ink.DefaultDrawingAttributes.Color = b != null ? b.Color : Colors.White;
            ink.Strokes.Clear();
            candidates.Children.Clear();
        }

        // the cross that helps to centre the character
        UIElement Guides()
        {
            var g = new Canvas { IsHitTestVisible = false };
            Action<Line> style = l => { l.StrokeThickness = 1; l.StrokeDashArray = new DoubleCollection { 4, 4 }; l.SetResourceReference(Shape.StrokeProperty, "Line"); };
            var h = new Line { X1 = 0, Y1 = 0, X2 = 1, Y2 = 0 }; var v = new Line { X1 = 0, Y1 = 0, X2 = 0, Y2 = 1 };
            style(h); style(v);
            g.Children.Add(h); g.Children.Add(v);
            var host = new Grid { IsHitTestVisible = false };
            host.Children.Add(g);
            host.SizeChanged += (s, e) =>
            {
                h.X1 = 0; h.X2 = host.ActualWidth; h.Y1 = h.Y2 = host.ActualHeight / 2;
                v.Y1 = 0; v.Y2 = host.ActualHeight; v.X1 = v.X2 = host.ActualWidth / 2;
            };
            return host;
        }

        // ---- recognising ----------------------------------------------------------------------------------------------
        void Schedule() { debounce.Stop(); debounce.Start(); }

        public void Clear()
        {
            ink.Strokes.Clear();
            token++;
            candidates.Children.Clear();
        }

        async void Recognise()
        {
            int my = ++token;
            var strokes = ink.Strokes.Select(st => st.StylusPoints.Select(p => new[] { p.X, p.Y }).ToList()).ToList();
            if (strokes.Count == 0) { candidates.Children.Clear(); return; }
            if (!Handwriting.Ready) return;
            List<char> found = null;
            try { found = await Task.Run(() => Handwriting.Recognise(strokes, 20)); }
            catch (Exception ex) { App.Log(ex); }
            if (my != token || found == null) return;
            candidates.Children.Clear();
            foreach (char c in found) candidates.Children.Add(Candidate(c));
            bar.ScrollToStart();
        }

        UIElement Candidate(char c)
        {
            var label = new TextBlock { Text = c.ToString(), FontSize = 30, FontFamily = Ui.JpFont, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
            var box = new Border { Width = 46, Height = 44, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Child = label };
            box.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            box.SetResourceReference(Border.BorderBrushProperty, "Line");
            box.MouseEnter += (s, e) => box.SetResourceReference(Border.BackgroundProperty, "HoverBg");
            box.MouseLeave += (s, e) => box.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            box.ToolTipOpening += (s, e) =>
            {
                var k = Engine.Kanji(c);
                box.ToolTip = k == null ? null : (k.AllMeanings ?? "");
            };
            box.ToolTip = "";
            box.MouseLeftButtonUp += (s, e) =>
            {
                if (KanjiChosen != null) KanjiChosen(c.ToString());
                Clear();   // the next kanji
            };
            return box;
        }

        // ---- for automated tests: write a kanji by laying its template strokes (a little distorted) on the pad ----------
        public void TestDraw(char c)
        {
            var strokes = Handwriting.SampleStrokes(c);
            if (strokes == null) return;
            var rnd = new Random(7);
            double side = pad.ActualWidth > 0 ? pad.ActualWidth : 300, margin = side * 0.12, scale = (side - 2 * margin) / 109;
            ink.Strokes.Clear();
            foreach (var s in strokes)
            {
                var pts = new StylusPointCollection();
                foreach (var p in s) pts.Add(new StylusPoint(margin + p[0] * scale + (rnd.NextDouble() - 0.5) * 3, margin + p[1] * scale + (rnd.NextDouble() - 0.5) * 3));
                if (pts.Count > 1) ink.Strokes.Add(new Stroke(pts, ink.DefaultDrawingAttributes.Clone()));
            }
            Schedule();
        }
    }
}
