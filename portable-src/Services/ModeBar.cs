using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TakobotoApp
{
    /// <summary>The bar at the top of the handwriting and the radicals panels:
    ///   [ a band of tiles, ONE row, scrolls sideways ]  [ round X: clear ]  [ round green check: done ]
    /// The bar always has the same height, so the panel under it never moves. X clears what has been entered in the panel
    /// (the pad / the picked radicals); the green check closes the panel and shows the results.</summary>
    public class ModeBar : Border
    {
        public event Action ClearPressed;
        public event Action DonePressed;

        readonly ScrollViewer scroll;

        /// <param name="band">The tiles (candidates / picked radicals): a horizontal StackPanel.</param>
        public ModeBar(UIElement band)
        {
            Height = 72;
            BorderThickness = new Thickness(0, 0, 0, 1);
            SetResourceReference(BackgroundProperty, "SurfaceBg");
            SetResourceReference(BorderBrushProperty, "Line");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // the tiles: the same room above them as the thin scroll bar takes below, so that they stand in the middle of the bar
            var fe = band as FrameworkElement;
            if (fe != null) fe.Margin = new Thickness(0, 10, 0, 0);
            scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0), Content = band, ClipToBounds = true,
            };
            scroll.PreviewMouseWheel += (s, e) => { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta); e.Handled = true; };   // the wheel scrolls the row sideways
            scroll.ScrollChanged += (s, e) => UpdateFade();
            scroll.SizeChanged += (s, e) => UpdateFade();
            grid.Children.Add(scroll);

            var clear = new Button { Style = (Style)Application.Current.FindResource("RoundIconButton"), Content = Icons.Make(Icons.Close, 20, "Fg"), Width = 44, Height = 44, Margin = new Thickness(4, 0, 6, 0), ToolTip = "Clear" };
            System.Windows.Automation.AutomationProperties.SetName(clear, "Clear");
            clear.Click += (s, e) => { if (ClearPressed != null) ClearPressed(); };
            Grid.SetColumn(clear, 1);
            grid.Children.Add(clear);

            var done = new Button { Style = (Style)Application.Current.FindResource("RoundAccentButton"), Content = Icons.Make(Icons.Check, 20, "AccentFg", 2.4), Width = 44, Height = 44, Margin = new Thickness(0, 0, 12, 0), ToolTip = "Done: close and show the results" };
            System.Windows.Automation.AutomationProperties.SetName(done, "Done");
            done.Click += (s, e) => { if (DonePressed != null) DonePressed(); };
            Grid.SetColumn(done, 2);
            grid.Children.Add(done);

            Child = grid;
        }

        /// <summary>Back to the first tile (a new set of candidates).</summary>
        public void ScrollToStart() { scroll.ScrollToHorizontalOffset(0); }

        // a tile cut by the edge of the row fades out instead of looking like something lies over it
        void UpdateFade()
        {
            bool left = scroll.HorizontalOffset > 1, right = scroll.HorizontalOffset < scroll.ScrollableWidth - 1;
            if (!left && !right) { scroll.OpacityMask = null; return; }
            double f = Math.Min(0.2, 22 / Math.Max(1, scroll.ViewportWidth));
            var br = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            br.GradientStops.Add(new GradientStop(Color.FromArgb(left ? (byte)0 : (byte)255, 0, 0, 0), 0));
            br.GradientStops.Add(new GradientStop(Colors.Black, left ? f : 0));
            br.GradientStops.Add(new GradientStop(Colors.Black, right ? 1 - f : 1));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(right ? (byte)0 : (byte)255, 0, 0, 0), 1));
            scroll.OpacityMask = br;
        }
    }
}
