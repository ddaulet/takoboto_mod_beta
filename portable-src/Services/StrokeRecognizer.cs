using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TakobotoApp
{
    /// <summary>Recognises a handwritten kanji from its strokes. No external data or libraries: the templates are the stroke
    /// shapes (KanjiVG, the same data the stroke-order diagrams of the program come from) that are in the dictionary file.
    ///
    /// How it works (the usual approach of stroke-based recognisers such as Kanji Draw / kanjidraw / Zinnia):
    ///  - every character - the user's and every template - is brought to the same frame (its bounding box scaled to 100 x 100,
    ///    centred, aspect ratio kept) and every stroke is resampled to a fixed number of points along its length;
    ///  - a template is compared stroke by stroke with the user's strokes: the distance of two strokes is the mean distance
    ///    of their points (so direction, shape, size and position all count);
    ///  - strokes in the right order are compared in order (with a cost for a missing or an extra stroke); strokes written
    ///    in the wrong order are matched by closeness instead, for a small extra cost;
    ///  - the templates with the lowest cost are the candidates.</summary>
    public class StrokeRecognizer
    {
        public const int Points = 16;          // points per stroke
        const double Frame = 100;              // the characters are scaled into 100 x 100
        const double SkipCost = 38;            // a stroke that has no partner (missing / extra)
        const double OrderPenalty = 5;         // strokes matched out of order
        const int MaxStrokeDifference = 3;

        public class Template
        {
            public char Ch;
            public float[][] Strokes;          // each: Points * 2 floats (x, y, x, y ...)
            public float Penalty;              // added to the cost: the small kana (ぁ ゃ っ) look like the big ones, the big ones are offered first
        }

        readonly List<Template> templates = new List<Template>();
        public int Count { get { return templates.Count; } }
        public IEnumerable<char> Characters { get { return templates.Select(t => t.Ch); } }

        // ---- building the templates ------------------------------------------------------------------------------------
        /// <summary>Adds a character from its strokes as SVG path data (KanjiVG: 109 x 109 units).</summary>
        public void AddTemplate(char ch, IList<string> strokePaths, float penalty = 0)
        {
            var strokes = strokePaths.Select(p => FlattenPath(p)).Where(s => s.Count > 0).ToList();
            if (strokes.Count == 0) return;
            var t = Normalise(strokes);
            if (t != null) templates.Add(new Template { Ch = ch, Strokes = t, Penalty = penalty });
        }

        // ---- recognising -----------------------------------------------------------------------------------------------
        /// <summary>The most likely characters for the strokes the user wrote (each stroke: points in any units).</summary>
        public List<char> Recognise(IList<List<double[]>> userStrokes, int max)
        {
            return RecogniseScored(userStrokes, max).Select(r => r.Key).ToList();
        }

        public List<KeyValuePair<char, double>> RecogniseScored(IList<List<double[]>> userStrokes, int max)
        {
            var user = Normalise(userStrokes.Where(s => s.Count > 0).ToList());
            var result = new List<KeyValuePair<char, double>>();
            if (user == null) return result;
            int n = user.Length;
            var scored = new List<KeyValuePair<char, double>>();
            foreach (var t in templates)
            {
                int m = t.Strokes.Length;
                if (Math.Abs(m - n) > MaxStrokeDifference) continue;
                double cost = Cost(user, t.Strokes) + t.Penalty;
                scored.Add(new KeyValuePair<char, double>(t.Ch, cost));
            }
            return scored.OrderBy(k => k.Value).Take(max).ToList();
        }

        // ---- the cost of one template -----------------------------------------------------------------------------------
        static double Cost(float[][] u, float[][] t)
        {
            int n = u.Length, m = t.Length;
            var d = new double[n, m];
            for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) d[i, j] = StrokeDistance(u[i], t[j]);

            // in order: dynamic programming; a stroke may be missing (skip a template stroke) or extra (skip a user stroke)
            var dp = new double[n + 1, m + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i * SkipCost;
            for (int j = 0; j <= m; j++) dp[0, j] = j * SkipCost;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    dp[i, j] = Math.Min(dp[i - 1, j - 1] + d[i - 1, j - 1], Math.Min(dp[i - 1, j] + SkipCost, dp[i, j - 1] + SkipCost));
            double ordered = dp[n, m] / Math.Max(n, m);

            // out of order: pair the strokes by closeness (closest pairs first)
            double unordered = double.MaxValue;
            if (n > 1 && m > 1)
            {
                var pairs = new List<Tuple<double, int, int>>();
                for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) pairs.Add(Tuple.Create(d[i, j], i, j));
                pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                var usedU = new bool[n]; var usedT = new bool[m];
                double sum = 0; int matched = 0;
                foreach (var p in pairs)
                {
                    if (usedU[p.Item2] || usedT[p.Item3]) continue;
                    usedU[p.Item2] = true; usedT[p.Item3] = true; sum += p.Item1; matched++;
                }
                sum += (Math.Max(n, m) - matched) * SkipCost;
                unordered = sum / Math.Max(n, m) + OrderPenalty;
            }
            return Math.Min(ordered, unordered);
        }

        static double StrokeDistance(float[] a, float[] b)
        {
            double sum = 0;
            for (int i = 0; i < Points; i++)
            {
                double dx = a[2 * i] - b[2 * i], dy = a[2 * i + 1] - b[2 * i + 1];
                sum += Math.Sqrt(dx * dx + dy * dy);
            }
            return sum / Points;
        }

        // ---- normalisation ----------------------------------------------------------------------------------------------
        static float[][] Normalise(IList<List<double[]>> strokes)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var s in strokes) foreach (var p in s) { minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]); minY = Math.Min(minY, p[1]); maxY = Math.Max(maxY, p[1]); }
            if (minX == double.MaxValue) return null;
            double w = maxX - minX, h = maxY - minY;
            double scale = Frame / Math.Max(Math.Max(w, h), 12);   // a tiny mark is not blown up to fill the frame
            double cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
            var result = new float[strokes.Count][];
            for (int i = 0; i < strokes.Count; i++)
            {
                var shifted = strokes[i].Select(p => new[] { (p[0] - cx) * scale + Frame / 2, (p[1] - cy) * scale + Frame / 2 }).ToList();
                result[i] = Resample(shifted);
            }
            return result;
        }

        // equally spaced points along the stroke
        static float[] Resample(List<double[]> pts)
        {
            var res = new float[Points * 2];
            double total = 0;
            var seg = new double[Math.Max(0, pts.Count - 1)];
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i][0] - pts[i - 1][0], dy = pts[i][1] - pts[i - 1][1];
                seg[i - 1] = Math.Sqrt(dx * dx + dy * dy); total += seg[i - 1];
            }
            if (total < 1e-6)
            {
                for (int k = 0; k < Points; k++) { res[2 * k] = (float)pts[0][0]; res[2 * k + 1] = (float)pts[0][1]; }
                return res;
            }
            double step = total / (Points - 1), acc = 0; int s = 0;
            for (int k = 0; k < Points; k++)
            {
                double target = k * step;
                while (s < seg.Length - 1 && acc + seg[s] < target) { acc += seg[s]; s++; }
                double f = seg[s] > 1e-9 ? Math.Min(1, Math.Max(0, (target - acc) / seg[s])) : 0;
                res[2 * k] = (float)(pts[s][0] + (pts[s + 1][0] - pts[s][0]) * f);
                res[2 * k + 1] = (float)(pts[s][1] + (pts[s + 1][1] - pts[s][1]) * f);
            }
            return res;
        }

        // ---- SVG path -> points (KanjiVG uses M m C c S s, sometimes L l H h V v) ---------------------------------------
        public static List<double[]> FlattenPath(string d)
        {
            var pts = new List<double[]>();
            if (string.IsNullOrEmpty(d)) return pts;
            var tokens = Tokenise(d);
            int i = 0; char cmd = 'M';
            double x = 0, y = 0, sx = 0, sy = 0, lastCx = 0, lastCy = 0; bool lastWasCubic = false;
            Func<double> next = () => tokens[i++].Number;
            while (i < tokens.Count)
            {
                if (tokens[i].IsCommand) cmd = tokens[i++].Command;
                else if (cmd == 'M') cmd = 'L'; else if (cmd == 'm') cmd = 'l';   // extra pairs after a moveto are linetos
                bool rel = char.IsLower(cmd);
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M': { double nx = next(), ny = next(); x = rel ? x + nx : nx; y = rel ? y + ny : ny; sx = x; sy = y; pts.Add(new[] { x, y }); lastWasCubic = false; break; }
                    case 'L': { double nx = next(), ny = next(); x = rel ? x + nx : nx; y = rel ? y + ny : ny; pts.Add(new[] { x, y }); lastWasCubic = false; break; }
                    case 'H': { double nx = next(); x = rel ? x + nx : nx; pts.Add(new[] { x, y }); lastWasCubic = false; break; }
                    case 'V': { double ny = next(); y = rel ? y + ny : ny; pts.Add(new[] { x, y }); lastWasCubic = false; break; }
                    case 'C':
                    case 'S':
                        {
                            double x1, y1;
                            if (char.ToUpperInvariant(cmd) == 'C') { double a = next(), b = next(); x1 = rel ? x + a : a; y1 = rel ? y + b : b; }
                            else { x1 = lastWasCubic ? 2 * x - lastCx : x; y1 = lastWasCubic ? 2 * y - lastCy : y; }
                            double c2 = next(), d2 = next(), e = next(), f = next();
                            double x2 = rel ? x + c2 : c2, y2 = rel ? y + d2 : d2, x3 = rel ? x + e : e, y3 = rel ? y + f : f;
                            for (int k = 1; k <= 10; k++)
                            {
                                double t = k / 10.0, u = 1 - t;
                                pts.Add(new[] { u * u * u * x + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3, u * u * u * y + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3 });
                            }
                            lastCx = x2; lastCy = y2; x = x3; y = y3; lastWasCubic = true; break;
                        }
                    case 'Z': x = sx; y = sy; break;
                    default: i = tokens.Count; break;   // a command that does not occur in KanjiVG
                }
            }
            return pts;
        }

        class Token { public bool IsCommand; public char Command; public double Number; }

        static List<Token> Tokenise(string d)
        {
            var list = new List<Token>();
            int i = 0;
            while (i < d.Length)
            {
                char c = d[i];
                if (char.IsLetter(c)) { list.Add(new Token { IsCommand = true, Command = c }); i++; }
                else if (c == '-' || c == '.' || c == '+' || char.IsDigit(c))
                {
                    int start = i; i++;
                    bool dot = c == '.';
                    while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot))) { if (d[i] == '.') dot = true; i++; }
                    if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) { i++; if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++; while (i < d.Length && char.IsDigit(d[i])) i++; }
                    list.Add(new Token { Number = double.Parse(d.Substring(start, i - start), CultureInfo.InvariantCulture) });
                }
                else i++;
            }
            return list;
        }

        // ---- cache (so the templates are built from the dictionary once) -----------------------------------------------------
        public void Save(Stream s)
        {
            using (var w = new BinaryWriter(s))
            {
                w.Write(1); w.Write(templates.Count);
                foreach (var t in templates)
                {
                    w.Write((ushort)t.Ch); w.Write((byte)t.Strokes.Length);
                    foreach (var st in t.Strokes) foreach (var f in st) w.Write((short)Math.Round(f * 100));   // 0.01 unit precision
                }
            }
        }

        public static StrokeRecognizer Load(Stream s)
        {
            var r = new StrokeRecognizer();
            using (var br = new BinaryReader(s))
            {
                if (br.ReadInt32() != 1) throw new InvalidDataException("unknown handwriting cache version");
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var t = new Template { Ch = (char)br.ReadUInt16(), Strokes = new float[br.ReadByte()][] };
                    for (int k = 0; k < t.Strokes.Length; k++)
                    {
                        t.Strokes[k] = new float[Points * 2];
                        for (int q = 0; q < Points * 2; q++) t.Strokes[k][q] = br.ReadInt16() / 100f;
                    }
                    r.templates.Add(t);
                }
            }
            return r;
        }
    }
}
