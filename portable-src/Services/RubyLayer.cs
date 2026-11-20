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
                    // the rectangle of every kanji of the run
                    var rects = new List<Rect>();
                    var start = item.Run.ContentStart.GetPositionAtOffset(item.Offset, LogicalDirection.Forward);
                    for (int k = 0; k < item.Length; k++)
                    {
                        var p = start != null ? start.GetPositionAtOffset(k, LogicalDirection.Forward) : null;
                        if (p == null) break;
                        var r = p.GetCharacterRect(LogicalDirection.Forward);
                        if (r.IsEmpty) break;
                        rects.Add(r);
                    }
                    if (rects.Count == 0) continue;

                    // a word that wraps onto the next line: the reading goes over the part on the first line
                    double top = rects[0].Top;
                    var line = rects.Where(r => Math.Abs(r.Top - top) < 4).ToList();
                    double x0 = line.Min(r => r.Left), x1 = line.Max(r => r.Right);

                    double size = ReadingSize;
                    FormattedText ft = Text(item.Reading, size, brush);
                    // a long reading over few kanji: make it smaller instead of letting it run into the neighbours
                    double room = Math.Max(x1 - x0, 12) * 1.6;
                    if (ft.Width > room) { size = Math.Max(7, size * room / ft.Width); ft = Text(item.Reading, size, brush); }

                    double x = (x0 + x1) / 2 - ft.Width / 2;
                    if (Math.Abs(top - prevTop) < 4 && x < prevRight + 1) x = prevRight + 1;   // do not touch the reading before
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
