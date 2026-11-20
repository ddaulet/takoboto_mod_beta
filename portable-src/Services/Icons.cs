using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TakobotoApp
{
    /// <summary>The icons of the input-mode buttons and of the round buttons: ONE simple line style (outline only, round ends, drawn in
    /// a 24 x 24 box), so that they look like parts of one design. Shown at 22 px; the round buttons are all 44 px.</summary>
    public static class Icons
    {
        static Geometry G(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }

        // the three modes
        public static readonly Geometry Pen = G("M4 20 L5 15.5 L16.5 4 L20 7.5 L8.5 19 Z M14 6.5 L17.5 10");
        public static readonly Geometry Puzzle = G("M4 8 H9 A2 2 0 1 1 13 8 H18 V12 A2 2 0 1 1 18 16 V20 H4 Z");
        public static readonly Geometry Keyboard = G("M3 6.5 H21 A1 1 0 0 1 22 7.5 V16.5 A1 1 0 0 1 21 17.5 H3 A1 1 0 0 1 2 16.5 V7.5 A1 1 0 0 1 3 6.5 Z M6.5 10.5 H6.6 M10.5 10.5 H10.6 M14.5 10.5 H14.6 M18 10.5 H18.1 M7.5 14 H16.5");

        // the round buttons
        public static readonly Geometry Undo = G("M8 5 L3 10 L8 15 M3 10 H14 A5.5 5.5 0 0 1 14 21 H8");
        public static readonly Geometry Check = G("M5 12.5 L10 17.5 L19 7");
        public static readonly Geometry Close = G("M6 6 L18 18 M18 6 L6 18");

        /// <summary>The icon for a button: a <paramref name="size"/> px line drawing, in a brush of the theme (a resource key).</summary>
        public static FrameworkElement Make(Geometry icon, double size, string brushKey, double thickness = 2)
        {
            var path = new Path
            {
                Data = icon, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round, Width = 24, Height = 24, Stretch = Stretch.None, IsHitTestVisible = false,
            };
            path.SetResourceReference(Shape.StrokeProperty, brushKey);
            return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
        }
    }
}
