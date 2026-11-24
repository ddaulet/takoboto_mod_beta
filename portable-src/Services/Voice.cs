using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>One voice the user can choose: a style of a speaker inside one of the voice files (models\*.vvm).</summary>
        public class VoiceInfo
        {
            public string Key, Name, Credit, Model;
            public uint Style;
            public VoiceInfo(string key, string name, string credit, string model, uint style) { Key = key; Name = name; Credit = credit; Model = model; Style = style; }
            /// <summary>The voice file is there.</summary>
            public bool Installed { get { return File.Exists(Path.Combine(ModelDir, Model)); } }
        }

        /// <summary>All voices the program knows (those whose file is missing are not offered). The names are what the Settings page shows.</summary>
        public static readonly VoiceInfo[] All =
        {
            new VoiceInfo("female", "Female (plain)", "VOICEVOX Nemo", "n0.vvm", 10005),
            new VoiceInfo("male", "Male (plain)", "VOICEVOX Nemo", "n0.vvm", 10001),
            new VoiceInfo("samurai", "Samurai (rough man)", "VOICEVOX:青山龍星", "15.vvm", 13),
            new VoiceInfo("hero", "Anime hero (young man)", "VOICEVOX:玄野武宏", "4.vvm", 11),
            new VoiceInfo("kawaii", "Kawaii (bright anime girl)", "VOICEVOX:ずんだもん", "0.vvm", 1),
        };

        /// <summary>The voices that can be used now.</summary>
        public static List<VoiceInfo> Installed()
        {
            var r = new List<VoiceInfo>();
            foreach (var v in All) if (v.Installed) r.Add(v);
            return r;
        }

        /// <summary>The chosen voice; an unknown or missing one gives the first that is there. <paramref name="legacyMale"/> is the old male/female setting.</summary>
        public static VoiceInfo Find(string key, bool legacyMale)
        {
            if (string.IsNullOrEmpty(key)) key = legacyMale ? "male" : "female";
            VoiceInfo first = null;
            foreach (var v in All)
            {
                if (!v.Installed) continue;
                if (first == null) first = v;
                if (v.Key == key) return v;
            }
            return first;
        }

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
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_synthesizer_create_audio_query(IntPtr synth, byte[] text, uint style, out IntPtr json);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] static extern int voicevox_synthesizer_synthesis(IntPtr synth, byte[] audioQueryJson, uint style, TtsOptions options, out UIntPtr length, out IntPtr wav);
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
        static readonly HashSet<string> loadedModels = new HashSet<string>();   // a voice file is loaded when a voice from it is first used (each takes seconds and memory)

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
                synth = s;
                return true;
            }
            catch (Exception ex) { App.Log(ex); return false; }
        }

        static bool EnsureModel(string file)
        {
            if (loadedModels.Contains(file)) return true;
            try
            {
                IntPtr model;
                string path = Path.Combine(ModelDir, file);
                Ok(voicevox_voice_model_file_open(Utf8(path), out model), "model " + file);
                Ok(voicevox_synthesizer_load_voice_model(synth, model, new LoadModelOptions { on_existing = 2 }), "load " + file);
                voicevox_voice_model_file_delete(model);
                loadedModels.Add(file);
                return true;
            }
            catch (Exception ex) { App.Log(ex); return false; }
        }

        /// <summary>Start loading the chosen voice in the background, so that the first sentence does not wait for it.</summary>
        public static void Warm(string key, bool legacyMale)
        {
            if (!Available) return;
            var v = Find(key, legacyMale);
            if (v == null) return;
            Task.Run(() => { lock (Gate) { if (Init()) EnsureModel(v.Model); } });
        }

        // ---- text -> WAV ---------------------------------------------------------------------------------------------------
        static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>();

        static string Clean(string text)
        {
            if (text == null) return "";
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>The speed of the speech for the three settings: 0 fast (the voice as it is), 1 medium, 2 slow.</summary>
        public static double SpeedFor(int level) { return level <= 0 ? 1.0 : level == 1 ? 0.85 : 0.7; }

        /// <summary>The spoken text as a WAV file (24 kHz, 16 bit, mono); null when it cannot be spoken. Slow: do not call on the window thread.
        /// <paramref name="speed"/> 1.0 is the normal speed of the voice, smaller is slower.</summary>
        public static byte[] Synthesize(string text, VoiceInfo voice, double speed = 1.0)
        {
            text = Clean(text);
            if (text.Length == 0) return null;
            uint style = voice.Style;
            string key = voice.Key + "|" + speed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + text;
            byte[] wav;
            lock (Cache) { if (Cache.TryGetValue(key, out wav)) return wav; }
            try
            {
                lock (Gate)
                {
                    if (!Init() || !EnsureModel(voice.Model)) return null;
                    UIntPtr len; IntPtr ptr;
                    if (Math.Abs(speed - 1.0) < 0.001)
                        Ok(voicevox_synthesizer_tts(synth, Utf8(text), style, new TtsOptions { enable_interrogative_upspeak = true }, out len, out ptr), "tts");
                    else
                    {
                        // the speed is a field of the "audio query" (the plan of the speech): make the plan, change the speed, speak the plan
                        IntPtr qp;
                        Ok(voicevox_synthesizer_create_audio_query(synth, Utf8(text), style, out qp), "audio query");
                        string query = FromUtf8(qp);
                        voicevox_json_free(qp);
                        string slow = Regex.Replace(query, "\"speedScale\"\\s*:\\s*[-0-9.eE+]+", "\"speedScale\":" + speed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                        Ok(voicevox_synthesizer_synthesis(synth, Utf8(slow), style, new TtsOptions { enable_interrogative_upspeak = true }, out len, out ptr), "synthesis");
                    }
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
        public static void Speak(string text, string voiceKey, bool legacyMale, int speedLevel, Action<bool> state)
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
                    var voice = Find(voiceKey, legacyMale);
                    if (voice == null) return;
                    var wav = Synthesize(text, voice, SpeedFor(speedLevel));
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
