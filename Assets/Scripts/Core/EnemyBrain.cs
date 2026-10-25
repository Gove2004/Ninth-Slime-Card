using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 怪物招式的数值模板。同一招在不同章节会按难度缩放。
    /// </summary>
    public sealed class EnemyMove
    {
        public string Label = "攻击";
        public IntentKind Kind = IntentKind.Attack;
        public int Damage;
        public int Hits = 1;
        public int Shield;
        public int Heal;
        public StatusKind Status = StatusKind.None;
        public int StatusStacks;

        /// <summary>权重：越高越常出。0 表示不随机（走固定脚本）。</summary>
        public int Weight = 10;

        /// <summary>生命低于此百分比时才可用（0 表示不限）。</summary>
        public int OnlyBelowHpPct;

        /// <summary>冷却回合数：同一招最快多少回合后能再来一次。</summary>
        public int Cooldown = 1;
    }

    /// <summary>
    /// 敌人模板。围绕 DQ 怪物谱系：史莱姆系 / 兽系 / 魔物系 / 亡灵系 / 龙系 / 人形系。
    /// 每种怪物有自己的招式池与被动，精英与 Boss 通过额外招式与起始状态区分。
    /// </summary>
    public sealed class EnemyDef
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Title = string.Empty;
        /// <summary>图鉴分组：史莱姆 / 野兽 / 魔物 / 亡灵 / 龙 / 人形。</summary>
        public string Family = string.Empty;

        public int Hp = 40;
        /// <summary>玩家的生命上限（同一章节内保持一致，随章节提升）。</summary>
        public int PlayerHp = 70;

        public bool IsElite;
        public bool IsBoss;

        /// <summary>该模板出现在第几章。生成时据此筛选普通与精英池。</summary>
        public int Chapter = 1;

        /// <summary>开场自带的永久状态（如狂暴、荆棘）。</summary>
        public readonly Dictionary<StatusKind, int> StartStatuses = new Dictionary<StatusKind, int>();

        public readonly List<EnemyMove> Moves = new List<EnemyMove>();

        /// <summary>每回合行动次数。Boss 可为 2。</summary>
        public int ActionsPerTurn = 1;

        /// <summary>美术资源名（Resources/Art/Monsters/<Sprite>）。</summary>
        public string Sprite = string.Empty;

        /// <summary>一句话介绍，用于图鉴与地图节点悬停。</summary>
        public string Blurb = string.Empty;

        /// <summary>立绘配色系列。表现层据此染色，图鉴里同类怪物才有一致的视觉语言。</summary>
        public CardSeries Accent = CardSeries.Strike;

        /// <summary>立绘程序化生成的随机种子。固定下来，同一只怪每次长得一样。</summary>
        public int Seed;
    }

    /// <summary>
    /// 敌人决策器。按招式权重抽招，并遵守冷却与生命门槛；
    /// 会在玩家回合开头就把「下一步要做什么」规划好，供 UI 展示意图。
    /// </summary>
    public sealed class EnemyBrain
    {
        private readonly EnemyDef def;
        private readonly DeterministicRng rng;
        private readonly Dictionary<EnemyMove, int> lastUsed = new Dictionary<EnemyMove, int>();

        public readonly Queue<EnemyIntent> Queue = new Queue<EnemyIntent>();

        public EnemyBrain(EnemyDef definition, DeterministicRng random)
        {
            def = definition;
            rng = random;
        }

        public void Plan(BattleUnit self, BattleUnit foe, int turn, bool elite, bool boss)
        {
            Queue.Clear();
            if (def == null || def.Moves.Count == 0)
            {
                Queue.Enqueue(new EnemyIntent { Kind = IntentKind.Attack, Label = "攻击", Value = 5 });
                return;
            }

            int actions = Math.Max(1, def.ActionsPerTurn);
            for (int i = 0; i < actions; i++)
            {
                var move = Pick(self, turn);
                Queue.Enqueue(ToIntent(move));
            }

            // 多动回合必须至少有一次攻击。否则「防御 + 回血」会连续出现，
            // 玩家每回合的输出追不上敌人的恢复，战斗就变成单方面的消耗，谁也打不死谁。
            if (actions > 1 && !HasOffensiveAction())
            {
                var forced = PickOffensive();
                if (forced != null)
                {
                    Queue.Clear();
                    Queue.Enqueue(ToIntent(forced));
                    for (int i = 1; i < actions; i++)
                    {
                        Queue.Enqueue(ToIntent(Pick(self, turn)));
                    }
                }
            }
        }

        private bool HasOffensiveAction()
        {
            foreach (var intent in Queue)
            {
                if (intent.Kind == IntentKind.Attack || intent.Kind == IntentKind.AttackDebuff)
                {
                    return true;
                }
            }

            return false;
        }

        private EnemyMove PickOffensive()
        {
            var pool = new List<EnemyMove>();
            for (int i = 0; i < def.Moves.Count; i++)
            {
                var move = def.Moves[i];
                if (move.Weight > 0 && (move.Kind == IntentKind.Attack || move.Kind == IntentKind.AttackDebuff))
                {
                    pool.Add(move);
                }
            }

            return pool.Count == 0 ? null : pool[rng.Range(0, pool.Count)];
        }

        private EnemyMove Pick(BattleUnit self, int turn)
        {
            var candidates = new List<EnemyMove>();
            int totalWeight = 0;

            for (int i = 0; i < def.Moves.Count; i++)
            {
                var move = def.Moves[i];
                if (move.Weight <= 0)
                {
                    continue;
                }

                // 生命门槛
                if (move.OnlyBelowHpPct > 0)
                {
                    int pct = self.HpMax <= 0 ? 0 : self.Hp * 100 / self.HpMax;
                    if (pct > move.OnlyBelowHpPct)
                    {
                        continue;
                    }
                }

                // 冷却
                int last;
                if (lastUsed.TryGetValue(move, out last))
                {
                    if (turn - last < move.Cooldown)
                    {
                        continue;
                    }
                }

                candidates.Add(move);
                totalWeight += move.Weight;
            }

            if (candidates.Count == 0)
            {
                return def.Moves[0];
            }

            int roll = rng.Range(0, totalWeight);
            int acc = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                acc += candidates[i].Weight;
                if (roll < acc)
                {
                    lastUsed[candidates[i]] = turn;
                    return candidates[i];
                }
            }

            var fallback = candidates[candidates.Count - 1];
            lastUsed[fallback] = turn;
            return fallback;
        }

        private static EnemyIntent ToIntent(EnemyMove move)
        {
            return new EnemyIntent
            {
                Kind = move.Kind,
                Label = move.Label,
                Value = move.Kind == IntentKind.Attack || move.Kind == IntentKind.AttackDebuff
                    ? move.Damage
                    : move.Kind == IntentKind.Defend ? move.Shield : move.Heal,
                Hits = Math.Max(1, move.Hits),
                AppliesStatus = move.Status,
                StatusStacks = move.StatusStacks
            };
        }
    }
}
