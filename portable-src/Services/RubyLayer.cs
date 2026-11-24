using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TakobotoApp
{
    /// <summary>One run of kanji of a sentence and its reading.</summary>
    public class RubyItem
    {
        public Run Run;        // the piece of the text that holds the kanji
        public int Offset;     // position of the first kanji inside that run
        public int Length;     // how many kanji
        public string Reading; // hiragana
    }

    /// <summary>Draws furigana - the reading in small kana - right above the kanji of a <see cref="SelText"/>. The text stays
    /// ordinary text (so it can be selected and copied, the words stay clickable); the readings are painted over it in a
    /// layer that sits on top of the text box and lets every mouse click through. The positions come from the text box
    /// itself (where each character was laid out), so they follow wrapping and resizing.
    /// The room for them is made by <see cref="SelText.MakeRoomForFurigana"/>.</summary>
    public class RubyLayer : FrameworkElement
    {
        public const double ReadingSize = 10;
        readonly SelText source;
        readonly Typeface typeface = new Typeface(Ui.JpFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public RubyLayer(SelText source)
        {
            this.source = source;
            IsHitTestVisible = false;
            source.Loaded += (s, e) => Refresh();
            source.SizeChanged += (s, e) => Refresh();
            Loaded += (s, e) => Theme.Changed += Refresh;
            Unloaded += (s, e) => Theme.Changed -= Refresh;
        }

        void Refresh()
        {
            InvalidateVisual();
            // the text box finishes laying its text out a moment after it is shown: look again then
            Dispatcher.BeginInvoke(new Action(InvalidateVisual), System.Windows.Threading.DispatcherPriority.Background);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var brush = TryFindResource("FgMuted") as Brush ?? Brushes.Gray;
            double prevRight = double.NegativeInfinity, prevTop = double.NaN;
            foreach (var item in source.Ruby)
            {
                try
                {
                    // where every kanji of the run stands. The rectangle GetCharacterRect gives has NO width (it is where the caret
                    // would be), so the right edge is the left edge of the next character on the same line, or one em for a kanji.
                    var start = item.Run.ContentStart.GetPositionAtOffset(item.Offset, LogicalDirection.Forward);
                    if (start == null) continue;
                    Rect first = start.GetCharacterRect(LogicalDirection.Forward);
                    if (first.IsEmpty) continue;
                    double top = first.Top;
                    double x0 = first.Left, x1 = first.Left;
                    for (int k = 0; k < item.Length; k++)
                    {
                        var p = start.GetPositionAtOffset(k, LogicalDirection.Forward);
                        var r = p != null ? p.GetCharacterRect(LogicalDirection.Forward) : Rect.Empty;
                        if (r.IsEmpty || Math.Abs(r.Top - top) > 4) break;   // a word that wraps: the reading goes over the part on the first line
                        var pn = p.GetPositionAtOffset(1, LogicalDirection.Forward);
                        var rn = pn != null ? pn.GetCharacterRect(LogicalDirection.Forward) : Rect.Empty;
                        double right = !rn.IsEmpty && Math.Abs(rn.Top - top) < 4 && rn.Left > r.Left ? rn.Left : r.Left + source.FontSize;
                        x1 = Math.Max(x1, right);
                    }
                    double center = (x0 + x1) / 2, width = x1 - x0;

                    // the reading, no wider than the kanji it belongs to plus a little (a long reading is made smaller)
                    double size = ReadingSize;
                    FormattedText ft = Text(item.Reading, size, brush);
                    double room = item.Length == 1 ? width + 4 : width * 1.2;
                    if (ft.Width > room) { size = Math.Max(6.5, size * room / ft.Width); ft = Text(item.Reading, size, brush); }

                    // it must not run into the reading before it: first make it smaller, then push it aside
                    if (Math.Abs(top - prevTop) < 4 && center - ft.Width / 2 < prevRight + 0.5)
                    {
                        double avail = (center - prevRight - 0.5) * 2;
                        if (avail >= 8 && avail < ft.Width) { size = Math.Max(6.5, size * avail / ft.Width); ft = Text(item.Reading, size, brush); }
                    }
                    double x = center - ft.Width / 2;
                    if (Math.Abs(top - prevTop) < 4 && x < prevRight + 0.5) x = prevRight + 0.5;

                    // the reading sits in the room above the line's text: its baseline a pixel above where the ink of the kanji starts
                    double y = top + source.InkTop - 1.0 - ft.Baseline;
                    dc.DrawText(ft, new Point(x, y));
                    prevRight = x + ft.Width; prevTop = top;
                }
                catch (InvalidOperationException) { /* the text is not laid out yet: the next Refresh draws it */ }
            }
        }

        FormattedText Text(string s, double size, Brush brush)
        {
            return new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush, 1.0);
        }
    }
}
