using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 状态种类。全部按「层数」计量：层数既是强度也是持续回合数的来源，
    /// 每回合结束时按各自规则衰减。这一套取代了旧版互相独立的 Buff/Hook 两套机制。
    /// </summary>
    public enum StatusKind
    {
        None = 0,

        // ---- 增益 ----
        Strength,    // 力量：每层攻击 +1（永久，不衰减）
        TempStrength,// 临时力量：每层攻击 +1，回合结束清零（能力牌每回合重新给）
        Dexterity,   // 敏捷：每层获得护盾 +1（永久）
        Regen,       // 再生：回合结束恢复等量生命，每回合 -1
        Thorns,      // 荆棘：受到攻击时反弹等量伤害（永久）
        Focus,       // 专注：每层咒文伤害 +1（永久）
        Barrier,     // 结界：受击时先扣结界，不衰减，回合结束清零

        // ---- 减益 ----
        Poison,      // 中毒：回合开始失去等量生命，每回合 -1
        Burn,        // 灼烧：回合结束失去等量生命，每回合减半向下取整
        Freeze,      // 冻结：层数 > 0 时跳过本回合出牌，回合结束 -1
        Stun,        // 眩晕：层数 > 0 时跳过本回合，回合结束清零
        Weak,        // 虚弱：造成伤害 -25%，每回合 -1
        Vulnerable,  // 易伤：受到伤害 +50%，每回合 -1
        Frail,       // 脆化：获得护盾 -25%，每回合 -1
        Blind,       // 致盲：抽牌 -1，每回合 -1

        // ---- 敌方专用 ----
        Enrage,      // 狂暴：每层攻击 +2（永久，Boss 用）
        RegenAura    // 回灵：回合开始回复等量生命，每回合 -1
    }

    /// <summary>状态的元信息：中文名、显示色、是否增益、是否有回合衰减。</summary>
    public static class Statuses
    {
        public static string DisplayName(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Strength: return "力量";
                case StatusKind.TempStrength: return "临时力量";
                case StatusKind.Dexterity: return "敏捷";
                case StatusKind.Regen: return "再生";
                case StatusKind.Thorns: return "荆棘";
                case StatusKind.Focus: return "专注";
                case StatusKind.Barrier: return "结界";
                case StatusKind.Poison: return "中毒";
                case StatusKind.Burn: return "灼烧";
                case StatusKind.Freeze: return "冻结";
                case StatusKind.Stun: return "眩晕";
                case StatusKind.Weak: return "虚弱";
                case StatusKind.Vulnerable: return "易伤";
                case StatusKind.Frail: return "脆化";
                case StatusKind.Blind: return "致盲";
                case StatusKind.Enrage: return "狂暴";
                case StatusKind.RegenAura: return "回灵";
                default: return "";
            }
        }

        public static bool IsBuff(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Strength:
                case StatusKind.TempStrength:
                case StatusKind.Dexterity:
                case StatusKind.Regen:
                case StatusKind.Thorns:
                case StatusKind.Focus:
                case StatusKind.Barrier:
                case StatusKind.Enrage:
                case StatusKind.RegenAura:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>该状态是否在回合结束时衰减。</summary>
        public static bool Decays(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Strength:
                case StatusKind.TempStrength:
                case StatusKind.Dexterity:
                case StatusKind.Thorns:
                case StatusKind.Focus:
                case StatusKind.Enrage:
                    return false;
                default:
                    return true;
            }
        }

        public static string Describe(StatusKind kind, int stacks)
        {
            switch (kind)
            {
                case StatusKind.Strength: return "力量 +" + stacks + "（攻击 +" + stacks + "）";
                case StatusKind.TempStrength: return "临时力量 +" + stacks + "（攻击 +" + stacks + "，回合结束消失）";
                case StatusKind.Dexterity: return "敏捷 +" + stacks + "（护盾 +" + stacks + "）";
                case StatusKind.Regen: return "再生 " + stacks + "（回合结束 +" + stacks + " 生命）";
                case StatusKind.Thorns: return "荆棘 " + stacks + "（反弹 " + stacks + " 伤害）";
                case StatusKind.Focus: return "专注 +" + stacks + "（咒文 +" + stacks + "）";
                case StatusKind.Barrier: return "结界 " + stacks + "（抵挡下 " + stacks + " 点伤害）";
                case StatusKind.Poison: return "中毒 " + stacks + "（回合开始 -" + stacks + " 生命）";
                case StatusKind.Burn: return "灼烧 " + stacks + "（回合结束 -" + stacks + " 生命）";
                case StatusKind.Freeze: return "冻结 " + stacks + " 回合";
                case StatusKind.Stun: return "眩晕";
                case StatusKind.Weak: return "虚弱 " + stacks + "（输出 -25%）";
                case StatusKind.Vulnerable: return "易伤 " + stacks + "（受伤 +50%）";
                case StatusKind.Frail: return "脆化 " + stacks + "（护盾 -25%）";
                case StatusKind.Blind: return "致盲 " + stacks + "（少抽 1 张）";
                case StatusKind.Enrage: return "狂暴 +" + stacks + "（攻击 +" + (stacks * 2) + "）";
                case StatusKind.RegenAura: return "回灵 " + stacks + "（回合开始 +" + stacks + " 生命）";
                default: return "";
            }
        }

        /// <summary>所有可被卡牌施加的状态，顺序即 UI 显示顺序。</summary>
        public static readonly StatusKind[] All =
        {
            StatusKind.Strength, StatusKind.TempStrength, StatusKind.Dexterity, StatusKind.Focus, StatusKind.Thorns,
            StatusKind.Regen, StatusKind.Barrier,
            StatusKind.Poison, StatusKind.Burn, StatusKind.Freeze, StatusKind.Stun,
            StatusKind.Weak, StatusKind.Vulnerable, StatusKind.Frail, StatusKind.Blind,
            StatusKind.Enrage, StatusKind.RegenAura
        };

        /// <summary>DSL 里的状态短名 → 枚举。</summary>
        public static StatusKind FromId(string id)
        {
            switch ((id ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "str": case "strength": return StatusKind.Strength;
                case "tstr": case "tempstr": return StatusKind.TempStrength;
                case "dex": case "dexterity": return StatusKind.Dexterity;
                case "regen": return StatusKind.Regen;
                case "thorns": case "thorn": return StatusKind.Thorns;
                case "focus": return StatusKind.Focus;
                case "barrier": return StatusKind.Barrier;
                case "poison": case "poisoned": return StatusKind.Poison;
                case "burn": case "burning": return StatusKind.Burn;
                case "freeze": case "frozen": return StatusKind.Freeze;
                case "stun": return StatusKind.Stun;
                case "weak": return StatusKind.Weak;
                case "vuln": case "vulnerable": return StatusKind.Vulnerable;
                case "frail": return StatusKind.Frail;
                case "blind": return StatusKind.Blind;
                case "enrage": return StatusKind.Enrage;
                case "aura": case "regenaura": return StatusKind.RegenAura;
                default: return StatusKind.None;
            }
        }
    }
}
