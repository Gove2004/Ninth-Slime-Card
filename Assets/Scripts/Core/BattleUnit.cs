using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>持续增益种类。对应旧版 UnitAttributeEffect / TemporaryAttributeEffect。</summary>
    public enum BuffKind
    {
        DmgFlatUp = 0,     // 伤害固定提升
        HealFlatUp = 1,    // 治疗追加
        DmgFlatDown = 2,   // 伤害固定减免
        DmgTakenPctUp = 3, // 受到伤害提高（破甲）
        DmgOutPctUp = 4    // 造成伤害提高（钻石）
    }

    public sealed class Buff
    {
        public BuffKind Kind;
        public int Value;
        public int TurnsLeft;

        public string Describe()
        {
            switch (Kind)
            {
                case BuffKind.DmgFlatUp: return "伤害 +" + Value;
                case BuffKind.HealFlatUp: return "治疗 +" + Value;
                case BuffKind.DmgFlatDown: return "减伤 " + Value;
                case BuffKind.DmgTakenPctUp: return "承受伤害 +" + Value + "%";
                case BuffKind.DmgOutPctUp: return "输出伤害 +" + Value + "%";
                default: return "增益";
            }
        }
    }

    /// <summary>挂在单位上的延迟触发效果。旧版的 HookEffects。</summary>
    public sealed class Hook
    {
        public HookTiming Timing;
        public List<CardOp> Ops = new List<CardOp>();
        public int Times;
        public int Delay;
        public int Fired;
        public string Label = string.Empty;

        public bool Expired { get { return Times <= 0; } }

        /// <summary>剩余回合数（含未被延迟消耗的当前值）。</summary>
        public int TurnsLeft { get { return Times; } }
    }

    /// <summary>一张进入战斗的卡牌实例。可携带运行时费用修正与成长状态。</summary>
    public sealed class CardInstance
    {
        public CardDef Def;
        public int CostDelta;
        public int PowerMul = 1;
        public int FreeTurns;
        public int ExtraTriggers;
        public bool IsFree { get { return Def.Cost == 0 || FreeTurns > 0; } }

        public int Cost
        {
            get
            {
                int c = Def.Cost + CostDelta;
                if (FreeTurns > 0)
                {
                    c = 0;
                }

                return c < 0 ? 0 : c;
            }
        }

        public string Name { get { return Def.Name; } }

        public bool IsTech { get { return Def.Series == CardSeries.Tech; } }

        public static CardInstance Wrap(CardDef def)
        {
            return new CardInstance { Def = def };
        }
    }

    /// <summary>
    /// 战斗单位：玩家与敌人共用。承载生命/魔力/护盾/牌堆/增益/钩子，
    /// 以及一整套旧版规则的伤害与治疗管线。
    /// </summary>
    public sealed class BattleUnit
    {
        public string Name = "单位";
        public bool IsPlayer;

        public int Hp;
        public int HpMax = 10;
        public int Mana;
        public int ManaRegen = 2;
        public int Shield;

        public readonly List<CardInstance> Deck = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> Discard = new List<CardInstance>();
        public readonly List<Buff> Buffs = new List<Buff>();
        public readonly List<Hook> Hooks = new List<Hook>();

        // ---- 本回合统计（成就与条件判定用）----
        public int CardsPlayedThisTurn;
        public int TechCardsPlayedThisTurn;
        public int DamageTakenThisTurn;
        public bool TookDamageThisTurn;
        public bool EnemyAttackedMeThisTurn;
        public int DamageDealtTotal;
        public int HealedTotal;
        public int ManaGainedTotal;
        public int CardsDrawnTotal;

        // ---- 待消耗的下一张牌修正 ----
        public int PendingExtraTriggers;
        public int PendingDamageBonus;
        public int PendingCostReduction;
        public int PendingTechExtraTriggers;
        public int PendingTechDamageBonus;
        public int PendingTechDamageBonusUses;

        /// <summary>免疫指向性效果的次数（傲慢/虚荣）。</summary>
        public int ImmunityCharges;
        /// <summary>免疫伤害实例的次数（影遁）。</summary>
        public int DodgeCharges;
        /// <summary>待转移给对手的伤害量（移祸）。</summary>
        public int TransferPool;

        public bool IsDead { get { return Hp <= 0; } }

        public float HealthPercent
        {
            get { return HpMax <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)Hp / HpMax)); }
        }

        public bool HasBuff(BuffKind kind)
        {
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Kind == kind && Buffs[i].TurnsLeft > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public int BuffValue(BuffKind kind)
        {
            int total = 0;
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Kind == kind && Buffs[i].TurnsLeft > 0)
                {
                    total += Buffs[i].Value;
                }
            }

            return total;
        }

        public void AddBuff(BuffKind kind, int value, int turns)
        {
            if (turns <= 0 || value == 0)
            {
                return;
            }

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Kind == kind && Buffs[i].TurnsLeft == turns)
                {
                    Buffs[i].Value += value;
                    return;
                }
            }

            Buffs.Add(new Buff { Kind = kind, Value = value, TurnsLeft = turns });
        }

        /// <summary>挂一条延迟效果；不合法时返回 null（调用方可据此决定是否附加延迟）。</summary>
        public Hook AddHook(HookTiming timing, List<CardOp> ops, int turns, string label)
        {
            if (timing == HookTiming.None || ops == null || ops.Count == 0 || turns <= 0)
            {
                return null;
            }

            var hook = new Hook
            {
                Timing = timing,
                Ops = ops,
                Times = turns,
                Label = label ?? string.Empty
            };

            Hooks.Add(hook);
            return hook;
        }

        /// <summary>回合结束：流逝增益与钩子计数。</summary>
        public void TickDurations()
        {
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buffs[i].TurnsLeft--;
                if (Buffs[i].TurnsLeft <= 0)
                {
                    Buffs.RemoveAt(i);
                }
            }

            for (int i = Hooks.Count - 1; i >= 0; i--)
            {
                if (Hooks[i].Delay > 0)
                {
                    Hooks[i].Delay--;
                    continue;
                }

                Hooks[i].Times--;
                if (Hooks[i].Times <= 0)
                {
                    Hooks.RemoveAt(i);
                }
            }
        }

        public void ResetTurnFlags()
        {
            CardsPlayedThisTurn = 0;
            TechCardsPlayedThisTurn = 0;
            DamageTakenThisTurn = 0;
            TookDamageThisTurn = false;
            EnemyAttackedMeThisTurn = false;
        }

        public void ClearPending()
        {
            PendingExtraTriggers = 0;
            PendingDamageBonus = 0;
            PendingCostReduction = 0;
            PendingTechExtraTriggers = 0;
            PendingTechDamageBonus = 0;
            PendingTechDamageBonusUses = 0;
        }
    }
}
