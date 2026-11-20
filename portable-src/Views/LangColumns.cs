using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TakobotoApp
{
    /// <summary>The widths a column of <see cref="LangColumns"/> wants: Natural = all lines unwrapped, Minimum = the longest word (both with the flag).</summary>
    public class ColumnInfo { public double Natural, Minimum; }

    /// <summary>Lays the translations of one meaning out as invisible columns, side by side (English, French, German,
    /// Russian - the order the children are added in). Each column is only as wide as its text needs; when the row is too
    /// narrow the WIDEST columns give up width first (their lines wrap), but never so far that a word would break in the
    /// middle. When even that does not fit, the languages are simply stacked under each other (full width each).
    /// Every child carries a <see cref="ColumnInfo"/> in its Tag.</summary>
    public class LangColumns : Panel
    {
        const double Gap = 18, RowGap = 6;
        double[] widths = new double[0];
        bool stacked;

        protected override Size MeasureOverride(Size available)
        {
            int n = InternalChildren.Count;
            var nat = new double[n];
            var min = new double[n];
            for (int i = 0; i < n; i++)
            {
                // the text boxes inside cannot tell their natural width (they shrink to nothing when measured without a limit), so each
                // child brings it: the width of its widest line and of its longest word, measured by the caller
                var info = ((FrameworkElement)InternalChildren[i]).Tag as ColumnInfo;
                nat[i] = info != null ? info.Natural : 100;
                min[i] = info != null ? Math.Min(info.Minimum, nat[i]) : nat[i];
            }
            double w = available.Width;
            double gaps = Gap * Math.Max(0, n - 1);
            stacked = n <= 1;
            widths = nat;
            if (n > 1 && !double.IsInfinity(w) && nat.Sum() + gaps > w)
            {
                if (min.Sum() + gaps > w) stacked = true;
                else widths = Shrink(nat, min, w - gaps);
            }

            if (stacked)
            {
                double width = double.IsInfinity(w) ? nat.DefaultIfEmpty(0).Max() : w;
                double h = 0;
                for (int i = 0; i < n; i++)
                {
                    InternalChildren[i].Measure(new Size(width, double.PositiveInfinity));
                    h += InternalChildren[i].DesiredSize.Height + (i > 0 ? RowGap : 0);
                }
                return new Size(width, h);
            }
            double height = 0;
            for (int i = 0; i < n; i++)
            {
                InternalChildren[i].Measure(new Size(widths[i], double.PositiveInfinity));
                height = Math.Max(height, InternalChildren[i].DesiredSize.Height);
            }
            return new Size(widths.Sum() + gaps, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int n = InternalChildren.Count;
            if (stacked)
            {
                double y = 0;
                for (int i = 0; i < n; i++)
                {
                    var c = InternalChildren[i];
                    c.Arrange(new Rect(0, y, finalSize.Width, c.DesiredSize.Height));
                    y += c.DesiredSize.Height + RowGap;
                }
                return finalSize;
            }
            double x = 0;
            for (int i = 0; i < n; i++)
            {
                InternalChildren[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
                x += widths[i] + Gap;
            }
            return finalSize;
        }

        /// <summary>Take <c>sum(nat) - room</c> pixels away: always from the widest columns, none below its minimum.</summary>
        static double[] Shrink(double[] nat, double[] min, double room)
        {
            var w = (double[])nat.Clone();
            double need = w.Sum() - room;
            for (int guard = 0; guard < 50 && need > 0.5; guard++)
            {
                double max = 0;
                for (int i = 0; i < w.Length; i++) if (w[i] > min[i] + 0.5 && w[i] > max) max = w[i];
                if (max <= 0) break;                                           // nothing can shrink any more
                var top = Enumerable.Range(0, w.Length).Where(i => w[i] >= max - 0.01 && w[i] > min[i] + 0.5).ToList();
                // the next level: the widest of the others (or a column's own minimum)
                double next = 0;
                for (int i = 0; i < w.Length; i++) if (!top.Contains(i) && w[i] > next) next = w[i];
                double step = Math.Min(max - next, need / top.Count);
                foreach (int i in top) step = Math.Min(step, w[i] - min[i]);
                if (step <= 0) break;
                foreach (int i in top) w[i] -= step;
                need -= step * top.Count;
            }
            return w;
        }
    }
}
