using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Tetris118.Data;

namespace Tetris118.Audio
{
    // Một luồng trộn nhạc + SFX chung. SoundPlayer.Play sẽ ngắt âm cũ,
    // vì vậy không phù hợp khi vừa nghe nhạc vừa xoay/thả khối.
    public sealed class AudioEngine : IDisposable
    {
        private const int Rate = 44100, Samples = 1024;
        private readonly object gate = new object();
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<string, float[]> effects = new Dictionary<string, float[]>();
        private readonly List<Buffer> buffers = new List<Buffer>();
        private Thread worker;
        private IntPtr device;
        private volatile bool running;
        public volatile bool Muted, MusicPaused, Cinematic;
        public volatile float MusicVolume = .45f, EffectsVolume = .7f;
        public string Error { get; private set; }
        private float[] music, typeBMusic, activeMusic, requestedMusic;
        private bool restartRequested;
        private readonly float[][] bossTracks = new float[12][];
        private int musicPosition;
        private float musicGain, musicDuck = 1;
        private int activeBoss = -1;
        private bool gameplay;
        // 3 buffer x 1024 mẫu: giữ mức đã chạy ổn trên máy nhóm.
        private long lastMove, lastSoft;
        private sealed class Voice { public float[] Data; public int Position; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public ushort Tag, Channels;
            public uint Rate, Average;
            public ushort Align, Bits, Extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public uint Length, Recorded;
            public IntPtr User;
            public uint Flags, Loops;
            public IntPtr Next, Reserved;
        }
        private sealed class Buffer
        {
            public readonly short[] Data = new short[Samples];
            public GCHandle Pinned;
            public IntPtr Header;
            public bool Prepared;
        }
        [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr h, uint id, ref WaveFormat f, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr h, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr h, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr h, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr h);
        private static uint HeaderSize { get { return (uint)Marshal.SizeOf(typeof(WaveHeader)); } }
        private static void Check(uint result) { if (result != 0) throw new IOException("waveOut: " + result); }
        // Muốn đổi mốc level thì chỉ sửa số này.
        private const int FinalBattleStartLevel = 9;

        private int currentLevel = 1;
        private float[] finalBattleMusic;
        public AudioEngine(string assetFolder, Settings settings)
        {
            
            Muted = settings.Muted; MusicVolume = settings.Music / 100f; EffectsVolume = settings.Effects / 100f;
            try
            {

                music = ReadWave(Path.Combine(assetFolder, "korobeiniki.wav"));
                typeBMusic = BuildTypeB();
                finalBattleMusic = ReadWave(
    Path.Combine(assetFolder, "finalbattle.wav"));
                activeMusic = requestedMusic = music;
                for (int i = 0; i < bossTracks.Length; i++)
                    bossTracks[i] = ReadWave(Path.Combine(assetFolder, "boss_" + i.ToString("D2") + ".wav"));
                foreach (string name in new[] { "move", "rotate", "soft", "hard", "lock", "clear", "tetris", "spin", "hold", "level", "over", "win", "menu", "single", "double", "triple", "combo", "b2b", "perfect", "quest", "warning", "bosshit", "bossintro", "bossvictory", "rushwin" })
                    effects[name] = ReadWave(Path.Combine(assetFolder, name + ".wav"));
                effects["confirm"] = BuildConfirm();
                var format = new WaveFormat { Tag = 1, Channels = 1, Rate = Rate, Average = Rate * 2, Align = 2, Bits = 16, Extra = 0 };
                Check(waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                for (int i = 0; i < 3; i++)
                {
                    var buffer = new Buffer(); buffers.Add(buffer);
                    buffer.Pinned = GCHandle.Alloc(buffer.Data, GCHandleType.Pinned);
                    buffer.Header = Marshal.AllocHGlobal((int)HeaderSize);
                    var header = new WaveHeader { Data = buffer.Pinned.AddrOfPinnedObject(), Length = Samples * 2 };
                    Marshal.StructureToPtr(header, buffer.Header, false);
                    Check(waveOutPrepareHeader(device, buffer.Header, HeaderSize)); buffer.Prepared = true;
                    Fill(buffer); Check(waveOutWrite(device, buffer.Header, HeaderSize));
                }
                running = true; worker = new Thread(Pump) { IsBackground = true, Name = "Tetris118.Audio" }; worker.Start();
            }
            catch (Exception ex) { Error = "Audio unavailable: " + ex.Message; SaveStore.Log(ex); Dispose(); }
        }
        private static float[] ReadWave(string path)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(r.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a WAV file.");
                r.ReadInt32(); if (new string(r.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAVE file.");
                bool valid = false;
                while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
                {
                    string id = new string(r.ReadChars(4)); int size = r.ReadInt32();
                    if (size < 0 || size > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("Truncated WAV data.");
                    long next = r.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        if (size < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                        int tag = r.ReadUInt16(), channels = r.ReadUInt16(), rate = r.ReadInt32();
                        r.ReadInt32(); r.ReadUInt16(); int bits = r.ReadUInt16();
                        valid = tag == 1 && channels == 1 && rate == Rate && bits == 16;
                    }
                    else if (id == "data")
                    {
                        if (!valid || size < 2 || size > 40 * 1024 * 1024) throw new InvalidDataException("Expected mono 44100 Hz / 16-bit PCM.");
                        var data = new float[size / 2];
                        for (int i = 0; i < data.Length; i++) data[i] = r.ReadInt16() / 32768f;
                        return data;
                    }
                    r.BaseStream.Position = next;
                }
                throw new InvalidDataException("WAV data chunk missing.");
            }
        }
        public void Play(string name)
        {
            if (!running || Muted) return;
            long now = Environment.TickCount & int.MaxValue;
            if (name == "move") { if (now - lastMove < 45 && now >= lastMove) return; lastMove = now; }
            if (name == "soft") { if (now - lastSoft < 65 && now >= lastSoft) return; lastSoft = now; }
            float[] data; if (!effects.TryGetValue(name, out data)) return;
            lock (gate)
            {
                if (voices.Count >= 24) voices.RemoveAt(0);
                voices.Add(new Voice { Data = data });
            }
        }
        private void Fill(Buffer buffer)
        {
            lock (gate)
            {
                for (int i = 0; i < Samples; i++)
                {
                    float value = 0;
                    if (!MusicPaused)
                    {
                        // Chỉ đọc đúng một nguồn nhạc. Không cộng menu/Type B/boss vào nhau.
                        if (!object.ReferenceEquals(activeMusic, requestedMusic) || restartRequested)
                        {
                            musicGain = Math.Max(0f, musicGain - 1f / (Rate * .08f));
                            if (musicGain == 0)
                            { activeMusic = requestedMusic; musicPosition = 0; restartRequested = false; }
                        }
                        else musicGain = Math.Min(1f, musicGain + 1f / (Rate * .12f));
                        float duckTarget = Cinematic ? .24f : 1f;
                        musicDuck += (duckTarget - musicDuck) * .00012f;
                        if (activeMusic != null && activeMusic.Length > 0)
                        {
                            value = activeMusic[musicPosition] * MusicVolume * musicGain * musicDuck;
                            musicPosition = (musicPosition + 1) % activeMusic.Length;
                        }
                    }
                    for (int v = voices.Count - 1; v >= 0; v--)
                    {
                        Voice voice = voices[v]; value += voice.Data[voice.Position++] * EffectsVolume;
                        if (voice.Position == voice.Data.Length) voices.RemoveAt(v);
                    }
                    // Soft limiter: nhiều tiếng cùng lúc vẫn không tràn số nguyên 16-bit.
                    buffer.Data[i] = Muted ? (short)0 : (short)(Math.Tanh(value) * 30000);
                }
            }
        }
        private void SelectMusic(bool restart)
        {
            float[] next;

            if (!gameplay)
            {
                next = music;
            }
            else if (activeBoss >= 0)
            {
                // Nhạc boss luôn ưu tiên hơn nhạc theo level.
                next = bossTracks[activeBoss];
            }
            else if (currentLevel >= FinalBattleStartLevel)
            {
                next = finalBattleMusic;
            }
            else
            {
                next = typeBMusic;
            }

            // Fill() đang xử lý tắt dần bài cũ rồi mới bật bài mới.
            // Không tạo thêm bộ phát nhạc.
            requestedMusic = next;

            if (restart)
                restartRequested = true;
        }
        public void SetBossMusic(int id)
        {
            lock (gate)
            {
                id = id >= 0 && id < bossTracks.Length ? id : -1;
                if (id == activeBoss) return;
                activeBoss = id; SelectMusic(false);
            }
        }
        public void SetLevelMusic(int level)
        {
            lock (gate)
            {
                level = Math.Max(1, level);

                // Không khởi động lại bài nhạc mỗi frame.
                if (currentLevel == level)
                    return;

                currentLevel = level;
                SelectMusic(false);
            }
        }
        public void SetGameplayMusic(bool enabled, bool restart)
        { lock (gate) { gameplay = enabled; SelectMusic(restart); } }
        // Tổng hợp một lần khi mở game, không tính sin/cấp phát trong luồng trộn.
        // Nhạc menu tự soạn: chuông và nền dây chậm, 16 giây, quay vòng liên tục.
        private static float[] BuildMenuMusic()
        {
            var data = new float[Rate * 16];
            int[] roots = { 45, 41, 48, 43 };
            for (int bar = 0; bar < 4; bar++)
            {
                foreach (int interval in new[] { 0, 7, 12 })
                    AddTone(data, bar * 4, 5.5, roots[bar] + interval, .075, 2);
                AddTone(data, bar * 4 + .4, 2.5, roots[bar] + 24, .12, 2);
                AddTone(data, bar * 4 + 2.4, 2, roots[bar] + 31, .08, 2);
            }
            return data;
        }
        private static float[] BuildConfirm()
        {
            var data = new float[Rate / 4];
            AddTone(data, 0, .10, 76, .20, 0);
            AddTone(data, .085, .15, 83, .18, 0);
            return data;
        }
        private static void AddTone(float[] data, double start, double duration, int note, double volume, int timbre)
        {
            int first = (int)(start * Rate), count = (int)(duration * Rate);
            double frequency = 440 * Math.Pow(2, (note - 69) / 12.0);
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)Rate, phase = 2 * Math.PI * frequency * t;
                double attack = timbre == 2 ? .15 : .004, release = timbre == 2 ? .7 : .012;
                double envelope = Math.Min(1, t / attack) * Math.Min(1, (count - 1 - i) / (Rate * release));
                double wave = Math.Sin(phase);
                if (timbre == 0) wave = (wave + .22 * Math.Sin(phase * 3) + .08 * Math.Sin(phase * 5)) / 1.3;
                if (timbre == 1) wave = (wave - Math.Sin(phase * 3) / 9) * .9;
                if (timbre == 2) wave = (wave + .2 * Math.Sin(phase * 2)) * Math.Exp(-t * .5);
                data[(first + i) % data.Length] += (float)(wave * envelope * volume);
            }
        }
        private static float[] BuildTypeB()
        {
            // Tetris Game Boy (1989), B-Type: Hirokazu Tanaka, 150 BPM.
            // Đối chiếu bản chép Mikhail Hogrefe: accurate-video-game-sheet-music,
            // github.com/mhogrefe/accurate-video-game-sheet-music/tree/main/gb/tetris/03-b-type-music
            // Một vòng 22 ô nhịp. Mỗi bộ: vị trí, độ dài (1/16 nốt), cao độ MIDI, lực đánh.
            // Âm sắc tự tổng hợp mềm hơn máy gốc; không phải bản ghi âm từ ROM.
            var data = new float[Rate * 352 / 10];
            RenderPart(data, LeadNotes, 0, .15);
            RenderPart(data, BassNotes, 1, .22);
            var random = new Random(118);
            for (int beat = 0; beat < 44; beat++)
                foreach (double offset in new[] { 0.0, .2, .3, .4, .6 })
                {
                    int first = (int)((beat * .8 + offset) * Rate);
                    for (int i = 0; i < Rate / 40 && first + i < data.Length; i++)
                        data[first + i] += (float)((random.NextDouble() * 2 - 1) * .022 * Math.Exp(-i / 180.0) * Math.Min(1, i / 32.0));
                }
            return data;
        }
        private static void RenderPart(float[] data, int[] notes, int timbre, double gain)
        {
            for (int i = 0; i < notes.Length; i += 4)
                AddTone(data, notes[i] * .1, notes[i + 1] * .1 * .94, notes[i + 2], gain * notes[i + 3] / 100.0, timbre);
        }
        private static readonly int[] LeadNotes = {
            0, 2, 64, 86,  2, 1, 64, 86,  3, 1, 64, 86,  4, 2, 59, 86,  6, 2, 59, 86,
            8, 2, 64, 86,  10, 1, 64, 86,  11, 1, 64, 86,  12, 2, 59, 86,  14, 2, 59, 86,
            16, 2, 64, 69,  18, 1, 64, 69,  19, 1, 64, 69,  20, 2, 59, 69,  22, 2, 59, 69,
            24, 2, 64, 69,  26, 1, 64, 69,  27, 1, 64, 69,  28, 2, 59, 69,  30, 2, 59, 69,
            32, 2, 59, 95,  32, 2, 64, 95,  34, 2, 67, 95,  34, 2, 71, 95,  36, 2, 67, 95,
            36, 2, 76, 95,  38, 2, 69, 95,  38, 2, 75, 95,  40, 2, 67, 95,  40, 2, 76, 95,
            42, 1, 66, 95,  42, 1, 71, 95,  43, 1, 67, 95,  43, 1, 71, 95,  44, 2, 69, 95,
            44, 2, 72, 95,  46, 2, 66, 95,  46, 2, 69, 95,  48, 2, 67, 95,  48, 2, 71, 95,
            50, 1, 63, 95,  50, 1, 67, 95,  51, 1, 64, 95,  51, 1, 67, 95,  52, 2, 66, 95,
            52, 2, 69, 95,  54, 2, 63, 95,  54, 2, 66, 95,  56, 2, 64, 95,  56, 2, 67, 95,
            58, 1, 58, 95,  58, 1, 64, 95,  59, 1, 59, 95,  59, 1, 64, 95,  60, 2, 63, 95,
            60, 2, 66, 95,  62, 2, 59, 95,  62, 2, 63, 95,  64, 2, 59, 95,  64, 2, 64, 95,
            66, 2, 55, 95,  66, 2, 59, 95,  68, 2, 57, 95,  68, 2, 60, 95,  70, 2, 57, 95,
            70, 2, 63, 95,  72, 2, 59, 95,  72, 2, 64, 95,  74, 2, 55, 95,  74, 2, 59, 95,
            76, 2, 57, 95,  76, 2, 60, 95,  78, 2, 63, 95,  78, 2, 66, 95,  80, 2, 59, 95,
            80, 2, 64, 95,  82, 2, 67, 95,  82, 2, 71, 95,  84, 2, 67, 95,  84, 2, 76, 95,
            86, 2, 69, 95,  86, 2, 75, 95,  88, 2, 67, 95,  88, 2, 76, 95,  90, 1, 66, 95,
            90, 1, 71, 95,  91, 1, 67, 95,  91, 1, 71, 95,  92, 2, 69, 95,  92, 2, 72, 95,
            94, 2, 66, 95,  94, 2, 69, 95,  96, 2, 67, 95,  96, 2, 71, 95,  98, 1, 63, 95,
            98, 1, 67, 95,  99, 1, 64, 95,  99, 1, 67, 95,  100, 2, 66, 95,  100, 2, 69, 95,
            102, 2, 63, 95,  102, 2, 66, 95,  104, 2, 64, 95,  104, 2, 67, 95,  106, 1, 58, 95,
            106, 1, 64, 95,  107, 1, 59, 95,  107, 1, 64, 95,  108, 2, 63, 95,  108, 2, 66, 95,
            110, 2, 59, 95,  110, 2, 63, 95,  112, 2, 59, 95,  112, 2, 64, 95,  114, 2, 55, 95,
            114, 2, 59, 95,  116, 2, 57, 95,  116, 2, 60, 95,  118, 2, 57, 95,  118, 2, 63, 95,
            120, 2, 59, 95,  120, 2, 64, 95,  122, 2, 55, 95,  122, 2, 59, 95,  124, 2, 57, 95,
            124, 2, 60, 95,  126, 2, 63, 95,  126, 2, 66, 95,  128, 2, 59, 95,  128, 2, 64, 95,
            140, 2, 59, 95,  140, 2, 66, 95,  142, 2, 59, 95,  142, 2, 63, 95,  144, 2, 59, 95,
            144, 2, 64, 95,  156, 2, 62, 95,  156, 2, 66, 95,  160, 2, 67, 95,  162, 1, 67, 95,
            163, 1, 67, 95,  164, 2, 69, 95,  166, 2, 66, 95,  168, 2, 67, 95,  170, 1, 67, 95,
            171, 1, 67, 95,  172, 2, 69, 95,  174, 2, 66, 95,  176, 2, 67, 95,  188, 2, 66, 95,
            188, 2, 69, 95,  192, 2, 67, 95,  192, 2, 71, 95,  194, 1, 67, 95,  194, 1, 71, 95,
            195, 1, 67, 95,  195, 1, 71, 95,  196, 2, 69, 95,  196, 2, 72, 95,  198, 2, 66, 95,
            198, 2, 69, 95,  200, 2, 67, 95,  200, 2, 71, 95,  202, 1, 67, 95,  202, 1, 71, 95,
            203, 1, 67, 95,  203, 1, 71, 95,  204, 2, 69, 95,  204, 2, 72, 95,  206, 2, 66, 95,
            206, 2, 69, 95,  208, 2, 62, 95,  208, 2, 71, 95,  220, 2, 64, 95,  220, 2, 73, 95,
            224, 2, 66, 95,  224, 2, 74, 95,  226, 1, 67, 95,  226, 1, 74, 95,  227, 1, 69, 95,
            227, 1, 74, 95,  228, 2, 66, 95,  228, 2, 74, 95,  230, 2, 69, 95,  230, 2, 74, 95,
            232, 2, 71, 95,  232, 2, 76, 95,  234, 2, 71, 95,  234, 2, 74, 95,  236, 2, 71, 95,
            236, 2, 74, 95,  238, 2, 64, 95,  238, 2, 73, 95,  240, 2, 66, 95,  240, 2, 74, 95,
            242, 1, 67, 95,  242, 1, 74, 95,  243, 1, 69, 95,  243, 1, 74, 95,  244, 2, 66, 95,
            244, 2, 74, 95,  246, 2, 69, 95,  246, 2, 74, 95,  248, 2, 70, 95,  248, 2, 76, 95,
            250, 2, 70, 95,  250, 2, 74, 95,  252, 2, 70, 95,  252, 2, 74, 95,  254, 2, 64, 95,
            254, 2, 73, 95,  256, 2, 66, 95,  256, 2, 74, 95,  258, 1, 67, 95,  258, 1, 74, 95,
            259, 1, 69, 95,  259, 1, 74, 95,  260, 2, 66, 95,  260, 2, 74, 95,  262, 2, 69, 95,
            262, 2, 74, 95,  264, 2, 64, 95,  264, 2, 73, 95,  266, 1, 66, 95,  266, 1, 73, 95,
            267, 1, 67, 95,  267, 1, 73, 95,  268, 2, 64, 95,  268, 2, 73, 95,  270, 2, 67, 95,
            270, 2, 73, 95,  272, 2, 64, 95,  272, 2, 72, 95,  274, 1, 66, 95,  274, 1, 72, 95,
            275, 1, 67, 95,  275, 1, 72, 95,  276, 2, 66, 95,  276, 2, 72, 95,  278, 2, 66, 95,
            278, 2, 69, 95,  280, 2, 57, 95,  280, 2, 66, 95,  282, 2, 64, 95,  282, 2, 67, 95,
            284, 2, 66, 95,  284, 2, 69, 95,  286, 2, 54, 95,  286, 2, 62, 95,  288, 2, 59, 95,
            288, 2, 69, 95,  290, 1, 59, 95,  290, 1, 67, 95,  291, 1, 59, 95,  291, 1, 67, 95,
            292, 2, 59, 95,  292, 2, 62, 95,  294, 2, 59, 95,  294, 2, 67, 95,  298, 1, 59, 95,
            298, 1, 62, 95,  299, 1, 61, 95,  299, 1, 64, 95,  300, 2, 59, 95,  300, 2, 62, 95,
            302, 2, 55, 95,  302, 2, 59, 95,  304, 2, 58, 95,  304, 2, 69, 95,  306, 1, 58, 95,
            306, 1, 67, 95,  307, 1, 58, 95,  307, 1, 67, 95,  308, 2, 58, 95,  308, 2, 62, 95,
            310, 2, 58, 95,  310, 2, 67, 95,  314, 1, 58, 95,  314, 1, 62, 95,  315, 1, 61, 95,
            315, 1, 64, 95,  316, 2, 58, 95,  316, 2, 62, 95,  318, 2, 55, 95,  318, 2, 58, 95,
            320, 2, 54, 95,  320, 2, 62, 95,  348, 2, 57, 95,  348, 2, 63, 95,
        };
        private static readonly int[] BassNotes = {
            0, 2, 52, 90,  2, 1, 52, 90,  3, 1, 52, 90,  4, 2, 47, 90,  6, 2, 47, 90,
            8, 2, 52, 90,  10, 1, 52, 90,  11, 1, 52, 90,  12, 2, 47, 90,  14, 2, 47, 90,
            16, 2, 52, 90,  18, 1, 52, 90,  19, 1, 52, 90,  20, 2, 47, 90,  22, 2, 47, 90,
            24, 2, 52, 90,  26, 1, 52, 90,  27, 1, 52, 90,  28, 2, 47, 90,  30, 2, 47, 90,
            32, 2, 52, 90,  34, 1, 52, 90,  35, 1, 52, 90,  36, 2, 47, 90,  38, 2, 47, 90,
            40, 2, 52, 90,  42, 1, 52, 90,  43, 1, 52, 90,  44, 2, 47, 90,  46, 2, 47, 90,
            48, 2, 52, 90,  50, 1, 52, 90,  51, 1, 52, 90,  52, 2, 47, 90,  54, 2, 47, 90,
            56, 2, 52, 90,  58, 1, 52, 90,  59, 1, 52, 90,  60, 2, 47, 90,  62, 2, 47, 90,
            64, 2, 52, 90,  66, 1, 52, 90,  67, 1, 52, 90,  68, 2, 47, 90,  70, 2, 47, 90,
            72, 2, 52, 90,  74, 1, 52, 90,  75, 1, 52, 90,  76, 2, 47, 90,  78, 2, 47, 90,
            80, 2, 52, 90,  82, 1, 52, 90,  83, 1, 52, 90,  84, 2, 47, 90,  86, 2, 47, 90,
            88, 2, 52, 90,  90, 1, 52, 90,  91, 1, 52, 90,  92, 2, 47, 90,  94, 2, 47, 90,
            96, 2, 52, 90,  98, 1, 52, 90,  99, 1, 52, 90,  100, 2, 47, 90,  102, 2, 47, 90,
            104, 2, 52, 90,  106, 1, 52, 90,  107, 1, 52, 90,  108, 2, 47, 90,  110, 2, 47, 90,
            112, 2, 52, 90,  114, 1, 52, 90,  115, 1, 52, 90,  116, 2, 47, 90,  118, 2, 47, 90,
            120, 2, 52, 90,  122, 1, 52, 90,  123, 1, 52, 90,  124, 2, 47, 90,  126, 2, 47, 90,
            128, 2, 52, 90,  130, 1, 52, 90,  131, 1, 52, 90,  132, 2, 48, 90,  134, 2, 45, 90,
            136, 2, 52, 90,  138, 1, 52, 90,  139, 1, 52, 90,  140, 2, 51, 90,  142, 2, 47, 90,
            144, 2, 52, 90,  146, 1, 52, 90,  147, 1, 52, 90,  148, 2, 48, 90,  150, 2, 45, 90,
            152, 2, 52, 90,  154, 1, 52, 90,  155, 1, 52, 90,  156, 2, 45, 90,  158, 2, 38, 90,
            160, 2, 43, 90,  162, 1, 55, 90,  163, 1, 43, 90,  164, 2, 38, 90,  166, 2, 50, 90,
            168, 2, 43, 90,  170, 1, 55, 90,  171, 1, 43, 90,  172, 2, 38, 90,  174, 2, 50, 90,
            176, 2, 43, 90,  178, 1, 55, 90,  179, 1, 43, 90,  180, 2, 38, 90,  182, 2, 50, 90,
            184, 2, 43, 90,  186, 1, 55, 90,  187, 1, 43, 90,  188, 2, 38, 90,  190, 2, 50, 90,
            192, 2, 43, 90,  194, 1, 55, 90,  195, 1, 43, 90,  196, 2, 38, 90,  198, 2, 50, 90,
            200, 2, 43, 90,  202, 1, 55, 90,  203, 1, 43, 90,  204, 2, 38, 90,  206, 2, 50, 90,
            208, 2, 43, 90,  210, 1, 55, 90,  211, 1, 43, 90,  212, 2, 38, 90,  214, 2, 50, 90,
            216, 2, 43, 90,  218, 1, 55, 90,  219, 1, 43, 90,  220, 2, 45, 90,  222, 2, 57, 90,
            224, 2, 38, 90,  226, 1, 50, 90,  227, 1, 38, 90,  228, 2, 38, 90,  230, 2, 50, 90,
            232, 2, 43, 90,  234, 1, 55, 90,  235, 1, 43, 90,  236, 2, 43, 90,  238, 2, 55, 90,
            240, 2, 38, 90,  242, 1, 50, 90,  243, 1, 38, 90,  244, 2, 38, 90,  246, 2, 50, 90,
            248, 2, 43, 90,  250, 1, 55, 90,  251, 1, 43, 90,  252, 2, 43, 90,  254, 2, 55, 90,
            256, 2, 38, 90,  258, 1, 50, 90,  259, 1, 38, 90,  260, 2, 38, 90,  262, 2, 50, 90,
            264, 2, 38, 90,  266, 1, 50, 90,  267, 1, 38, 90,  268, 2, 38, 90,  270, 2, 50, 90,
            272, 2, 38, 90,  274, 1, 50, 90,  275, 1, 38, 90,  276, 2, 38, 90,  278, 2, 50, 90,
            280, 2, 38, 90,  282, 1, 50, 90,  283, 1, 38, 90,  284, 2, 38, 90,  286, 2, 50, 90,
            288, 2, 40, 90,  290, 1, 52, 90,  291, 1, 40, 90,  292, 2, 40, 90,  294, 2, 52, 90,
            296, 2, 40, 90,  298, 1, 52, 90,  299, 1, 40, 90,  300, 2, 40, 90,  302, 2, 52, 90,
            304, 2, 40, 90,  306, 1, 52, 90,  307, 1, 40, 90,  308, 2, 40, 90,  310, 2, 52, 90,
            312, 2, 40, 90,  314, 1, 52, 90,  315, 1, 40, 90,  316, 2, 40, 90,  318, 2, 52, 90,
            320, 2, 38, 90,  322, 1, 50, 90,  323, 1, 38, 90,  324, 2, 38, 90,  326, 2, 50, 90,
            328, 2, 38, 90,  330, 1, 50, 90,  331, 1, 38, 90,  332, 2, 38, 90,  334, 2, 50, 90,
            336, 2, 38, 90,  338, 1, 50, 90,  339, 1, 38, 90,  340, 2, 38, 90,  342, 2, 50, 90,
            344, 2, 38, 90,  346, 1, 50, 90,  347, 1, 38, 90,  348, 2, 26, 90,  350, 2, 54, 90,
        };
        private void Pump()
        {
            try
            {
                while (running)
                {
                    foreach (Buffer buffer in buffers)
                    {
                        var header = (WaveHeader)Marshal.PtrToStructure(buffer.Header, typeof(WaveHeader));
                        if ((header.Flags & 1) != 0) { Fill(buffer); Check(waveOutWrite(device, buffer.Header, HeaderSize)); }
                    }
                    Thread.Sleep(2);
                }
            }
            catch (Exception ex) { Error = "Audio device disconnected. Restart the game to retry."; SaveStore.Log(ex); running = false; }
        }
        public void Dispose()
        {
            running = false;
            if (worker != null && worker.IsAlive) worker.Join();
            if (device != IntPtr.Zero) waveOutReset(device);
            foreach (Buffer buffer in buffers)
            {
                if (device != IntPtr.Zero && buffer.Prepared) waveOutUnprepareHeader(device, buffer.Header, HeaderSize);
                if (buffer.Header != IntPtr.Zero) Marshal.FreeHGlobal(buffer.Header);
                if (buffer.Pinned.IsAllocated) buffer.Pinned.Free();
            }
            buffers.Clear();
            if (device != IntPtr.Zero) { waveOutClose(device); device = IntPtr.Zero; }
        }
    }
}
