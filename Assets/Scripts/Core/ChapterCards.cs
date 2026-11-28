using System;
using System.Collections.Generic;

namespace Slime.Core
{
    public enum NodeKind
    {
        Battle = 0,
        Elite = 1,
        Treasure = 2,
        Rest = 3,
        Shop = 4,
        Event = 5,
        Boss = 6
    }

    public static class NodeKinds
    {
        public static string DisplayName(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "战斗";
                case NodeKind.Elite: return "精英";
                case NodeKind.Treasure: return "宝箱";
                case NodeKind.Rest: return "篝火";
                case NodeKind.Shop: return "商人";
                case NodeKind.Event: return "事件";
                case NodeKind.Boss: return "首领";
                default: return "";
            }
        }

        public static string Glyph(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "剑";
                case NodeKind.Elite: return "焰";
                case NodeKind.Treasure: return "箱";
                case NodeKind.Rest: return "火";
                case NodeKind.Shop: return "商";
                case NodeKind.Event: return "问";
                case NodeKind.Boss: return "王";
                default: return "?";
            }
        }
    }

    /// <summary>
    /// 一层里摆在面前的一张遭遇卡。卡面写的是什么，进去就是什么：
    /// 战斗卡绑定具体敌人，选完照这张卡生成战斗，不做二次抽取。
    /// </summary>
    public sealed class EncounterCard
    {
        public int Slot;
        public NodeKind Kind;
        /// <summary>该卡绑定的内容种子：事件脚本、商店库存都从这里取，读档后不会重roll。</summary>
        public int Seed;
        /// <summary>战斗类卡绑定的敌人 id；非战斗类为空。</summary>
        public string EnemyId;
        /// <summary>卡面大字：战斗类是对手名，其余是遭遇类型名。</summary>
        public string Title;
    }

    /// <summary>
    /// 章节发牌器。每层发三张遭遇卡（两张战斗 + 一张补给/事件），玩家选一张、
    /// 结算完推进到下一层。没有坐标、连线与可达性：进度就是层号，
    /// 摆在面前的牌只由 (seed, chapter, floor) 决定，存档存这三个值即可复现。
    /// </summary>
    public static class ChapterCardDealer
    {
        /// <summary>每层摆几张。三张够做取舍，再多在 16:9 屏上每张就窄得放不下大字。</summary>
        public const int HandSize = 3;

        /// <summary>无限年每十层一个首领，其余层与普通章节同构。</summary>
        public const int EndlessSegment = 10;

        public static List<EncounterCard> Deal(int chapter, int floor, int seed, bool endless)
        {
            int ch = EnemyRoster.ClampChapter(chapter);
            int rel = endless ? floor % EndlessSegment : floor;
            int last = endless ? EndlessSegment - 1 : EnemyRoster.FloorsPerChapter - 1;

            var rng = new DeterministicRng(unchecked(
                seed * 31 + ch * 7919 + floor * 104729 + (endless ? 17 : 0)));

            var hand = new List<EncounterCard>();

            // 首领层只摆一张：章末不该再让玩家绕开决战去做别的事。
            if (rel >= last)
            {
                hand.Add(Make(0, NodeKind.Boss, rng, ch, floor, endless));
                return hand;
            }

            // 两张战斗遭遇。中后段有机会把其中一张换成精英，开局前几层不塞硬骨头。
            bool elite = rel >= 5 && rng.Range(0, 100) < 30;
            var first = Make(0, elite ? NodeKind.Elite : NodeKind.Battle, rng, ch, floor, endless);
            var second = Make(1, NodeKind.Battle, rng, ch, floor, endless, first.EnemyId);
            hand.Add(first);
            hand.Add(second);

            // 第三张是补给或事件。首领前一层固定给篝火，玩家一定能在决战前补血。
            NodeKind supply;
            if (rel == last - 1)
            {
                supply = NodeKind.Rest;
            }
            else
            {
                // 篝火给两份权重：每层都有一张补给卡，血量是跨层保留的稀缺资源，
                // 补给点太少会把整局变成一场没有喘息的长跑。
                var bag = new List<NodeKind>
                {
                    NodeKind.Rest, NodeKind.Rest, NodeKind.Event,
                    NodeKind.Treasure, NodeKind.Shop
                };
                supply = bag[rng.Range(0, bag.Count)];
            }

            hand.Add(Make(2, supply, rng, ch, floor, endless));

            rng.Shuffle(hand);
            for (int i = 0; i < hand.Count; i++)
            {
                hand[i].Slot = i;
            }

            return hand;
        }

        private static EncounterCard Make(int slot, NodeKind kind, DeterministicRng rng,
            int chapter, int floor, bool endless, string excludeEnemy = null)
        {
            var card = new EncounterCard { Slot = slot, Kind = kind, Seed = rng.Next() };

            if (kind == NodeKind.Boss)
            {
                var boss = endless ? EnemyRoster.EndlessBossFor(floor) : EnemyRoster.BossOf(chapter);
                card.EnemyId = boss.Id;
                card.Title = boss.Name;
                return card;
            }

            if (kind == NodeKind.Battle || kind == NodeKind.Elite)
            {
                var pool = kind == NodeKind.Elite
                    ? EnemyRoster.ElitePool(chapter)
                    : EnemyRoster.NormalPool(chapter);

                // 同层两张战斗卡不能指向同一只怪，并排摆着就成了「两张一样的牌」。
                if (excludeEnemy != null && pool.Count > 1)
                {
                    var rest = new List<EnemyDef>();
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (pool[i].Id != excludeEnemy)
                        {
                            rest.Add(pool[i]);
                        }
                    }

                    if (rest.Count > 0)
                    {
                        pool = rest;
                    }
                }

                var def = pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : EnemyRoster.Default;
                card.EnemyId = def.Id;
                card.Title = def.Name;
                return card;
            }

            card.Title = NodeKinds.DisplayName(kind);
            return card;
        }
    }
}
