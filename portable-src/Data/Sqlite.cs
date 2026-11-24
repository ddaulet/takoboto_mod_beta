using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TakobotoApp
{
    /// <summary>SQLite through winsqlite3.dll, which is part of Windows 10 / 11 (nothing to download, nothing to ship).
    /// One connection, one lock; the statements are prepared once and used again.</summary>
    public sealed class Sqlite : IDisposable
    {
        const string Lib = "winsqlite3.dll";
        const int OK = 0, ROW = 100, DONE = 101, OPEN_READONLY = 1, OPEN_FULLMUTEX = 0x10000, TRANSIENT = -1;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] file, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db, string sql, int n, out IntPtr stmt, IntPtr tail);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_reset(IntPtr stmt);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_clear_bindings(IntPtr stmt);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr stmt, int i, long v);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr stmt, int i, string v, int n, IntPtr destructor);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text16(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg16(IntPtr db);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_blob(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr stmt, int i);

        IntPtr db;
        readonly object gate = new object();
        readonly Dictionary<string, IntPtr> cache = new Dictionary<string, IntPtr>();

        /// <summary>The current row while a query runs.</summary>
        public struct Row
        {
            internal IntPtr S;
            public string Str(int i) { var p = sqlite3_column_text16(S, i); return p == IntPtr.Zero ? null : Marshal.PtrToStringUni(p); }
            public int Int(int i) { return (int)sqlite3_column_int64(S, i); }
            public long Long(int i) { return sqlite3_column_int64(S, i); }
            public bool IsNull(int i) { return sqlite3_column_type(S, i) == 5; }
            public byte[] Bytes(int i)
            {
                int n = sqlite3_column_bytes(S, i);
                var b = new byte[n];
                if (n > 0) Marshal.Copy(sqlite3_column_blob(S, i), b, 0, n);
                return b;
            }
        }

        public static Sqlite OpenReadOnly(string path)
        {
            var s = new Sqlite();
            var name = Encoding.UTF8.GetBytes(path + "\0");
            int rc = sqlite3_open_v2(name, out s.db, OPEN_READONLY | OPEN_FULLMUTEX, IntPtr.Zero);
            if (rc != OK) throw new InvalidOperationException("The dictionary could not be opened (SQLite " + rc + "): " + path);
            return s;
        }

        string Error() { var p = sqlite3_errmsg16(db); return p == IntPtr.Zero ? "" : Marshal.PtrToStringUni(p); }

        /// <summary>Runs a query; <paramref name="read"/> is called for every row (it must not run another query).</summary>
        public void Each(string sql, Action<Row> read, params object[] args)
        {
            lock (gate)
            {
                IntPtr st;
                if (!cache.TryGetValue(sql, out st))
                {
                    int rc0 = sqlite3_prepare16_v2(db, sql, -1, out st, IntPtr.Zero);
                    if (rc0 != OK) throw new InvalidOperationException("SQL: " + Error() + " in " + sql);
                    cache[sql] = st;
                }
                try
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        object a = args[i];
                        if (a == null) sqlite3_bind_null(st, i + 1);
                        else if (a is string) sqlite3_bind_text16(st, i + 1, (string)a, -1, new IntPtr(TRANSIENT));
                        else if (a is int) sqlite3_bind_int64(st, i + 1, (int)a);
                        else if (a is long) sqlite3_bind_int64(st, i + 1, (long)a);
                        else if (a is bool) sqlite3_bind_int64(st, i + 1, (bool)a ? 1 : 0);
                        else throw new ArgumentException("SQL argument type " + a.GetType());
                    }
                    var row = new Row { S = st };
                    while (true)
                    {
                        int rc = sqlite3_step(st);
                        if (rc == ROW) read(row);
                        else if (rc == DONE) break;
                        else throw new InvalidOperationException("SQL: " + Error());
                    }
                }
                finally { sqlite3_reset(st); sqlite3_clear_bindings(st); }
            }
        }

        public List<T> Query<T>(string sql, Func<Row, T> map, params object[] args)
        {
            var list = new List<T>();
            Each(sql, r => list.Add(map(r)), args);
            return list;
        }

        /// <summary>The first column of the first row, or null.</summary>
        public string Scalar(string sql, params object[] args)
        {
            string v = null;
            bool got = false;
            Each(sql, r => { if (!got) { v = r.Str(0); got = true; } }, args);
            return v;
        }

        /// <summary>"?,?,?" for an IN (...) list.</summary>
        public static string Marks(int n) { return string.Join(",", System.Linq.Enumerable.Repeat("?", n)); }

        public void Dispose()
        {
            lock (gate)
            {
                foreach (var st in cache.Values) sqlite3_finalize(st);
                cache.Clear();
                if (db != IntPtr.Zero) { sqlite3_close_v2(db); db = IntPtr.Zero; }
            }
        }
    }
}
