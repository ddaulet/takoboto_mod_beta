using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TakobotoApp
{
    /// <summary>A colour scheme ("окраска"): the accent colour and the tint of the greys. It is independent of the
    /// light / dark theme - every scheme exists in both.</summary>
    public class Scheme
    {
        public string Id, Name;
        public string Swatch;                    // the dot shown in Settings
        public double Hue = -1;                  // tint of the greys / backgrounds (-1: the built-in golf green is kept as it is)
        public string AccentDark, AccentLight;   // accent for text / lines on the dark / light theme
        public string FillDark, FillLight;       // accent for filled buttons
        public string OnFillDark = "#FFFFFF", OnFillLight = "#FFFFFF";   // text on those buttons
        public double MascotHue, MascotSat = 1, MascotLight = 1;         // how the mascot / icon is recoloured (from the green one)
    }

    public static class Schemes
    {
        public static readonly Scheme[] All =
        {
            new Scheme { Id = "green", Name = "Golf green", Swatch = "#2C6E49" },   // the built-in colours of Light.xaml / Dark.xaml
            new Scheme { Id = "red", Name = "Original red", Swatch = "#D32F2F", Hue = 2,
                AccentDark = "#EF6A62", AccentLight = "#B3261E", FillDark = "#C62828", FillLight = "#C62828",
                MascotHue = 0, MascotSat = 2.0, MascotLight = 1.14 },
            new Scheme { Id = "blue", Name = "Blue", Swatch = "#2F6FD0", Hue = 214,
                AccentDark = "#6FA8F5", AccentLight = "#2058B8", FillDark = "#2F6FD0", FillLight = "#2563C9",
                MascotHue = 214, MascotSat = 1.3, MascotLight = 1.2 },
            new Scheme { Id = "yellow", Name = "Yellow", Swatch = "#E0B72E", Hue = 44,
                AccentDark = "#E8C547", AccentLight = "#8A6A00", FillDark = "#D9AE22", FillLight = "#E0B72E",
                OnFillDark = "#1B1608", OnFillLight = "#1B1608",
                MascotHue = 46, MascotSat = 1.7, MascotLight = 1.3 },
        };

        public static Scheme Current
        {
            get { return All.FirstOrDefault(s => s.Id == Store.Settings.Scheme) ?? All[0]; }
        }

        // the neutral colours of a theme that get the tint of the scheme
        static readonly string[] Neutral =
        {
            "PageBg", "SurfaceBg", "SurfaceAlt", "InputBg", "Line", "Fg", "FgMuted", "FgFaint", "HoverBg", "SelectedBg",
            "ScrollThumb", "ScrollThumbHover", "SidebarBg", "SidebarFg", "SidebarFgMuted", "SidebarHover", "SidebarSelected",
        };

        static ResourceDictionary over;   // the scheme's colours; it sits after the theme in the application resources and wins

        /// <summary>Put the colours of the current scheme over the theme that is loaded (call after the theme changed).</summary>
        public static void Apply(bool dark)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            if (over != null) { dicts.Remove(over); over = null; }
            var s = Current;
            if (s.Hue < 0 || dicts.Count == 0) return;   // golf green: nothing to change

            var theme = dicts[0];
            var d = new ResourceDictionary();
            foreach (var key in Neutral)
            {
                var b = theme[key] as SolidColorBrush;
                if (b != null) d[key] = Brush(Retint(b.Color, s.Hue));
            }
            d["Accent"] = Brush(Parse(dark ? s.AccentDark : s.AccentLight));
            d["AccentFill"] = Brush(Parse(dark ? s.FillDark : s.FillLight));
            d["AccentFg"] = Brush(Parse(dark ? s.OnFillDark : s.OnFillLight));
            dicts.Add(d);
            over = d;
        }

        public static void Set(string id)
        {
            Store.Settings.Scheme = id;
            Store.SaveSettings();
            Theme.Apply(Store.Settings.Theme);   // re-applies theme + scheme and tells the windows
        }

        // ---- the mascot and the program icon follow the scheme --------------------------------------------------
        /// <summary>The logo of the sidebar (a lighter one on the dark theme), in the colours of the scheme.</summary>
        public static BitmapSource Logo(bool dark)
        {
            return Tint(Ui.Resource(dark ? "LogoLight.png" : "Logo.png"), Current);
        }

        /// <summary>The icon of the window (title bar, taskbar, Alt+Tab) in the colours of the scheme. (The icon of the
        /// .exe file in Explorer is fixed inside the file: it stays green.)</summary>
        public static BitmapSource WindowIcon()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Assets.app.ico"))
            {
                if (s == null) return null;
                var dec = new IconBitmapDecoder(s, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var best = dec.Frames.OrderByDescending(f => f.PixelWidth).First();
                return Tint(best, Current);
            }
        }

        static BitmapSource Tint(BitmapSource src, Scheme s)
        {
            if (src == null || s.Hue < 0) return src;
            var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] == 0) continue;
                double hh, ss, ll;
                RgbToHsl(px[i + 2] / 255.0, px[i + 1] / 255.0, px[i] / 255.0, out hh, out ss, out ll);
                double r, g, b;
                HslToRgb(s.MascotHue, Math.Min(1, ss * s.MascotSat), Math.Min(1, ll * s.MascotLight), out r, out g, out b);
                px[i + 2] = (byte)Math.Round(r * 255); px[i + 1] = (byte)Math.Round(g * 255); px[i] = (byte)Math.Round(b * 255);
            }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
            bmp.Freeze();
            return bmp;
        }

        // ---- colour helpers -------------------------------------------------------------------------------------
        static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        static Color Parse(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }

        static Color Retint(Color c, double hue)
        {
            double h, s, l, r, g, b;
            RgbToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0, out h, out s, out l);
            HslToRgb(hue, s, l, out r, out g, out b);
            return Color.FromArgb(c.A, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }

        static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s < 1e-9) { r = g = b = l; return; }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q, hk = ((h % 360) + 360) % 360 / 360;
            r = Channel(p, q, hk + 1.0 / 3); g = Channel(p, q, hk); b = Channel(p, q, hk - 1.0 / 3);
        }

        static double Channel(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
