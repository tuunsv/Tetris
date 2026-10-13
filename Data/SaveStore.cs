using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Tetris118.Core;

namespace Tetris118.Data
{
    [DataContract]
    public sealed class Settings
    {
        [DataMember] public string Player = "PLAYER 118";
        [DataMember] public string Team = "Team 118";
        [DataMember] public bool Muted;
        [DataMember] public int Music = 45;
        [DataMember] public int Effects = 70;
        [DataMember] public int Das = 150;
        [DataMember] public int Arr = 40;
        [DataMember] public int MatchSeed;
        [DataMember] public bool Ghost = true;
        [DataMember] public bool Scanlines = true;
        public void Validate()
        {
            Player = string.IsNullOrWhiteSpace(Player) ? "PLAYER 118" : Player.Trim().Substring(0, Math.Min(24, Player.Trim().Length));
            Team = string.IsNullOrWhiteSpace(Team) ? "Team 118" : Team.Substring(0, Math.Min(240, Team.Length));
            Team = Team.Replace("\r", " ").Replace("\n", " / ");
            Music = Math.Max(0, Math.Min(100, Music)); Effects = Math.Max(0, Math.Min(100, Effects));
            MatchSeed = Math.Max(0, MatchSeed);
            Das = Math.Max(80, Math.Min(250, Das)); Arr = Math.Max(10, Math.Min(100, Arr));
        }
    }
    [DataContract]
    public sealed class Record
    {
        [DataMember] public string Player;
        [DataMember] public string Mode;
        [DataMember] public long Score;
        [DataMember] public int Lines;
        [DataMember] public int Pieces;
        [DataMember] public double Seconds;
        [DataMember] public bool Won;
        [DataMember] public string Date;
        [DataMember] public int Seed;
        [DataMember] public int Bosses;
    }
    [DataContract]
    public sealed class Profile
    {
        [DataMember] public int Version = 1;
        [DataMember] public Settings Settings = new Settings();
        [DataMember] public List<Record> Records = new List<Record>();
        [DataMember] public int Games, Lines, Pieces, Tetrises, TSpins, PerfectClears, SprintWins, BestCombo;
        [DataMember] public int BossRuns, BossWins, BossKills;
        [DataMember] public long TotalScore, BestScore;
        [DataMember] public double PlaySeconds;
        public void Add(GameEngine game)
        {
            Games++; Lines += game.Lines; Pieces += game.PiecesPlaced;
            Tetrises += game.Tetrises; TSpins += game.TSpins; PerfectClears += game.PerfectClears;
            PlaySeconds += game.Elapsed; TotalScore += game.Score; BestScore = Math.Max(BestScore, game.Score);
            BestCombo = Math.Max(BestCombo, game.MaxCombo);
            if (game.Mode == GameMode.Sprint && game.Won) SprintWins++;
            if (game.Rush != null) { BossRuns++; BossKills += game.Rush.Defeated; if (game.Won) BossWins++; }
            Records.Add(new Record { Player = Settings.Player, Mode = game.Mode.ToString(), Score = game.Score,
                Lines = game.Lines, Pieces = game.PiecesPlaced, Seconds = game.Elapsed, Won = game.Won, Bosses = game.Rush == null ? 0 : game.Rush.Defeated,
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Seed = game.Seed });
            // Giữ top 50 từng chế độ, không để JSON tăng mãi theo số ván.
            var keep = new List<Record>();
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode))) keep.AddRange(Ranking(mode).Take(50));
            Records = keep;
        }
        public IEnumerable<Record> Ranking(GameMode mode)
        {
            var entries = Records.Where(r => r.Mode == mode.ToString());
            return mode == GameMode.BossRush ? entries.OrderByDescending(r => r.Won).ThenByDescending(r => r.Bosses).ThenByDescending(r => r.Score).ThenBy(r => r.Seconds) :
                mode == GameMode.Sprint ? entries.OrderByDescending(r => r.Won).ThenBy(r => r.Won ? r.Seconds : double.MaxValue).ThenByDescending(r => r.Lines) :
                entries.OrderByDescending(r => r.Score).ThenBy(r => r.Seconds);
        }
    }
    public sealed class SaveStore
    {
        public string Folder { get; private set; }
        public string Warning { get; private set; }
        public SaveStore()
        {
            Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris118");
        }
        internal SaveStore(string folder) { Folder = folder; }
        private Profile Read(string path)
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Save file is too large.");
            using (var stream = File.OpenRead(path))
            {
                var data = (Profile)new DataContractJsonSerializer(typeof(Profile)).ReadObject(stream);
                if (data == null || data.Version != 1 || data.Settings == null || data.Records == null)
                    throw new InvalidDataException("Invalid save file structure.");
                data.Settings.Validate();
                data.Records = data.Records.Where(r => r != null && r.Score >= 0 && r.Lines >= 0 &&
                    r.Seconds >= 0 && !double.IsNaN(r.Seconds) && !double.IsInfinity(r.Seconds) &&
                    Enum.GetNames(typeof(GameMode)).Contains(r.Mode)).Take(200).ToList();
                return data;
            }
        }
        public Profile Load()
        {
            string path = Path.Combine(Folder, "profile.json");
            if (!File.Exists(path)) return new Profile();
            try { return Read(path); }
            catch (Exception ex)
            {
                Log(ex);
                try
                {
                    Profile backup = Read(path + ".bak");
                    Warning = "Save file damaged. Backup loaded; the damaged file will be preserved.";
                    return backup;
                }
                catch (Exception backupError)
                { Log(backupError); Warning = "Could not read previous data. A new profile is active; old files are preserved."; return new Profile(); }
            }
        }
        public bool Save(Profile data)
        {
            try
            {
                Directory.CreateDirectory(Folder); data.Settings.Validate();
                string path = Path.Combine(Folder, "profile.json"), temp = path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(Profile)).WriteObject(stream, data); stream.Flush(true); }
                if (File.Exists(path))
                {
                    if (!string.IsNullOrEmpty(Warning))
                        File.Copy(path, path + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"), false);
                    // Replace là thay file nguyên khối. Mất điện không để lại nửa JSON.
                    File.Replace(temp, path, path + ".bak");
                }
                else File.Move(temp, path);
                Warning = null; return true;
            }
            catch (Exception ex)
            { Log(ex); Warning = "Could not save scores or settings. Check disk space and write permissions."; return false; }
        }
        public static void Log(Exception ex)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris118");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "errors.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("s") + " " + ex + Environment.NewLine);
            }
            catch { /* Nếu ổ đĩa hết chỗ thì ghi log cũng lỗi. Không để lỗi log làm sập game. */ }
        }
    }
}
