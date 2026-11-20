using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>The small pop-up that shows what a word is - reading and meaning - right where you clicked or
    /// selected it. Clicking the pop-up opens the full entry. Shown for a selection in the word card and for
    /// the words of the example sentences.</summary>
    public static class Preview
    {
        static Popup current;

        /// <summary>The user selected some text.</summary>
        static string lastText; static int lastTick;

        public static void ShowForSelection(string selected)
        {
            string text = (selected ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length == 0 || text.Length > 40 || !text.Any(char.IsLetterOrDigit)) return;
            // a selection can be reported twice (mouse released, then "selection settled"): show it once
            int now = Environment.TickCount;
            if (text == lastText && now - lastTick < 1500) return;
            lastText = text; lastTick = now;
            ShowWord(text, null);
        }

        /// <summary>Look the word up (an exact dictionary entry; the one with <paramref name="reading"/> if there are
        /// several) and pop up its preview at the mouse pointer. Nothing is shown when there is no such entry.</summary>
        public static async void ShowWord(string text, string reading)
        {
            WordTag tag = null;
            try { tag = await Task.Run(() => Engine.Lookup(text, reading)); }
            catch (Exception ex) { App.Log(ex); }
            if (tag == null) return;
            var item = WordItem.From(tag);
            Open(tag, item);
        }

        static void Open(WordTag tag, WordItem item)
        {
            if (current != null) current.IsOpen = false;

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

            var box = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                MinWidth = 220,
                MaxWidth = 380,
                Margin = new Thickness(12),
                Cursor = Cursors.Hand,
                Child = stack,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black },
            };
            box.SetResourceReference(Border.BackgroundProperty, "SurfaceBg");
            box.SetResourceReference(Border.BorderBrushProperty, "Line");

            var pop = new Popup
            {
                Placement = PlacementMode.Mouse,
                VerticalOffset = 8,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Child = box,
            };
            box.MouseLeftButtonUp += (s, e) =>
            {
                pop.IsOpen = false;
                MainWindow.Instance.OpenWord(tag, item.Main);
            };
            current = pop;
            pop.IsOpen = true;
        }

        static TextBlock Label(string text, double size, string brush, bool jp, Thickness margin, VerticalAlignment va = VerticalAlignment.Top)
        {
            var t = new TextBlock { Text = text ?? "", FontSize = size, Margin = margin, VerticalAlignment = va };
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            if (jp) t.FontFamily = Ui.JpFont;
            return t;
        }
    }
}
