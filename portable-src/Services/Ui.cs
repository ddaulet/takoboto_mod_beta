using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TakobotoApp
{
    /// <summary>A TextBlock that highlights the first occurrence of <see cref="Mark"/> in <see cref="Raw"/> in the accent colour.</summary>
    public class MarkedText : TextBlock
    {
        public static readonly DependencyProperty RawProperty = DependencyProperty.Register("Raw", typeof(string), typeof(MarkedText),
            new PropertyMetadata(null, (d, e) => ((MarkedText)d).Rebuild()));
        public static readonly DependencyProperty MarkProperty = DependencyProperty.Register("Mark", typeof(string), typeof(MarkedText),
            new PropertyMetadata(null, (d, e) => ((MarkedText)d).Rebuild()));

        public string Raw { get { return (string)GetValue(RawProperty); } set { SetValue(RawProperty, value); } }
        public string Mark { get { return (string)GetValue(MarkProperty); } set { SetValue(MarkProperty, value); } }

        void Rebuild()
        {
            Inlines.Clear();
            string t = Raw ?? "";
            string m = Mark;
            int i = string.IsNullOrEmpty(m) ? -1 : t.IndexOf(m, StringComparison.OrdinalIgnoreCase);
            if (i < 0) { Inlines.Add(new Run(t)); return; }
            if (i > 0) Inlines.Add(new Run(t.Substring(0, i)));
            var hit = new Run(t.Substring(i, m.Length)) { FontWeight = FontWeights.SemiBold };
            hit.SetResourceReference(TextElement.ForegroundProperty, "Accent");
            Inlines.Add(hit);
            if (i + m.Length < t.Length) Inlines.Add(new Run(t.Substring(i + m.Length)));
        }
    }

    /// <summary>Small helpers to build the dynamic parts of the UI (all colours are theme resources).</summary>
    public static class Ui
    {
        static readonly FontFamily Jp = new FontFamily("Yu Gothic UI, Meiryo UI, Segoe UI, MS Gothic");
        public static FontFamily JpFont { get { return Jp; } }
        static readonly Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();

        public static TextBlock Text(string s, double size = 15, string brush = "Fg", FontWeight? weight = null, Thickness? margin = null, bool jp = false)
        {
            var t = new TextBlock { Text = s ?? "", FontSize = size, TextWrapping = TextWrapping.Wrap };
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            if (weight.HasValue) t.FontWeight = weight.Value;
            if (margin.HasValue) t.Margin = margin.Value;
            if (jp) t.FontFamily = Jp;
            return t;
        }

        /// <summary>Like <see cref="Text"/>, but the user can select and copy it.</summary>
        public static SelText SText(string s, double size = 15, string brush = "Fg", FontWeight? weight = null, Thickness? margin = null, bool jp = false)
        {
            var t = new SelText { FontSize = size };
            t.Text = s ?? "";
            t.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, brush);
            if (weight.HasValue) t.FontWeight = weight.Value;
            if (margin.HasValue) t.Margin = margin.Value;
            if (jp) t.FontFamily = Jp;
            return t;
        }

        public static Run Run(string s, string brush = null, double size = 0, bool bold = false)
        {
            var r = new Run(s);
            if (brush != null) r.SetResourceReference(TextElement.ForegroundProperty, brush);
            if (size > 0) r.FontSize = size;
            if (bold) r.FontWeight = FontWeights.SemiBold;
            return r;
        }

        public static TextBlock Title(string s)
        {
            return Text(s, 15, "Accent", FontWeights.SemiBold, new Thickness(0, 0, 0, 5));
        }

        /// <summary>The loudspeaker button: speaks the text with the voice chosen in the settings. A click while it speaks stops it. Without the voice
        /// files (see <see cref="Voice"/>) the button does not show at all.</summary>
        public static Button SpeakButton(Func<string> text, double size = 28, Func<string> voiceKey = null, Func<int> speed = null)
        {
            var b = new Button
            {
                Style = (Style)Application.Current.FindResource("IconButton"), Width = size, Height = size, ToolTip = "Listen",
                Content = Icons.Make(Icons.Speaker, 18, null), Visibility = Voice.Available ? Visibility.Visible : Visibility.Collapsed,
            };
            System.Windows.Automation.AutomationProperties.SetName(b, "Listen");
            bool speaking = false;
            b.Click += (s, e) =>
            {
                if (speaking) { Voice.Stop(); return; }
                Voice.Speak(text(), voiceKey != null ? voiceKey() : Store.Settings.VoiceKey, Store.Settings.VoiceMale, speed != null ? speed() : Store.Settings.VoiceSpeed, on =>
                {
                    speaking = on;
                    if (on) b.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Accent"); else b.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
                });
            };
            return b;
        }

        public static Border Card(UIElement content, Thickness? margin = null)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(13),                       // the same on all four sides
                Margin = margin ?? new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                Child = content,
            };
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            b.SetResourceReference(Border.BorderBrushProperty, "Line");
            return b;
        }

        public static Image Flag(Language l, double size = 18)
        {
            string name = l == Language.English ? "FlagEn.png" : l == Language.French ? "FlagFr.png" : l == Language.German ? "FlagDe.png" : l == Language.Russian ? "FlagRu.png" : null;
            var img = new Image { Width = size, Height = size, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            if (name != null) img.Source = Resource(name);
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        /// <summary>A flag icon by file name (FlagEn.png, FlagZh.png ...), like <see cref="Flag"/>.</summary>
        public static Image FlagImage(string name, double size = 18)
        {
            var img = new Image { Width = size, Height = size, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            img.Source = Resource(name);
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        public static BitmapImage Resource(string name)
        {
            BitmapImage bi;
            if (imageCache.TryGetValue(name, out bi)) return bi;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Assets." + name))
            {
                if (s == null) return null;
                bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = s;
                bi.EndInit();
                bi.Freeze();
            }
            imageCache[name] = bi;
            return bi;
        }

        /// <summary>A small themed pop-up menu under <paramref name="anchor"/>.</summary>
        public static void ShowMenu(FrameworkElement anchor, IList<KeyValuePair<string, Action>> items)
        {
            var panel = new StackPanel { Margin = new Thickness(4) };
            var box = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                MinWidth = 200,
                Margin = new Thickness(10),
                Child = panel,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.35, Color = Colors.Black },
            };
            box.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            box.SetResourceReference(Border.BorderBrushProperty, "Line");
            var pop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = anchor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = box,
            };
            foreach (var it in items)
            {
                var action = it.Value;
                var b = new Button { Content = it.Key, Style = (Style)Application.Current.FindResource("MenuRow") };
                b.Click += (s, e) => { pop.IsOpen = false; action(); };
                panel.Children.Add(b);
            }
            pop.IsOpen = true;
        }

        public static string Romaji(string kana)
        {
            try { return TakobotoApp.Romaji.FromKana(kana); }
            catch { return null; }
        }

        public static bool HasKanji(string s)
        {
            try { return TakobotoApp.Romaji.ContainsKanji(s); }
            catch { return false; }
        }
    }
}
