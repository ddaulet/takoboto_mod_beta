using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>The small pop-up that shows what a word is - reading and meaning - right where you clicked or
    /// selected it. A selected word (or a few kana that several words are written with) shows ALL the entries it can be:
    /// as a list, the first ones of the dictionary first. Clicking an entry opens it. The pop-up opens below the mouse
    /// pointer, or ABOVE it when there is not enough room below (near the bottom of the screen).</summary>
    public static class Preview
    {
        static Popup current;
        const int MaxEntries = 40;

        // ---- entry points --------------------------------------------------------------------------------------------
        static string lastText; static int lastTick;

        /// <summary>The user selected some text.</summary>
        public static void ShowForSelection(string selected)
        {
            string text = (selected ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length == 0 || text.Length > 40 || !text.Any(char.IsLetterOrDigit)) return;
            // a selection can be reported twice (mouse released, then "selection settled"): show it once
            int now = Environment.TickCount;
            if (text == lastText && now - lastTick < 1500) return;
            lastText = text; lastTick = now;
            ShowEntries(text);
        }

        /// <summary>Every dictionary entry the text is (exact matches): one entry is shown as a card, several as a list.</summary>
        static async void ShowEntries(string text)
        {
            var cursor = CursorDips();   // where the mouse is NOW (it may have moved when the lookup is done)
            List<WordTag> tags = null;
            try { tags = await Task.Run(() => Engine.LookupAll(text, MaxEntries)); }
            catch (Exception ex) { App.Log(ex); }
            if (tags == null || tags.Count == 0) return;
            if (tags.Count == 1) OpenOne(tags[0], WordItem.From(tags[0]), cursor);
            else OpenList(text, tags, cursor);
        }

        /// <summary>The word of an example sentence (written + reading): the entry with that reading, as a card.</summary>
        public static async void ShowWord(string text, string reading)
        {
            var cursor = CursorDips();
            WordTag tag = null;
            try { tag = await Task.Run(() => Engine.Lookup(text, reading)); }
            catch (Exception ex) { App.Log(ex); }
            if (tag == null) return;
            OpenOne(tag, WordItem.From(tag), cursor);
        }

        // ---- the pop-up ---------------------------------------------------------------------------------------------
        static void OpenOne(WordTag tag, WordItem item, Point cursor)
        {
            var stack = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            var head = new WrapPanel();
            head.Children.Add(Label(item.Main, 24, "Fg", true, new Thickness(0, 0, 12, 0)));
            if (!string.IsNullOrEmpty(item.Reading)) head.Children.Add(Label(item.Reading, 16, "FgMuted", true, new Thickness(0, 0, 0, 0), VerticalAlignment.Bottom));
            stack.Children.Add(head);
            if (!string.IsNullOrEmpty(item.Conjugation))
                stack.Children.Add(Label(item.Conjugation, 12.5, "FgMuted", false, new Thickness(0, 2, 0, 0)));
            var meaning = Label(item.Meaning, 14.5, "Fg", false, new Thickness(0, 6, 0, 0));
            meaning.TextWrapping = TextWrapping.Wrap;
            meaning.TextTrimming = TextTrimming.CharacterEllipsis;
            meaning.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            meaning.LineHeight = 20;
            meaning.MaxHeight = 80;
            stack.Children.Add(meaning);
            stack.Children.Add(Label("Click to open the entry", 12, "FgFaint", false, new Thickness(0, 8, 0, 0)));

            var box = Box(stack, 220, 380);
            var pop = Show(box, cursor);
            box.Cursor = Cursors.Hand;
            box.MouseLeftButtonUp += (s, e) => { pop.IsOpen = false; MainWindow.Instance.OpenWord(tag, item.Main); };
        }

        /// <summary>Several entries have this spelling / reading (e.g. こうこう = 高校, 孝行, 航行 ...): all of them, as a list.</summary>
        static void OpenList(string text, List<WordTag> tags, Point cursor)
        {
            var rows = new StackPanel();
            Popup pop = null;
            foreach (var tag in tags)
            {
                var item = WordItem.From(tag);
                var row = new StackPanel { Margin = new Thickness(0) };
                var head = new WrapPanel();
                head.Children.Add(Label(item.Main, 20, "Fg", true, new Thickness(0, 0, 10, 0)));
                if (!string.IsNullOrEmpty(item.Reading)) head.Children.Add(Label(item.Reading, 14, "FgMuted", true, new Thickness(0), VerticalAlignment.Bottom));
                row.Children.Add(head);
                if (!string.IsNullOrEmpty(item.Conjugation)) row.Children.Add(Label(item.Conjugation, 12, "FgMuted", false, new Thickness(0, 1, 0, 0)));
                var meaning = Label(item.Meaning, 13.5, "FgMuted", false, new Thickness(0, 1, 0, 0));
                meaning.TextTrimming = TextTrimming.CharacterEllipsis;
                meaning.TextWrapping = TextWrapping.NoWrap;
                row.Children.Add(meaning);

                var cell = new Border { Padding = new Thickness(16, 6, 16, 6), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = row };
                var tagCopy = tag; var itemCopy = item;
                cell.MouseEnter += (s, e) => cell.SetResourceReference(Border.BackgroundProperty, "HoverBg");
                cell.MouseLeave += (s, e) => cell.Background = Brushes.Transparent;
                cell.MouseLeftButtonUp += (s, e) => { if (pop != null) pop.IsOpen = false; MainWindow.Instance.OpenWord(tagCopy, itemCopy.Main); };
                rows.Children.Add(cell);
            }

            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 6) };
            string caption = tags.Count >= MaxEntries ? "first " + MaxEntries + " entries for " + text : tags.Count + " entries for " + text;
            stack.Children.Add(Label(caption + " - click one to open it", 12, "FgFaint", false, new Thickness(16, 0, 16, 4)));
            stack.Children.Add(new ScrollViewer { MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rows });

            var box = Box(stack, 300, 440);
            pop = Show(box, cursor);
        }

        static Border Box(UIElement content, double minWidth, double maxWidth)
        {
            var box = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                MaxWidth = maxWidth,
                Margin = new Thickness(12),   // room for the shadow
                Child = content,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black },
            };
            box.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            box.SetResourceReference(Border.BorderBrushProperty, "Line");
            return box;
        }

        /// <summary>Open the pop-up next to the mouse pointer: below it, or above it when the screen ends below.</summary>
        static Popup Show(Border box, Point cursor)
        {
            if (current != null) current.IsOpen = false;
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = box.DesiredSize;
            Rect work = WorkAreaDips(cursor);

            double x = Math.Min(cursor.X - 16, work.Right - size.Width);
            x = Math.Max(work.Left, x);
            double below = cursor.Y + 4;
            double above = cursor.Y - size.Height - 4;
            double y = below + size.Height <= work.Bottom ? below : (above >= work.Top ? above : Math.Max(work.Top, work.Bottom - size.Height));

            var pop = new Popup
            {
                Placement = PlacementMode.Absolute,
                HorizontalOffset = x,
                VerticalOffset = y,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = box,
            };
            current = pop;
            pop.IsOpen = true;
            return pop;
        }

        static TextBlock Label(string text, double size, string brush, bool jp, Thickness margin, VerticalAlignment va = VerticalAlignment.Top)
        {
            var t = new TextBlock { Text = text ?? "", FontSize = size, Margin = margin, VerticalAlignment = va };
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            if (jp) t.FontFamily = Ui.JpFont;
            return t;
        }

        // ---- where the mouse is, and where the screen ends --------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        static Matrix FromDevice()
        {
            var src = PresentationSource.FromVisual(MainWindow.Instance);
            return src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice : Matrix.Identity;
        }

        static Point CursorDips()
        {
            POINT p;
            if (!GetCursorPos(out p)) return new Point(100, 100);
            return FromDevice().Transform(new Point(p.X, p.Y));
        }

        /// <summary>The usable area (without the taskbar) of the screen the pointer is on, in WPF units.</summary>
        static Rect WorkAreaDips(Point cursorDips)
        {
            try
            {
                POINT p; GetCursorPos(out p);
                var mon = MonitorFromPoint(p, 2);   // nearest monitor
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(mon, ref info))
                {
                    var m = FromDevice();
                    var tl = m.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
                    var br = m.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
                    return new Rect(tl, br);
                }
            }
            catch { }
            return SystemParameters.WorkArea;
        }
    }
}
