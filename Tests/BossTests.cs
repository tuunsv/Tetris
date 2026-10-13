using System;
using System.Linq;
using Tetris118.Core;

internal static class BossTests
{
    private static void A(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static GameEngine G(BossId id)
    { var g = new GameEngine(GameMode.BossRush, 118); g.Rush.SelectForTest(id); g.Rush.ChallengeForTest(0); return g; }
    private static void Empty(GameEngine g) { Array.Clear(g.Board, 0, g.Board.Length); }
    private static void Single(GameEngine g)
    {
        Empty(g); for (int x = 0; x < 10; x++) if (x < 3 || x > 6) g.Board[39, x] = 2;
        g.Board[38, 0] = 3; g.SetActiveForTest(PieceKind.I, 3, 38, 0, false); g.HardDrop();
    }
    public static void Run(Action<string, Action> test)
    {
        test("V2: 18 named challenges, 30-120 seconds, no T-spin goal", delegate {
            var all = ChallengeDefinition.All;
            A(all.Length == 18 && all.Select(c => c.Id).Distinct().Count() == 18, "IDs");
            A(all.Select(c => c.Name).Distinct().Count() == 18, "Names");
            A(all.All(c => c.Seconds >= 30 && c.Seconds <= 120 && !c.Description.ToUpperInvariant().Contains("T-SPIN")), "Limits");
        });
        test("V2: each challenge accepts its distinct success condition", delegate {
            var g = new GameEngine(GameMode.Marathon, 118);
            for (int id = 0; id < 18; id++) {
                Empty(g); var c = new ChallengeState(ChallengeDefinition.All[id]);
                Action<int, PieceKind, bool> add = (n,p,e) => c.Update(g,n,p,e,30);
                switch(id) {
                    case 0: case 11: case 17: add(4,PieceKind.I,false);break;
                    case 1: for(int i=0;i<3;i++)add(1,PieceKind.I,false);break;
                    case 2: add(2,PieceKind.O,false);break;
                    case 3: for(int i=0;i<12;i++)add(i==0?1:0,PieceKind.I,false);break;
                    case 4: add(1,PieceKind.I,false);add(1,PieceKind.O,false);add(1,PieceKind.T,false);break;
                    case 5: for(int i=0;i<6;i++)add(0,PieceKind.I,false);break;
                    case 6: add(3,PieceKind.I,false);break;
                    case 7: add(4,PieceKind.I,false);break;
                    case 8: for(int i=0;i<3;i++)add(1,PieceKind.I,false);break;
                    case 9: add(1,PieceKind.I,false);add(2,PieceKind.O,false);break;
                    case 10: for(int i=0;i<3;i++)add(1,PieceKind.I,true);break;
                    case 12: add(1,PieceKind.I,false);add(2,PieceKind.O,false);add(3,PieceKind.I,false);break;
                    case 13: add(4,PieceKind.I,false);add(0,PieceKind.O,false);add(4,PieceKind.I,false);break;
                    case 14: for(int i=0;i<4;i++)add(1,PieceKind.I,false);break;
                    case 15: case 16: add(1,PieceKind.I,false);break;
                }
                A(c.Complete,"Challenge " + id);
            }
        });
        test("V2: challenge restrictions fail only their matching challenge", delegate {
            var hold=new ChallengeState(ChallengeDefinition.All[11]);hold.OnHold();A(hold.Failed,"Hold restriction");
            var drop=new ChallengeState(ChallengeDefinition.All[17]);drop.OnHardDrop();A(drop.Failed,"Drop restriction");
            var normal=new ChallengeState(ChallengeDefinition.All[0]);normal.OnHold();normal.OnHardDrop();A(!normal.Failed,"Unrelated challenge");
        });
        test("V2: combo and consecutive-Tetris goals reset correctly", delegate {
            var g=new GameEngine(GameMode.Marathon,1);var c=new ChallengeState(ChallengeDefinition.All[8]);
            c.Update(g,1,PieceKind.I,false,30);c.Update(g,0,PieceKind.I,false,30);c.Update(g,1,PieceKind.I,false,30);A(c.Progress==1,"Combo reset");
            c=new ChallengeState(ChallengeDefinition.All[13]);c.Update(g,4,PieceKind.I,false,30);c.Update(g,1,PieceKind.O,false,30);c.Update(g,4,PieceKind.I,false,30);A(c.Progress==1,"Tetris reset");
        });
        test("V2: three unique bosses and 1-2 boss HP", delegate {
            for(int seed=0;seed<200;seed++) {
                var g=new GameEngine(GameMode.BossRush,seed);
                A(g.Rush.RouteForTest.Distinct().Count()==3,"Duplicate route");
                A(g.Rush.BossHealth>=1 && g.Rush.BossHealth<=2,"Boss HP range");
            }
        });
        test("V2: one challenge deals exactly one boss damage", delegate {
            var g=G(BossId.Godrick);int hp=g.Rush.BossHealth;
            for(int i=0;i<4;i++)Single(g);
            A(g.Rush.BossHealth==hp-1,"Damage");
            A(g.Rush.Defeated==(hp==1?1:0),"Defeat count");
        });
        test("V2: level carries over after board reset", delegate {
            var g=G(BossId.Ranni);
            g.Rush.SelectForTest(BossId.Ranni);
            for(int i=0;i<10;i++)Single(g);
            A(g.Level==2,"Initial level");g.Rush.SelectForTest(BossId.Godfrey);
            A(g.Level==2 && g.Rush.StageLines==0,"Carry-over");
        });
        test("V2: Godfrey hides only active piece and deals two HP", delegate {
            var g=G(BossId.Godfrey);A(g.Rush.HideFalling && !g.Rush.HoldBlocked && g.Next.Length>=5,"Helpers");
            var seq=new System.Collections.Generic.List<PieceKind>();seq.Add(g.Current);
            for(int i=0;i<6;i++){Empty(g);g.HardDrop();seq.Add(g.Current);}
            A(seq.Distinct().Count()==7,"Normal seven-bag");
            g.Rush.AttackForTest(BossId.Godfrey);A(g.Rush.Health==3,"Damage");
        });
        test("V2: Maliketh has one HP, no shield and plain lethal damage", delegate {
            var g=G(BossId.Maliketh);A(g.Rush.Health==1,"Entry HP");g.Rush.AttackForTest(BossId.Maliketh);A(g.Finished,"No shield");
        });
        test("V2: Radahn pushes stack by three solid unbreakable rows", delegate {
            var g=G(BossId.Radahn);Empty(g);g.Board[38,0]=1;int y=g.Y;
            g.Rush.AttackForTest(BossId.Radahn);
            A(!g.Finished && g.Board[35,0]==1 && g.Y==y-3,"Push");
            A(g.Board.Cast<int>().Count(v=>v==99)==30,"Solid rows");
            for(int row=37;row<40;row++)for(int x=0;x<10;x++)A(g.Board[row,x]==99,"Hole in solid floor");
        });
        test("V2: Morgott pushes breakable rows, blocks Hold, no gravity boost", delegate {
            var g=G(BossId.Morgott);Empty(g);g.Board[38,0]=1;g.Rush.AttackForTest(BossId.Morgott);
            A(g.Board[36,0]==1 && g.Board.Cast<int>().Count(v=>v==20)==18,"Garbage push");
            A(g.Rush.HoldBlocked && g.Rush.GravityScale==1 && !g.Rush.MaxGravity,"Morgott rules");
            g.Hold();A(!g.Held.HasValue,"Hold blocked");
        });
        test("V2: marked garbage is breakable and counted for siege", delegate {
            var g=G(BossId.Godfrey);Empty(g);g.PushGarbage(2,false,true);A(g.MarkedGarbageRows==2,"Marked rows");
            Empty(g);g.Board[39,0]=98;A(g.MarkedGarbageRows==1,"Row count");
        });
        test("V2: Mohg executes at two HP, otherwise deals one", delegate {
            var g=G(BossId.Mohg);
            for(int i=0;i<3;i++)g.Rush.AttackForTest(BossId.Mohg);
            A(g.Rush.Health==2 && !g.Finished,"Nonlethal above 2");g.Rush.AttackForTest(BossId.Mohg);A(g.Finished && g.Rush.Health==0,"Execution");
        });
        test("V2: pause freezes challenge deadline", delegate {
            var g=G(BossId.Godfrey);g.Paused=true;double t=g.Rush.Remaining;
            for(int i=0;i<120;i++)g.Tick(1.0/120,false);A(g.Rush.Remaining==t,"Paused timer");
        });
        test("V2: all boss inputs keep active position valid", delegate {
            foreach(BossId id in Enum.GetValues(typeof(BossId))) {
                var g=G(id);var rng=new Random(900+(int)id);
                for(int i=0;i<3000 && !g.Finished;i++) {
                    switch(rng.Next(8)){case 0:g.Move(-1);break;case 1:g.Move(1);break;case 2:g.Rotate(1);break;case 3:g.Rotate(-1);break;case 4:g.Hold();break;case 5:g.HardDrop();break;default:g.Tick(1.0/120,true);break;}
                    if(!g.Finished && !g.Rush.Waiting)A(g.Fits(g.Current,g.Rotation,g.X,g.Y),"Overlap " + id);
                }
            }
        });
    }
}
