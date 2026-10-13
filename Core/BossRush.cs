using System;
using System.Linq;
using System.Collections.Generic;

namespace Tetris118.Core
{
    public enum BossId { Godrick, Malenia, Rennala, Radagon, Radahn, Margit, Ranni, Godfrey, Gideon, Maliketh, Mohg, Morgott }
    public sealed class BossDefinition
    {
        public readonly BossId Id;
        public readonly string Name, Rule, Attack;
        public BossDefinition(BossId id, string name, string rule, string attack)
        { Id = id; Name = name; Rule = rule; Attack = attack; }
        public static readonly BossDefinition[] All = {
            new BossDefinition(BossId.Godrick, "GODRICK", "Each bag has 7 tetrominoes and 1 random pentomino.", "Lose 1 HP. Next 3 pieces are pentominoes."),
            new BossDefinition(BossId.Malenia, "MALENIA", "Gravity is doubled.", "Lose 1 HP. For 15s: maximum gravity, 140ms lock delay, 5 lock resets."),
            new BossDefinition(BossId.Rennala, "RENNALA", "Stack limit: 16 rows.", "Lose 1 HP. Stack limit becomes 13 rows for 30s. Crossing it is fatal."),
            new BossDefinition(BossId.Radagon, "RADAGON", "No extra passive effect.", "Failed challenge: instant death, regardless of HP."),
            new BossDefinition(BossId.Radahn, "RADAHN", "No lock delay: pieces lock on contact.", "Lose 1 HP. Push the stack up by 3 solid, unbreakable gray rows."),
            new BossDefinition(BossId.Margit, "MARGIT", "Gravity is multiplied by 1.5.", "Set your HP to 1. This attack cannot reduce HP below 1."),
            new BossDefinition(BossId.Ranni, "RANNI", "Reach level 9 to pass. Your level carries over.", "None. No challenge timer."),
            new BossDefinition(BossId.Godfrey, "GODFREY", "Only the falling piece is invisible. Normal bag, Next, Ghost and Hold.", "Lose 2 HP. No extra effect."),
            new BossDefinition(BossId.Gideon, "GIDEON OFNIR", "Borrows one announced attack per challenge. No borrowed passive.", "Uses the announced attack, including its HP damage."),
            new BossDefinition(BossId.Maliketh, "MALIKETH", "Your HP becomes 1 on entry. No shield.", "Lose 1 HP. No extra effect."),
            new BossDefinition(BossId.Mohg, "MOHG", "Every 20s: clear 2 lines within 12s or lose 1 HP to bleeding.", "If your HP is 2 or less: instant death. Otherwise lose 1 HP."),
            new BossDefinition(BossId.Morgott, "MORGOTT", "Hold is disabled for this fight.", "Lose 1 HP. Push up 2 breakable gray garbage rows with holes. No gravity boost.")
        };
    }
    public sealed class BossRush
    {
        private int challengesAssigned;
        private readonly GameEngine game;
        private readonly Random random;
        private readonly BossId[] route;
        private readonly HashSet<int> usedChallenges = new HashSet<int>();
        public BossDefinition Boss { get { return BossDefinition.All[(int)route[Stage]]; } }
        public int Stage { get; private set; }
        public int Defeated { get; private set; }
        public int Health { get; private set; }
        public int BossHealth { get; private set; }
        public int BossMaxHealth { get; private set; }
        public int StageLines { get; private set; }
        public int LocalLevel { get { return Math.Min(20, 1 + game.Lines / 10); } }
        public ChallengeState Challenge { get; private set; }
        public double Remaining { get; private set; }
        public double Intermission { get; private set; }
        public bool Waiting { get { return Intermission > 0; } }
        public double EffectTime { get { return Math.Max(frenzyTime, ceilingTime); } }
        public bool HoldBlocked { get { return Boss.Id == BossId.Morgott; } }
        public double BloodTime { get; private set; }
        public double Flash { get; private set; }
        public string Notice { get; private set; }
        public double NoticeTime { get; private set; }
        public BossId BorrowedAttack { get; private set; }
        public string AttackText { get { return Boss.Id == BossId.Gideon ? BossDefinition.All[(int)BorrowedAttack].Name + ": " + BossDefinition.All[(int)BorrowedAttack].Attack : Boss.Attack; } }
        public int Ceiling { get { return ceilingTime > 0 ? 13 : Boss.Id == BossId.Rennala ? 16 : 20; } }
        public bool HideFalling { get { return Boss.Id == BossId.Godfrey; } }
        public double GravityScale { get { return Boss.Id == BossId.Malenia ? 2 : Boss.Id == BossId.Margit ? 1.5 : 1; } }
        public bool MaxGravity { get { return frenzyTime > 0; } }
        public double LockDelay { get { return Boss.Id == BossId.Radahn ? 0 : frenzyTime > 0 ? .140 : .5; } }
        public int ResetLimit { get { return frenzyTime > 0 ? 5 : 15; } }
        private double frenzyTime, ceilingTime, bloodClock;
        private int bloodLines;
        private bool warning;
        public BossRush(GameEngine engine, int seed)
        {
            game = engine; random = new Random(seed ^ 118901);
            var candidates = (BossId[])Enum.GetValues(typeof(BossId));
            for (int i = candidates.Length - 1; i > 0; i--) { int j = random.Next(i + 1); var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t; }
            route = candidates.Take(3).ToArray(); Health = 5; EnterStage();
        }
        private void Say(string text, string sound)
        { Notice = text; NoticeTime = 4; game.BossSound(sound); }
        private void EnterStage()
        {
            challengesAssigned = 0;
            StageLines = 0; BloodTime = frenzyTime = ceilingTime = bloodClock = 0;
            if (Boss.Id == BossId.Maliketh) Health = 1;
            BossHealth = BossMaxHealth = Boss.Id == BossId.Ranni ? 1 : random.Next(3, 7);
            game.PrepareBossBoard(Boss.Id);
            Intermission = 5; NewChallenge(); Say(Boss.Name, "bossintro");
        }
        private bool Eligible(ChallengeDefinition c)
        {
            // Morgott khóa Hold, không giao nhiệm vụ cần Hold.
            if ((c.Id == 11 || c.Id == 14) && HoldBlocked)
                return false;

            // NewChallenge đã tăng bộ đếm trước khi chọn.
            // Chỉ cho hai nhiệm vụ này xuất hiện từ lần thứ 3 của boss.
            if ((c.Id == 15 || c.Id == 16) && challengesAssigned < 3)
                return false;

            if (c.Id == 15 &&
                (game.StackHeight < 9 || game.StoneFloorHeight > 4))
                return false;

            if (c.Id == 16 &&
                (game.StackHeight > Ceiling - 5 || game.MarkedGarbageRows > 0))
                return false;

            if (c.Id == 5 && game.StoneFloorHeight >= 10)
                return false;

            // SECOND WIND cần có khả năng hạ đống khối xuống 5.
            if (c.Id == 21 && game.StoneFloorHeight > 5)
                return false;

            // "Dưới 6" nghĩa là từ 0 đến 5 hàng.
            if (c.Id == 22 && game.StackHeight >= 6)
                return false;

            return true;
        }
        private void NewChallenge()
        {
            challengesAssigned++;
            warning = false;
            if (Boss.Id == BossId.Ranni)
            { Challenge = new ChallengeState(ChallengeDefinition.Moon); Remaining = 0; return; }
            var options = ChallengeDefinition.All.Where(c => Eligible(c) && !usedChallenges.Contains(c.Id)).ToArray();
            if (options.Length == 0) { usedChallenges.Clear(); options = ChallengeDefinition.All.Where(Eligible).ToArray(); }
            var definition = options[random.Next(options.Length)]; usedChallenges.Add(definition.Id);
            Challenge = new ChallengeState(definition); Remaining = definition.Seconds;
            // Nếu lúc nhận nhiệm vụ đã đủ cao, ghi nhận mốc ngay.
            // Chỉ những hàng xóa sau khi nhận mới được tính.
            Challenge.ObserveHeight(game);
            BossId[] attacks = { BossId.Godrick, BossId.Malenia, BossId.Rennala, BossId.Radagon, BossId.Radahn, BossId.Margit, BossId.Godfrey, BossId.Maliketh, BossId.Mohg, BossId.Morgott };
            BorrowedAttack = attacks[random.Next(attacks.Length)];
            // Đẩy hàng challenge vào ngay lúc chọn; engine biết đang khóa hay đang rơi.
            if (definition.Id == 16) game.PushGarbage(2, false, true);
        }
        public void OnHold() { if (Challenge != null) Challenge.OnHold(); }
        public void OnRotate()
        {
            if (Challenge != null)
                Challenge.OnRotate();
        }
        public void OnHardDrop() { if (Challenge != null) Challenge.OnHardDrop(); }
        public void OnLock(int lines, PieceKind piece, bool edge, int top)
        {
            StageLines += lines;
            if (BloodTime > 0)
            {
                bloodLines += lines;
                if (bloodLines >= 2) { BloodTime = 0; Say("BLEEDING CURED", "quest"); }
            }
            Challenge.Update(game, lines, piece, edge, top);
            if (Challenge.Complete) CompleteChallenge();
        }
        private void CompleteChallenge()
        {
            BossHealth = Math.Max(0, BossHealth - 1);
            if (BossHealth == 0) { BeatBoss(); return; }
            Say("CHALLENGE CLEARED - BOSS HP -1", "quest"); NewChallenge();
        }
        private void BeatBoss()
        {
            BossHealth = 0; Defeated++; Intermission = 6.5;
            Say("DEMIGOD FELLED", Defeated == 3 ? "rushwin" : "bossvictory"); NoticeTime = 6.5;
        }
        public void Tick(double dt)
        {
            Flash = Math.Max(0, Flash - dt); NoticeTime = Math.Max(0, NoticeTime - dt);
            if (Waiting)
            {
                Intermission = Math.Max(0, Intermission - dt);
                if (!Waiting && Defeated == 3) game.FinishBossRush(true);
                else if (!Waiting && Defeated > Stage) { Stage++; EnterStage(); }
                return;
            }
            frenzyTime = Math.Max(0, frenzyTime - dt); ceilingTime = Math.Max(0, ceilingTime - dt);
            if (BloodTime > 0)
            {
                BloodTime = Math.Max(0, BloodTime - dt);
                if (BloodTime == 0) { Damage(1); Say("BLEEDING -1 HP", "bosshit"); }
            }
            if (game.Finished) return;
            if (Boss.Id == BossId.Mohg)
            {
                bloodClock += dt;
                if (bloodClock >= 20) { bloodClock -= 20; BloodTime = 12; bloodLines = 0; Say("BLEED: CLEAR 2 LINES IN 12S", "warning"); }
            }
            if (Boss.Id == BossId.Ranni)
            {
                if (game.Level >= 9) CompleteChallenge();
                return;
            }
            // Theo dõi cả thay đổi chiều cao ngoài thao tác đặt khối.
            Challenge.ObserveHeight(game);

            if (Challenge.Complete)
            {
                CompleteChallenge();
                return;
            }
            Remaining = Math.Max(0, Remaining - dt);
            if (!warning && Remaining <= 10) { warning = true; game.BossSound("warning"); }
            if (Remaining <= 0 || Challenge.Failed)
            {
                Attack(Boss.Id == BossId.Gideon ? BorrowedAttack : Boss.Id);
                if (!game.Finished) NewChallenge();
            }
        }
        private void Damage(int amount)
        { Health = Math.Max(0, Health - amount); Flash = .5; if (Health == 0) game.FinishBossRush(false); }
        private void Attack(BossId id)
        {
            Say("ATTACK: " + BossDefinition.All[(int)id].Name, "bosshit"); Flash = .7;
            if (id == BossId.Radagon || (id == BossId.Mohg && Health <= 2)) { Damage(Health); return; }
            if (id == BossId.Margit) { Health = 1; return; }
            Damage(id == BossId.Godfrey ? 2 : 1);
            if (game.Finished) return;
            switch (id)
            {
                case BossId.Godrick: game.ForcePentomino(3); break;
                case BossId.Malenia: frenzyTime = 15; break;
                case BossId.Rennala: ceilingTime = 30; game.CheckBossCeiling(); break;
                case BossId.Radahn: game.PushGarbage(3, true, false); break;
                case BossId.Morgott: game.PushGarbage(2, false, false); break;
            }
        }
        internal BossId[] RouteForTest { get { return (BossId[])route.Clone(); } }
        internal void SelectForTest(BossId id) { route[Stage] = id; EnterStage(); Intermission = 0; }
        internal void AttackForTest(BossId id) { Attack(id); }
        internal void ChallengeForTest(int id)
        {
            ChallengeDefinition definition = id == 18
                ? ChallengeDefinition.Moon
                : ChallengeDefinition.All.First(c => c.Id == id);

            Challenge = new ChallengeState(definition);
            Remaining = definition.Seconds;
            Challenge.ObserveHeight(game);
        }
    }
}
