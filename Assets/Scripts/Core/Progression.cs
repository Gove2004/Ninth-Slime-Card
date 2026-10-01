using System;
using System.Collections.Generic;
using System.Text;

namespace Slime.Core
{
    /// <summary>
    /// 跨局存档。奖杯、最高层数、图鉴收集、成就解锁与统计数据。
    /// 用 key=value 文本序列化，纯 C# 可测。
    /// </summary>
    public sealed class MetaState
    {
        public int Trophy;
        public int BestLevel = 1;
        /// <summary>历史最高战序（1..27），主界面按它显示赛程进度。</summary>
        public int BestBattle = 1;
        public int RunsPlayed;
        public int Wins;
        public int Losses;

        /// <summary>当前局正在进行到第几战。局内进度随存档持久化，长赛程可中途退出。</summary>
        public int RunBattle = 1;
        /// <summary>当前局的牌组。为空表示还没有开局的残局可以续。</summary>
        public readonly List<int> RunDeck = new List<int>();

        public readonly HashSet<int> DiscoveredCards = new HashSet<int>();
        public readonly HashSet<string> Unlocked = new HashSet<string>();
        public readonly Dictionary<string, long> Counters = new Dictionary<string, long>();
        public readonly HashSet<string> Flags = new HashSet<string>();

        public long Counter(string key)
        {
            long value;
            return Counters.TryGetValue(key, out value) ? value : 0L;
        }

        public void AddCounter(string key, long delta)
        {
            if (string.IsNullOrEmpty(key) || delta == 0)
            {
                return;
            }

            Counters[key] = Counter(key) + delta;
        }

        public bool IsUnlocked(string achievementId)
        {
            return !string.IsNullOrEmpty(achievementId) && Unlocked.Contains(achievementId);
        }

        public void Discover(int cardId)
        {
            DiscoveredCards.Add(cardId);
        }

        // ---------------------------------------------------------- 序列化

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("v=3\n");
            sb.Append("trophy=").Append(Trophy).Append('\n');
            sb.Append("best=").Append(BestLevel).Append('\n');
            sb.Append("bestbattle=").Append(BestBattle).Append('\n');
            sb.Append("runs=").Append(RunsPlayed).Append('\n');
            sb.Append("wins=").Append(Wins).Append('\n');
            sb.Append("losses=").Append(Losses).Append('\n');
            sb.Append("runbattle=").Append(RunBattle).Append('\n');
            sb.Append("rundeck=").Append(string.Join(",", RunDeck.ConvertAll(i => i.ToString()).ToArray())).Append('\n');

            sb.Append("cards=");
            bool first = true;
            foreach (int id in DiscoveredCards)
            {
                if (!first) sb.Append(',');
                sb.Append(id);
                first = false;
            }

            sb.Append('\n');

            sb.Append("unlocked=").Append(string.Join(",", ToArray(Unlocked))).Append('\n');
            sb.Append("flags=").Append(string.Join(",", ToArray(Flags))).Append('\n');

            sb.Append("counters=");
            first = true;
            foreach (var pair in Counters)
            {
                if (!first) sb.Append(',');
                sb.Append(pair.Key).Append('=').Append(pair.Value);
                first = false;
            }

            sb.Append('\n');
            return sb.ToString();
        }

        private static string[] ToArray(HashSet<string> set)
        {
            var list = new List<string>(set);
            return list.ToArray();
        }

        public static MetaState Deserialize(string text)
        {
            var meta = new MetaState();
            if (string.IsNullOrEmpty(text))
            {
                return meta;
            }

            var lines = text.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "trophy": meta.Trophy = ParseInt(value, 0); break;
                    case "best": meta.BestLevel = Math.Max(1, ParseInt(value, 1)); break;
                    case "bestbattle": meta.BestBattle = Math.Max(1, ParseInt(value, 1)); break;
                    case "runbattle": meta.RunBattle = Math.Max(1, ParseInt(value, 1)); break;
                    case "rundeck":
                        meta.RunDeck.Clear();
                        foreach (var piece in Split(value))
                        {
                            int id;
                            if (int.TryParse(piece, out id))
                            {
                                meta.RunDeck.Add(id);
                            }
                        }

                        break;
                    case "runs": meta.RunsPlayed = ParseInt(value, 0); break;
                    case "wins": meta.Wins = ParseInt(value, 0); break;
                    case "losses": meta.Losses = ParseInt(value, 0); break;
                    case "cards":
                        foreach (var piece in Split(value))
                        {
                            int id;
                            if (int.TryParse(piece, out id))
                            {
                                meta.DiscoveredCards.Add(id);
                            }
                        }

                        break;
                    case "unlocked":
                        foreach (var piece in Split(value))
                        {
                            meta.Unlocked.Add(piece);
                        }

                        break;
                    case "flags":
                        foreach (var piece in Split(value))
                        {
                            meta.Flags.Add(piece);
                        }

                        break;
                    case "counters":
                        foreach (var piece in Split(value))
                        {
                            int at = piece.IndexOf('=');
                            if (at <= 0)
                            {
                                continue;
                            }

                            long number;
                            if (long.TryParse(piece.Substring(at + 1), out number))
                            {
                                meta.Counters[piece.Substring(0, at)] = number;
                            }
                        }

                        break;
                }
            }

            return meta;
        }

        private static string[] Split(string value)
        {
            return string.IsNullOrEmpty(value)
                ? new string[0]
                : value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }
    }

    /// <summary>一场战斗结束后的战报，用于成就结算。</summary>
    public struct BattleFacts
    {
        public bool Victory;
        public int Battle;
        public int Stage;
        public int Level;
        public int TurnCount;
        public int DamageDealt;
        public int BestTurnDamage;
        public int CardsPlayed;
        public int CardsDrawn;
        public int ManaGained;
        public int Healed;
        public int SelfDamageTaken;
        public int Overheat;
        public bool EnemyCardsPlayed;
        public int EnemyCardsStolen;

        public static BattleFacts Capture(BattleSession session, int selfDamage)
        {
            var facts = new BattleFacts();
            if (session == null)
            {
                return facts;
            }

            facts.Victory = session.Victory;
            facts.Battle = session.Battle;
            facts.Stage = session.Stage;
            facts.Level = session.Level;
            facts.TurnCount = session.TurnNumber;
            facts.DamageDealt = session.DamageDealtByPlayer;
            facts.BestTurnDamage = session.BestTurnDamage;
            facts.CardsPlayed = session.CardsPlayedByPlayer;
            facts.CardsDrawn = session.DrawnTotal;
            facts.ManaGained = session.ManaGainedTotal;
            facts.Healed = session.HealedTotal;
            facts.SelfDamageTaken = selfDamage;
            return facts;
        }
    }
}
