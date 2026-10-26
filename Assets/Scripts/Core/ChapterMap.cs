using System;
using System.Collections.Generic;

namespace Slime.Core
{
    public enum NodeKind
    {
        Battle = 0,    // 普通战
        Elite = 1,     // 精英战
        Treasure = 2,  // 宝箱
        Rest = 3,      // 篝火
        Shop = 4,      // 商人
        Event = 5,     // 随机事件
        Boss = 6       // 章节 Boss
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
                case NodeKind.Boss: return "BOSS";
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

        public static string Blurb(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return "一场普通战斗，胜利后获得卡牌奖励";
                case NodeKind.Elite: return "强敌，但掉落更好的奖励";
                case NodeKind.Treasure: return "打开宝箱，直接获得一张卡";
                case NodeKind.Rest: return "休息：恢复生命或强化一张牌";
                case NodeKind.Shop: return "用金币购买卡牌或移除卡牌";
                case NodeKind.Event: return "未知的遭遇，可能有好有坏";
                case NodeKind.Boss: return "章节首领，击败后进入下一章";
                default: return "";
            }
        }
    }

    /// <summary>地图上的一个节点。</summary>
    public sealed class MapNode
    {
        public int Id;
        public NodeKind Kind;
        /// <summary>层号（0 起）。</summary>
        public int Floor;
        /// <summary>该层的横向序号，用于布局。</summary>
        public int Slot;
        /// <summary>归一化坐标（0..1），由生成器给出，UI 直接按此绘制。</summary>
        public float X;
        public float Y;

        public readonly List<int> Next = new List<int>();
        public readonly List<int> Prev = new List<int>();

        public bool Visited;

        /// <summary>该节点绑定的事件 id / 商店库存等，由生成器或进入时填充。</summary>
        public string Payload = string.Empty;

        public string Label { get { return NodeKinds.DisplayName(Kind); } }
    }

    /// <summary>
    /// 一张章节地图。生成算法参考尖塔但做了简化：
    /// 每章固定 Floors 层，层内 2~4 个节点，相邻层之间按位置就近连边并保证连通；
    /// 最后一层只有 Boss，倒数第二层固定一个篝火（给玩家战前补状态的机会）。
    /// </summary>
    public sealed class ChapterMap
    {
        public int Chapter;
        public readonly List<MapNode> Nodes = new List<MapNode>();

        public MapNode Get(int id)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == id)
                {
                    return Nodes[i];
                }
            }

            return null;
        }

        public List<MapNode> FloorNodes(int floor)
        {
            var result = new List<MapNode>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Floor == floor)
                {
                    result.Add(Nodes[i]);
                }
            }

            result.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            return result;
        }

        public int MaxFloor
        {
            get
            {
                int max = 0;
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].Floor > max)
                    {
                        max = Nodes[i].Floor;
                    }
                }

                return max;
            }
        }
    }

    /// <summary>地图生成器。同一 seed 生成同一张图，便于调试与录像。</summary>
    public static class MapGenerator
    {
        public static ChapterMap Generate(int chapter, int seed)
        {
            var rng = new DeterministicRng(seed == 0 ? 20261024 + chapter * 137 : seed);
            var map = new ChapterMap { Chapter = EnemyRoster.ClampChapter(chapter) };

            int floors = EnemyRoster.FloorsPerChapter;
            int nextId = 1;

            // 每层的节点数：中段最宽，头尾收窄
            var floors_ = new List<List<MapNode>>();

            for (int f = 0; f < floors; f++)
            {
                var layer = new List<MapNode>();

                if (f == floors - 1)
                {
                    // 最后一层：只放 Boss
                    layer.Add(NewNode(ref nextId, map, NodeKind.Boss, f, 0, 1));
                    layer[0].X = 0.5f;
                    layer[0].Y = 0f;
                }
                else
                {
                    int count = FloorWidth(f, floors, rng);
                    for (int s = 0; s < count; s++)
                    {
                        var kind = PickKind(f, floors, s, count, rng);
                        var node = NewNode(ref nextId, map, kind, f, s, count);
                        layer.Add(node);
                    }
                }

                floors_.Add(layer);
            }

            // 强制：倒数第二层（Boss 前）放一个篝火，保证战前有喘息
            if (floors >= 2)
            {
                var preBoss = floors_[floors - 2];
                for (int i = 0; i < preBoss.Count; i++)
                {
                    preBoss[i].Kind = NodeKind.Rest;
                }
            }

            // 强制：第一层全是普通战，最后两层之前保证有精英
            if (floors_.Count > 0)
            {
                var first = floors_[0];
                for (int i = 0; i < first.Count; i++)
                {
                    first[i].Kind = NodeKind.Battle;
                }
            }

            EnsureElite(floors_, floors, rng);
            Connect(map, floors_, rng);
            EnsureSupplyOnPaths(map, floors_, floors, rng);
            AssignY(map, floors);

            return map;
        }

        /// <summary>
        /// 保证每条通往 Boss 的路线都够补给。
        ///
        /// 篝火只是「地图上有」不够用：它可能挂在一条玩家根本不会走的支路上，
        /// 于是整章打下来一次都补不到血，七八场战斗的损耗累积到最后一层必死。
        /// 这里从起点正向推一遍「哪些层在任何路线上都必经」，把中段每隔几层的
        /// 必经节点改成篝火，让补给点落在玩家一定会经过的地方。
        /// </summary>
        private static void EnsureSupplyOnPaths(ChapterMap map, List<List<MapNode>> layers, int floors, DeterministicRng rng)
        {
            // 倒数第二层已经整层是篝火，Boss 层不参与，两端都跳过
            int from = 2;
            int to = floors - 3;
            if (to - from < 4)
            {
                return;
            }

            // 每章三处中途补给。一章 12 层要打七八场，每场掉 10~20 血，
            // 两处补给（各补 45%）刚好被损耗吃掉，第三处才开始有净收益。
            int[] targets = { from, from + (to - from) / 2, to };
            for (int t = 0; t < targets.Length; t++)
            {
                int floor = targets[t];
                if (floor < 0 || floor >= layers.Count)
                {
                    continue;
                }

                var layer = layers[floor];
                if (layer.Count == 0)
                {
                    continue;
                }

                // 优先改「整层唯一」的节点——那必然是必经点；否则挑中间那个
                int index = layer.Count == 1 ? 0 : rng.Range(0, layer.Count);
                if (layer[index].Kind == NodeKind.Battle)
                {
                    layer[index].Kind = NodeKind.Rest;
                }
            }
        }

        private static MapNode NewNode(ref int nextId, ChapterMap map, NodeKind kind, int floor, int slot, int count)
        {
            var node = new MapNode
            {
                Id = nextId++,
                Kind = kind,
                Floor = floor,
                Slot = slot,
                X = count <= 1 ? 0.5f : 0.14f + 0.72f * slot / (count - 1)
            };

            map.Nodes.Add(node);
            return node;
        }

        private static int FloorWidth(int floor, int totalFloors, DeterministicRng rng)
        {
            if (floor == 0)
            {
                return 2;
            }

            if (floor >= totalFloors - 2)
            {
                return 2;
            }

            // 中段 3~4 个
            int mid = rng.Next() % 100;
            return mid < 55 ? 3 : 4;
        }

        /// <summary>
        /// 节点类型分布：普通战为主，穿插篝火/事件/商店/宝箱，精英每章 2~3 个。
        /// 篝火权重给得偏高——玩家生命是跨节点保留的，回血点太少会把整局变成一次长跑。
        /// </summary>
        private static NodeKind PickKind(int floor, int totalFloors, int slot, int count, DeterministicRng rng)
        {
            int roll = rng.Range(0, 100);

            // 每层至少留一条战斗线：同一层里 slot 0 一定是战斗
            if (slot == 0)
            {
                return NodeKind.Battle;
            }

            if (roll < 38)
            {
                return NodeKind.Battle;
            }

            if (roll < 58)
            {
                return NodeKind.Rest;
            }

            if (roll < 70)
            {
                return NodeKind.Event;
            }

            if (roll < 80)
            {
                return NodeKind.Treasure;
            }

            if (roll < 90)
            {
                return NodeKind.Shop;
            }

            // 精英只放在中后段（第 7 层以后）——此时玩家已经过 5~6 次补强，
            // 牌组才有能力处理「高血量 + 开场荆棘」这种硬骨头。
            return floor >= 7 ? NodeKind.Elite : NodeKind.Battle;
        }

        /// <summary>保证每章至少两个精英节点。</summary>
        private static void EnsureElite(List<List<MapNode>> layers, int floors, DeterministicRng rng)
        {
            int eliteCount = 0;
            for (int f = 1; f < floors - 2; f++)
            {
                for (int i = 0; i < layers[f].Count; i++)
                {
                    if (layers[f][i].Kind == NodeKind.Elite)
                    {
                        eliteCount++;
                    }
                }
            }

            int need = 2 - eliteCount;
            int guard = 0;
            while (need > 0 && guard++ < 60)
            {
                int f = rng.Range(7, Math.Max(8, floors - 2));
                if (f < 7 || f >= layers.Count)
                {
                    continue;
                }

                var layer = layers[f];
                if (layer.Count == 0)
                {
                    continue;
                }

                int index = rng.Range(0, layer.Count);
                if (layer[index].Kind == NodeKind.Battle)
                {
                    layer[index].Kind = NodeKind.Elite;
                    need--;
                }
            }
        }

        /// <summary>
        /// 连线：相邻层按横向距离就近连边，并保证每个节点至少有一条入边和一条出边。
        /// </summary>
        private static void Connect(ChapterMap map, List<List<MapNode>> layers, DeterministicRng rng)
        {
            for (int f = 0; f < layers.Count - 1; f++)
            {
                var current = layers[f];
                var next = layers[f + 1];

                if (current.Count == 0 || next.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < current.Count; i++)
                {
                    var from = current[i];

                    // 按横向位置寻找最近的 1~2 个后继
                    int nearest = Nearest(next, from.X);
                    Link(from, next[nearest]);

                    // 有一定概率再连一个相邻的，形成分叉
                    if (next.Count > 1 && rng.Range(0, 100) < 55)
                    {
                        int second = nearest;
                        if (nearest == 0)
                        {
                            second = 1;
                        }
                        else if (nearest == next.Count - 1)
                        {
                            second = next.Count - 2;
                        }
                        else
                        {
                            second = rng.Range(0, 2) == 0 ? nearest - 1 : nearest + 1;
                        }

                        if (second != nearest)
                        {
                            Link(from, next[second]);
                        }
                    }
                }

                // 保证下一层每个节点都有入边
                for (int i = 0; i < next.Count; i++)
                {
                    if (next[i].Prev.Count == 0)
                    {
                        int near = Nearest(current, next[i].X);
                        Link(current[near], next[i]);
                    }
                }
            }
        }

        private static void Link(MapNode from, MapNode to)
        {
            if (from == null || to == null)
            {
                return;
            }

            if (!from.Next.Contains(to.Id))
            {
                from.Next.Add(to.Id);
            }

            if (!to.Prev.Contains(from.Id))
            {
                to.Prev.Add(from.Id);
            }
        }

        private static int Nearest(List<MapNode> nodes, float x)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = Math.Abs(nodes[i].X - x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>把层号映射成 0..1 的纵向坐标，第一层在下、Boss 在上。</summary>
        private static void AssignY(ChapterMap map, int floors)
        {
            int maxFloor = Math.Max(1, map.MaxFloor);
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                map.Nodes[i].Y = (float)map.Nodes[i].Floor / maxFloor;
            }
        }
    }
}
