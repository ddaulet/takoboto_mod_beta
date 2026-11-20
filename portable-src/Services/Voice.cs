using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TakobotoApp
{
    /// <summary>Pronunciation: the text is spoken by VOICEVOX CORE, an offline neural voice (nothing is sent anywhere).
    /// The voice files are NOT part of the program itself: they are one folder, "voice", next to Takoboto.exe:
    ///   voice\voicevox_core.dll, voice\voicevox_onnxruntime.dll, voice\open_jtalk_dic\ (the Japanese text analysis),
    ///   voice\models\*.vvm (the voices). Without that folder <see cref="Available"/> is false and the program simply has no
    /// speaker buttons. The voices are "VOICEVOX Nemo" (credit required, see the About page).</summary>
    public static class Voice
    {
        /// <summary>Where the voice folder is (tests may change it).</summary>
        public static string PackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice");

        // VOICEVOX Nemo, model n0.vvm: style ids of the plain female and male voices
        public const uint FemaleStyle = 10005;   // "female 1"
        public const uint MaleStyle = 10001;     // "male 1"

        static string CoreDll { get { return Path.Combine(PackDir, "voicevox_core.dll"); } }
        static string OrtDll { get { return Path.Combine(PackDir, "voicevox_onnxruntime.dll"); } }
        static string DicDir { get { return Path.Combine(PackDir, "open_jtalk_dic"); } }
        static string ModelDir { get { return Path.Combine(PackDir, "models"); } }

        static bool? available;

        /// <summary>The voice files are there (looked up once).</summary>
        public static bool Available
        {
            get
            {
                if (available == null)
                {
                    try
                    {
                        available = File.Exists(CoreDll) && File.Exists(OrtDll) && Directory.Exists(DicDir)
                            && Directory.Exists(ModelDir) && Directory.GetFiles(ModelDir, "*.vvm").Length > 0;
                    }
                    catch { available = false; }
                }
                return available.Value;
            }
        }

        // ---- the native library (C interface of VOICEVOX CORE 0.17) ---------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)] struct LoadOrtOptions { public IntPtr filename; }
        [StructLayout(LayoutKind.Sequential)] struct InitOptions { public int acceleration_mode; public ushort cpu_num_threads; }
        [StructLayout(LayoutKind.Sequential)] struct LoadModelOptions { public int on_existing; }
        [StructLayout(LayoutKind.Sequential)] struct TtsOptions { [MarshalAs(UnmanagedType.U1)] public bool enable_interrogative_upspeak; }

        const string Lib = "voicevox_core";
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_onnxruntime_load_once(LoadOrtOptions options, out IntPtr ort);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_open_jtalk_rc_new(byte[] dir, out IntPtr jtalk);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_synthesizer_new(IntPtr ort, IntPtr jtalk, InitOptions options, out IntPtr synth);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_voice_model_file_open(byte[] path, out IntPtr model);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_synthesizer_load_voice_model(IntPtr synth, IntPtr model, LoadModelOptions options);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void voicevox_voice_model_file_delete(IntPtr model);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr voicevox_synthesizer_create_metas_json(IntPtr synth);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void voicevox_json_free(IntPtr json);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_synthesizer_tts(IntPtr synth, byte[] text, uint style, TtsOptions options, out UIntPtr length, out IntPtr wav);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern void voicevox_wav_free(IntPtr wav);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr voicevox_error_result_to_message(int code);
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

        static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes((s ?? "") + "\0"); }
        static string FromUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            int n = 0; while (Marshal.ReadByte(p, n) != 0) n++;
            var b = new byte[n]; Marshal.Copy(p, b, 0, n);
            return Encoding.UTF8.GetString(b);
        }

        static void Ok(int code, string what)
        {
            if (code != 0) throw new InvalidOperationException(what + ": " + code + " " + FromUtf8(voicevox_error_result_to_message(code)));
        }

        // ---- start (once, the first time something is spoken: loading the voices takes a moment) -------------------------------
        static readonly object Gate = new object();   // the engine is used by one sentence at a time
        static IntPtr synth;
        static bool initTried;
        public static string Metas;                   // the voices of the loaded files, as JSON (for tests)

        static bool Init()
        {
            if (synth != IntPtr.Zero) return true;
            if (initTried) return false;
            initTried = true;
            try
            {
                if (LoadLibraryEx(CoreDll, IntPtr.Zero, 8) == IntPtr.Zero)   // 8 = look for its neighbours in its own folder
                    throw new Win32Exception(Marshal.GetLastWin32Error(), CoreDll);
                var name = Utf8(OrtDll);
                var mem = Marshal.AllocHGlobal(name.Length);   // lives as long as the program
                Marshal.Copy(name, 0, mem, name.Length);
                IntPtr ort, jtalk, s;
                Ok(voicevox_onnxruntime_load_once(new LoadOrtOptions { filename = mem }, out ort), "onnxruntime");
                Ok(voicevox_open_jtalk_rc_new(Utf8(DicDir), out jtalk), "open_jtalk");
                Ok(voicevox_synthesizer_new(ort, jtalk, new InitOptions { acceleration_mode = 1, cpu_num_threads = 0 }, out s), "synthesizer");   // 1 = CPU
                foreach (var f in Directory.GetFiles(ModelDir, "*.vvm"))
                {
                    IntPtr model;
                    Ok(voicevox_voice_model_file_open(Utf8(f), out model), "model " + Path.GetFileName(f));
                    Ok(voicevox_synthesizer_load_voice_model(s, model, new LoadModelOptions { on_existing = 2 }), "load " + Path.GetFileName(f));
                    voicevox_voice_model_file_delete(model);
                }
                var json = voicevox_synthesizer_create_metas_json(s);
                Metas = FromUtf8(json);
                voicevox_json_free(json);
                synth = s;
                return true;
            }
            catch (Exception ex) { App.Log(ex); return false; }
        }

        /// <summary>Start loading in the background, so that the first sentence does not wait for it.</summary>
        public static void Warm()
        {
            if (!Available || synth != IntPtr.Zero) return;
            Task.Run(() => { lock (Gate) Init(); });
        }

        // ---- text -> WAV ---------------------------------------------------------------------------------------------------
        static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>();

        static string Clean(string text)
        {
            if (text == null) return "";
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>The spoken text as a WAV file (24 kHz, 16 bit, mono); null when it cannot be spoken. Slow: do not call on the window thread.</summary>
        public static byte[] Synthesize(string text, uint style)
        {
            text = Clean(text);
            if (text.Length == 0) return null;
            string key = style + "|" + text;
            byte[] wav;
            lock (Cache) { if (Cache.TryGetValue(key, out wav)) return wav; }
            try
            {
                lock (Gate)
                {
                    if (!Init()) return null;
                    UIntPtr len; IntPtr ptr;
                    int rc = voicevox_synthesizer_tts(synth, Utf8(text), style, new TtsOptions { enable_interrogative_upspeak = true }, out len, out ptr);
                    Ok(rc, "tts");
                    wav = new byte[(int)len];
                    Marshal.Copy(ptr, wav, 0, wav.Length);
                    voicevox_wav_free(ptr);
                }
            }
            catch (Exception ex) { App.Log(ex); return null; }
            lock (Cache) { if (Cache.Count > 80) Cache.Clear(); Cache[key] = wav; }
            return wav;
        }

        // ---- speaking --------------------------------------------------------------------------------------------------------
        static int generation;
        static SoundPlayer player;
        static readonly object PlayGate = new object();

        static void StopPlayer()
        {
            lock (PlayGate) { if (player != null) { try { player.Stop(); } catch { } } }
        }

        /// <summary>Stop what is being spoken.</summary>
        public static void Stop() { Interlocked.Increment(ref generation); StopPlayer(); }

        /// <summary>Speak the text (a new request stops the one before it). <paramref name="state"/> gets true at once and false when this
        /// request is over (finished, stopped, replaced or failed); it is called on the thread that called Speak (the window thread).</summary>
        public static void Speak(string text, bool male, Action<bool> state)
        {
            var sync = SynchronizationContext.Current;
            Action<bool> tell = b => { if (state == null) return; if (sync != null) sync.Post(_ => state(b), null); else state(b); };
            int my = Interlocked.Increment(ref generation);
            StopPlayer();
            tell(true);
            Task.Run(() =>
            {
                try
                {
                    if (my != generation) return;
                    var wav = Synthesize(text, male ? MaleStyle : FemaleStyle);
                    if (wav == null || my != generation) return;
                    using (var ms = new MemoryStream(wav))
                    using (var p = new SoundPlayer(ms))
                    {
                        lock (PlayGate) { if (my != generation) return; player = p; }
                        try { p.PlaySync(); }
                        finally { lock (PlayGate) { if (player == p) player = null; } }
                    }
                }
                catch (Exception ex) { App.Log(ex); }
                finally { tell(false); }
            });
        }
    }
}
