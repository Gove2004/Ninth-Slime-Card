using System;
using System.Collections.Generic;
using System.Text;

namespace Slime.Core
{
    /// <summary>
    /// 跨局存档。奖杯、图鉴收集、成就解锁与统计数据。
    /// 用 key=value 文本序列化，纯 C# 可测。
    /// </summary>
    public sealed class MetaState
    {
        public int Trophy;
        public int RunsPlayed;
        public int RunsCleared;
        public int Wins;
        public int Losses;
        /// <summary>历史到达的最深章节。</summary>
        public int BestChapter = 1;

        // ---- 当前局的续档 ----
        public int RunChapter;
        public int RunHp;
        public int RunHpMax;
        public int RunGold;
        public int RunNodeId;
        /// <summary>本章地图的生成种子。必须持久化，否则读档后 NodeId 会指向另一张地图上的节点。</summary>
        public int RunMapSeed;
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
            sb.Append("v=4\n");
            sb.Append("trophy=").Append(Trophy).Append('\n');
            sb.Append("runs=").Append(RunsPlayed).Append('\n');
            sb.Append("cleared=").Append(RunsCleared).Append('\n');
            sb.Append("wins=").Append(Wins).Append('\n');
            sb.Append("losses=").Append(Losses).Append('\n');
            sb.Append("bestchapter=").Append(BestChapter).Append('\n');

            sb.Append("runchapter=").Append(RunChapter).Append('\n');
            sb.Append("runhp=").Append(RunHp).Append('\n');
            sb.Append("runhpmax=").Append(RunHpMax).Append('\n');
            sb.Append("rungold=").Append(RunGold).Append('\n');
            sb.Append("runnode=").Append(RunNodeId).Append('\n');
            sb.Append("runmapseed=").Append(RunMapSeed).Append('\n');
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
                    case "runs": meta.RunsPlayed = ParseInt(value, 0); break;
                    case "cleared": meta.RunsCleared = ParseInt(value, 0); break;
                    case "wins": meta.Wins = ParseInt(value, 0); break;
                    case "losses": meta.Losses = ParseInt(value, 0); break;
                    case "bestchapter": meta.BestChapter = Math.Max(1, ParseInt(value, 1)); break;

                    case "runchapter": meta.RunChapter = ParseInt(value, 0); break;
                    case "runhp": meta.RunHp = ParseInt(value, 0); break;
                    case "runhpmax": meta.RunHpMax = ParseInt(value, 0); break;
                    case "rungold": meta.RunGold = ParseInt(value, 0); break;
                    case "runnode": meta.RunNodeId = ParseInt(value, 0); break;
                    case "runmapseed": meta.RunMapSeed = ParseInt(value, 0); break;
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
        public int Chapter;
        public int TurnCount;
        public int DamageDealt;
        public int BestTurnDamage;
        public int CardsPlayed;
        public int CardsDrawn;
        public int EnergyGained;
        public int Healed;
        public int SelfDamageTaken;

        public static BattleFacts Capture(BattleSession session, int currentHp)
        {
            var facts = new BattleFacts();
            if (session == null)
            {
                return facts;
            }

            facts.Victory = session.Victory;
            facts.Chapter = session.Chapter;
            facts.TurnCount = session.TurnNumber;
            facts.DamageDealt = session.DamageDealtByPlayer;
            facts.BestTurnDamage = session.BestTurnDamage;
            facts.CardsPlayed = session.CardsPlayedByPlayer;
            facts.CardsDrawn = session.CardsDrawnTotal;
            facts.EnergyGained = session.ManaGainedTotal;
            facts.Healed = session.HealedTotal;
            facts.SelfDamageTaken = Math.Max(0, session.Player.HpMax - currentHp);
            return facts;
        }
    }
}
