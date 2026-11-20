using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TakobotoApp
{
    /// <summary>The standard icons of the round buttons (the Material Design icon set, Apache 2.0): undo, check, close.</summary>
    public static class Icons
    {
        public const string Undo = "M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z";
        public const string Check = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";
        public const string Close = "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z";

        /// <summary>The icon as a shape of <paramref name="size"/> pixels, filled with a brush of the theme (a resource key).</summary>
        public static Path Make(string data, double size, string brushKey)
        {
            var p = new Path { Data = Geometry.Parse(data), Stretch = Stretch.Uniform, Width = size, Height = size, IsHitTestVisible = false };
            p.SetResourceReference(Shape.FillProperty, brushKey);
            return p;
        }
    }
}
