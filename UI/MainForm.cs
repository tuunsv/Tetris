using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Tetris118.Audio;
using Tetris118.Core;
using Tetris118.Data;

namespace Tetris118.UI
{
    public sealed partial class MainForm : Form
    {
        private const int CanvasW = 384, CanvasH = 288;
        private bool BossLayout { get { return game != null && game.Rush != null; } }
        private int CellSize { get { return BossLayout ? 8 : 10; } }
        private int BoardX { get { return BossLayout ? 152 : 142; } }
        private int BoardY { get { return BossLayout ? 108 : 42; } }
        private readonly BossPortraits portraits = new BossPortraits();
        private int galleryBoss, playingBoss = -2;
        private readonly SaveStore store = new SaveStore();
        private readonly Profile profile;
        private readonly AudioEngine audio;
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly HashSet<Keys> keys = new HashSet<Keys>();
        private readonly HashSet<Keys> downKeys = new HashSet<Keys>();
        private readonly List<Hit> hits = new List<Hit>();
        private readonly Random random = new Random();
        private GameEngine game;
        private string page = "home", toast = "";
        private GameMode selectedMode = GameMode.Marathon, recordMode = GameMode.Marathon;
        private double previousTime, accumulator, visualTime, toastTime, heldTime, repeatTime;
        private int horizontal, focused, hovered = -1;
        private bool recorded;
        private string entryName = "";
        private bool enteringName;
        private readonly Bitmap screen = new Bitmap(CanvasW, CanvasH);
        private PointF mouse = new PointF(-1, -1);
        private sealed class Hit { public RectangleF Rect; public Action Action; }
        private Settings Settings { get { return profile.Settings; } }
        public MainForm()
        {
            Text = "Tetris 118";
            ClientSize = new Size(960, 720); MinimumSize = new Size(656, 520);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.Black;
            AutoScaleMode = AutoScaleMode.None; KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            profile = store.Load();
            audio = new AudioEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets"), Settings); ApplyAudio();
            if (store.Warning != null) Notify(store.Warning);
            if (audio.Error != null) Notify(audio.Error);
            timer.Interval = 16; timer.Tick += Frame; timer.Start();
            Deactivate += delegate { if (page == "play" && game != null && !game.Finished) Pause(); keys.Clear(); downKeys.Clear(); horizontal = 0; };
            FormClosing += ClosingGame;
        }
        private void ApplyAudio()
        { audio.Muted = Settings.Muted; audio.MusicVolume = Settings.Music / 100f; audio.EffectsVolume = Settings.Effects / 100f; }
        private void Notify(string text) { toast = text; toastTime = 7; }
        private void Save() { if (!store.Save(profile)) Notify(store.Warning); }
        private void Go(string target) { page = target; focused = 0; hovered = -1; audio.SetGameplayMusic(target == "play" && game != null && !game.Finished, false); keys.Clear(); horizontal = 0; audio.MusicPaused = false; Invalidate(); }
        private void StartGame()
        {
            game = new GameEngine(selectedMode, Settings.MatchSeed > 0 ? Settings.MatchSeed : random.Next(1, int.MaxValue)); game.Sound += audio.Play;
            recorded = false; enteringName = false; entryName = ""; accumulator = heldTime = repeatTime = 0; playingBoss = game.Rush == null ? -1 : (int)game.Rush.Boss.Id;
            audio.SetLevelMusic(game.Level);
            audio.SetBossMusic(playingBoss);
            Go("play");
            audio.SetGameplayMusic(true, true);
            if (game.Rush != null) audio.Play("bossintro");
        }
        private void FinishRecord()
        {
            // Kết thúc ván mới hỏi tên; chưa ghi bảng điểm khi đang nhập.
            if (game == null || recorded || enteringName) return;
            enteringName = true; entryName = ""; audio.SetGameplayMusic(false, false);
            keys.Clear(); horizontal = 0; focused = 0;
        }
        private void CommitResult()
        {
            if (recorded || game == null || !game.Finished) return;
            Settings.Player = string.IsNullOrWhiteSpace(entryName) ? "ANON" : entryName.Trim();
            profile.Add(game); recorded = true; enteringName = false; Save();
            keys.Clear(); horizontal = 0; focused = 0;
        }
        private void CountAbandoned()
        {
            // Bỏ dở chỉ cộng thống kê, không đẩy một tên cũ vào bảng thành tích.
            if (game == null || recorded) return;
            if (game.Rush != null && game.Rush.Defeated == 3)
            { game.FinishBossRush(true); CommitResult(); return; }
            if (game.Rush != null) { profile.BossRuns++; profile.BossKills += game.Rush.Defeated; }
            profile.Games++; profile.Lines += game.Lines; profile.Pieces += game.PiecesPlaced;
            profile.Tetrises += game.Tetrises; profile.TSpins += game.TSpins;
            profile.PerfectClears += game.PerfectClears; profile.PlaySeconds += game.Elapsed;
            profile.TotalScore += game.Score; profile.BestScore = Math.Max(profile.BestScore, game.Score);
            profile.BestCombo = Math.Max(profile.BestCombo, game.MaxCombo);
            recorded = true; Save();
        }
        private void Pause()
        { if (game == null || game.Finished) return; game.Paused = true; keys.Clear(); horizontal = 0; focused = 0; audio.MusicPaused = true; Invalidate(); }
        private void Resume()
        { game.Paused = false; keys.Clear(); horizontal = 0; audio.MusicPaused = false; focused = 0; }
        private void RestartGame()
        {
            if (game == null) return;
            selectedMode = game.Mode; CountAbandoned(); StartGame();
        }
        private void Abandon()
        {
            if (MessageBox.Show(this, "End this run and return to the menu? Your statistics will be saved.",
                "Tetris 118", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            CountAbandoned(); Go("home");
        }
        private void ClosingGame(object sender, FormClosingEventArgs e)
        {
            if (game != null && page == "play" && !game.Finished)
            {
                Pause();
                if (MessageBox.Show(this, "Exit and save the current run statistics?", "Tetris 118", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
                CountAbandoned();
            }
            if (game != null && game.Finished && !recorded) CommitResult();
            if (!store.Save(profile) && MessageBox.Show(this, store.Warning + "\nExit anyway?", "Tetris 118",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
            timer.Stop(); audio.Dispose(); timer.Dispose(); screen.Dispose(); portraits.Dispose(); if (menuBackdrop != null) menuBackdrop.Dispose();
        }
        private void Frame(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds, rawDelta = now - previousTime; previousTime = now;
            if (rawDelta > .25 && game != null && page == "play" && !game.Paused && !game.Finished)
            { Pause(); Notify("Paused after a frame delay over 250 ms. Press P to resume."); }
            double dt = Math.Min(.25, rawDelta);
            visualTime += dt; toastTime = Math.Max(0, toastTime - dt);
            if (game != null && page == "play" && !game.Paused && !game.Finished)
            {
                // Timer chỉ đánh thức UI. Logic bước cố định 120Hz để không lệ thuộc tốc độ vẽ.
                accumulator += dt;
                while (accumulator >= 1.0 / 120)
                {
                    const double step = 1.0 / 120;
                    if (horizontal != 0)
                    {
                        heldTime += step;
                        if (heldTime >= Settings.Das / 1000.0)
                        {
                            repeatTime += step;
                            while (repeatTime >= Settings.Arr / 1000.0)
                            { repeatTime -= Settings.Arr / 1000.0; game.Move(horizontal); }
                        }
                    }
                    game.Tick(step, keys.Contains(Keys.Down));
                    audio.SetLevelMusic(game.Level);
                    accumulator -= step;
                    if (game.Finished) { FinishRecord(); keys.Clear(); horizontal = 0; focused = 0; break; }
                }
            }
            else accumulator = 0;
            if (game != null && game.Rush != null && page == "play")
            {
                int id = (int)game.Rush.Boss.Id;
                if (id != playingBoss)
                { playingBoss = id; audio.SetBossMusic(id); keys.Clear(); horizontal = 0; }
                audio.Cinematic = game.Rush.Waiting && !game.Finished;
            }
            else audio.Cinematic = false;
            Invalidate();
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Tab || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); e.Handled = true; e.SuppressKeyPress = true;
            if (!downKeys.Add(e.KeyCode)) return;
            keys.Add(e.KeyCode); // Không dùng auto-repeat của Windows cho xoay / hard drop.
            if (e.KeyCode == Keys.F11) { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; return; }
            if (page == "play" && game != null && game.Finished && enteringName)
            {
                if (e.KeyCode == Keys.Back && entryName.Length > 0) entryName = entryName.Substring(0, entryName.Length - 1);
                else if (e.KeyCode == Keys.Enter) { audio.Play("confirm"); CommitResult(); }
                // Cho KeyPress nhận chữ, kể cả M/P/C. Không bật shortcut lúc nhập tên.
                if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Back) e.SuppressKeyPress = false;
                Invalidate(); return;
            }
            if (e.KeyCode == Keys.M) { Settings.Muted = !Settings.Muted; ApplyAudio(); Save(); return; }
            if (page == "play" && game != null)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P)
                { if (!game.Finished) { if (game.Paused) Resume(); else Pause(); } return; }
                if (!game.Paused && !game.Finished)
                {
                    if (game.Rush != null && game.Rush.Waiting) return;
                    switch (e.KeyCode)
                    {
                        case Keys.Left: horizontal = -1; heldTime = 0; repeatTime = Settings.Arr / 1000.0; game.Move(-1); break;
                        case Keys.Right: horizontal = 1; heldTime = 0; repeatTime = Settings.Arr / 1000.0; game.Move(1); break;
                        case Keys.Up: case Keys.X: game.Rotate(1); break;
                        case Keys.Z: game.Rotate(-1); break;
                        case Keys.Space: game.HardDrop(); break;
                        case Keys.C: case Keys.ShiftKey: game.Hold(); break;
                    }
                    if (game.Finished) FinishRecord();
                    return;
                }
            }
            if (e.KeyCode == Keys.Escape && page != "home" && page != "play") { Save(); Go("home"); return; }
            if (hits.Count == 0) return;
            int oldFocus = focused;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Tab) focused = (focused + 1) % hits.Count;
            if (e.KeyCode == Keys.Up) focused = (focused + hits.Count - 1) % hits.Count;
            if (focused != oldFocus) audio.Play("menu");
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            { audio.Play("confirm"); hits[Math.Min(focused, hits.Count - 1)].Action(); }
            Invalidate();
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (!enteringName || page != "play") return;
            char c = char.ToUpperInvariant(e.KeyChar);
            // Font máy cổ dùng A-Z/0-9; tên tối đa 12 ký tự để bảng điểm không tràn.
            if (entryName.Length < 12 && ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-')) entryName += c;
            e.Handled = true; Invalidate();
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e); keys.Remove(e.KeyCode); downKeys.Remove(e.KeyCode);
            if ((horizontal == -1 && e.KeyCode == Keys.Left) || (horizontal == 1 && e.KeyCode == Keys.Right))
            {
                horizontal = keys.Contains(Keys.Left) ? -1 : keys.Contains(Keys.Right) ? 1 : 0;
                heldTime = 0; repeatTime = Settings.Arr / 1000.0;
                if (horizontal != 0 && game != null && !game.Paused) game.Move(horizontal);
            }
        }
        private PointF CanvasPoint(Point point)
        {
            float scale = Math.Min(ClientSize.Width / (float)CanvasW, ClientSize.Height / (float)CanvasH);
            return new PointF((point.X - (ClientSize.Width - CanvasW * scale) / 2) / scale,
                (point.Y - (ClientSize.Height - CanvasH * scale) / 2) / scale);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); mouse = CanvasPoint(e.Location);
            int index = hits.FindIndex(h => h.Rect.Contains(mouse));
            if (index >= 0) { focused = index; if (index != hovered) audio.Play("menu"); }
            hovered = index; Invalidate();
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = -1; mouse = new PointF(-1, -1); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
            PointF p = CanvasPoint(e.Location); Hit hit = hits.FirstOrDefault(h => h.Rect.Contains(p));
            if (hit != null) { audio.Play("confirm"); hit.Action(); Invalidate(); }
        }
        // Vẽ vào bitmap nhỏ rồi phóng nearest-neighbor: pixel thật, không phải chữ mượt giả retro.
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Graphics g = Graphics.FromImage(screen))
            {
                g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.None;
                hits.Clear(); DrawBackdrop(g); StoneFrame(g);
                switch (page)
                {
                    case "home": DrawHomeWithTeam(g); break;
                    case "team": DrawNoCTeam(g); break;
                    case "modes": DrawModes(g); break;
                    case "play": DrawGame(g); break;
                    case "records": DrawRecords(g); break;
                    case "stats": DrawStats(g); break;
                    case "settings": DrawSettings(g); break;
                    case "help": DrawHelp(g); break;
                    case "bosses": DrawBestiary(g); break;
                }
                if (toastTime > 0)
                {
                    R(g, Color.Black, 18, 248, 348, 22);
                    // Thông báo lỗi vẫn cần rõ tiếng Việt, chỉ dùng font hệ thống ở dòng này.
                    using (var f = new Font("Arial", 8, GraphicsUnit.Pixel))
                    using (var b = new SolidBrush(Nes.Yellow))
                        g.DrawString(toast, f, b, new RectangleF(22, 249, 340, 21));
                }
            }
            Graphics target = e.Graphics; target.Clear(Color.Black);
            float scale = Math.Min(ClientSize.Width / (float)CanvasW, ClientSize.Height / (float)CanvasH);
            target.InterpolationMode = InterpolationMode.NearestNeighbor;
            target.PixelOffsetMode = PixelOffsetMode.Half;
            target.DrawImage(screen, new RectangleF((ClientSize.Width - CanvasW * scale) / 2,
                (ClientSize.Height - CanvasH * scale) / 2, CanvasW * scale, CanvasH * scale),
                new RectangleF(0, 0, CanvasW, CanvasH), GraphicsUnit.Pixel);
        }
        private static void R(Graphics g, Color color, int x, int y, int w, int h)
        { using (var b = new SolidBrush(color)) g.FillRectangle(b, x, y, w, h); }
        private static void T(Graphics g, string text, int x, int y, int size = 1, Color? color = null)
        { Nes.Text(g, text, x, y, size, color ?? Nes.White); }
        private static void Center(Graphics g, string text, int y, int size = 1, Color? color = null)
        { T(g, text, (CanvasW - (text.Length * 6 - 1) * size) / 2, y, size, color); }
        private static void Box(Graphics g, int x, int y, int w, int h)
        {
            R(g, Nes.Dark, x, y, w, h);
            using (var pen = new Pen(Color.FromArgb(99, 82, 49))) g.DrawRectangle(pen, x, y, w - 1, h - 1);
            R(g, Color.Black, x + 3, y + 3, w - 6, h - 6);
            R(g, Nes.Yellow, x, y, 3, 1); R(g, Nes.Yellow, x + w - 3, y, 3, 1);
        }
        private static void StoneFrame(Graphics g)
        {
            // Hai nét viền cùng khoảng cách ở cả bốn cạnh. Góc phản chiếu từ một mẫu.
            using (var pen = new Pen(Color.FromArgb(110, 91, 53))) g.DrawRectangle(pen, 9, 9, 365, 269);
            using (var pen = new Pen(Color.FromArgb(48, 42, 29))) g.DrawRectangle(pen, 12, 12, 359, 263);
            for (int corner = 0; corner < 4; corner++)
            {
                int x = (corner & 1) == 0 ? 9 : 374, y = corner < 2 ? 9 : 278;
                int dx = (corner & 1) == 0 ? 1 : -1, dy = corner < 2 ? 1 : -1;
                using (var pen = new Pen(Nes.Yellow))
                {
                    g.DrawLine(pen, x, y, x + dx * 17, y);
                    g.DrawLine(pen, x, y, x, y + dy * 17);
                    g.DrawLines(pen, new[] { new Point(x + dx * 5, y + dy * 12), new Point(x + dx * 5, y + dy * 5), new Point(x + dx * 12, y + dy * 5) });
                }
                R(g, Nes.Yellow, x + dx * 7, y + dy * 7, 1, 1);
            }
            // Nhịp trang trí ở giữa cạnh, không dùng gạch to lấn nội dung.
            foreach (int y in new[] { 9, 278 })
                using (var pen = new Pen(Nes.Yellow))
                    g.DrawPolygon(pen, new[] { new Point(188, y), new Point(192, y - 3), new Point(196, y), new Point(192, y + 3) });
        }
        private void DrawBackdropOld(Graphics g)
        {
            // Đen ở lớp đáy; kiến trúc xa, sương và bụi là các lớp độc lập.
            // Màu nền rất tối để không tranh độ tương phản với bảng chơi.
            for (int side = 0; side < 2; side++)
            {
                int x = side == 0 ? 22 : 322;
                R(g, Color.FromArgb(13, 15, 15), x, 81, 40, 183);
                R(g, Color.FromArgb(20, 21, 19), x + 7, 97, 5, 166);
                R(g, Color.FromArgb(9, 10, 10), x + 16, 103, 18, 161);
                using (var pen = new Pen(Color.FromArgb(26, 26, 21)))
                    g.DrawLines(pen, new[] { new Point(x, 99), new Point(x + 20, 62), new Point(x + 40, 99) });
            }
            if (page == "home")
            {
                // Tán cây khô xa sau logo; hình tự dựng, không dùng biểu tượng của game khác.
                using (var pen = new Pen(Color.FromArgb(37, 33, 22)))
                {
                    g.DrawLines(pen, new[] { new Point(191, 144), new Point(188, 106), new Point(195, 70), new Point(189, 24) });
                    for (int branch = 0; branch < 5; branch++)
                    {
                        int y = 43 + branch * 17;
                        g.DrawLines(pen, new[] { new Point(191, y + 25), new Point(158 - branch * 9, y), new Point(147 - branch * 13, y - 13) });
                        g.DrawLines(pen, new[] { new Point(191, y + 25), new Point(226 + branch * 9, y - 3), new Point(239 + branch * 13, y - 19) });
                    }
                }
            }
            // Các dải sương dùng alpha thấp, không phủ lên chữ (vẽ trước toàn bộ UI).
            for (int band = 0; band < 4; band++)
            {
                int x = (int)(Math.Sin(visualTime * .09 + band * 1.7) * 52);
                using (var brush = new SolidBrush(Color.FromArgb(9, 111, 108, 83)))
                    g.FillEllipse(brush, x - 80, 165 + band * 24, 460, 30);
            }
            for (int i = 0; i < 34; i++)
            {
                int x = 20 + (i * 71 % 344) + (int)(Math.Sin(visualTime * .19 + i) * 4);
                int y = 20 + (int)((i * 37 + 256 - visualTime * (1.4 + i % 3) % 256) % 256);
                int glow = 26 + (int)(16 * (1 + Math.Sin(visualTime * .8 + i)));
                R(g, Color.FromArgb(glow, glow * 4 / 5, glow / 2), x, y, 1, 1);
            }
        }
        private void Menu(Graphics g, string label, int x, int y, int width, Action action, int size = 1)
        {
            var rect = new RectangleF(x - 16, y - 4, width + 16, 15 + (size - 1) * 7);
            bool active = hits.Count == focused;
            if (active)
            {
                using (var pen = new Pen(Nes.Yellow))
                    g.DrawPolygon(pen, new[] { new Point(x - 13, y + 3), new Point(x - 10, y), new Point(x - 7, y + 3), new Point(x - 10, y + 6) });
            }
            T(g, label, x, y, size, active ? Nes.Yellow : Nes.White);
            hits.Add(new Hit { Rect = rect, Action = action });
        }
        private void Back(Graphics g)
        { Menu(g, "BACK", 160, 255, 80, delegate { Save(); Go("home"); }); }
        private void HomeMenu(Graphics g, string label, int y, Action action)
        {
            const int width = 144; int left = (CanvasW - width) / 2;
            bool active = hits.Count == focused;
            Center(g, label, y, 1, active ? Nes.Yellow : Nes.White);
            if (active) using (var pen = new Pen(Nes.Yellow))
                foreach (int cx in new[] { left + 8, left + width - 8 })
                    g.DrawPolygon(pen, new[] { new Point(cx, y), new Point(cx + 3, y + 3), new Point(cx, y + 6), new Point(cx - 3, y + 3) });
            hits.Add(new Hit { Rect = new RectangleF(left, y - 4, width, 15), Action = action });
        }
        private void DrawHome(Graphics g)
        {
            Nes.Text(g, "TETRIS", 89, 46, 6, Nes.Red); Center(g, "TETRIS", 42, 6, Nes.Yellow);
            Nes.Text(g, "118", 160, 103, 4, Nes.Red); Center(g, "118", 100, 4, Nes.White);
            HomeMenu(g, "PLAY", 157, delegate { Go("modes"); });
            HomeMenu(g, "LEADERBOARD", 174, delegate { Go("records"); });
            HomeMenu(g, "STATISTICS", 191, delegate { Go("stats"); });
            HomeMenu(g, "SETTINGS", 208, delegate { Go("settings"); });
            HomeMenu(g, "HOW TO PLAY", 225, delegate { Go("help"); });
            HomeMenu(g, "EXIT", 242, Close);
        }
        private void DrawModes(Graphics g)
        {
            Center(g, "SELECT MODE", 39, 2, Nes.Yellow);
            Menu(g, "MARATHON", 131, 72, 170, delegate { selectedMode = GameMode.Marathon; StartGame(); });
            T(g, "PLAY UNTIL TOP OUT", 131, 84, 1, Nes.Gray);
            Menu(g, "SPRINT 40", 131, 107, 170, delegate { selectedMode = GameMode.Sprint; StartGame(); });
            T(g, "CLEAR 40 LINES", 131, 119, 1, Nes.Gray);
            Menu(g, "ULTRA 120", 131, 142, 170, delegate { selectedMode = GameMode.Ultra; StartGame(); });
            T(g, "SCORE IN 120 SECONDS", 131, 154, 1, Nes.Gray);
            Menu(g, "BOSS RUSH", 131, 177, 170, delegate { selectedMode = GameMode.BossRush; StartGame(); });
            T(g, "3 BOSSES / 5 HP", 131, 189, 1, Nes.Gray);
            Menu(g, "SEED " + (Settings.MatchSeed == 0 ? "RANDOM" : Settings.MatchSeed.ToString()), 106, 214, 240, EditSeed);
            Menu(g, "BOSS BESTIARY", 148, 235, 120, delegate { Go("bosses"); });
            Back(g);
        }
        private void DrawGame(Graphics g)
        {
            if (!BossLayout || game.Finished || game.Paused) Center(g, "TETRIS 118", 23, 1, Nes.Yellow);
            if (game.Finished) { DrawResult(g); return; }
            if (game.Paused)
            {
                Center(g, "PAUSED", 90, 2, Nes.Yellow);
                Menu(g, "RESUME", 144, 129, 120, Resume);
                Menu(g, "RESTART", 144, 161, 120, RestartGame);
                Menu(g, "MAIN MENU", 144, 193, 120, Abandon);
                return;
            }
            if (BossLayout) { DrawBossGame(g); return; }
            Box(g, BoardX - 4, BoardY - 4, 108, 208);
            for (int y = GameEngine.Hidden; y < GameEngine.Height; y++)
                for (int x = 0; x < GameEngine.Width; x++)
                    if (game.Board[y, x] != 0) Nes.Block(g, game.Board[y, x], BoardX + x * CellSize,
                        BoardY + (y - GameEngine.Hidden) * CellSize, CellSize, false);
            if (Settings.Ghost) Active(g, game.GhostY(), true);
            Active(g, game.Y, false);
            if (game.ClearFlash > 0 && ((int)(game.ClearFlash * 40) & 1) == 0)
                foreach (int y in game.ClearedRows) if (y >= GameEngine.Hidden)
                    R(g, Nes.White, BoardX, BoardY + (y - GameEngine.Hidden) * CellSize, 100, CellSize);
            T(g, "HOLD", 45, 46); Box(g, 30, 59, 91, 42);
            if (game.Held.HasValue) Nes.Mini(g, game.Held.Value, 75, 80, 8, game.HoldUsed);
            T(g, "SCORE", 38, 116); T(g, game.Score.ToString("D8"), 38, 130, 1, Nes.Yellow);
            T(g, "LEVEL", 38, 150); T(g, game.Level.ToString("D2"), 38, 163);
            T(g, "LINES", 38, 184); T(g, game.Lines.ToString("D3"), 38, 197);
            T(g, "TIME", 38, 218); T(g, TimeText(game.Mode == GameMode.Ultra ? Math.Max(0, 120 - game.Elapsed) : game.Elapsed), 38, 231);
            T(g, "NEXT", 281, 46); Box(g, 265, 59, 87, 138);
            PieceKind[] next = game.Next;
            for (int i = 0; i < 5; i++) Nes.Mini(g, next[i], 308, 75 + i * 25, 7, false);
            if (game.BackToBack) T(g, "B2B", 272, 210, 1, Nes.Yellow);
            if (game.Combo > 0) T(g, "COMBO " + game.Combo, 272, 223, 1, Nes.Yellow);
            Menu(g, "PAUSE", 280, 246, 61, Pause);
            if (game.MessageTime > 0) Center(g, game.LastClear, 257, 1, Nes.Yellow);
        }
        private int Wrapped(Graphics g, string text, int x, int y, int chars, Color color)
        {
            string line = "";
            foreach (string word in text.Split(' '))
            {
                if (line.Length + word.Length + 1 > chars && line.Length > 0)
                { T(g, line, x, y, 1, color); y += 10; line = ""; }
                line += (line.Length == 0 ? "" : " ") + word;
            }
            if (line.Length > 0) { T(g, line, x, y, 1, color); y += 10; }
            return y;
        }
        private void DrawBestiary(Graphics g)
        {
            BossDefinition boss = BossDefinition.All[galleryBoss];
            Center(g, boss.Name, 24, 2, Nes.Yellow);
            g.DrawImageUnscaled(portraits.Get(boss.Id), 156, 47);
            T(g, "PASSIVE", 32, 131, 1, Nes.Yellow);
            int y = Wrapped(g, boss.Rule, 32, 144, 53, Nes.White);
            T(g, "ATTACK", 32, y + 5, 1, Nes.Yellow);
            Wrapped(g, boss.Attack, 32, y + 18, 53, Nes.White);
            Menu(g, "PREV", 77, 229, 65, delegate { galleryBoss = (galleryBoss + 11) % 12; });
            T(g, (galleryBoss + 1) + "/12", 177, 229, 1, Nes.Yellow);
            Menu(g, "NEXT", 281, 229, 50, delegate { galleryBoss = (galleryBoss + 1) % 12; });
            Menu(g, "BACK", 180, 255, 60, delegate { Go("modes"); });
        }
        private void DrawBossGame(Graphics g)
        {
            BossRush rush = game.Rush;
            if (rush.Waiting)
            {
                bool defeated = rush.Defeated > rush.Stage;
                Center(g, defeated ? "DEMIGOD FELLED" : "BOSS " + (rush.Stage + 1) + "/3", 24, 2, Nes.Yellow);
                g.DrawImageUnscaled(portraits.Get(rush.Boss.Id), 156, 46);
                Center(g, rush.Boss.Name, 123, 1, Nes.White);
                if (defeated)
                {
                    Center(g, rush.Defeated == 3 ? "JOURNEY COMPLETE" : "YOUR LEVEL AND HP CARRY OVER", 160, 1, Nes.Yellow);
                }
                else
                {
                    T(g, "PASSIVE", 30, 144, 1, Nes.Yellow);
                    int y = Wrapped(g, rush.Boss.Rule, 30, 156, 54, Nes.White);
                    T(g, "ATTACK", 30, y + 4, 1, Nes.Yellow);
                    Wrapped(g, rush.AttackText, 30, y + 16, 54, Nes.White);
                }
                Center(g, defeated ? "" : "BOSS HP " + rush.BossHealth, 236, 1, Nes.Yellow);
                Center(g, Math.Ceiling(rush.Intermission).ToString(), 253, 1, Nes.Yellow);
                return;
            }
            g.DrawImageUnscaled(portraits.Get(rush.Boss.Id), 156, 17);
            Center(g, rush.Boss.Name, 93, 1, Nes.Yellow);
            T(g, "YOUR HP " + rush.Health + "/5", 24, 22, 1, Nes.Yellow);
            T(g, "PASSIVE", 24, 40, 1, Nes.Yellow);
            int end = Wrapped(g, rush.Boss.Rule, 24, 52, 19, Nes.White);
            T(g, "ATTACK", 24, end + 5, 1, Nes.Yellow);
            Wrapped(g, rush.AttackText, 24, end + 17, 19, Nes.White);
            if (rush.NoticeTime > 0) Wrapped(g, rush.Notice, 24, 173, 19, Nes.Yellow);
            T(g, "LEVEL " + game.Level, 24, 198, 1, Nes.Yellow);
            T(g, "SCORE " + game.Score, 24, 210);
            T(g, rush.HoldBlocked ? "HOLD DISABLED" : "HOLD", 24, 230, 1, Nes.Gray);
            Box(g, 24, 241, 111, 29);
            if (game.Held.HasValue && !rush.HoldBlocked) Nes.Mini(g, game.Held.Value, 79, 255, 6, game.HoldUsed);

            ChallengeDefinition challenge = rush.Challenge.Definition;
            T(g, "BOSS HP " + rush.BossHealth + "/" + rush.BossMaxHealth, 249, 22, 1, Nes.Yellow);
            T(g, "CHALLENGE", 249, 39, 1, Nes.Yellow);
            Wrapped(g, challenge.Name, 249, 51, 19, Nes.White);
            T(g, challenge.Difficulty + " " + (challenge.Seconds == 0 ? "" : Math.Ceiling(rush.Remaining) + "S"), 249, 75, 1,
                challenge.Seconds > 0 && rush.Remaining <= 10 ? Color.FromArgb(220, 92, 59) : Nes.Yellow);
            Wrapped(g, challenge.Description, 249, 88, 19, Nes.White);
            int progress = rush.Boss.Id == BossId.Ranni ? game.Level : rush.Challenge.Progress;
            T(g, "PROGRESS " + Math.Min(progress, challenge.Target) + "/" + challenge.Target, 249, 142, 1, Nes.Yellow);
            if (rush.BloodTime > 0) T(g, "BLEED " + Math.Ceiling(rush.BloodTime) + "S", 249, 156, 1, Nes.Yellow);
            else if (rush.EffectTime > 0) T(g, "EFFECT " + Math.Ceiling(rush.EffectTime) + "S", 249, 156, 1, Nes.Yellow);
            T(g, "NEXT", 249, 173);
            Box(g, 249, 184, 111, 70);
            PieceKind[] next = game.Next;
            for (int i = 0; i < 5; i++) Nes.Mini(g, next[i], 304, 193 + i * 13, 5, false);
            Menu(g, "PAUSE", 286, 262, 60, Pause);

            Box(g, BoardX - 3, BoardY - 3, 86, 166);
            for (int y = GameEngine.Hidden; y < GameEngine.Height; y++)
                for (int x = 0; x < GameEngine.Width; x++)
                    if (game.Board[y, x] != 0) Nes.Block(g, game.Board[y, x], BoardX + x * CellSize,
                        BoardY + (y - GameEngine.Hidden) * CellSize, CellSize, false);
            if (Settings.Ghost) Active(g, game.GhostY(), true);
            if (!rush.HideFalling) Active(g, game.Y, false);
            if (game.ClearFlash > 0 && ((int)(game.ClearFlash * 40) & 1) == 0)
                foreach (int y in game.ClearedRows) if (y >= GameEngine.Hidden)
                    R(g, Nes.White, BoardX, BoardY + (y - GameEngine.Hidden) * CellSize, 80, CellSize);
            if (rush.Ceiling < 20)
                R(g, Color.FromArgb(210, 73, 57), BoardX, BoardY + (20 - rush.Ceiling) * CellSize, 80, 1);
            if (rush.Flash > 0)
                using (var pen = new Pen(Color.FromArgb(200, 60, 40))) g.DrawRectangle(pen, 146, 102, 91, 171);
        }
        private void Active(Graphics g, int y, bool ghost)
        {
            foreach (Cell c in Pieces.Cells(game.Current, game.Rotation))
            {
                int row = y + c.Y - GameEngine.Hidden;
                if (row >= 0 && row < 20) Nes.Block(g, (int)game.Current, BoardX + (game.X + c.X) * CellSize,
                    BoardY + row * CellSize, CellSize, ghost);
            }
        }
        private void DrawResult(Graphics g)
        {
            Center(g, game.Won ? "COMPLETE" : "GAME OVER", 47, 2, Nes.Yellow);
            if (game.Rush != null) Center(g, "BOSS " + game.Rush.Defeated + "/3  HP " + game.Rush.Health, 66, 1, Nes.Yellow);
            string[] labels = { "SCORE", "LINES", "TIME", "PIECES", "PIECES / SEC", "TETRIS", "T-SPIN", "PERFECT CLEAR", "BEST COMBO" };
            string[] values = { game.Score.ToString("N0"), game.Lines.ToString(), TimeText(game.Elapsed), game.PiecesPlaced.ToString(),
                (game.Elapsed > 0 ? game.PiecesPlaced / game.Elapsed : 0).ToString("0.00"), game.Tetrises.ToString(),
                game.TSpins.ToString(), game.PerfectClears.ToString(), game.MaxCombo.ToString() };
            for (int i = 0; i < labels.Length; i++)
            { T(g, labels[i], 67, 78 + i * 13); T(g, values[i], 246, 78 + i * 13, 1, Nes.Yellow); }
            if (enteringName)
            {
                T(g, "NAME", 67, 207);
                T(g, entryName + (((int)(visualTime * 2) & 1) == 0 ? "_" : ""), 109, 207, 1, Nes.Yellow);
                Menu(g, "SAVE RESULT", 139, 235, 120, CommitResult);
            }
            else
            {
                Menu(g, "PLAY AGAIN", 145, 219, 105, StartGame);
                Menu(g, "MAIN MENU", 145, 244, 105, delegate { Go("home"); });
            }
        }
        private static string TimeText(double seconds)
        {
            seconds = Math.Max(0, seconds);
            return ((int)seconds / 60).ToString("D2") + ":" + ((int)seconds % 60).ToString("D2") + "." + ((int)(seconds * 100) % 100).ToString("D2");
        }
        private void ModeTabs(Graphics g)
        {
            for (int i = 0; i < 4; i++)
            {
                GameMode m = (GameMode)i;
                Menu(g, m == GameMode.Marathon ? "MARATHON" : m == GameMode.Sprint ? "SPRINT" : m == GameMode.Ultra ? "ULTRA" : "BOSS", 37 + i * 84, 58,  70,
                    delegate { recordMode = m; });
                if (recordMode == m) R(g, Nes.Red, 37 + i * 84, 70, 42, 2);
            }
        }
        private void DrawRecords(Graphics g)
        {
            Center(g, "LEADERBOARD", 27, 2, Nes.Yellow); ModeTabs(g);
            T(g, "NAME", 55, 87); T(g, "SCORE", 155, 87); T(g, recordMode == GameMode.BossRush ? "BOSS" : "LINES", 227, 87); T(g, "TIME", 284, 87);
            Record[] records = profile.Ranking(recordMode).Take(9).ToArray();
            if (records.Length == 0) Center(g, "NO RECORDS YET", 148, 1, Nes.Gray);
            for (int i = 0; i < records.Length; i++)
            {
                Record r = records[i]; int y = 106 + i * 15;
                T(g, (i + 1).ToString(), 32, y, 1, Nes.Red);
                T(g, ShortName(r.Player, 12), 55, y);
                T(g, ShortName(r.Score.ToString(), 10), 155, y, 1, Nes.Yellow);
                T(g, recordMode == GameMode.BossRush ? r.Bosses + "/3" : r.Lines.ToString(), 227, y);
                T(g, recordMode == GameMode.Sprint && !r.Won ? "DNF" : TimeText(r.Seconds), 284, y);
            }
            Back(g);
        }
        private void DrawStats(Graphics g)
        {
            Center(g, "STATISTICS", 28, 2, Nes.Yellow);
            string[] labels = { "GAMES PLAYED", "PLAY TIME", "TOTAL SCORE", "BEST SCORE", "TOTAL LINES", "TOTAL PIECES", "TETRIS", "T-SPIN",
                "PERFECT CLEAR", "BEST COMBO", "SPRINT WINS", "SCORE / GAME", "LINES / GAME", "PIECES / SEC", "BOSS RUSH RUNS", "BOSS RUSH WINS", "BOSSES DEFEATED" };
            string[] values = { profile.Games.ToString(), ((int)(profile.PlaySeconds / 3600)).ToString() + "H " + ((int)(profile.PlaySeconds / 60) % 60).ToString("D2") + "M",
                profile.TotalScore.ToString("N0"), profile.BestScore.ToString("N0"), profile.Lines.ToString("N0"), profile.Pieces.ToString("N0"),
                profile.Tetrises.ToString(), profile.TSpins.ToString(), profile.PerfectClears.ToString(), profile.BestCombo.ToString(), profile.SprintWins.ToString(),
                (profile.Games > 0 ? profile.TotalScore / profile.Games : 0).ToString("N0"),
                (profile.Games > 0 ? profile.Lines / (double)profile.Games : 0).ToString("0.0"),
                (profile.PlaySeconds > 0 ? profile.Pieces / profile.PlaySeconds : 0).ToString("0.00"),
                profile.BossRuns.ToString(), profile.BossWins.ToString(), profile.BossKills.ToString() };
            for (int i = 0; i < labels.Length; i++)
            { T(g, labels[i], 40, 55 + i * 11); T(g, ShortName(values[i], 17), 245, 55 + i * 11, 1, Nes.Yellow); }
            Back(g);
        }
        private void DrawSettings(Graphics g)
        {
            Center(g, "SETTINGS", 32, 2, Nes.Yellow);
            Setting(g, "MUSIC", Settings.Music, 87, delegate { Settings.Music = Math.Max(0, Settings.Music - 5); ApplyAudio(); }, delegate { Settings.Music = Math.Min(100, Settings.Music + 5); ApplyAudio(); });
            Setting(g, "SFX", Settings.Effects, 120, delegate { Settings.Effects = Math.Max(0, Settings.Effects - 5); ApplyAudio(); }, delegate { Settings.Effects = Math.Min(100, Settings.Effects + 5); ApplyAudio(); audio.Play("rotate"); });
            Menu(g, "SOUND  " + (Settings.Muted ? "OFF" : "ON"), 100, 158, 188, delegate { Settings.Muted = !Settings.Muted; ApplyAudio(); });
            Menu(g, "GHOST  " + (Settings.Ghost ? "ON" : "OFF"), 100, 188, 188, delegate { Settings.Ghost = !Settings.Ghost; });
            Menu(g, "TEST TETRIS SFX", 100, 218, 188, delegate { audio.Play("tetris"); });
            Back(g);
        }
        private void Setting(Graphics g, string label, int value, int y, Action minus, Action plus)
        {
            T(g, label, 60, y); T(g, value.ToString("D3"), 232, y, 1, Nes.Yellow);
            Menu(g, "-", 207, y, 13, minus); Menu(g, "+", 279, y, 13, plus);
        }
        private void DrawHelp(Graphics g)
        {
            Center(g, "HOW TO PLAY", 29, 2, Nes.Yellow);
            string[] lines = {
                "LEFT / RIGHT: MOVE", "X / UP      : ROTATE RIGHT", "Z           : ROTATE LEFT",
                "DOWN        : SOFT DROP", "SPACE       : HARD DROP", "C / SHIFT   : HOLD",
                "P / ESC     : PAUSE", "M           : MUTE / UNMUTE", "F11         : MAXIMIZE", "",
                "FILL A ROW TO CLEAR IT.", "HOLD ONCE PER PIECE.",
                "CLEAR 1/2/3/4 LINES:", "100/300/500/800 POINTS X LEVEL.",
                "T-SPIN, B2B, COMBO: BONUS." };
            for (int i = 0; i < lines.Length; i++) T(g, lines[i], 46, 62 + i * 12);
            Back(g);
        }
        private static string ShortName(string text, int max)
        {
            string s = Nes.Plain(text ?? "ANON");
            return s.Length > max ? s.Substring(0, max) : s;
        }
        private void EditSeed()
        {
            using (var dialog = new Form())
            using (var number = new NumericUpDown())
            using (var ok = new System.Windows.Forms.Button())
            {
                dialog.Text = "Seed"; dialog.ClientSize = new Size(300, 120);
                dialog.StartPosition = FormStartPosition.CenterParent; dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.Controls.Add(new Label { Text = "0 = random", Left = 20, Top = 12, Width = 260 });
                number.SetBounds(20, 38, 260, 26); number.Maximum = int.MaxValue; number.Value = Settings.MatchSeed;
                ok.Text = "OK"; ok.SetBounds(190, 78, 90, 27); ok.DialogResult = DialogResult.OK;
                dialog.Controls.Add(number); dialog.Controls.Add(ok); dialog.AcceptButton = ok;
                if (dialog.ShowDialog(this) == DialogResult.OK) { Settings.MatchSeed = (int)number.Value; Save(); }
            }
        }
        // Renderer nằm cùng form để bản cập nhật chỉ cần thay hai file có sẵn.
        private static class Nes
        {
            public static readonly Color White = Color.FromArgb(210, 201, 173);
            public static readonly Color Yellow = Color.FromArgb(184, 155, 91);
            public static readonly Color Red = Color.FromArgb(109, 80, 42);
            public static readonly Color Gray = Color.FromArgb(129, 124, 104);
            public static readonly Color Dark = Color.FromArgb(32, 30, 24);
            private static readonly Color[] Colors = { Color.Black,
                Color.FromArgb(72, 124, 132), Color.FromArgb(171, 139, 65),
                Color.FromArgb(122, 90, 135), Color.FromArgb(91, 124, 76),
                Color.FromArgb(153, 76, 63), Color.FromArgb(77, 94, 140), Color.FromArgb(164, 107, 57) };
            public static string Plain(string text)
            {
                var result = new System.Text.StringBuilder();
                foreach (char c in text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD))
                    if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                        result.Append(char.ToUpperInvariant(c));
                return result.ToString();
            }
            public static void Text(Graphics g, string text, int x, int y, int size, Color color)
            {
                foreach (char c in Plain(text))
                {
                    string bits;
                    if (Glyphs.TryGetValue(c, out bits))
                        for (int n = 0; n < 35; n++) if (bits[n] == '1')
                            R(g, color, x + n % 5 * size, y + n / 5 * size, size, size);
                    x += 6 * size;
                }
            }
            private static readonly string[] Marks = { "", "101101101", "111101111", "111010010", "011010110", "110010011", "001101111", "100101111" };
            public static void Block(Graphics g, int kind, int x, int y, int size, bool ghost)
            {
                if (kind == 99 || kind == 98 || kind == 20)
                {
                    R(g, Color.FromArgb(75, 77, 76), x, y, size - 1, size - 1);
                    R(g, Color.FromArgb(123, 125, 119), x + 1, y + 1, size - 3, 1);
                    using (var pen = new Pen(Color.FromArgb(35, 37, 36)))
                        g.DrawLines(pen, new[] { new Point(x + 2, y + 2), new Point(x + 4, y + 4), new Point(x + 2, y + size - 2) });
                    if (kind == 98) R(g, Yellow, x + 2, y + size - 2, size - 4, 1);
                    if (kind == 20) R(g, White, x + size / 2, y + size / 2, 1, 1);
                    return;
                }
                int style = 1 + (kind - 1) % 7;
                Color c = Colors[style];
                if (ghost)
                {
                    Color edge = Color.FromArgb(c.R / 2, c.G / 2, c.B / 2);
                    R(g, edge, x + 1, y + 1, size - 2, 1); R(g, edge, x + 1, y + size - 2, size - 2, 1);
                    R(g, edge, x + 1, y + 1, 1, size - 2); R(g, edge, x + size - 2, y + 1, 1, size - 2);
                    return;
                }
                R(g, Color.FromArgb(c.R / 2, c.G / 2, c.B / 2), x, y, size - 1, size - 1);
                R(g, c, x + 1, y + 1, size - 3, size - 3);
                R(g, Color.FromArgb((c.R + 184) / 2, (c.G + 155) / 2, (c.B + 91) / 2), x + 1, y, size - 3, 1);
                // I: hai trụ, O: nhẫn, T: đinh ba, S/Z: hai nét chéo, J/L: móc.
                // Dấu 3x3 vẫn đọc được ở khung NEXT nhỏ, không phụ thuộc màu.
                string mark = Marks[style]; int ox = x + (size - 4) / 2, oy = y + (size - 4) / 2;
                for (int i = 0; i < 9; i++) if (mark[i] == '1') R(g, White, ox + i % 3, oy + i / 3, 1, 1);
                if (kind >= 8) R(g, Yellow, x, y + size - 2, size - 1, 1);
            }
            public static void Mini(Graphics g, PieceKind kind, int cx, int cy, int size, bool faded)
            {
                Cell[] cells = Pieces.Cells(kind, 0);
                int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X);
                int minY = cells.Min(c => c.Y), maxY = cells.Max(c => c.Y);
                int x = cx - (maxX - minX + 1) * size / 2, y = cy - (maxY - minY + 1) * size / 2;
                foreach (Cell c in cells) Block(g, (int)kind, x + (c.X - minX) * size, y + (c.Y - minY) * size, size, faded);
            }
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string> {
            {'A',"01110100011000111111100011000110001"}, {'B',"11110100011000111110100011000111110"},
            {'C',"01111100001000010000100001000001111"}, {'D',"11110100011000110001100011000111110"},
            {'E',"11111100001000011110100001000011111"}, {'F',"11111100001000011110100001000010000"},
            {'G',"01111100001000010111100011000101111"}, {'H',"10001100011000111111100011000110001"},
            {'I',"11111001000010000100001000010011111"}, {'J',"00111000100001000010100101001001100"},
            {'K',"10001100101010011000101001001010001"}, {'L',"10000100001000010000100001000011111"},
            {'M',"10001110111010110101100011000110001"}, {'N',"10001110011010110011100011000110001"},
            {'O',"01110100011000110001100011000101110"}, {'P',"11110100011000111110100001000010000"},
            {'Q',"01110100011000110001101011001001101"}, {'R',"11110100011000111110101001001010001"},
            {'S',"01111100001000001110000010000111110"}, {'T',"11111001000010000100001000010000100"},
            {'U',"10001100011000110001100011000101110"}, {'V',"10001100011000110001100010101000100"},
            {'W',"10001100011000110101101011101110001"}, {'X',"10001100010101000100010101000110001"},
            {'Y',"10001100010101000100001000010000100"}, {'Z',"11111000010001000100010001000011111"},
            {'0',"01110100011001110101110011000101110"}, {'1',"00100011000010000100001000010001110"},
            {'2',"01110100010000100010001000100011111"}, {'3',"11110000010000101110000010000111110"},
            {'4',"00010001100101010010111110001000010"}, {'5',"11111100001000011110000010000111110"},
            {'6',"01111100001000011110100011000101110"}, {'7',"11111000010001000100010000100001000"},
            {'8',"01110100011000101110100011000101110"}, {'9',"01110100011000101111000010000111110"},
            {'!',"00100001000010000100001000000000100"}, {'-',"00000000000000011111000000000000000"},
            {'>',"10000010000010000010001000100010000"},
            {':',"00000001000010000000001000010000000"},
            {'/',"00001000010001000100010001000010000"},
            {'+',"00000001000010011111001000010000000"},
            {'_',"00000000000000000000000000000011111"},
            {',',"00000000000000000000001100010001000"},
            {'.',"00000000000000000000000000011000110"}
        };
        }
    }
}
