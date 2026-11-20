using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TakobotoApp
{
    /// <summary>A block of text that can be SELECTED and copied (a read-only RichTextBox without any chrome; WPF's
    /// TextBlock cannot do that). When the user finishes selecting a word, a small preview of that word pops up
    /// (see <see cref="Preview"/>). A Run whose Tag is an Action works as a clickable word: a click that is not a
    /// drag runs the action.</summary>
    public class SelText : RichTextBox
    {
        readonly Paragraph para = new Paragraph { Margin = new Thickness(0) };
        readonly DispatcherTimer settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        Point downAt;

        public SelText()
        {
            IsReadOnly = true;
            IsDocumentEnabled = true;
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            Padding = new Thickness(0);
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            FocusVisualStyle = null;
            SelectionOpacity = 0.35;
            ContextMenuService.SetIsEnabled(this, false);   // the default (light) context menu does not fit the theme
            Document = new FlowDocument(para) { PagePadding = new Thickness(0) };
            SetResourceReference(ForegroundProperty, "Fg");
            SetResourceReference(SelectionBrushProperty, "Accent");

            // a text box swallows the mouse wheel: hand it on to the scroll viewer of the card
            PreviewMouseWheel += (s, e) =>
            {
                if (e.Handled) return;
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(this) as UIElement;
                if (parent == null) return;
                e.Handled = true;
                parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = this });
            };

            PreviewMouseLeftButtonDown += (s, e) => downAt = e.GetPosition(this);

            // the mouse button went up: a selection was finished (preview the selected text) or a word was clicked
            PreviewMouseLeftButtonUp += (s, e) =>
            {
                Point at = e.GetPosition(this);
                bool still = Math.Abs(at.X - downAt.X) < 4 && Math.Abs(at.Y - downAt.Y) < 4;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Selection != null && !Selection.IsEmpty) { settle.Stop(); Preview.ShowForSelection(Selection.Text); }
                    else if (still) ClickWordAt(at);
                }), DispatcherPriority.Input);
            };

            // a selection made with the keyboard (Shift+arrows) is previewed too, once it stops changing
            settle.Tick += (s, e) =>
            {
                settle.Stop();
                if (Mouse.LeftButton == MouseButtonState.Pressed) return;
                if (Selection != null && !Selection.IsEmpty) Preview.ShowForSelection(Selection.Text);
            };
            SelectionChanged += (s, e) => { settle.Stop(); if (Selection != null && !Selection.IsEmpty) settle.Start(); };
        }

        void ClickWordAt(Point p)
        {
            var pos = GetPositionFromPoint(p, false);
            for (DependencyObject o = pos != null ? pos.Parent : null; o != null; o = LogicalTreeHelper.GetParent(o))
            {
                var run = o as Run;
                if (run != null && run.Tag is Action) { ((Action)run.Tag)(); return; }
                if (o is Paragraph) break;
            }
        }

        /// <summary>For automated tests (--word-test): act on the run with exactly this text as the mouse would -
        /// "click" clicks it (through the same hit-testing as a real click), "select" selects it.</summary>
        public bool Probe(string text, bool click)
        {
            foreach (var inline in para.Inlines)
            {
                var run = inline as Run;
                if (run == null || run.Text != text) continue;
                if (click)
                {
                    var r = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                    ClickWordAt(new Point(r.X + r.Width / 2 + 1, r.Y + r.Height / 2));
                }
                else Selection.Select(run.ContentStart, run.ContentEnd);
                return true;
            }
            return false;
        }

        public InlineCollection Inlines { get { return para.Inlines; } }

        // ---- furigana ---------------------------------------------------------------------------------------------
        /// <summary>The kanji of this text that get a reading above them (see <see cref="RubyLayer"/>).</summary>
        public readonly List<RubyItem> Ruby = new List<RubyItem>();

        /// <summary>Distance from the top of a character's cell (what GetCharacterRect returns) to the top of a kanji's ink.</summary>
        public double InkTop { get; private set; }

        /// <summary>Makes every line <paramref name="extra"/> pixels taller; the extra room is ABOVE the text (that is where the
        /// furigana go). Also works out where the ink of a kanji starts inside its character cell.</summary>
        public void MakeRoomForFurigana(double extra)
        {
            var ft = new FormattedText("漢", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.Black, 1.0);
            para.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            para.LineHeight = ft.Height + extra;
            // a kanji's ink reaches about 0.86 em above the baseline; the cell starts at the top of the font's ascent
            InkTop = Math.Max(0, ft.Baseline - FontSize * 0.86);
        }

        public string Text
        {
            get { return new TextRange(Document.ContentStart, Document.ContentEnd).Text.TrimEnd('\r', '\n'); }
            set
            {
                para.Inlines.Clear();
                if (!string.IsNullOrEmpty(value)) para.Inlines.Add(new Run(value));
            }
        }
    }
}
