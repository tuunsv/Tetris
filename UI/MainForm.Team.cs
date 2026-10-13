using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Tetris118.UI
{
    // Them file nay vao UI. Trong switch(page) cua OnPaint:
    // case "home": DrawHomeWithTeam(g); break;
    // case "team": DrawNoCTeam(g); break;
    public sealed partial class MainForm
    {
        // Sua ten/MSSV tai day; khong can sua code ve giao dien.
        private static readonly string[,] NoCMembers =
        {
            { "Phan Công Minh Chí",       "51.01.104.008" },
            { "Đặng Trung Hiếu",          "51.01.104.026" },
            { "Đoàn Viễn Phương",         "51.01.104.079" },
            { "Phan Nguyễn Thành Tuân",   "51.01.104.112" }
        };

        private void DrawHomeWithTeam(Graphics g)
        {
            // Giu logo cu; 7 nut cach nhau 16px, hitbox cao 15px.
            // Nut cuoi ket thuc o y=248, van nam trong khung 384x288.
            Nes.Text(g, "TETRIS", 89, 46, 6, Nes.Red);
            Center(g, "TETRIS", 42, 6, Nes.Yellow);
            Nes.Text(g, "118", 160, 103, 4, Nes.Red);
            Center(g, "118", 100, 4, Nes.White);

            HomeMenu(g, "PLAY",        141, delegate { Go("modes"); });
            HomeMenu(g, "LEADERBOARD", 157, delegate { Go("records"); });
            HomeMenu(g, "STATISTICS",  173, delegate { Go("stats"); });
            HomeMenu(g, "SETTINGS",    189, delegate { Go("settings"); });
            HomeMenu(g, "HOW TO PLAY", 205, delegate { Go("help"); });
            HomeMenu(g, "TEAM",        221, delegate { Go("team"); });
            HomeMenu(g, "EXIT",        237, Close);
        }

        private void DrawNoCTeam(Graphics g)
        {
            // Nen/khung ben ngoai van do OnPaint ve nhu cac menu khac.
            // Luu va tra lai trang thai Graphics de khong anh huong game.
            GraphicsState saved = g.Save();
            try
            {
                g.SetClip(new Rectangle(24, 24, 336, 207), CombineMode.Intersect);
                using (var shade = new SolidBrush(Color.FromArgb(224, 8, 8, 7)))
                    g.FillRectangle(shade, 32, 29, 320, 199);

                Center(g, "NO C", 39, 3, Nes.Yellow);
                Center(g, "TEAM MEMBERS", 68, 1, Nes.White);

                using (var border = new Pen(Color.FromArgb(112, 92, 49)))
                using (var nameBrush = new SolidBrush(Color.FromArgb(235, 226, 201)))
                using (var nameFont = new Font("Arial", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Near;
                    format.LineAlignment = StringAlignment.Center;
                    format.FormatFlags = StringFormatFlags.NoWrap;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    for (int i = 0; i < NoCMembers.GetLength(0); i++)
                    {
                        int y = 85 + i * 36;
                        g.DrawRectangle(border, 43, y, 298, 31);

                        // So thu tu va MSSV dung font pixel; ten dung Arial
                        // vi font pixel cu khong co dau tieng Viet.
                        T(g, (i + 1).ToString("00"), 53, y + 12, 1, Nes.Yellow);
                        g.DrawString(NoCMembers[i, 0], nameFont, nameBrush,
                            new RectangleF(78, y + 2, 252, 16), format);
                        T(g, NoCMembers[i, 1], 79, y + 21, 1, Nes.Gray);
                    }
                }
            }
            finally
            {
                g.Restore(saved);
            }

            // Dung chung hitbox, ban phim va SFX cua menu hien tai.
            // Nut nam tren vung thong bao loi y=248, khong bi che.
            HomeMenu(g, "BACK", 236, delegate { Go("home"); });
        }
    }
}
