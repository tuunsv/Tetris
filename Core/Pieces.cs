using System;
using System.Collections.Generic;

namespace Tetris118.Core
{
    public enum PieceKind { I = 1, O, T, S, Z, J, L, PentF, PentI, PentL, PentP, PentN, PentT, PentU, PentV, PentW, PentX, PentY, PentZ }
    public struct Cell
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
    }

    public static class Pieces
    {
        // Bảng nằm riêng để khi sửa giao diện không vô tình sửa luật xoay.
        // I xoay quanh (1.5,1.5), nhóm còn lại quanh (1,1). O giữ nguyên.
        private static readonly string[][] Shapes = {
            new[] { "....", "####", "....", "...." },
            new[] { ".##.", ".##.", "....", "...." },
            new[] { ".#.", "###", "..." },
            new[] { ".##", "##.", "..." },
            new[] { "##.", ".##", "..." },
            new[] { "#..", "###", "..." },
            new[] { "..#", "###", "..." },
            new[] { ".##", "##.", ".#." },
            new[] { ".....", ".....", "#####", ".....", "....." },
            new[] { "#...", "#...", "#...", "##.." },
            new[] { "##.", "##.", "#.." },
            new[] { "##..", ".###", "....", "...." },
            new[] { "###", ".#.", ".#." },
            new[] { "#.#", "###", "..." },
            new[] { "#..", "#..", "###" },
            new[] { "#..", "##.", ".##" },
            new[] { ".#.", "###", ".#." },
            new[] { ".#..", "##..", ".#..", ".#.." },
            new[] { "##.", ".#.", ".##" }
        };
        private static readonly Cell[][][] Cache = Build();
        private static Cell[][][] Build()
        {
            var all = new Cell[Shapes.Length][][];
            for (int k = 0; k < Shapes.Length; k++)
            {
                all[k] = new Cell[4][];
                for (int r = 0; r < 4; r++)
                {
                    var cells = new List<Cell>();
                    int size = Shapes[k].Length;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            if (Shapes[k][y][x] == '#')
                            {
                                int px = x, py = y;
                                for (int n = 0; n < (k == 1 ? 0 : r); n++)
                                { int oldX = px; px = size - 1 - py; py = oldX; }
                                cells.Add(new Cell(px, py));
                            }
                    all[k][r] = cells.ToArray();
                }
            }
            return all;
        }
        public static Cell[] Cells(PieceKind kind, int rotation)
        { return Cache[(int)kind - 1][rotation & 3]; }

        // SRS gốc dùng trục Y hướng lên. Ở màn hình Y hướng xuống,
        // nên GameEngine sẽ TRỪ dy của bảng kick, không cộng.
        private static readonly int[][] Normal = {
            new[] { 0,0, -1,0, -1,1, 0,-2, -1,-2 }, // 0 -> R
            new[] { 0,0, 1,0, 1,-1, 0,2, 1,2 },    // R -> 0
            new[] { 0,0, 1,0, 1,-1, 0,2, 1,2 },    // R -> 2
            new[] { 0,0, -1,0, -1,1, 0,-2, -1,-2 },// 2 -> R
            new[] { 0,0, 1,0, 1,1, 0,-2, 1,-2 },   // 2 -> L
            new[] { 0,0, -1,0, -1,-1, 0,2, -1,2 }, // L -> 2
            new[] { 0,0, -1,0, -1,-1, 0,2, -1,2 }, // L -> 0
            new[] { 0,0, 1,0, 1,1, 0,-2, 1,-2 }    // 0 -> L
        };
        private static readonly int[][] Long = {
            new[] { 0,0, -2,0, 1,0, -2,-1, 1,2 },
            new[] { 0,0, 2,0, -1,0, 2,1, -1,-2 },
            new[] { 0,0, -1,0, 2,0, -1,2, 2,-1 },
            new[] { 0,0, 1,0, -2,0, 1,-2, -2,1 },
            new[] { 0,0, 2,0, -1,0, 2,1, -1,-2 },
            new[] { 0,0, -2,0, 1,0, -2,-1, 1,2 },
            new[] { 0,0, 1,0, -2,0, 1,-2, -2,1 },
            new[] { 0,0, -1,0, 2,0, -1,2, 2,-1 }
        };
        public static int[] Kicks(PieceKind kind, int from, int to)
        {
            // Pentomino dùng kick riêng (không tự nhận là SRS cho khối 5 ô).
            if ((int)kind >= 8) return new[] { 0,0, -1,0, 1,0, -2,0, 2,0, 0,1, -1,1, 1,1, 0,2 };
            if (kind == PieceKind.O) return new[] { 0, 0 };
            int key = from * 4 + to;
            int index;
            switch (key)
            {
                case 1: index = 0; break; case 4: index = 1; break;
                case 6: index = 2; break; case 9: index = 3; break;
                case 11: index = 4; break; case 14: index = 5; break;
                case 12: index = 6; break; case 3: index = 7; break;
                default: throw new ArgumentException("SRS accepts only 90-degree rotations.");
            }
            return (kind == PieceKind.I ? Long : Normal)[index];
        }
    }

    public sealed class SevenBag
    {
        private readonly Random random;
        private readonly Queue<PieceKind> queue = new Queue<PieceKind>();
        private readonly Queue<PieceKind> forced = new Queue<PieceKind>();
        private bool pentomino, independent;
        public void Configure(bool addPentomino, bool randomPieces)
        { pentomino = addPentomino; independent = randomPieces; queue.Clear(); forced.Clear(); Refill(); }
        public void ForcePentomino(int count)
        { for (int i = 0; i < count; i++) forced.Enqueue((PieceKind)random.Next(8, 20)); }
        public SevenBag(int seed) { random = new Random(seed); Refill(); }
        private void Refill()
        {
            while (queue.Count < 8)
            {
                if (independent) { queue.Enqueue((PieceKind)random.Next(1, 8)); continue; }
                var bag = new List<PieceKind> { PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S,
                    PieceKind.Z, PieceKind.J, PieceKind.L };
                if (pentomino) bag.Add((PieceKind)random.Next(8, 20));
                // Fisher-Yates: mỗi túi có đủ 7 loại, không bốc từng khối độc lập.
                for (int i = bag.Count - 1; i > 0; i--)
                { int j = random.Next(i + 1); PieceKind t = bag[i]; bag[i] = bag[j]; bag[j] = t; }
                foreach (var p in bag) queue.Enqueue(p);
            }
        }
        public PieceKind Take() { if (forced.Count > 0) return forced.Dequeue(); PieceKind p = queue.Dequeue(); Refill(); return p; }
        public PieceKind[] Preview() { var result = new List<PieceKind>(forced); result.AddRange(queue); return result.ToArray(); }
    }
}
