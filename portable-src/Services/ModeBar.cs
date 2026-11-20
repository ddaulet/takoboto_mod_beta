using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TakobotoApp
{
    /// <summary>The bar at the top of the handwriting and the radicals panels (like the bar of Pleco):
    ///   [ backspace ]  [ a band of tiles, ONE row, scrolls sideways ]  [ DONE ]
    /// The bar always has the same height, so the panel under it never moves. Backspace deletes the last character of the search
    /// box; DONE closes the panel and shows the results.</summary>
    public class ModeBar : Border
    {
        public event Action BackspacePressed;
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var back = new Button
            {
                Style = (Style)Application.Current.FindResource("KeyButton"), Content = "", FontFamily = (System.Windows.Media.FontFamily)Application.Current.FindResource("IconFont"),
                FontSize = 20, Width = 58, Height = 44, Margin = new Thickness(10, 0, 4, 0), Focusable = false, ToolTip = "Delete the last character of the search box",
            };
            System.Windows.Automation.AutomationProperties.SetName(back, "Backspace");
            back.Click += (s, e) => { if (BackspacePressed != null) BackspacePressed(); };
            grid.Children.Add(back);

            var fe = band as FrameworkElement;
            if (fe != null) fe.Margin = new Thickness(0, 0, 0, 9);   // room for the thin scroll bar under the tiles
            scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), Content = band,
            };
            // the wheel scrolls the row sideways
            scroll.PreviewMouseWheel += (s, e) => { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta); e.Handled = true; };
            Grid.SetColumn(scroll, 1);
            grid.Children.Add(scroll);

            var done = new Button { Style = (Style)Application.Current.FindResource("AccentButton"), Content = "DONE", Height = 44, MinWidth = 72, Margin = new Thickness(4, 0, 10, 0), Focusable = false, ToolTip = "Close and show the results" };
            done.Click += (s, e) => { if (DonePressed != null) DonePressed(); };
            Grid.SetColumn(done, 2);
            grid.Children.Add(done);

            Child = grid;
        }

        /// <summary>Back to the first tile (a new set of candidates).</summary>
        public void ScrollToStart() { scroll.ScrollToHorizontalOffset(0); }
    }
}
