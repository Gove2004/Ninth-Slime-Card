using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>卡牌种类。决定卡面框架配色与敌人 AI 的取舍。</summary>
    public enum CardKind
    {
        Attack = 0,  // 攻击：吃力量加成，触发荆棘
        Skill = 1,   // 技能：护盾、抽牌、抽能
        Spell = 2,   // 咒文：吃专注加成
        Power = 3    // 能力：打出后整场生效，不进弃牌堆
    }

    /// <summary>目标选择。</summary>
    public enum CardTarget
    {
        Enemy = 0,   // 敌方单体
        Self = 1,    // 自身
        AllEnemies = 2,
        None = 3
    }

    /// <summary>条件判定。对应卡面的「若……则……」分支。</summary>
    public enum Condition
    {
        Always = 0,
        FirstCard,       // 本回合第一张
        ThirdCard,       // 本回合第三张及以后
        HalfHp,          // 自身生命 <= 50%
        LowHp,           // 自身生命 <= 30%
        HasShield,       // 自身有护盾
        EnemyHasShield,  // 敌方有护盾
        EnemyPoisoned,   // 敌方中毒
        HasStatus,       // 自身拥有指定状态（参数为层数门槛）
        HandCount,       // 手牌数 >= A
        EnergyZero,      // 能量已耗尽
        TookDamage,      // 本回合受过伤
        DrawPileLow,     // 抽牌堆 <= A 张
        EveryOtherTurn   // 偶数回合
    }

    /// <summary>
    /// 效果节点。DSL 形如 <c>dmg:v1;status:weak:2;draw:1</c>。
    /// 数值位支持 v1/v2/v3 占位，结算时替换为卡牌当前数值（含升级）。
    /// </summary>
    public sealed class EffectOp
    {
        public string Kind = string.Empty;
        public int A;
        public int B;
        public int C;
        public string Text = string.Empty;

        /// <summary>条件（Kind == "if"）。</summary>
        public Condition Cond = Condition.Always;
        public int CondValue;
        public List<EffectOp> Then;
        public List<EffectOp> Else;

        /// <summary>状态载荷（kind == "status"）。</summary>
        public StatusKind StatusKind = StatusKind.None;

        /// <summary>倍率（kind == "mul"）。</summary>
        public bool IsIf { get { return Kind == "if"; } }
    }

    /// <summary>效果脚本解析器。纯字符串处理，无引擎依赖。</summary>
    public static class EffectScript
    {
        public static List<EffectOp> Parse(string spec, int v1, int v2, int v3)
        {
            var list = new List<EffectOp>();
            if (string.IsNullOrEmpty(spec))
            {
                return list;
            }

            var parts = spec.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var op = ParseOp(parts[i], v1, v2, v3);
                if (op != null)
                {
                    list.Add(op);
                }
            }

            return list;
        }

        private static List<EffectOp> ParseSeq(string spec, int v1, int v2, int v3)
        {
            var list = new List<EffectOp>();
            if (string.IsNullOrEmpty(spec))
            {
                return list;
            }

            var parts = spec.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                var op = ParseOp(parts[i], v1, v2, v3);
                if (op != null)
                {
                    list.Add(op);
                }
            }

            return list;
        }

        private static EffectOp ParseOp(string raw, int v1, int v2, int v3)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            raw = raw.Trim();
            if (raw.Length == 0)
            {
                return null;
            }

            var all = raw.Split('|');
            var head = all[0];
            var seg = head.Split(':');

            var op = new EffectOp { Kind = seg[0].Trim().ToLowerInvariant() };

            if (op.Kind == "if")
            {
                string condSpec = all.Length > 1 ? all[1] : "always";
                string thenSpec = all.Length > 2 ? all[2] : string.Empty;
                string elseSpec = all.Length > 3 ? all[3] : string.Empty;

                op.Cond = ParseCondition(condSpec, out op.CondValue);
                op.Then = ParseSeq(thenSpec, v1, v2, v3);
                op.Else = elseSpec.Length > 0 ? ParseSeq(elseSpec, v1, v2, v3) : null;
                return op;
            }

            if (op.Kind == "status" || op.Kind == "estatus" || op.Kind == "sstatus")
            {
                if (seg.Length > 1)
                {
                    op.StatusKind = Statuses.FromId(seg[1]);
                }

                if (seg.Length > 2)
                {
                    op.A = Arg(seg[2], v1, v2, v3, 1);
                }

                if (seg.Length > 3)
                {
                    op.B = Arg(seg[3], v1, v2, v3, 0);
                }

                return op;
            }

            if (seg.Length > 1)
            {
                op.A = Arg(seg[1], v1, v2, v3, 0);
            }

            if (seg.Length > 2)
            {
                op.B = Arg(seg[2], v1, v2, v3, 0);
            }

            if (seg.Length > 3)
            {
                op.C = Arg(seg[3], v1, v2, v3, 0);
            }

            // 字符串载荷：抽牌筛选的系列名、生成指定牌等
            if (seg.Length > 1 && NeedsText(op.Kind))
            {
                op.Text = seg[1].Trim();
            }

            return op;
        }

        private static bool NeedsText(string kind)
        {
            switch (kind)
            {
                case "addcard":
                case "addrandom":
                case "discardall":
                case "choice":
                    return true;
                default:
                    return false;
            }
        }

        private static int Arg(string raw, int v1, int v2, int v3, int fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            string t = raw.Trim();
            switch (t)
            {
                case "v1": return v1;
                case "v2": return v2;
                case "v3": return v3;
                case "": return fallback;
            }

            int value;
            return int.TryParse(t, out value) ? value : fallback;
        }

        private static Condition ParseCondition(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(raw))
            {
                return Condition.Always;
            }

            var seg = raw.Trim().Split(':');
            string name = seg[0].Trim().ToLowerInvariant();
            if (seg.Length > 1)
            {
                int.TryParse(seg[1].Trim(), out value);
            }

            switch (name)
            {
                case "first": case "firstcard": return Condition.FirstCard;
                case "third": case "thirdcard": return Condition.ThirdCard;
                case "halfhp": return Condition.HalfHp;
                case "lowhp": return Condition.LowHp;
                case "hasshield": return Condition.HasShield;
                case "enemyhasshield": return Condition.EnemyHasShield;
                case "enemypoisoned": return Condition.EnemyPoisoned;
                case "hasstatus": return Condition.HasStatus;
                case "hand": case "handcount": return Condition.HandCount;
                case "energyzero": case "outofenergy": return Condition.EnergyZero;
                case "tookdamage": return Condition.TookDamage;
                case "drawpilelow": case "decklow": return Condition.DrawPileLow;
                case "even": case "everyother": return Condition.EveryOtherTurn;
                default: return Condition.Always;
            }
        }
    }

    // ---------------------------------------------------------------- 卡牌定义

    /// <summary>静态卡牌定义，来自 cards.csv。</summary>
    public sealed class CardDef
    {
        public int Id;
        public string Name = string.Empty;
        public CardKind Kind = CardKind.Attack;
        public CardSeries Series = CardSeries.Strike;
        public int Cost;

        public int V1;
        public int V2;
        public int V3;

        /// <summary>升级后每级的数值增量。</summary>
        public int UpV1;
        public int UpV2;
        public int UpV3;

        public string Raw = string.Empty;
        public string Flavor = string.Empty;
        public string Script = string.Empty;
        public List<EffectOp> Ops = new List<EffectOp>();

        /// <summary>目标选择。</summary>
        public CardTarget Target = CardTarget.Enemy;

        /// <summary>打出后消耗（不进弃牌堆）。</summary>
        public bool Exhaust;

        /// <summary>稀有度：0 普通 / 1 罕见 / 2 稀有。</summary>
        public int Rarity;

        public string Text(int level)
        {
            return Render(Raw, Cost - level, V1 + UpV1 * level, V2 + UpV2 * level, V3 + UpV3 * level);
        }

        public static string Render(string raw, int cost, int v1, int v2, int v3)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return raw
                .Replace("[费用]", Math.Max(0, cost).ToString())
                .Replace("[数值1]", v1.ToString())
                .Replace("[数值2]", v2.ToString())
                .Replace("[数值3]", v3.ToString())
                .Replace("[数值]", v1.ToString());
        }
    }

    /// <summary>
    /// 卡牌系列。自由混池模式下系列不再是职业限制，而是「流派标签」：
    /// 决定卡面配色、图鉴分组、奖励池的取样分布。
    /// </summary>
    public enum CardSeries
    {
        None = 0,
        Strike = 1,   // 战技 · 物理近战
        Blade = 2,    // 剑技 · 连击与蓄势
        Fire = 3,     // 炎咒 · 爆发与灼烧
        Ice = 4,      // 冰咒 · 冻结与减速
        Thunder = 5,  // 雷咒 · 多段与感电
        Light = 6,    // 光咒 · 治疗与守护
        Dark = 7,     // 暗咒 · 中毒与诅咒
        Arcane = 8    // 秘术 · 抽牌与能量
    }

    public static class Series
    {
        public static readonly CardSeries[] All =
        {
            CardSeries.Strike, CardSeries.Blade, CardSeries.Fire, CardSeries.Ice,
            CardSeries.Thunder, CardSeries.Light, CardSeries.Dark, CardSeries.Arcane
        };

        public static string DisplayName(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Strike: return "战技";
                case CardSeries.Blade: return "剑技";
                case CardSeries.Fire: return "炎咒";
                case CardSeries.Ice: return "冰咒";
                case CardSeries.Thunder: return "雷咒";
                case CardSeries.Light: return "光咒";
                case CardSeries.Dark: return "暗咒";
                case CardSeries.Arcane: return "秘术";
                default: return "无系";
            }
        }

        public static string IdOf(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Strike: return "strike";
                case CardSeries.Blade: return "blade";
                case CardSeries.Fire: return "fire";
                case CardSeries.Ice: return "ice";
                case CardSeries.Thunder: return "thunder";
                case CardSeries.Light: return "light";
                case CardSeries.Dark: return "dark";
                case CardSeries.Arcane: return "arcane";
                default: return "none";
            }
        }

        public static CardSeries FromName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return CardSeries.None;
            }

            raw = raw.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (DisplayName(All[i]) == raw || IdOf(All[i]) == raw.ToLowerInvariant())
                {
                    return All[i];
                }
            }

            return CardSeries.None;
        }

        public static string Blurb(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Strike: return "直取要害，力量流的骨架";
                case CardSeries.Blade: return "连击与蓄势，越打越猛";
                case CardSeries.Fire: return "即时爆发，附带灼烧";
                case CardSeries.Ice: return "冻结与减速，控住节奏";
                case CardSeries.Thunder: return "多段打击，感电连锁";
                case CardSeries.Light: return "治疗与护盾，站得住才打得久";
                case CardSeries.Dark: return "中毒与诅咒，慢性绞杀";
                case CardSeries.Arcane: return "抽牌与能量，把牌握在手里";
                default: return string.Empty;
            }
        }

        /// <summary>初始牌组用的基础系。</summary>
        public static bool IsBasic(CardSeries s)
        {
            return s == CardSeries.Strike || s == CardSeries.Light || s == CardSeries.Arcane;
        }
    }
}
