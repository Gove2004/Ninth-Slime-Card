using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>词条分组。图鉴按这个顺序分块展示。</summary>
    public enum GlossaryGroup
    {
        Rule = 0,    // 通用规则：护盾、能量、抽牌……
        Buff = 1,    // 增益
        Debuff = 2,  // 减益
        Payoff = 3   // 兑现：把攒起来的层数换成伤害
    }

    /// <summary>一条机制词条。</summary>
    public sealed class GlossaryEntry
    {
        /// <summary>卡面文字里出现的写法，用于反查这张卡涉及哪些机制。</summary>
        public string Key = string.Empty;

        public string Name = string.Empty;
        public string Text = string.Empty;
        public GlossaryGroup Group = GlossaryGroup.Rule;

        /// <summary>关联的状态（没有则 None）。战斗界面要按状态色标色时用得上。</summary>
        public StatusKind Status = StatusKind.None;

        /// <summary>
        /// 玩家拿不到、只有敌人会用的机制。
        ///
        /// 图鉴里这几种和玩家的增益并列排着，玩家会以为自己也该去凑 ——
        /// 尤其是「再生」这种看起来像治疗手段的。标出来省得白找。
        /// </summary>
        public bool EnemyOnly;
    }

    /// <summary>
    /// 机制词典。
    ///
    /// 卡面只写结果（「引爆冻结」），不解释过程（引爆到底是什么、吃不吃易伤）。
    /// 玩家看得到 144 张卡，却拼不出这些卡怎么连起来 —— 缺的就是这张表。
    ///
    /// 每一条的措辞必须与 BattleSession 的实际结算一致：这里写错比不写更糟，
    /// 因为玩家会照着它去构筑。改动结算规则时，这张表要同步改。
    /// </summary>
    public static class Glossary
    {
        public static readonly GlossaryEntry[] All =
        {
            // ---------------------------------------------------------- 通用规则
            new GlossaryEntry
            {
                Key = "护盾", Name = "护盾", Group = GlossaryGroup.Rule, Status = StatusKind.None,
                Text = "优先于生命承受伤害。回合结束时清空 —— 所以护盾是「这一回合」的资源，攒不到下一回合。"
            },
            new GlossaryEntry
            {
                Key = "结界", Name = "结界", Group = GlossaryGroup.Buff, Status = StatusKind.Barrier,
                Text = "比护盾更先承受伤害，不按回合衰减，但每回合结束时同样清零。适合挡下已知的大招。"
            },
            new GlossaryEntry
            {
                Key = "能量", Name = "能量", Group = GlossaryGroup.Rule, Status = StatusKind.None,
                Text = "出牌的费用来源。每回合开始回满 3 点，用不完不保留。"
            },
            new GlossaryEntry
            {
                Key = "抽", Name = "抽牌", Group = GlossaryGroup.Rule, Status = StatusKind.None,
                Text = "每回合抽 5 张，回合结束时整手弃掉。手牌上限 10 张，抽满即止。"
            },
            new GlossaryEntry
            {
                Key = "消耗", Name = "消耗", Group = GlossaryGroup.Rule, Status = StatusKind.None,
                Text = "打出后不进弃牌堆，这一局不会再抽到。"
            },
            new GlossaryEntry
            {
                Key = "能力", Name = "能力牌", Group = GlossaryGroup.Rule, Status = StatusKind.None,
                Text = "打出后整场生效、不进弃牌堆。带「每回合」的效果会在每回合开始自动重放。"
            },

            // ---------------------------------------------------------- 增益
            new GlossaryEntry
            {
                Key = "临时力量", Name = "临时力量", Group = GlossaryGroup.Buff, Status = StatusKind.TempStrength,
                Text = "每层让攻击 +1，回合结束时整段清零。给的是本回合的爆发，不是永久成长。"
            },
            new GlossaryEntry
            {
                Key = "力量", Name = "力量", Group = GlossaryGroup.Buff, Status = StatusKind.Strength,
                Text = "每层让每次攻击 +1，永久不衰减。多段攻击每段都吃，所以力量与「打 N 次」是乘法关系。"
            },
            new GlossaryEntry
            {
                Key = "敏捷", Name = "敏捷", Group = GlossaryGroup.Buff, Status = StatusKind.Dexterity,
                Text = "每层让「获得护盾」+1，永久。护盾类卡越多，这一点敏捷的收益就越高。"
            },
            new GlossaryEntry
            {
                Key = "专注", Name = "专注", Group = GlossaryGroup.Buff, Status = StatusKind.Focus,
                Text = "每层让咒文伤害 +1，永久。和多段咒文也是乘法关系。"
            },
            new GlossaryEntry
            {
                Key = "再生", Name = "再生", Group = GlossaryGroup.Buff, Status = StatusKind.Regen,
                Text = "回合结束回复等量生命，然后层数 -1。", EnemyOnly = true
            },
            new GlossaryEntry
            {
                Key = "荆棘", Name = "荆棘", Group = GlossaryGroup.Buff, Status = StatusKind.Thorns,
                Text = "被攻击时反弹等量伤害给攻击者，永久。对多段攻击每段都会反弹。"
            },

            // ---------------------------------------------------------- 减益
            new GlossaryEntry
            {
                Key = "中毒", Name = "中毒", Group = GlossaryGroup.Debuff, Status = StatusKind.Poison,
                Text = "回合开始时失去等量生命，然后层数 -1。无视护盾。层数越高掉得越多，适合提前布局。"
            },
            new GlossaryEntry
            {
                Key = "灼烧", Name = "灼烧", Group = GlossaryGroup.Debuff, Status = StatusKind.Burn,
                Text = "回合结束时失去等量生命，然后层数减半。衰减比中毒快，但可以一次堆很高。"
            },
            new GlossaryEntry
            {
                Key = "冻结", Name = "冻结", Group = GlossaryGroup.Debuff, Status = StatusKind.Freeze,
                Text = "层数大于 0 时跳过该回合的出牌，回合结束 -1。醒来后有一回合免疫，不会被连冻。"
            },
            new GlossaryEntry
            {
                Key = "眩晕", Name = "眩晕", Group = GlossaryGroup.Debuff, Status = StatusKind.Stun,
                Text = "直接跳过该回合，回合结束清零。醒来后有一回合免疫。"
            },
            new GlossaryEntry
            {
                Key = "虚弱", Name = "虚弱", Group = GlossaryGroup.Debuff, Status = StatusKind.Weak,
                Text = "造成的伤害 -25%，每回合 -1。对多段攻击是每段都减。"
            },
            new GlossaryEntry
            {
                Key = "易伤", Name = "易伤", Group = GlossaryGroup.Debuff, Status = StatusKind.Vulnerable,
                Text = "受到的伤害 +50%，每回合 -1。先上易伤再打，等于给整套输出乘 1.5。"
            },
            new GlossaryEntry
            {
                Key = "脆化", Name = "脆化", Group = GlossaryGroup.Debuff, Status = StatusKind.Frail,
                Text = "获得的护盾 -25%，每回合 -1。", EnemyOnly = true
            },
            new GlossaryEntry
            {
                Key = "致盲", Name = "致盲", Group = GlossaryGroup.Debuff, Status = StatusKind.Blind,
                Text = "每回合少抽 1 张牌，每回合 -1。"
            },
            new GlossaryEntry
            {
                Key = "狂暴", Name = "狂暴", Group = GlossaryGroup.Buff, Status = StatusKind.Enrage,
                Text = "每层攻击 +2，永久。", EnemyOnly = true
            },
            new GlossaryEntry
            {
                Key = "回灵", Name = "回灵", Group = GlossaryGroup.Buff, Status = StatusKind.RegenAura,
                Text = "回合开始回复等量生命，每回合 -1。", EnemyOnly = true
            },

            // ---------------------------------------------------------- 兑现
            new GlossaryEntry
            {
                Key = "引爆", Name = "引爆", Group = GlossaryGroup.Payoff, Status = StatusKind.None,
                Text = "清空目标身上该状态的全部层数，每层折算成伤害。清空后层数归零，"
                     + "所以要先把层数堆高再引爆 —— 这是「攒」与「兑现」的分界点。"
                     + "引爆造成的是生命损失，无视护盾，也不吃易伤加成。"
            },
            new GlossaryEntry
            {
                Key = "倾泻", Name = "倾泻", Group = GlossaryGroup.Payoff, Status = StatusKind.None,
                Text = "把当前护盾全部清零，每点护盾折算成伤害。护盾本来到回合结束也会消失，"
                     + "倾泻等于把它提前换成一发重击。同样无视护盾与易伤。"
            },
            new GlossaryEntry
            {
                Key = "无视护盾", Name = "无视护盾", Group = GlossaryGroup.Payoff, Status = StatusKind.None,
                Text = "这一击直接扣生命，护盾拦不住。仍吃力量、专注与易伤的加成。"
            },
            new GlossaryEntry
            {
                Key = "双倍伤害", Name = "破盾", Group = GlossaryGroup.Payoff, Status = StatusKind.None,
                Text = "这一击对护盾造成双倍伤害，溢出到生命的部分按原值算。专门对付叠盾的敌人。"
            },
            new GlossaryEntry
            {
                Key = "翻倍", Name = "翻倍", Group = GlossaryGroup.Payoff, Status = StatusKind.None,
                Text = "把目标身上该状态的现有层数再加一份。层数越高收益越大，是暗咒的兑现方式之一。"
            }
        };

        /// <summary>
        /// 从一段卡面文字里反查出涉及的全部词条，按「长词优先」匹配，
        /// 避免「临时力量」被「力量」抢走、命中后重复计数。
        /// </summary>
        public static List<GlossaryEntry> Match(string text)
        {
            var found = new List<GlossaryEntry>();
            if (string.IsNullOrEmpty(text))
            {
                return found;
            }

            // 按 Key 长度倒序，保证「临时力量」先于「力量」被吃掉
            var order = new List<GlossaryEntry>(All);
            order.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));

            string rest = text;
            for (int i = 0; i < order.Count; i++)
            {
                var entry = order[i];
                if (string.IsNullOrEmpty(entry.Key) || rest.IndexOf(entry.Key, System.StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                found.Add(entry);
                rest = rest.Replace(entry.Key, "\u0000");
            }

            // 还原成词典里的固定顺序，图鉴上每次看到的排列都一致
            var sorted = new List<GlossaryEntry>();
            for (int i = 0; i < All.Length; i++)
            {
                if (found.Contains(All[i]))
                {
                    sorted.Add(All[i]);
                }
            }

            return sorted;
        }

        public static List<GlossaryEntry> OfGroup(GlossaryGroup group)
        {
            var list = new List<GlossaryEntry>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Group == group)
                {
                    list.Add(All[i]);
                }
            }

            return list;
        }

        /// <summary>按状态反查词条。战斗里点状态标记时用。</summary>
        public static GlossaryEntry ForStatus(StatusKind kind)
        {
            if (kind == StatusKind.None)
            {
                return null;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Status == kind)
                {
                    return All[i];
                }
            }

            return null;
        }

        /// <summary>按显示名反查词条（护盾这类没有状态枚举的走这条）。</summary>
        public static GlossaryEntry ByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Name == name || All[i].Key == name)
                {
                    return All[i];
                }
            }

            return null;
        }

        public static string GroupName(GlossaryGroup group)
        {
            switch (group)
            {
                case GlossaryGroup.Rule: return "基础规则";
                case GlossaryGroup.Buff: return "增益";
                case GlossaryGroup.Debuff: return "减益";
                case GlossaryGroup.Payoff: return "兑现 · 把攒起来的层数换成伤害";
                default: return string.Empty;
            }
        }
    }
}
