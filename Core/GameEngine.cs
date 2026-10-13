using System;

namespace Tetris118.Core
{
    public enum GameMode { Marathon, Sprint, Ultra, BossRush }
    public sealed class GameEngine
    {
        public bool CurrentFromHold { get; private set; }
        public const int Width = 10, Height = 40, Hidden = 20;
        public readonly int[,] Board = new int[Height, Width];
        public PieceKind Current { get; private set; }
        public PieceKind? Held { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Rotation { get; private set; }
        public bool HoldUsed { get; private set; }
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public bool Paused { get; set; }
        public long Score { get; private set; }
        public int Lines { get; private set; }
        public BossRush Rush { get; private set; }
        public int Level { get { return Rush != null ? Rush.LocalLevel : Mode == GameMode.Marathon ? Math.Min(20, 1 + Lines / 10) : 1; } }
        public int PiecesPlaced { get; private set; }
        public int Tetrises { get; private set; }
        public int TSpins { get; private set; }
        public int PerfectClears { get; private set; }
        public int MaxCombo { get; private set; }
        public int Combo { get; private set; }
        public bool BackToBack { get; private set; }
        public double Elapsed { get; private set; }
        public GameMode Mode { get; private set; }
        public int Seed { get; private set; }
        public string LastClear { get; private set; }
        public double MessageTime { get; private set; }
        public int[] ClearedRows { get; private set; }
        public double ClearFlash { get; private set; }
        public double LockProgress { get { return lockTime / Math.Max(.001, Rush == null ? .5 : Rush.LockDelay); } }
        public bool Grounded { get { return !Fits(Current, Rotation, X, Y + 1); } }
        public event Action<string> Sound;
        private readonly SevenBag bag;
        private readonly Random garbageRandom;
        private bool locking;
        private double gravity, lockTime, softTime;
        private int resets, lowestY, lastKick;
        private bool lastWasRotation;
        private bool CanPlay { get { return !Paused && !Finished && (Rush == null || !Rush.Waiting); } }

        public GameEngine(GameMode mode, int seed)
        {
            Mode = mode; Seed = seed; garbageRandom = new Random(seed ^ 9218); bag = new SevenBag(seed); Combo = -1;
            LastClear = "READY"; MessageTime = 1.2; ClearedRows = new int[0];
            if (Mode == GameMode.BossRush) Rush = new BossRush(this, seed);
            else Spawn(bag.Take());
        }
        public PieceKind[] Next { get { return bag.Preview(); } }
        private void Emit(string name) { if (Sound != null) Sound(name); }
        public bool Fits(PieceKind kind, int rotation, int px, int py)
        {
            foreach (Cell c in Pieces.Cells(kind, rotation))
            {
                int x = px + c.X, y = py + c.Y;
                if (x < 0 || x >= Width || y < 0 || y >= Height || Board[y, x] != 0) return false;
            }
            return true;
        }
        private void Spawn(PieceKind kind)
        {
            CurrentFromHold = false;
            Current = kind; X = 3; Y = Hidden - 2; Rotation = 0;
            gravity = softTime = lockTime = 0; resets = 0; lowestY = Y;
            lastWasRotation = false; lastKick = 0;
            if (!Fits(Current, Rotation, X, Y)) End(false);
            else if (Fits(Current, Rotation, X, Y + 1)) { Y++; lowestY = Y; }
        }
        private void End(bool won)
        {
            Finished = true; Won = won;
            LastClear = won ? "COMPLETE!" : "GAME OVER"; MessageTime = 1000;
            if (!won || Rush == null) Emit(won ? "win" : "over");
        }
        private void ResetLock(bool wasGrounded)
        {
            if (wasGrounded && resets < (Rush == null ? 15 : Rush.ResetLimit)) { lockTime = 0; resets++; }
            if (Y > lowestY) { lowestY = Y; lockTime = 0; }
        }
        public bool Move(int direction)
        {
            if (!CanPlay || (direction != -1 && direction != 1)) return false;
            bool grounded = Grounded;
            if (!Fits(Current, Rotation, X + direction, Y)) return false;
            X += direction; lastWasRotation = false; ResetLock(grounded); Emit("move"); InstantLock(); return true;
        }
        public bool Rotate(int direction)
        {
            if (!CanPlay || Current == PieceKind.O || (direction != 1 && direction != -1)) return false;
            int next = (Rotation + direction + 4) % 4;
            int[] kicks = Pieces.Kicks(Current, Rotation, next);
            bool grounded = Grounded;
            for (int i = 0; i < kicks.Length; i += 2)
            {
                int nx = X + kicks[i], ny = Y - kicks[i + 1];
                if (!Fits(Current, next, nx, ny)) continue;
                Rotation = next; X = nx; Y = ny; lastWasRotation = true; lastKick = i / 2;
                // Báo trước InstantLock vì Radahn có thể khóa khối ngay.
                if (Rush != null)
                    Rush.OnRotate();

                ResetLock(grounded);
                Emit("rotate");
                InstantLock();
                return true;
            }
            return false;
        }
        public int GhostY()
        {
            int y = Y;
            while (Fits(Current, Rotation, X, y + 1)) y++;
            return y;
        }
        public void Hold()
        {
            if (!CanPlay || HoldUsed || (Rush != null && Rush.HoldBlocked))
                return;

            if (Rush != null)
                Rush.OnHold();

            PieceKind old = Current;
            bool tookStoredPiece = Held.HasValue;

            if (tookStoredPiece)
                Spawn(Held.Value);
            else
                Spawn(bag.Take());

            // Gán sau Spawn vì Spawn vừa đặt cờ về false.
            CurrentFromHold = tookStoredPiece;

            Held = old;
            HoldUsed = true;
            Emit("hold");
        }
        public void HardDrop()
        {
            if (!CanPlay) return;
            if (Rush != null) Rush.OnHardDrop();
            int target = GhostY(), distance = target - Y;
            Y = target; Score += distance * 2;
            // Hard drop 0 ô vẫn có thể là T-spin; rơi thật sự thì mất cờ xoay.
            if (distance > 0) lastWasRotation = false;
            Emit("hard"); Lock();
        }
        public void Tick(double dt, bool softDrop)
        {
            if (Paused || Finished) return;
            dt = Math.Max(0, Math.Min(.1, dt));
            if (Rush != null)
            {
                bool waiting = Rush.Waiting;
                Rush.Tick(dt);
                if (Finished || Rush.Waiting || waiting) return;
            }
            Elapsed += dt; MessageTime = Math.Max(0, MessageTime - dt);
            ClearFlash = Math.Max(0, ClearFlash - dt);
            if (Mode == GameMode.Ultra && Elapsed >= 120) { Elapsed = 120; End(true); return; }
            double interval = Math.Max(.005, Math.Pow(.8 - ((Level - 1) * .007), Level - 1));
            if (Rush != null) interval = Rush.MaxGravity ? .005 : interval / Rush.GravityScale;
            gravity += dt;
            while (gravity >= interval)
            {
                gravity -= interval;
                if (!StepDown(false)) { gravity = 0; break; }
            }
            if (softDrop)
            {
                softTime += dt;
                while (softTime >= .025)
                {
                    softTime -= .025;
                    if (!StepDown(true)) { softTime = 0; break; }
                }
            }
            else softTime = 0;
            if (Grounded)
            {
                lockTime += dt;
                if (lockTime >= (Rush == null ? .5 : Rush.LockDelay)) Lock();
            }
        }
        private bool StepDown(bool manual)
        {
            if (!Fits(Current, Rotation, X, Y + 1)) return false;
            Y++; lastWasRotation = false;
            if (Y > lowestY) { lowestY = Y; lockTime = 0; }
            if (manual) { Score++; Emit("soft"); }
            return true;
        }
        private bool Occupied(int x, int y)
        { return x < 0 || x >= Width || y < 0 || y >= Height || Board[y, x] != 0; }
        private int SpinType()
        {
            if (Current != PieceKind.T || !lastWasRotation) return 0;
            bool a = Occupied(X, Y), b = Occupied(X + 2, Y);
            bool c = Occupied(X, Y + 2), d = Occupied(X + 2, Y + 2);
            int corners = (a ? 1 : 0) + (b ? 1 : 0) + (c ? 1 : 0) + (d ? 1 : 0);
            if (corners < 3) return 0;
            bool front = Rotation == 0 ? a && b : Rotation == 1 ? b && d : Rotation == 2 ? c && d : a && c;
            return front || lastKick == 4 ? 2 : 1;
        }
        private void Lock()
        {
            locking = true;
            try
            {
            PieceKind lockedPiece = Current;
            bool edge = false; int top = Height;
            foreach (Cell c in Pieces.Cells(Current, Rotation))
            { if (X + c.X == 0 || X + c.X == Width - 1) edge = true; top = Math.Min(top, Y + c.Y); }
            int spin = SpinType(), oldLevel = Level;
            bool allHidden = true;
            foreach (Cell c in Pieces.Cells(Current, Rotation))
            {
                Board[Y + c.Y, X + c.X] = (int)Current;
                if (Y + c.Y >= Hidden) allHidden = false;
            }
            PiecesPlaced++;
            var rows = new System.Collections.Generic.List<int>();
            for (int y = 0; y < Height; y++)
            {
                bool full = true;
                for (int x = 0; x < Width; x++) if (Board[y, x] == 0 || Board[y, x] == 99) { full = false; break; }
                if (full) rows.Add(y);
            }
            // Nén từng đoạn cột giữa các ô đá. Đá 99 giữ nguyên tọa độ, không bị xóa.
            for (int x = 0; x < Width; x++)
            {
                int write = Height - 1;
                for (int read = Height - 1; read >= 0; read--)
                {
                    if (Board[read, x] == 99)
                    {
                        for (; write > read; write--) Board[write, x] = 0;
                        write = read - 1; continue;
                    }
                    if (rows.Contains(read)) continue;
                    Board[write--, x] = Board[read, x];
                }
                for (; write >= 0; write--) Board[write, x] = 0;
            }
            int count = rows.Count;
            ClearedRows = rows.ToArray(); ClearFlash = count > 0 ? .28 : 0;
            if (spin == 1 && count >= 2) spin = 2;
            bool difficult = count >= 4 || (spin > 0 && count > 0);
            int baseScore = spin == 2 ? new[] { 400, 800, 1200, 1600 }[Math.Min(3, count)] :
                spin == 1 ? new[] { 100, 200, 400 }[Math.Min(2, count)] : new[] { 0, 100, 300, 500, 800, 1200 }[Math.Min(5, count)];
            bool b2bBonus = difficult && BackToBack;
            if (b2bBonus) baseScore = baseScore * 3 / 2;
            if (count > 0)
            {
                Combo++; MaxCombo = Math.Max(MaxCombo, Combo);
                Score += 50L * Combo * oldLevel;
                BackToBack = difficult;
            }
            else Combo = -1;
            Score += (long)baseScore * oldLevel;
            Lines += count;
            if (count == 4) Tetrises++;
            if (spin > 0) TSpins++;
            bool empty = count > 0;
            for (int y = 0; y < Height && empty; y++)
                for (int x = 0; x < Width; x++) if (Board[y, x] != 0) { empty = false; break; }
            if (empty)
            { PerfectClears++; Score += (long)new[] { 0, 800, 1200, 1800, b2bBonus ? 3200 : 2000, 4000 }[Math.Min(5, count)] * oldLevel; }
            string label = count >= 5 ? "PENTA CLEAR!" : count == 4 ? "TETRIS!" : count == 3 ? "TRIPLE" : count == 2 ? "DOUBLE" : count == 1 ? "SINGLE" : "";
            if (spin > 0) label = (spin == 1 ? "MINI T-SPIN " : "T-SPIN ") + label;
            if (empty) label = "PERFECT CLEAR!";
            if (b2bBonus) label = "B2B / " + label;
            if (count > 0 || spin > 0) { LastClear = label; MessageTime = 1.5; }
            Emit(count >= 4 ? "tetris" : spin > 0 ? "spin" : count == 3 ? "triple" : count == 2 ? "double" : count == 1 ? "single" : "lock");
            if (count > 0 && Combo > 0) Emit("combo");
            if (b2bBonus) Emit("b2b");
            if (empty) Emit("perfect");
            if (Rush == null && Level > oldLevel) Emit("level");
            if (Mode == GameMode.Sprint && Lines >= 40) { End(true); return; }
            if (allHidden) { End(false); return; }
            if (Rush != null)
            {
                CheckBossCeiling(); if (Finished) return;
                Rush.OnLock(count, lockedPiece, edge, top);
                if (Level > oldLevel && !Rush.Waiting) Emit("level");
                if (Finished || Rush.Waiting) return;
            }
            HoldUsed = false; Spawn(bag.Take());
            }
            finally { locking = false; }
        }

        private void InstantLock()
        { if (Rush != null && Rush.LockDelay == 0 && Grounded && CanPlay) Lock(); }
        internal void BossSound(string sound) { Emit(sound); }
        internal void FinishBossRush(bool won) { if (!Finished) End(won); }
        internal void PrepareBossBoard(BossId id)
        {
            Array.Clear(Board, 0, Board.Length); Held = null; HoldUsed = false; Combo = -1; BackToBack = false;
            bag.Configure(id == BossId.Godrick, false);
            gravity = lockTime = softTime = 0; ClearedRows = new int[0]; ClearFlash = 0;
            Spawn(bag.Take());
        }
        internal void ForcePentomino(int count) { bag.ForcePentomino(count); }
        internal void CheckBossCeiling()
        {
            if (Rush == null) return;
            // Kiểm tra ô đã khóa, không giết khối đang spawn phía trên vạch.
            int ceilingRow = Height - Rush.Ceiling;
            for (int y = 0; y < ceilingRow; y++)
                for (int x = 0; x < Width; x++) if (Board[y, x] != 0) { End(false); return; }
        }
        public int StackHeight
        {
            get { for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) if (Board[y, x] != 0) return Height - y; return 0; }
        }
        public int StoneFloorHeight
        {
            get { for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) if (Board[y, x] == 99) return Height - y; return 0; }
        }
        public int MarkedGarbageRows
        {
            get
            {
                int rows = 0;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++) if (Board[y, x] == 98) { rows++; break; }
                return rows;
            }
        }
        internal void PushGarbage(int rows, bool solid, bool marked)
        {
            if (Finished || rows <= 0 || rows >= Height) return;
            // Không âm thầm xóa các ô bị đẩy khỏi đỉnh.
            for (int y = 0; y < rows; y++) for (int x = 0; x < Width; x++)
                if (Board[y, x] != 0) { End(false); return; }
            for (int y = 0; y < Height - rows; y++)
                for (int x = 0; x < Width; x++) Board[y, x] = Board[y + rows, x];
            for (int y = Height - rows; y < Height; y++)
            {
                int hole = garbageRandom.Next(Width);
                for (int x = 0; x < Width; x++) Board[y, x] = solid ? 99 : x == hole ? 0 : marked ? 98 : 20;
            }
            if (!locking)
            {
                Y -= rows;
                if (!Fits(Current, Rotation, X, Y)) { End(false); return; }
                lowestY = Y; lockTime = 0; lastWasRotation = false;
            }
            ClearFlash = 0; ClearedRows = new int[0];
            CheckBossCeiling();
        }

        // Chỉ test project dùng hàm này để dựng thế cờ, game thật không gọi.
        internal void SetActiveForTest(PieceKind piece, int x, int y, int rotation, bool rotated)
        { Current = piece; X = x; Y = y; Rotation = rotation; lastWasRotation = rotated; lastKick = 0; }
    }
}
