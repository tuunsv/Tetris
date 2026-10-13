using System;
using System.IO;
using System.Linq;
using Tetris118.Core;
using Tetris118.Data;

internal static class RulesTests
{
    private static int passed, failed;
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action run)
    {
        try { run(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception ex) { Console.WriteLine("FAIL " + name + " :: " + ex.Message); failed++; }
    }
    private static GameEngine Game() { return new GameEngine(GameMode.Marathon, 118); }
    private static void Time(GameEngine game, double seconds)
    { for (int i = 0; i < (int)Math.Round(seconds * 120); i++) game.Tick(1.0 / 120, false); }
    private static void Empty(GameEngine game) { Array.Clear(game.Board, 0, game.Board.Length); }
    private static void TetrisSetup(GameEngine game, bool leaveExtra)
    {
        Empty(game);
        for (int y = 36; y < 40; y++) for (int x = 0; x < 10; x++) if (x != 4) game.Board[y, x] = 2;
        if (leaveExtra) game.Board[35, 0] = 3;
        game.SetActiveForTest(PieceKind.I, 2, 36, 1, false);
    }
    private static void SingleSetup(GameEngine game)
    {
        Empty(game);
        for (int x = 0; x < 10; x++) if (x < 3 || x > 6) game.Board[39, x] = 2;
        game.Board[38, 0] = 4;
        game.SetActiveForTest(PieceKind.I, 3, 38, 0, false);
    }
    public static int Main()
    {
        Test("Seven-bag: 1000 bags contain all seven pieces", delegate {
            var bag = new SevenBag(118);
            for (int i = 0; i < 1000; i++) { var found = new System.Collections.Generic.HashSet<PieceKind>(); for (int j = 0; j < 7; j++) found.Add(bag.Take()); Assert(found.Count == 7, "bad bag"); }
        });
        Test("Same seed produces same 1000 pieces", delegate {
            var a = new SevenBag(118); var b = new SevenBag(118);
            for (int i = 0; i < 1000; i++) Assert(a.Take() == b.Take(), "seed");
        });
        Test("Preview never consumes queue and includes 5", delegate {
            var b = new SevenBag(13); var p = b.Preview(); Assert(p.Length >= 5, "short"); Assert(b.Preview()[0] == b.Take(), "consumed");
        });
        Test("All rotation states have correct cell count", delegate {
            foreach (PieceKind p in Enum.GetValues(typeof(PieceKind))) for (int r = 0; r < 4; r++)
                Assert(Pieces.Cells(p,r).Select(c => c.X + 10*c.Y).Distinct().Count() == ((int)p <= 7 ? 4 : 5),"shape");
        });
        Test("Four rotations restore T shape", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.T,3,24,0,false);
            for(int i=0;i<4;i++) Assert(g.Rotate(1),"rotate"); Assert(g.Rotation==0 && g.X==3 && g.Y==24,"cycle");
        });
        Test("O rotation does not drift", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.O,3,24,0,false); g.Rotate(1);
            Assert(g.X==3 && g.Y==24 && g.Rotation==0,"O drift");
        });
        Test("I wall kick from right wall", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.I,7,25,1,false);
            Assert(g.Rotate(1) && g.X==6 && g.Rotation==2,"I kick");
        });
        Test("I floor kick uses upward Y conversion", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.I,3,38,0,false);
            Assert(g.Rotate(1) && g.Y==36 && g.X==4,"floor kick");
        });
        Test("T floor kick", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.T,3,38,0,false);
            Assert(g.Rotate(1) && g.Y==37 && g.X==2,"T floor");
        });
        Test("Left wall prevents movement outside board", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.T,0,25,0,false); Assert(!g.Move(-1) && g.X==0,"wall");
        });
        Test("Blocked rotation leaves state unchanged", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.T,3,25,0,false);
            for(int y=20;y<32;y++)for(int x=0;x<10;x++)g.Board[y,x]=1;
            foreach(Cell c in Pieces.Cells(PieceKind.T,0))g.Board[25+c.Y,3+c.X]=0;
            Assert(!g.Rotate(1) && g.Rotation==0 && g.X==3,"invalid rotate");
        });
        Test("Hold allowed once before lock", delegate {
            var g=Game(); var first=g.Current; g.Hold(); var active=g.Current; g.Hold();
            Assert(g.Held==first && g.Current==active && g.HoldUsed,"hold twice");
        });
        Test("Hold re-enabled after lock, swap resets rotation", delegate {
            var g=Game(); var first=g.Current; g.Hold(); g.HardDrop(); g.Rotate(1); g.Hold();
            Assert(g.Current==first && g.Rotation==0 && g.HoldUsed,"hold reset");
        });
        Test("Ghost is valid with blocked row below", delegate {
            var g=Game(); int y=g.GhostY(); Assert(g.Fits(g.Current,g.Rotation,g.X,y) && !g.Fits(g.Current,g.Rotation,g.X,y+1),"ghost");
        });
        Test("Hard drop scores two per cell", delegate {
            var g=Game(); int distance=g.GhostY()-g.Y; g.HardDrop(); Assert(g.Score==distance*2 && g.PiecesPlaced==1,"drop points");
        });
        Test("Soft drop adds score without immediate lock", delegate {
            var g=Game(); g.Tick(.05,true); Assert(g.Score==2 && g.PiecesPlaced==0,"soft");
        });
        Test("500 ms lock delay", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.O,3,38,0,false); Time(g,.4); Assert(g.PiecesPlaced==0,"early"); Time(g,.11); Assert(g.PiecesPlaced==1,"late");
        });
        Test("Lock reset limited to 15 grounded moves", delegate {
            var g=Game(); g.SetActiveForTest(PieceKind.O,3,38,0,false);
            for(int i=0;i<15;i++){Time(g,.1);g.Move(i%2==0?1:-1);}
            Assert(g.PiecesPlaced==0,"reset failed"); Time(g,.4);g.Move(-1);Time(g,.12);Assert(g.PiecesPlaced==1,"infinite resets");
        });
        Test("Pause stops clock and commands", delegate {
            var g=Game(); int x=g.X,y=g.Y;g.Paused=true;Time(g,3);g.Move(1);g.HardDrop();g.Hold();
            Assert(g.Elapsed==0 && g.X==x && g.Y==y && g.PiecesPlaced==0 && !g.HoldUsed,"pause");
        });
        Test("Single scores 100 and compacts rows", delegate {
            var g=Game();SingleSetup(g);g.HardDrop();Assert(g.Score==100 && g.Lines==1 && g.Board[39,0]==4,"single");
        });
        Test("Tetris scores 800 and clears 4 consecutive rows", delegate {
            var g=Game();TetrisSetup(g,true);g.HardDrop();Assert(g.Score==800 && g.Lines==4 && g.Tetrises==1 && g.Board[39,0]==3,"tetris");
        });
        Test("B2B + combo bonus on consecutive Tetrises", delegate {
            var g=Game();TetrisSetup(g,true);g.HardDrop();TetrisSetup(g,true);g.HardDrop();Assert(g.Score==2050 && g.Combo==1 && g.BackToBack,"b2b");
        });
        Test("No clear preserves B2B but resets combo", delegate {
            var g=Game();TetrisSetup(g,true);g.HardDrop();Empty(g);g.SetActiveForTest(PieceKind.O,3,38,0,false);g.HardDrop();Assert(g.BackToBack && g.Combo==-1,"chain");
        });
        Test("Ordinary clear breaks B2B", delegate {
            var g=Game();TetrisSetup(g,true);g.HardDrop();SingleSetup(g);g.HardDrop();Assert(!g.BackToBack,"ordinary clear");
        });
        Test("Four-line perfect clear adds 2000", delegate {
            var g=Game();TetrisSetup(g,false);g.HardDrop();Assert(g.Score==2800 && g.PerfectClears==1,"pc");
        });
        Test("Level increases after 10 lines", delegate {
            var g=Game();for(int i=0;i<10;i++){SingleSetup(g);g.HardDrop();}Assert(g.Lines==10 && g.Level==2,"level");
        });
        Test("Full T-spin zero-line recognized", delegate {
            var g=Game();g.Board[37,3]=1;g.Board[37,5]=1;g.Board[39,3]=1;
            g.SetActiveForTest(PieceKind.T,3,37,0,true);g.HardDrop();Assert(g.TSpins==1 && g.Score==400,"spin");
        });
        Test("Mini T-spin recognized", delegate {
            var g=Game();g.Board[37,3]=1;g.Board[39,3]=1;g.Board[39,5]=1;
            g.SetActiveForTest(PieceKind.T,3,37,0,true);g.HardDrop();Assert(g.TSpins==1 && g.Score==100,"mini");
        });
        Test("T with three corners but no last rotation is not spin", delegate {
            var g=Game();g.Board[37,3]=1;g.Board[37,5]=1;g.Board[39,3]=1;
            g.SetActiveForTest(PieceKind.T,3,37,0,false);g.HardDrop();Assert(g.TSpins==0,"false spin");
        });
        Test("Sprint wins at 40 lines", delegate {
            var g=new GameEngine(GameMode.Sprint,118);for(int i=0;i<10;i++){TetrisSetup(g,true);g.HardDrop();}Assert(g.Won && g.Finished && g.Lines==40,"sprint");
        });
        Test("Ultra ends exactly at 120 seconds", delegate {
            var g=new GameEngine(GameMode.Ultra,118);
            for(int i=0;i<14401 && !g.Finished;i++){Empty(g);g.SetActiveForTest(PieceKind.O,3,20,0,false);g.Tick(1.0/120,false);}
            Assert(g.Won && g.Elapsed==120,"ultra");
        });
        Test("Spawn collision tops out", delegate {
            var g=Game();for(int y=16;y<22;y++)for(int x=0;x<10;x++)g.Board[y,x]=1;g.Hold();Assert(g.Finished && !g.Won,"block out");
        });
        Test("Fully hidden lock tops out", delegate {
            var g=Game();g.Board[20,4]=1;g.Board[20,5]=1;g.SetActiveForTest(PieceKind.O,3,18,0,false);g.HardDrop();Assert(g.Finished,"lock out");
        });
        Test("Finished game ignores input", delegate {
            var g=Game();for(int y=16;y<22;y++)for(int x=0;x<10;x++)g.Board[y,x]=1;g.Hold();int n=g.PiecesPlaced;g.HardDrop();g.Move(1);g.Rotate(1);Assert(g.PiecesPlaced==n,"post-game");
        });
        Test("Settings validates out-of-range data", delegate {
            var s=new Settings{Music=900,Effects=-10,Das=1,Arr=999,Player=""};s.Validate();Assert(s.Music==100 && s.Effects==0 && s.Das==80 && s.Arr==100 && s.Player.Length>0,"validate");
        });
        Test("Sprint ranking prioritizes finished then faster", delegate {
            var p=new Profile();p.Records.Add(new Record{Mode="Sprint",Won=false,Seconds=1,Lines=39});
            p.Records.Add(new Record{Mode="Sprint",Won=true,Seconds=100});p.Records.Add(new Record{Mode="Sprint",Won=true,Seconds=80});
            Assert(p.Ranking(GameMode.Sprint).First().Seconds==80,"rank");
        });
        Test("Save/load roundtrip, backup recovery, invalid file fallback", delegate {
            string folder=Path.Combine(Path.GetTempPath(),"Tetris118-test-"+Guid.NewGuid());
            try {
                var store=new SaveStore(folder);var profile=new Profile();profile.Settings.Player="TEST 118";
                Assert(store.Save(profile),"first save");profile.Games=2;Assert(store.Save(profile),"second save");
                Assert(store.Load().Games==2 && store.Load().Settings.Player=="TEST 118","roundtrip");
                File.WriteAllText(Path.Combine(folder,"profile.json"),"{bad");
                Assert(store.Load().Games==0 && store.Warning!=null,"backup");
                File.WriteAllText(Path.Combine(folder,"profile.json.bak"),"bad");Assert(store.Load().Settings!=null,"fallback");
                Assert(store.Save(new Profile()),"repair save");Assert(Directory.GetFiles(folder,"*.damaged-*").Length==1,"preserve damaged");
            } finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        });
        Test("Random input stress keeps active piece valid", delegate {
            for(int seed=0;seed<30;seed++){
                var r=new Random(seed);var g=new GameEngine(GameMode.Marathon,seed);
                for(int i=0;i<4000 && !g.Finished;i++){
                    switch(r.Next(8)){case 0:g.Move(-1);break;case 1:g.Move(1);break;case 2:g.Rotate(1);break;case 3:g.Rotate(-1);break;case 4:g.Hold();break;case 5:g.HardDrop();break;default:g.Tick(1.0/120,true);break;}
                    if(!g.Finished)Assert(g.Fits(g.Current,g.Rotation,g.X,g.Y),"overlap seed "+seed);
                }
            }
        });
        BossTests.Run(Test);
        Console.WriteLine("\n"+passed+" passed / "+failed+" failed");return failed==0?0:1;
    }
}
