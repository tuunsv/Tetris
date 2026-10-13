using System;
using System.Drawing;
using Tetris118.Core;

namespace Tetris118.UI
{
    // Pixel-art tự vẽ bằng hình học. Mỗi boss có silhouette, mắt và chất liệu riêng.
    // Cache 12 bitmap, không vẽ lại hàng trăm chi tiết ở mỗi khung hình.
    internal sealed class BossPortraits : IDisposable
    {
        private readonly Bitmap[] portraits = new Bitmap[12];
        public Bitmap Get(BossId id)
        {
            int i = (int)id;
            if (portraits[i] == null) portraits[i] = Paint(id);
            return portraits[i];
        }
        private static readonly Color Gold = Color.FromArgb(184, 149, 77), Pale = Color.FromArgb(197, 181, 147),
            Shade = Color.FromArgb(65, 54, 46), Red = Color.FromArgb(120, 35, 28), Steel = Color.FromArgb(94, 103, 105);
        private static void R(Graphics g, Color c, int x, int y, int w, int h)
        { using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h); }
        private static void P(Graphics g, Color c, params int[] xy)
        {
            var points = new Point[xy.Length / 2]; for (int i = 0; i < points.Length; i++) points[i] = new Point(xy[i * 2], xy[i * 2 + 1]);
            using (var b = new SolidBrush(c)) g.FillPolygon(b, points);
        }
        private static void L(Graphics g, Color c, params int[] xy)
        {
            var points = new Point[xy.Length / 2]; for (int i = 0; i < points.Length; i++) points[i] = new Point(xy[i * 2], xy[i * 2 + 1]);
            using (var p = new Pen(c)) g.DrawLines(p, points);
        }
        private static void Face(Graphics g, Color skin)
        {
            P(g, Shade, 19,24, 49,24, 54,42, 47,59, 34,64, 23,57, 18,42);
            P(g, skin, 23,24, 46,24, 49,44, 44,55, 34,59, 25,52, 21,39);
            P(g, Color.FromArgb(skin.R * 2 / 3, skin.G * 2 / 3, skin.B * 2 / 3), 36,28, 46,27, 49,44, 44,55, 36,57);
            L(g, Shade, 33,36, 31,46, 36,47); L(g, Shade, 28,51, 36,53, 42,50);
            R(g, Shade, 24,36, 8,3); R(g, Shade, 38,36, 8,3);
            R(g, Gold, 26,37, 3,1); R(g, Gold, 40,37, 3,1);
        }
        private static void Hair(Graphics g, Color c, bool longHair)
        {
            P(g,c, 15,22, 22,12, 38,8, 52,19, 57,42, 65,69, 46,63, 43,24, 26,25, 23,63, 8,69, 16,44);
            for (int i=0;i<7;i++) L(g, Color.FromArgb(c.R/2,c.G/2,c.B/2), 17+i*6,23, 13+i*7,longHair?67:43);
        }
        private static void Horn(Graphics g, int x, int y, bool right, Color c)
        {
            int s=right?1:-1;
            P(g,c, x,y+15, x+s*13,y+7, x+s*15,y-5, x+s*7,y-12, x+s*10,y-3, x+s*7,y+4, x,y+8);
            L(g,Gold, x,y+12, x+s*10,y+6, x+s*12,y-2);
        }
        private static Bitmap Paint(BossId id)
        {
            var b = new Bitmap(72,72);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(9,10,12));
                // Vòng sáng đứt nét làm nền, giữ cùng khung cho đủ 12 chân dung.
                using(var pen=new Pen(Color.FromArgb(48,42,28))) { g.DrawEllipse(pen,8,3,55,63); g.DrawEllipse(pen,11,6,49,57); }
                P(g,Shade, 2,71, 12,59, 28,53, 45,53, 60,61, 70,71);
                switch(id)
                {
                    case BossId.Godrick:
                        Hair(g,Color.FromArgb(125,127,105),true); Face(g,Color.FromArgb(159,153,109));
                        P(g,Gold, 17,27, 14,12, 23,19, 27,6, 34,18, 40,5, 46,19, 56,12, 52,28);
                        R(g,Shade,20,23,31,4); R(g,Gold,21,24,29,1);
                        for(int i=0;i<4;i++) { L(g,Shade,25,42+i*2,30,43+i*2); L(g,Shade,39,41+i*2,45,40+i*2); }
                        P(g,Pale, 24,49, 29,56, 34,54, 43,49, 42,61, 32,70, 26,62);
                        for(int i=0;i<3;i++) L(g,Shade,29+i*4,54,27+i*4,65);
                        P(g,Color.FromArgb(113,110,78), 5,66, 8,39, 12,36, 13,52, 17,34, 20,34, 18,55, 23,44, 26,47, 20,67);
                        break;
                    case BossId.Malenia:
                        Hair(g,Color.FromArgb(141,48,31),true); Face(g,Color.FromArgb(209,182,150));
                        P(g,Gold, 22,18, 44,18, 49,32, 44,42, 37,42, 35,33, 31,42, 24,40, 20,30);
                        P(g,Shade, 25,32, 32,33, 30,36, 25,35); P(g,Shade, 38,33, 45,32, 45,35, 39,36);
                        P(g,Gold, 22,31, 8,25, 5,10, 15,17, 12,5, 24,21);
                        P(g,Gold, 46,31, 62,25, 68,10, 57,18, 60,5, 46,21);
                        L(g,Pale, 10,17, 18,24, 23,26); L(g,Pale, 63,17, 55,24, 47,26);
                        R(g,Pale,34,19,1,12); L(g,Red,30,51,36,52,41,50);
                        P(g,Gold,46,61,61,58,69,71,43,71); break;
                    case BossId.Rennala:
                        Hair(g,Color.FromArgb(24,27,38),true); Face(g,Color.FromArgb(190,190,180));
                        P(g,Color.FromArgb(44,61,84), 21,26, 17,8, 26,15, 36,2, 46,15, 54,8, 50,28);
                        L(g,Gold,18,9,24,26,49,26,53,9); L(g,Gold,27,17,36,4,45,17);
                        P(g,Gold, 32,14, 35,10, 40,13, 36,12, 34,15, 38,19, 34,18);
                        R(g,Color.FromArgb(117,166,204),26,37,3,1);R(g,Color.FromArgb(117,166,204),40,37,3,1);
                        P(g,Color.FromArgb(30,48,73), 8,70, 20,56, 34,62, 49,56, 65,70);
                        L(g,Gold,18,60,35,69,52,60); break;
                    case BossId.Radagon:
                        Hair(g,Color.FromArgb(153,44,25),true); Face(g,Color.FromArgb(149,121,82));
                        L(g,Gold,39,22,35,32,39,37,32,43,36,50,29,60);
                        L(g,Gold,36,34,27,29,22,32); L(g,Gold,34,44,45,46,49,52);
                        R(g,Color.FromArgb(245,205,113),40,36,4,2);
                        P(g,Color.FromArgb(64,57,42),18,58,31,61,38,58,57,67,61,71,10,71);
                        L(g,Gold,34,60,38,66,33,71); break;
                    case BossId.Radahn:
                        Hair(g,Color.FromArgb(139,48,26),true); Face(g,Color.FromArgb(135,94,64));
                        P(g,Gold,19,22,26,15,43,15,52,22,48,39,40,34,35,43,30,34,23,39);
                        Horn(g,22,16,false,Gold);Horn(g,48,16,true,Gold);
                        P(g,Shade,26,31,33,31,30,35);P(g,Shade,39,31,46,31,40,35);
                        for(int i=0;i<4;i++)L(g,Gold,26+i*6,52,21+i*9,69);
                        R(g,Gold,10,63,12,8);R(g,Gold,51,63,12,8); break;
                    case BossId.Margit:
                        Hair(g,Color.FromArgb(96,91,78),true); Face(g,Color.FromArgb(143,132,103));
                        Horn(g,23,16,false,Shade); Horn(g,48,15,true,Shade);Horn(g,26,28,false,Shade);
                        P(g,Shade,39,16,43,8,47,7,43,17,48,20,44,29,39,27);
                        R(g,Gold,25,36,4,2);R(g,Shade,39,34,8,6);
                        P(g,Pale,25,49,33,54,43,48,40,61,33,67,25,59);
                        P(g,Color.FromArgb(75,69,49),4,71,16,57,32,66,45,57,65,71);break;
                    case BossId.Ranni:
                        Hair(g,Color.FromArgb(94,130,148),true);Face(g,Color.FromArgb(112,160,179));
                        P(g,Color.FromArgb(193,199,185), 4,26,20,20,25,6,43,2,50,19,69,27,56,32,15,32);
                        P(g,Color.FromArgb(128,149,153), 27,8,39,5,47,21,22,21);
                        L(g,Pale,7,26,31,24,64,27);R(g,Color.FromArgb(66,90,113),20,21,33,3);
                        R(g,Shade,25,37,6,2);L(g,Shade,39,37,45,39);R(g,Color.FromArgb(177,235,245),26,37,2,2);
                        // Bóng mặt lệch sang bên với mắt thứ hai và vệt sáng dưới mắt.
                        L(g,Color.FromArgb(78,130,152),46,33,53,38,51,44,54,47,48,55);
                        R(g,Color.FromArgb(172,221,237),48,39,3,1);L(g,Color.FromArgb(172,221,237),42,39,42,48);
                        P(g,Pale,11,71,19,57,33,63,45,58,61,71);break;
                    case BossId.Godfrey:
                        Hair(g,Color.FromArgb(173,169,145),true);Face(g,Color.FromArgb(170,143,109));
                        P(g,Gold,20,28,21,21,28,24,35,17,43,24,50,21,49,29);R(g,Pale,23,26,24,1);
                        P(g,Pale,22,46,30,50,35,48,42,50,49,44,45,61,34,71,24,61);
                        for(int i=0;i<6;i++)L(g,Color.FromArgb(111,108,94),24+i*4,51,28+i*2,65);
                        P(g,Steel,2,70,10,57,20,60,27,71);P(g,Gold,47,64,61,55,70,64,69,71,49,71);
                        R(g,Pale,60,61,3,2);L(g,Shade,56,65,63,67,67,64);break;
                    case BossId.Gideon:
                        P(g,Steel,17,24,25,13,43,13,54,25,50,53,35,64,21,53);
                        P(g,Shade,20,29,49,29,46,50,35,59,25,49);
                        for(int row=0;row<4;row++) for(int col=0;col<3;col++)
                        { int x=23+col*9,y=25+row*8;P(g,Gold,x,y+2,x+3,y,x+6,y+2,x+3,y+4);R(g,Color.Black,x+3,y+1,1,3); }
                        L(g,Pale,19,25,25,17,43,17,51,25);P(g,Steel,8,71,15,59,34,66,54,59,66,71);
                        L(g,Gold,14,64,34,70,58,64);break;
                    case BossId.Maliketh:
                        Hair(g,Color.FromArgb(133,133,122),true);
                        P(g,Color.FromArgb(32,33,34),15,15,26,23,45,23,57,14,53,41,47,55,35,64,22,55,17,40);
                        P(g,Gold,19,16,26,27,35,23,45,27,54,16,48,38,41,35,35,45,29,35,21,38);
                        P(g,Shade,27,40,43,40,48,49,35,59,22,49);
                        R(g,Color.FromArgb(243,179,77),25,35,4,2);R(g,Color.FromArgb(243,179,77),41,35,4,2);
                        P(g,Color.Black,30,46,40,46,36,50);R(g,Color.Black,28,53,15,4);
                        P(g,Pale,27,52,30,53,29,59);P(g,Pale,42,52,39,53,40,59);
                        L(g,Gold,14,62,25,69,33,65,44,69,58,61);break;
                    case BossId.Mohg:
                        Face(g,Color.FromArgb(91,83,77));
                        Horn(g,22,18,false,Shade);Horn(g,47,18,true,Shade);Horn(g,28,11,false,Shade);Horn(g,42,10,true,Shade);
                        P(g,Shade,43,17,53,30,49,43,39,43,35,33,39,28,43,34,46,30);
                        R(g,Color.FromArgb(239,93,61),25,37,4,2);
                        P(g,Pale,25,48,29,53,30,48,35,55,39,48,42,53,45,47,42,59,28,59);
                        P(g,Red,4,71,13,54,24,58,35,66,49,56,61,56,70,71);
                        L(g,Gold,14,57,24,68,35,70,49,65,60,58);break;
                    case BossId.Morgott:
                        Hair(g,Color.FromArgb(143,141,123),true);Face(g,Color.FromArgb(127,125,105));
                        Horn(g,21,17,false,Shade);Horn(g,49,26,true,Shade);
                        P(g,Shade,23,23,22,9,26,5,26,17,32,23,31,29);
                        L(g,Gold,27,27,34,24,40,27,46,24);R(g,Gold,27,37,2,1);
                        P(g,Pale,23,46,31,52,37,50,47,45,42,62,34,70,26,61);
                        for(int i=0;i<4;i++)L(g,Shade,26+i*5,52,29+i*3,66);
                        P(g,Color.FromArgb(76,68,47),3,71,15,55,25,65,36,71);P(g,Color.FromArgb(91,83,61),46,63,60,54,70,71,36,71);break;
                }
                // Điểm sáng/độ sần chỉ ở phần giáp dưới, không rải noise lên mắt.
                var random=new Random(118+(int)id);
                for(int i=0;i<28;i++) { int x=random.Next(8,64),y=random.Next(62,72);R(g,Color.FromArgb(45,Gold),x,y,1,1); }
            }
            return b;
        }
        public void Dispose() { foreach(var b in portraits) if(b!=null) b.Dispose(); }
    }
}
