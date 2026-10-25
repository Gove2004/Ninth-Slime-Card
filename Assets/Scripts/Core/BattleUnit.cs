using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 一张进入战斗的卡牌实例。牌身数值在构筑时可被永久强化（升级），
    /// 运行时也可能被临时改费。
    /// </summary>
    public sealed class CardInstance
    {
        public CardDef Def;

        /// <summary>升级次数。每级按 CardDef.Upgrade* 提升数值。</summary>
        public int Level;

        /// <summary>运行时费用修正（临时降费/加费）。</summary>
        public int CostDelta;

        /// <summary>本回合免费（打出后清零）。</summary>
        public bool FreeThisTurn;

        /// <summary>被消耗：打出后进消耗堆而非弃牌堆，本场不再回来。</summary>
        public bool Exhaust;

        public int Cost
        {
            get
            {
                if (FreeThisTurn)
                {
                    return 0;
                }

                int c = Def.Cost + CostDelta - Level;
                return c < 0 ? 0 : c;
            }
        }

        public string Name { get { return Def.Name; } }

        /// <summary>结算用的实际数值（已含升级）。</summary>
        public int Value1 { get { return Def.V1 + Def.UpV1 * Level; } }
        public int Value2 { get { return Def.V2 + Def.UpV2 * Level; } }
        public int Value3 { get { return Def.V3 + Def.UpV3 * Level; } }

        public bool IsUpgraded { get { return Level > 0; } }

        public string DisplayText { get { return Def.Text(Level); } }

        public static CardInstance Wrap(CardDef def)
        {
            return new CardInstance { Def = def };
        }

        public CardInstance Clone()
        {
            return new CardInstance { Def = Def, Level = Level, Exhaust = Exhaust };
        }
    }

    /// <summary>
    /// 战斗单位。玩家与敌人共用同一套生命/护盾/状态/牌堆模型。
    /// 状态是唯一的力量来源——旧版分散在 Buff/Hook/Pending* 的机制已收敛到这里。
    /// </summary>
    public sealed class BattleUnit
    {
        public string Name = "单位";
        public bool IsPlayer;

        public int Hp;
        public int HpMax;
        /// <summary>能量：每回合恢复至上限（不是累加）。</summary>
        public int Energy;
        public int EnergyMax = 3;
        public int Shield;

        public readonly List<CardInstance> DrawPile = new List<CardInstance>();
        public readonly List<CardInstance> Hand = new List<CardInstance>();
        public readonly List<CardInstance> DiscardPile = new List<CardInstance>();
        public readonly List<CardInstance> ExhaustPile = new List<CardInstance>();

        /// <summary>状态层数。0 层的条目会被移除，保持列表干净。</summary>
        public readonly Dictionary<StatusKind, int> Status = new Dictionary<StatusKind, int>();

        // ---- 本回合统计 ----
        public int CardsPlayedThisTurn;
        public int AttacksPlayedThisTurn;
        public int SkillsPlayedThisTurn;
        public int DamageTakenThisTurn;
        public int DamageDealtThisTurn;
        public bool TookDamageThisTurn;
        public bool PlayedAttackThisTurn;

        // ---- 整场统计 ----
        public int DamageDealtTotal;
        public int DamageTakenTotal;
        public int CardsPlayedTotal;
        public int HealedTotal;

        /// <summary>
        /// 控制免疫回合数。刚被冻结/眩晕醒来后置 1，本回合不再吃同类控制。
        /// 没有这个窗口，一个每回合都能施加控制的目标会把对方永久锁死——
        /// 玩家一回合都动不了，再高的数值也毫无意义。
        /// </summary>
        public int ControlImmunity;

        /// <summary>敌人 AI 的本回合意图（玩家不显示）。</summary>
        public EnemyIntent Intent;

        public bool IsDead { get { return Hp <= 0; } }

        public float HealthPercent
        {
            get { return HpMax <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)Hp / HpMax)); }
        }

        // ---------------------------------------------------------- 状态

        public int GetStatus(StatusKind kind)
        {
            int value;
            return Status.TryGetValue(kind, out value) ? value : 0;
        }

        public void AddStatus(StatusKind kind, int stacks)
        {
            if (kind == StatusKind.None || stacks == 0)
            {
                return;
            }

            int next = GetStatus(kind) + stacks;
            if (next <= 0)
            {
                Status.Remove(kind);
            }
            else
            {
                Status[kind] = next;
            }
        }

        public void SetStatus(StatusKind kind, int stacks)
        {
            if (kind == StatusKind.None)
            {
                return;
            }

            if (stacks <= 0)
            {
                Status.Remove(kind);
            }
            else
            {
                Status[kind] = stacks;
            }
        }

        public void ClearStatuses()
        {
            Status.Clear();
        }

        public bool HasStatus(StatusKind kind)
        {
            return GetStatus(kind) > 0;
        }

        /// <summary>回合结束的衰减。力量/敏捷/荆棘/专注/狂暴是永久的，其余逐层递减。</summary>
        public void DecayStatuses()
        {
            var keys = new List<StatusKind>(Status.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var kind = keys[i];
                if (!Statuses.Decays(kind))
                {
                    continue;
                }

                int value = GetStatus(kind);
                switch (kind)
                {
                    case StatusKind.Burn:
                        // 灼烧每回合减半，向下取整，最少 1 层
                        value = value / 2;
                        break;
                    case StatusKind.Stun:
                        value = 0;
                        break;
                    case StatusKind.Barrier:
                        value = 0;
                        break;
                    default:
                        value -= 1;
                        break;
                }

                SetStatus(kind, value);
            }
        }

        // ---------------------------------------------------------- 派生数值

        /// <summary>攻击加成：力量 +（敌方的狂暴 ×2）。</summary>
        public int AttackBonus
        {
            get { return GetStatus(StatusKind.Strength) + GetStatus(StatusKind.Enrage) * 2; }
        }

        /// <summary>护盾加成：敏捷。</summary>
        public int ShieldBonus
        {
            get { return GetStatus(StatusKind.Dexterity); }
        }

        /// <summary>咒文加成：专注。</summary>
        public int SpellBonus
        {
            get { return GetStatus(StatusKind.Focus); }
        }

        /// <summary>是否被控制（冻结/眩晕），跳过本回合。</summary>
        public bool IsIncapacitated
        {
            get { return HasStatus(StatusKind.Freeze) || HasStatus(StatusKind.Stun); }
        }

        /// <summary>控制免疫：刚脱离控制的本回合内不再被冻/晕。</summary>
        public bool HasControlImmunity
        {
            get { return ControlImmunity > 0; }
        }

        // ---------------------------------------------------------- 回合

        public void ResetTurnStats()
        {
            CardsPlayedThisTurn = 0;
            AttacksPlayedThisTurn = 0;
            SkillsPlayedThisTurn = 0;
            DamageTakenThisTurn = 0;
            DamageDealtThisTurn = 0;
            TookDamageThisTurn = false;
            PlayedAttackThisTurn = false;
        }

        /// <summary>把牌堆清空并重新装填（战斗开始时用）。</summary>
        public void LoadDeck(IEnumerable<CardInstance> cards)
        {
            DrawPile.Clear();
            Hand.Clear();
            DiscardPile.Clear();
            ExhaustPile.Clear();

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card != null)
                    {
                        DrawPile.Add(card);
                    }
                }
            }
        }

        public int DeckSize { get { return DrawPile.Count + Hand.Count + DiscardPile.Count; } }
    }

    /// <summary>
    /// 敌人本回合的意图。在玩家回合开始时生成并展示，玩家据此决定攻防。
    /// 这是「可读的威胁」，取代旧版敌人随机抽牌的不可预测。
    /// </summary>
    public sealed class EnemyIntent
    {
        public IntentKind Kind = IntentKind.Attack;
        public string Label = "攻击";
        public int Value;
        /// <summary>多段攻击的段数。</summary>
        public int Hits = 1;
        public StatusKind AppliesStatus = StatusKind.None;
        public int StatusStacks;

        public string Describe()
        {
            switch (Kind)
            {
                case IntentKind.Attack:
                    return Hits > 1 ? Label + " " + Value + "×" + Hits : Label + " " + Value;
                case IntentKind.Defend:
                    return Label + " " + Value;
                case IntentKind.Buff:
                    return Label;
                case IntentKind.Debuff:
                    return Label + (AppliesStatus != StatusKind.None
                        ? "（" + Statuses.DisplayName(AppliesStatus) + " " + StatusStacks + "）"
                        : "");
                case IntentKind.AttackDebuff:
                    return Label + " " + Value + " + " + Statuses.DisplayName(AppliesStatus);
                case IntentKind.Heal:
                    return Label + " " + Value;
                default:
                    return Label;
            }
        }
    }

    public enum IntentKind
    {
        Attack = 0,
        Defend = 1,
        Buff = 2,
        Debuff = 3,
        AttackDebuff = 4,
        Heal = 5
    }
}
