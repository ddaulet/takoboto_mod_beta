using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TakobotoApp
{
    /// <summary>--render &lt;folder&gt; : draws the main window, Settings and About into PNG files WITHOUT showing any window (nothing takes the focus
    /// of the user's screen). For checking the layout.</summary>
    public static class RenderTest
    {
        public static void Run(string outArg, string[] extra)
        {
            // the folder is always INSIDE the folder of the program (a relative name like "shots"): nothing is ever written elsewhere
            string baseDir = Path.GetFullPath(App.AppDir);
            string outDir = Path.GetFullPath(Path.Combine(baseDir, outArg));
            if (!outDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(outDir);
            WordIndex.EnsureLoaded().Wait();
            Shot(outDir, "main", 1180, 760, w => { });
            Shot(outDir, "index", 1180, 760, w => w.TestIndex());
            Grammar.EnsureLoaded().Wait();
            if (Grammar.Available)
            {
                Shot(outDir, "grammar-ref", 1180, 560, w => w.TestGrammar("あいまって"));
                Shot(outDir, "grammar-target", 1180, 1250, w => w.TestGrammar("とあいまって"));
                Shot(outDir, "grammar-normal", 1180, 900, w => w.TestGrammar("から1"));
            }
            ShotElement(outDir, "sentences", 1000, 520, () =>
            {
                var st = new System.Windows.Controls.StackPanel();
                foreach (var s in Db.SentencesOf(1358280, true, 4)) st.Children.Add(WordView.SentenceBlock(s));
                foreach (var x in Grammar.Entries.SelectMany(e => e.Sections).SelectMany(sec => sec.Examples).Take(2)) st.Children.Add(WordView.BookSentenceBlock(x));
                return Ui.Card(st);
            });
            Shot(outDir, "settings", 1180, 760, w => w.ApplyArgs(new[] { "--page", "settings" }));
            Shot(outDir, "about", 1180, 760, w => w.ApplyArgs(new[] { "--page", "about" }));
            Shot(outDir, "settings-small-window", 900, 560, w => w.ApplyArgs(new[] { "--page", "settings" }));
            Shot(outDir, "settings-100", 1180, 760, w => { w.SetScale(100); w.ApplyArgs(new[] { "--page", "settings" }); });
        }

        static void ShotElement(string dir, string name, double width, double height, Func<UIElement> make)
        {
            var host = new Grid { Width = width, Height = height, Background = (Brush)Application.Current.FindResource("PageBg") };
            host.Children.Add(make());
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)(width * 1.5), (int)(height * 1.5), 144, 144, PixelFormats.Pbgra32);
            rtb.Render(host);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var f = File.Create(Path.Combine(dir, name + ".png"))) enc.Save(f);
        }

        static void Shot(string dir, string name, double width, double height, Action<MainWindow> prepare)
        {
            var win = new MainWindow { Width = width, Height = height };
            prepare(win);
            var root = (UIElement)win.Content;
            win.Content = null;
            var host = new Grid { Width = width, Height = height, Background = (Brush)Application.Current.FindResource("PageBg") };
            host.Children.Add(root);
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)(width * 1.5), (int)(height * 1.5), 144, 144, PixelFormats.Pbgra32);
            rtb.Render(host);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var f = File.Create(Path.Combine(dir, name + ".png"))) enc.Save(f);
        }
    }
}
