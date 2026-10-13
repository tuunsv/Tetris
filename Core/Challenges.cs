using System;
using System.Collections.Generic;

namespace Tetris118.Core
{
    public sealed class ChallengeDefinition
    {
        public readonly int Id, Target, Seconds;
        public readonly string Name, Difficulty, Description;
        public ChallengeDefinition(int id, string name, string difficulty, string description, int target, int seconds)
        { Id = id; Name = name; Difficulty = difficulty; Description = description; Target = target; Seconds = seconds; }
        public static readonly ChallengeDefinition[] All = {
            new ChallengeDefinition(0, "FIRST BLOOD", "EASY", "Clear 4 lines.", 4, 45),
            new ChallengeDefinition(1, "PRECISION", "EASY", "Make 3 Single clears.", 3, 60),
            new ChallengeDefinition(2, "TWIN STRIKE", "EASY", "Make a Double clear.", 1, 30),
            new ChallengeDefinition(3, "MARCH FORWARD", "EASY", "Lock 12 pieces and clear a line.", 12, 60),
            new ChallengeDefinition(4, "MANY FACES", "EASY", "Clear using 3 different piece types.", 3, 75),
            new ChallengeDefinition(5, "LOW PROFILE", "EASY", "Land 6 pieces in a row entirely in the lower half.", 6, 45),
            new ChallengeDefinition(6, "TRIDENT", "NORMAL", "Make a Triple clear.", 1, 90),
            new ChallengeDefinition(7, "GREAT STRIKE", "NORMAL", "Make a Tetris clear.", 1, 120),
            new ChallengeDefinition(8, "CHAIN REACTION", "HARD", "Clear on 3 consecutive pieces.", 3, 75),
            new ChallengeDefinition(9, "ONE TWO", "NORMAL", "Make both a Single and a Double.", 2, 75),
            new ChallengeDefinition(10, "EDGE WALKER", "NORMAL", "Make 3 clears with a piece touching a side wall.", 3, 90),
            new ChallengeDefinition(11, "NO RESERVE", "NORMAL", "Clear 4 lines without using Hold.", 4, 60),
            new ChallengeDefinition(12, "FULL ARSENAL", "HARD", "Make a Single, a Double and a Triple.", 3, 120),
            new ChallengeDefinition(13, "TWIN TITANS", "HARD", "Make 2 Tetrises with no other clears between them.", 2, 120),
            new ChallengeDefinition(14, "HOLD SHOT", "NORMAL", "Clear 2+ lines at once with a piece taken from Hold.", 1, 60),
            new ChallengeDefinition(15, "RECLAIM", "HARD", "Lower the locked stack to at most 4 rows.", 1, 90),
            new ChallengeDefinition(16, "BREAK THE SIEGE", "HARD", "Clear both marked garbage rows added for this challenge.", 2, 90),
            new ChallengeDefinition(17, "SOFT HANDS", "HARD", "Clear 4 lines without Hard Drop.", 4, 60),
            new ChallengeDefinition(19, "NO TWIST", "NORMAL",
    "Clear 2 lines without rotating any piece.", 2, 60),

new ChallengeDefinition(20, "HIGH TIDE", "NORMAL",
    "Reach stack height 8, then clear 2 lines.", 3, 70),

new ChallengeDefinition(21, "SECOND WIND", "HARD",
    "Reach stack height 12, then lower it to 5 or less.", 2, 150),

new ChallengeDefinition(22, "LOW CEILING", "HARD",
    "Clear 6 lines. Keep stack height at 10 or less.", 6, 60)
        };
        public static readonly ChallengeDefinition Moon = new ChallengeDefinition(18, "MOONLIT ASCENT", "SPECIAL", "Reach level 9. No time limit.", 9, 0);
    }
    // Một đối tượng mới cho mỗi challenge, không để bộ đếm từ lượt trước rò sang.
    public sealed class ChallengeState
    {
        private bool heightReached;
        private int linesAfterHeight;

        // Chỉ xoay thành công mới làm hỏng NO TWIST.
        // Bấm xoay nhưng bị chặn thì không tính.
        public void OnRotate()
        {
            if (Definition.Id == 19)
                Failed = true;
        }

        public void ObserveHeight(GameEngine game)
        {
            if (Failed) return;

            int id = Definition.Id;
            int height = game.StackHeight;

            if (id == 22 && height > 10)
            {
                Failed = true;
                return;
            }

            if (id == 20 && !heightReached && height >= 8)
            {
                heightReached = true;
                Progress = 1;
            }

            if (id == 21)
            {
                if (!heightReached && height >= 12)
                {
                    heightReached = true;
                    Progress = 1;
                }
                else if (heightReached && height <= 5)
                {
                    Progress = 2;
                }
            }
        }
        public ChallengeDefinition Definition { get; private set; }
        public int Progress { get; private set; }
        public bool Failed { get; private set; }
        public bool Complete { get { return !Failed && Progress >= Definition.Target; } }
        private int lines, pieces, streak, clearKinds, tetrises, edgeClears, singles;
        private readonly HashSet<PieceKind> types = new HashSet<PieceKind>();
        public ChallengeState(ChallengeDefinition definition) { Definition = definition; }
        public void OnHold() { if (Definition.Id == 11) Failed = true; }
        public void OnHardDrop() { if (Definition.Id == 17) Failed = true; }
        public void Update(
            GameEngine game, int count, PieceKind piece, bool edge, int top)
        {
            if (Failed) return;

            // Nhớ trạng thái trước lần đặt này để HIGH TIDE
            // không tính hàng xóa trước khi đạt mốc chiều cao.
            bool reachedBeforeLock = heightReached;

            ObserveHeight(game);
            if (Failed) return;

            lines += count;
            pieces++;

            if (count == 1)
                singles++;

            if (count > 0)
            {
                types.Add(piece);

                if (edge)
                    edgeClears++;
            }

            if (count >= 1 && count <= 3)
                clearKinds |= 1 << (count - 1);

            if (count == 4)
                tetrises++;
            else if (count > 0)
                tetrises = 0;

            int id = Definition.Id;

            if (id == 5)
                streak = top >= GameEngine.Hidden + 10 ? streak + 1 : 0;
            else
                streak = count > 0 ? streak + 1 : 0;

            switch (id)
            {
                case 0:
                case 11:
                case 17:
                case 19:
                case 22:
                    Progress = lines;
                    break;

                case 1:
                    Progress = singles;
                    break;

                case 2:
                    if (count == 2) Progress = 1;
                    break;

                case 3:
                    Progress = lines > 0
                        ? Math.Min(12, pieces)
                        : Math.Min(11, pieces);
                    break;

                case 4:
                    Progress = types.Count;
                    break;

                case 5:
                case 8:
                    Progress = streak;
                    break;

                case 6:
                    if (count == 3) Progress = 1;
                    break;

                case 7:
                    if (count == 4) Progress = 1;
                    break;

                case 9:
                    Progress = (clearKinds & 1)
                        + ((clearKinds >> 1) & 1);
                    break;

                case 10:
                    Progress = edgeClears;
                    break;

                case 12:
                    Progress = (clearKinds & 1)
                        + ((clearKinds >> 1) & 1)
                        + ((clearKinds >> 2) & 1);
                    break;

                case 13:
                    Progress = tetrises;
                    break;

                case 14:
                    if (game.CurrentFromHold && count >= 2)
                        Progress = 1;
                    break;

                case 15:
                    Progress = game.StackHeight <= 4 ? 1 : 0;
                    break;

                case 16:
                    Progress = 2 - game.MarkedGarbageRows;
                    break;

                case 18:
                    Progress = game.Level;
                    break;

                case 20:
                    if (reachedBeforeLock)
                        linesAfterHeight += count;

                    Progress = heightReached
                        ? 1 + Math.Min(2, linesAfterHeight)
                        : 0;
                    break;

                case 21:
                    // ObserveHeight đã xử lý hai giai đoạn.
                    break;
            }
        }
    }
}
