using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Tetris118.Core;

namespace Tetris118.UI
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(8, 10, 15);
        public static readonly Color Panel = Color.FromArgb(15, 19, 28);
        public static readonly Color Border = Color.FromArgb(42, 51, 64);
        public static readonly Color Ink = Color.FromArgb(230, 234, 222);
        public static readonly Color Dim = Color.FromArgb(132, 145, 156);
        public static readonly Color Mint = Color.FromArgb(170, 232, 186);
        public static readonly Color Gold = Color.FromArgb(246, 196, 109);
        public static readonly Color[] Colors = { Color.Transparent,
            Color.FromArgb(99, 207, 226), Color.FromArgb(243, 205, 98),
            Color.FromArgb(182, 140, 219), Color.FromArgb(153, 209, 141),
            Color.FromArgb(228, 123, 136), Color.FromArgb(121, 158, 230), Color.FromArgb(234, 166, 103) };
        public static void Rect(Graphics g, Color color, float x, float y, float w, float h)
        { using (var brush = new SolidBrush(color)) g.FillRectangle(brush, x, y, w, h); }
        public static void Line(Graphics g, Color color, float x, float y, float x2, float y2, float width)
        { using (var pen = new Pen(color, width)) g.DrawLine(pen, x, y, x2, y2); }
        public static void Box(Graphics g, float x, float y, float w, float h)
        {
            Rect(g, Panel, x, y, w, h);
            using (var pen = new Pen(Border)) g.DrawRectangle(pen, x, y, w, h);
        }
        public static void Text(Graphics g, string text, float x, float y, float size, Color color, bool bold = false)
        {
            using (var font = new Font("Consolas", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat(StringFormat.GenericTypographic))
                g.DrawString(text, font, brush, x, y, format);
        }
        public static void Paragraph(Graphics g, string text, RectangleF rect, float size, Color color)
        {
            using (var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
                g.DrawString(text, font, brush, rect);
        }
        public static void Block(Graphics g, int kind, float x, float y, float size, bool ghost = false, float alpha = 1)
        {
            Color c = Colors[kind];
            if (ghost)
            {
                Rect(g, Color.FromArgb(28, c), x + 2, y + 2, size - 4, size - 4);
                using (var pen = new Pen(Color.FromArgb(150, c))) g.DrawRectangle(pen, x + 3, y + 3, size - 6, size - 6);
                return;
            }
            int a = (int)(255 * alpha);
            Rect(g, Color.FromArgb(a, (int)(c.R * .38), (int)(c.G * .38), (int)(c.B * .38)), x + 1, y + 1, size - 2, size - 2);
            Rect(g, Color.FromArgb(a, c), x + 2, y + 2, size - 5, size - 5);
            Rect(g, Color.FromArgb((int)(120 * alpha), 255, 255, 245), x + 3, y + 3, size - 7, 2);
            Rect(g, Color.FromArgb((int)(40 * alpha), 0, 0, 0), x + 5, y + size - 8, size - 9, 3);
            Rect(g, Color.FromArgb((int)(70 * alpha), 255, 255, 255), x + 4, y + 6, 2, 2);
        }
        public static void Mini(Graphics g, PieceKind kind, float cx, float cy, int size, bool faded = false)
        {
            Cell[] cells = Pieces.Cells(kind, 0);
            int minX = 4, minY = 4, maxX = 0, maxY = 0;
            foreach (Cell c in cells) { minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X); minY = Math.Min(minY, c.Y); maxY = Math.Max(maxY, c.Y); }
            float x = cx - (maxX - minX + 1) * size / 2f, y = cy - (maxY - minY + 1) * size / 2f;
            foreach (Cell c in cells) Block(g, (int)kind, x + (c.X - minX) * size, y + (c.Y - minY) * size, size, false, faded ? .3f : 1);
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
            {'.',"00000000000000000000000000011000110"}
        };
        public static void Pixel(Graphics g, string text, float x, float y, int pixel, Color color)
        {
            foreach (char ch in text.ToUpperInvariant())
            {
                string bits;
                if (Glyphs.TryGetValue(ch, out bits))
                    for (int i = 0; i < 35; i++) if (bits[i] == '1') Rect(g, color, x + i % 5 * pixel, y + i / 5 * pixel, pixel, pixel);
                x += pixel * 6;
            }
        }
    }
}
