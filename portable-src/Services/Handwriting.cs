using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Takoboto.Database.Impl;

namespace TakobotoApp
{
    /// <summary>Handwriting recognition for the search page: the stroke templates are built from the dictionary file the first
    /// time they are needed (a few seconds, in the background) and kept in the userdata folder for the next time.</summary>
    public static class Handwriting
    {
        static StrokeRecognizer recognizer;
        static Task loading;
        static readonly object Gate = new object();

        public static bool Ready { get { return recognizer != null; } }
        public static int Characters { get { return recognizer == null ? 0 : recognizer.Count; } }

        /// <summary>Start preparing the templates (once). The task ends when they are ready.</summary>
        public static Task EnsureLoaded()
        {
            lock (Gate)
            {
                if (loading == null) loading = Task.Run(() => Build());
                return loading;
            }
        }

        static string CachePath { get { return Path.Combine(Store.Dir, "handwriting.bin"); } }

        static void Build()
        {
            try
            {
                long datSize = new FileInfo(Engine.DataFile).Length;
                // the cache says which dictionary file it was made from
                if (File.Exists(CachePath))
                {
                    try
                    {
                        using (var s = File.OpenRead(CachePath))
                        {
                            var head = new byte[8]; s.Read(head, 0, 8);
                            if (BitConverter.ToInt64(head, 0) == datSize) { recognizer = StrokeRecognizer.Load(s); return; }
                        }
                    }
                    catch (Exception ex) { App.Log(ex); }
                }

                var rec = new StrokeRecognizer();
                var chars = new SortedSet<char>();
                foreach (var r in KanjiRadicals.Radicals) foreach (char c in r.Kanjis) chars.Add(c);
                foreach (char c in chars)
                {
                    var paths = Engine.StrokePaths(c);
                    if (paths != null) rec.AddTemplate(c, paths);
                }
                recognizer = rec;
                try
                {
                    using (var s = File.Create(CachePath)) { s.Write(BitConverter.GetBytes(datSize), 0, 8); rec.Save(s); }
                }
                catch (Exception ex) { App.Log(ex); }
            }
            catch (Exception ex) { App.Log(ex); lock (Gate) loading = null; }
        }

        /// <summary>The kanji the strokes most likely are (best first). Empty while the templates are not ready.</summary>
        public static List<char> Recognise(IList<List<double[]>> strokes, int max)
        {
            var rec = recognizer;
            if (rec == null) return new List<char>();
            // a hand-drawn line wobbles: smooth it a little before it is compared
            var smooth = strokes.Select(Smooth).ToList();
            return rec.Recognise(smooth, max);
        }

        static List<double[]> Smooth(List<double[]> s)
        {
            if (s.Count < 4) return s;
            var r = new List<double[]>(s.Count) { s[0] };
            for (int i = 1; i < s.Count - 1; i++)
                r.Add(new[] { (s[i - 1][0] + s[i][0] + s[i + 1][0]) / 3, (s[i - 1][1] + s[i][1] + s[i + 1][1]) / 3 });
            r.Add(s[s.Count - 1]);
            return r;
        }

        /// <summary>The strokes of a template, for tests: points in the 100 x 100 frame.</summary>
        public static List<List<double[]>> SampleStrokes(char c)
        {
            var paths = Engine.StrokePaths(c);
            return paths == null ? null : paths.Select(StrokeRecognizer.FlattenPath).ToList();
        }
    }
}
