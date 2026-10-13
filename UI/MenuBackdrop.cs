using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Tetris118.UI
{
    public sealed partial class MainForm
    {
        private Bitmap menuBackdrop;
        private void DrawBackdrop(Graphics g)
        {
            if (page == "play") { DrawBackdropOld(g); return; }
            if (menuBackdrop == null) menuBackdrop = BuildMenuBackdrop();
            g.DrawImageUnscaled(menuBackdrop, 0, 0);
            foreach (int x in new[] { 51, 333 })
            {
                int sway = (int)Math.Round(Math.Sin(visualTime * 5.2 + x) * 2);
                using (var glow = new SolidBrush(Color.FromArgb(16, 221, 154, 59))) g.FillEllipse(glow, x - 14, 183, 28, 34);
                MenuPolygon(g, Color.FromArgb(149,83,29), x-4,205, x-5,198, x+sway,184, x+2,196, x+5,200, x+3,205);
                MenuPolygon(g, Color.FromArgb(227,176,79), x-2,204, x-2,198, x+sway,192, x+2,201, x+1,205);
                R(g, Color.FromArgb(255,223,144), x, 200, 1, 4);
            }
            for (int i = 0; i < 24; i++)
            {
                double travel = (visualTime * (5 + i % 4) + i * 17) % 115;
                int side = i % 2 == 0 ? 51 : 333;
                int x = side + (int)(Math.Sin(visualTime * .65 + i * 2.1) * 17);
                int light = 65 + (int)(70 * (1 - travel / 115));
                R(g, Color.FromArgb(light,light*4/5,light/3), x, 203-(int)travel, 1, 1);
            }
            for (int i = 0; i < 3; i++)
            {
                int drift = (int)(Math.Sin(visualTime * .18 + i * 2) * 28);
                using (var fog = new SolidBrush(Color.FromArgb(8,147,151,139)))
                { g.FillEllipse(fog,-60+drift,260+i*4,310,12); g.FillEllipse(fog,210-drift,257+i*5,230,12); }
            }
            // Dùng chung cảnh cho mọi menu, hạ độ sáng ở màn nhiều chữ.
            if (page != "home") using (var shade = new SolidBrush(Color.FromArgb(185,0,0,0)))
                g.FillRectangle(shade,14,14,CanvasW-28,CanvasH-28);
        }
        private Bitmap BuildMenuBackdrop()
        {
            var bitmap = new Bitmap(CanvasW,CanvasH);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.None;
                g.SetClip(new Rectangle(14,14,CanvasW-28,CanvasH-28));
                Color stone=Color.FromArgb(33,35,33), edge=Color.FromArgb(78,72,54), gold=Color.FromArgb(126,103,57);
                using (var pen=new Pen(Color.FromArgb(31,30,25),3))
                    g.DrawLines(pen,new[] {new Point(79,247),new Point(79,112),new Point(111,60),new Point(153,29),new Point(192,16),new Point(231,29),new Point(273,60),new Point(305,112),new Point(305,247)});
                foreach(int x in new[] {31,307})
                {
                    MenuPolygon(g,stone,x,190,x,89,x+10,67,x+20,54,x+30,67,x+40,89,x+40,190);
                    MenuPolygon(g,Color.FromArgb(5,8,10),x+5,186,x+5,91,x+12,74,x+20,63,x+28,74,x+35,91,x+35,186);
                    using(var pen=new Pen(edge))
                    {
                        g.DrawLines(pen,new[]{new Point(x+2,188),new Point(x+2,90),new Point(x+11,69),new Point(x+20,57),new Point(x+29,69),new Point(x+38,90),new Point(x+38,188)});
                        g.DrawLine(pen,x+20,66,x+20,185);g.DrawLine(pen,x+5,111,x+35,111);
                    }
                    using(var pen=new Pen(gold))g.DrawPolygon(pen,new[]{new Point(x+20,82),new Point(x+27,96),new Point(x+20,109),new Point(x+13,96)});
                    R(g,edge,x-3,190,46,3);R(g,stone,x-5,193,50,5);
                }
                DrawMenuTree(g,93,255,-1.70,57,5);DrawMenuTree(g,291,255,-1.44,57,5);
                foreach(int x in new[]{18,348})
                {
                    R(g,stone,x,44,18,215);R(g,edge,x+2,46,1,210);R(g,Color.FromArgb(12,14,14),x+12,46,4,210);
                    for(int y=57;y<250;y+=18){R(g,Color.FromArgb(15,17,17),x+3,y,12,1);R(g,Color.FromArgb(52,51,43),x+4,y+1,7,1);}
                    R(g,edge,x-3,38,24,3);R(g,stone,x-1,41,20,5);R(g,edge,x-3,259,24,3);R(g,stone,x-5,262,28,5);
                    for(int k=0;k<3;k++)
                    {int cx=x+3+k*5;using(var pen=new Pen(gold))g.DrawPolygon(pen,new[]{new Point(cx,28),new Point(cx+2,32),new Point(cx,36),new Point(cx-2,32)});}
                }
                foreach(int x in new[]{51,333})
                {
                    R(g,edge,x-6,206,12,3);MenuPolygon(g,stone,x-5,209,x+5,209,x+2,216,x-2,216);
                    R(g,gold,x-1,215,2,27);R(g,stone,x-6,242,12,4);R(g,edge,x-9,246,18,2);
                }
                for(int i=0;i<3;i++)
                {
                    int y=261+i*5;R(g,Color.FromArgb(27+i*3,28+i*3,25),70-i*15,y,244+i*30,1);
                    for(int x=87-i*10;x<320;x+=42)R(g,Color.FromArgb(18,19,18),x,y+1,1,4);
                }
                using(var path=new GraphicsPath())
                {
                    path.AddEllipse(91,16,202,266);
                    using(var shade=new PathGradientBrush(path))
                    {shade.CenterPoint=new PointF(192,153);shade.CenterColor=Color.FromArgb(225,0,0,0);shade.SurroundColors=new[]{Color.FromArgb(0,0,0,0)};g.FillPath(shade,path);}
                }
            }
            return bitmap;
        }
        private static void DrawMenuTree(Graphics g,double x,double y,double angle,double length,int depth)
        {
            double ex=x+Math.Cos(angle)*length,ey=y+Math.Sin(angle)*length;
            using(var pen=new Pen(Color.FromArgb(81,65,37),Math.Max(1,depth)))g.DrawLine(pen,(float)x,(float)y,(float)ex,(float)ey);
            using(var pen=new Pen(Color.FromArgb(141,111,56)))g.DrawLine(pen,(float)x-1,(float)y,(float)ex-1,(float)ey);
            if(depth<=1)
            {
                int px=(int)ex,py=(int)ey;
                MenuPolygon(g,Color.FromArgb(113,92,45),px-6,py,px-3,py-5,px+3,py-6,px+7,py-1,px+3,py+4,px-3,py+3);
                R(g,Color.FromArgb(164,134,69),px-2,py-3,3,1);R(g,Color.FromArgb(142,112,53),px+2,py,2,1);return;
            }
            DrawMenuTree(g,ex,ey,angle-.48,length*.72,depth-1);DrawMenuTree(g,ex,ey,angle+.57,length*.68,depth-1);
        }
        private static void MenuPolygon(Graphics g,Color color,params int[] xy)
        {
            var points=new Point[xy.Length/2];for(int i=0;i<points.Length;i++)points[i]=new Point(xy[i*2],xy[i*2+1]);
            using(var b=new SolidBrush(color))g.FillPolygon(b,points);
        }
    }
}
