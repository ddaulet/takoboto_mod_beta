using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace TakobotoApp
{
    /// <summary>Light / dark / follow-Windows, switched live (all brushes are DynamicResource).</summary>
    public static class Theme
    {
        public static bool IsDark { get; private set; }
        public static event Action Changed;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static bool SystemIsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public static void Apply(string mode)
        {
            bool dark = mode == "dark" || (mode != "light" && SystemIsDark());
            var dicts = Application.Current.Resources.MergedDictionaries;
            var uri = new Uri("/Takoboto;component/Themes/" + (dark ? "Dark" : "Light") + ".xaml", UriKind.Relative);
            var fresh = new ResourceDictionary { Source = uri };
            if (dicts.Count > 0) dicts[0] = fresh; else dicts.Add(fresh);
            IsDark = dark;
            foreach (Window w in Application.Current.Windows) ApplyToWindow(w);
            if (Changed != null) Changed();
        }

        /// <summary>Dark title bar / caption (Windows 10 1809+ / Windows 11).</summary>
        public static void ApplyToWindow(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int v = IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref v, 4) != 0) DwmSetWindowAttribute(h, 19, ref v, 4);
                // Windows 11: caption / text / border in the colours of the page instead of plain white or black
                int caption = ColorRef("PageBg"), text = ColorRef("Fg"), border = ColorRef("Line");
                if (caption >= 0) DwmSetWindowAttribute(h, 35, ref caption, 4);
                if (text >= 0) DwmSetWindowAttribute(h, 36, ref text, 4);
                if (border >= 0) DwmSetWindowAttribute(h, 34, ref border, 4);
            }
            catch { }
        }

        static int ColorRef(string key)
        {
            var b = Application.Current.TryFindResource(key) as System.Windows.Media.SolidColorBrush;
            if (b == null) return -1;
            var c = b.Color;
            return c.R | (c.G << 8) | (c.B << 16);   // COLORREF = 0x00BBGGRR
        }

        public static void WatchSystem()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (Store.Settings.Theme != "system") return;
                Application.Current.Dispatcher.BeginInvoke(new Action(() => Apply("system")));
            };
        }
    }
}
