using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 一张牌在「攒层 → 兑现」这条链上的位置。
    ///
    /// 卡面写的是「做什么」，不写「跟谁配合」。玩家抽到「引爆冻结」时，
    /// 如果手上没有施加冻结的牌，这张牌就是一张废纸；反过来手里攒了一堆
    /// 冻结牌却没兑现手段，那堆冻结也只是让敌人少动几下。这两种情况
    /// 光看卡面都看不出来 —— 这张分析就是要把它们说清楚。
    /// </summary>
    public enum CardRole
    {
        /// <summary>纯数值牌：不参与任何链，抽到就能用。</summary>
        Plain = 0,

        /// <summary>攒层：施加某种状态，为后续兑现铺路。</summary>
        Setup = 1,

        /// <summary>兑现：把已攒的层数换成伤害/收益。</summary>
        Payoff = 2,

        /// <summary>整张牌的价值取决于某个前置条件（若……则……）。</summary>
        Conditional = 3,

        /// <summary>既是攒层又是兑现（如翻倍中毒）。</summary>
        Both = 4
    }

    /// <summary>一张牌与某个状态的关系。同一状态可以同时具备多种关系。</summary>
    public sealed class CardLink
    {
        /// <summary>涉及的状态（护盾这类没有枚举的用 None）。</summary>
        public StatusKind Status = StatusKind.None;

        /// <summary>卡面术语，用来查词条。</summary>
        public string Term = string.Empty;

        /// <summary>这张牌施加该状态（攒层）。</summary>
        public bool Setup;

        /// <summary>这张牌把该状态换成伤害（兑现）。引爆、倾泻、翻倍中毒。</summary>
        public bool Consumes;

        /// <summary>这张牌要求该状态已经在场上（前置）。引爆、破盾、若目标已灼烧。</summary>
        public bool Needs;
    }

    /// <summary>
    /// 卡牌角色分析。全部从已解析的 Ops 反推，不额外维护一份手写表 ——
    /// 手写表一定会跟脚本走散，而脚本才是真正结算的东西。
    /// </summary>
    public static class CardRoles
    {
        /// <summary>分析一张牌涉及的全部状态关系。</summary>
        public static List<CardLink> Links(CardDef def)
        {
            var links = new List<CardLink>();
            if (def == null || def.Ops == null)
            {
                return links;
            }

            Collect(def.Ops, links);
            return Merge(links);
        }

        /// <summary>这张牌的整体角色。</summary>
        public static CardRole Role(CardDef def)
        {
            var links = Links(def);
            bool setup = false;
            bool payoff = false;
            bool needs = false;

            for (int i = 0; i < links.Count; i++)
            {
                var l = links[i];
                if (l.Consumes)
                {
                    payoff = true;
                }

                if (l.Setup)
                {
                    setup = true;
                }

                if (l.Needs)
                {
                    needs = true;
                }
            }

            // 既能攒又能兑现（翻倍中毒）：它自己就能闭环
            if (setup && payoff)
            {
                return CardRole.Both;
            }

            if (payoff)
            {
                return CardRole.Payoff;
            }

            // 有施加能力就先算攒层 —— 它能自己满足自己的条件。
            // 引燃既是「施加灼烧」又「若已灼烧抽牌」，主效果是施加，
            // 判成「看条件」会让玩家以为它没铺垫就打不出去。
            if (setup)
            {
                return CardRole.Setup;
            }

            if (needs)
            {
                return CardRole.Conditional;
            }

            return CardRole.Plain;
        }

        public static string RoleName(CardRole role)
        {
            switch (role)
            {
                case CardRole.Setup: return "铺垫";
                case CardRole.Payoff: return "兑现";
                case CardRole.Conditional: return "看条件";
                case CardRole.Both: return "自洽";
                default: return "通用";
            }
        }

        /// <summary>
        /// 这张牌在当前牌组里有没有配套的另一半。
        /// 返回 null 表示不需要配套（纯数值牌），否则返回一句人话。
        ///
        /// 这是奖励界面最重要的一行：玩家抽到「引爆冻结」时，
        /// 真正该问的不是「这张强不强」，而是「我手上有没有冻结」。
        /// </summary>
        public static string Fit(CardDef def, IEnumerable<CardDef> deck)
        {
            if (def == null)
            {
                return null;
            }

            var links = Links(def);
            if (links.Count == 0)
            {
                return null;
            }

            // 牌组里每种状态的施加者与兑现者各有几张（不含这张牌自己）
            var setupCount = new Dictionary<string, int>();
            var payoffCount = new Dictionary<string, int>();
            CountDeck(deck, def.Id, setupCount, payoffCount);

            // 1. 兑现牌：它最需要知道的是「有没有人帮我攒」
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!link.Consumes)
                {
                    continue;
                }

                // 自己先攒再兑现（圣裁、痛苦分享）：不依赖别人，说成「缺铺垫」是错的
                if (link.Setup)
                {
                    return "自己先攒后兑现，单独打也成立；同状态的牌越多收益越大";
                }

                int n = Count(setupCount, link);
                if (n == 0)
                {
                    return "牌组里还没有能造成" + link.Term + "的牌 —— 这条兑现现在打不出来";
                }

                return "牌组里有 " + n + " 张牌能造成" + link.Term + "，这条兑现吃得到";
            }

            // 2. 攒层牌：告诉玩家「有没有人替我兑现」
            // 只对卡池里真的存在兑现手段的状态给提示 —— 护盾、力量本身就是收益，
            // 没人兑现也不吃亏，说成「白攒」会误导。
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!link.Setup || !HasPayoffInPool(link))
                {
                    continue;
                }

                int n = Count(payoffCount, link);
                if (n == 0)
                {
                    return "牌组里还没有能兑现" + link.Term + "的牌 —— " + link.Term + "本身照常生效";
                }

                return "牌组里有 " + n + " 张牌能兑现" + link.Term;
            }

            // 3. 纯条件牌：需要场上已有某状态才生效
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!link.Needs || link.Consumes || link.Setup)
                {
                    continue;
                }

                int n = Count(setupCount, link);
                if (n == 0)
                {
                    return "牌组里还没有能造成" + link.Term + "的牌 —— 这条效果现在打不出来";
                }

                return "牌组里有 " + n + " 张牌能造成" + link.Term + "，这条效果能吃到";
            }

            return null;
        }

        // ------------------------------------------------------------ 内部

        private static int Count(Dictionary<string, int> table, CardLink link)
        {
            if (link.Status != StatusKind.None && table.TryGetValue("s" + (int)link.Status, out int byStatus))
            {
                return byStatus;
            }

            return table.TryGetValue("t" + link.Term, out int byTerm) ? byTerm : 0;
        }

        private static void Add(Dictionary<string, int> table, CardLink link)
        {
            if (link.Status != StatusKind.None)
            {
                string key = "s" + (int)link.Status;
                table.TryGetValue(key, out int n);
                table[key] = n + 1;
            }

            if (!string.IsNullOrEmpty(link.Term))
            {
                string key = "t" + link.Term;
                table.TryGetValue(key, out int n);
                table[key] = n + 1;
            }
        }

        private static void CountDeck(IEnumerable<CardDef> deck, int selfId,
            Dictionary<string, int> setupCount, Dictionary<string, int> payoffCount)
        {
            if (deck == null)
            {
                return;
            }

            foreach (var other in deck)
            {
                if (other == null || other.Id == selfId)
                {
                    continue;
                }

                var links = Links(other);
                for (int i = 0; i < links.Count; i++)
                {
                    if (links[i].Setup)
                    {
                        Add(setupCount, links[i]);
                    }

                    if (links[i].Consumes)
                    {
                        Add(payoffCount, links[i]);
                    }
                }
            }
        }

        /// <summary>
        /// 同一状态可能被多条 op 命中（引燃既施加灼烧、又「若已灼烧则抽牌」），
        /// 合成一条，三种关系各自保留 —— 丢掉任何一面都会让提示说反话。
        /// </summary>
        private static List<CardLink> Merge(List<CardLink> raw)
        {
            var merged = new List<CardLink>();
            for (int i = 0; i < raw.Count; i++)
            {
                var cur = raw[i];
                CardLink same = null;
                for (int j = 0; j < merged.Count; j++)
                {
                    if (merged[j].Status == cur.Status && merged[j].Term == cur.Term)
                    {
                        same = merged[j];
                        break;
                    }
                }

                if (same == null)
                {
                    merged.Add(new CardLink
                    {
                        Status = cur.Status,
                        Term = cur.Term,
                        Setup = cur.Setup,
                        Consumes = cur.Consumes,
                        Needs = cur.Needs
                    });
                }
                else
                {
                    same.Setup |= cur.Setup;
                    same.Consumes |= cur.Consumes;
                    same.Needs |= cur.Needs;
                }
            }

            // 兑现牌一定需要前置（引爆得先有层数），补上，别让 Fit 漏判
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].Consumes)
                {
                    merged[i].Needs = true;
                }
            }

            return merged;
        }

        /// <summary>整个卡池里有没有牌能兑现这个状态。没有的话，「缺兑现手段」就不算缺陷。</summary>
        private static bool HasPayoffInPool(CardLink link)
        {
            var pool = PayoffPool();
            if (pool == null)
            {
                return false;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if ((link.Status != StatusKind.None && pool[i].Status == link.Status)
                    || (!string.IsNullOrEmpty(link.Term) && pool[i].Term == link.Term))
                {
                    return true;
                }
            }

            return false;
        }

        private static CardDatabase poolDb;
        private static List<CardLink> poolLinks;

        /// <summary>
        /// 全卡池里所有「兑现」条目。按卡库实例缓存 —— 卡库换过一次就重算，
        /// 避免先算后设 PoolSource 时留下一个空缓存。
        /// </summary>
        private static List<CardLink> PayoffPool()
        {
            var db = PoolSource;
            if (db == null)
            {
                return null;
            }

            if (ReferenceEquals(db, poolDb) && poolLinks != null)
            {
                return poolLinks;
            }

            var all = new List<CardLink>();
            for (int i = 0; i < db.Count; i++)
            {
                var links = Links(db.Get(i));
                for (int j = 0; j < links.Count; j++)
                {
                    if (links[j].Consumes)
                    {
                        all.Add(links[j]);
                    }
                }
            }

            poolDb = db;
            poolLinks = all;
            return all;
        }

        /// <summary>
        /// 全局卡池。由 GameApp 在加载完卡表后写入一次。
        /// 没有它也能工作 —— 只是「缺兑现手段」这类提示不出现。
        /// </summary>
        public static CardDatabase PoolSource { get; set; }

        private static void Collect(List<EffectOp> ops, List<CardLink> links)
        {
            if (ops == null)
            {
                return;
            }

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null)
                {
                    continue;
                }

                if (op.IsIf)
                {
                    // 条件本身就是一个「前置需求」：卡面写「若目标已灼烧」
                    var cond = ConditionLink(op);
                    if (cond != null)
                    {
                        links.Add(cond);
                    }

                    Collect(op.Then, links);
                    Collect(op.Else, links);
                    continue;
                }

                switch (op.Kind)
                {
                    case "status":
                    case "estatus":
                    case "sstatus":
                        if (op.StatusKind != StatusKind.None && op.A > 0)
                        {
                            links.Add(new CardLink
                            {
                                Status = op.StatusKind,
                                Term = Statuses.DisplayName(op.StatusKind),
                                Setup = true
                            });
                        }
                        break;

                    case "shield":
                        if (op.A > 0)
                        {
                            links.Add(new CardLink { Term = "护盾", Setup = true });
                        }
                        break;

                    case "detonate":
                        if (op.StatusKind != StatusKind.None)
                        {
                            links.Add(new CardLink
                            {
                                Status = op.StatusKind,
                                Term = Statuses.DisplayName(op.StatusKind),
                                Consumes = true
                            });
                        }
                        break;

                    case "consume_shield":
                    case "double_shield":
                        links.Add(new CardLink { Term = "护盾", Consumes = true });
                        break;

                    case "shieldbreak":
                        links.Add(new CardLink { Term = "护盾", Needs = true });
                        break;

                    case "double_poison":
                        // 层数翻倍：既是兑现也是施加，之后再攒就更值钱
                        links.Add(new CardLink
                        {
                            Status = StatusKind.Poison,
                            Term = Statuses.DisplayName(StatusKind.Poison),
                            Consumes = true
                        });
                        break;
                }
            }
        }

        /// <summary>把 if 的条件翻译成一条「需要某状态」的链。</summary>
        private static CardLink ConditionLink(EffectOp op)
        {
            switch (op.Cond)
            {
                case Condition.EnemyPoisoned:
                    return Needs(StatusKind.Poison);
                case Condition.EnemyBurning:
                    return Needs(StatusKind.Burn);
                case Condition.EnemyFrozen:
                    return Needs(StatusKind.Freeze);
                case Condition.EnemyVulnerable:
                    return Needs(StatusKind.Vulnerable);
                case Condition.EnemyHasShield:
                    return NeedsTerm("护盾");
                case Condition.HasShield:
                    return NeedsTerm("护盾");
                case Condition.HasStatus:
                    return Needs((StatusKind)op.CondValue);
                default:
                    return null;
            }
        }

        private static CardLink Needs(StatusKind kind)
        {
            return new CardLink { Status = kind, Term = Statuses.DisplayName(kind), Needs = true };
        }

        private static CardLink NeedsTerm(string term)
        {
            return new CardLink { Term = term, Needs = true };
        }
    }
}
