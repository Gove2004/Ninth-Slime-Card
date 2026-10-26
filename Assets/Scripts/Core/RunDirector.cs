using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>一局（Run）的当前状态。</summary>
    public enum RunPhase
    {
        Map = 0,        // 在地图上选节点
        Battle = 1,     // 战斗中
        Reward = 2,     // 战斗奖励选择
        Rest = 3,       // 篝火
        Shop = 4,       // 商店
        Event = 5,      // 随机事件
        Treasure = 6,   // 宝箱
        Defeat = 7,     // 本局结束
        Victory = 8     // 通关
    }

    /// <summary>
    /// 一局流程。玩家章节推进、章内走随机节点地图：
    /// 地图 → 节点（战斗/精英/宝箱/篝火/商店/事件）→ 回地图 → … → Boss → 下一章。
    /// </summary>
    public sealed class RunDirector
    {
        public readonly CardDatabase Cards;
        public readonly AchievementDatabase Achievements;
        public readonly MetaState Meta;

        private readonly DeterministicRng rng;

        public readonly List<CardInstance> Deck = new List<CardInstance>();

        public int Chapter = 1;
        public ChapterMap Map;
        /// <summary>本章地图的生成种子，随存档一起保存，用于读档后复现同一张地图。</summary>
        public int MapSeed;
        /// <summary>当前所在节点 id（未进入任何节点时为 0）。</summary>
        public int CurrentNodeId;
        /// <summary>已经访问过、可以继续前进的节点集合。</summary>
        public readonly HashSet<int> Reachable = new HashSet<int>();

        public RunPhase Phase = RunPhase.Map;

        /// <summary>金币。商店与事件用。</summary>
        public int Gold;

        /// <summary>玩家当前生命（跨节点保留，篝火与事件可以回复）。</summary>
        public int Hp;
        public int HpMax;

        /// <summary>本局已获得的金币总量（成就统计）。</summary>
        public int GoldEarned;

        public readonly List<AchievementDef> JustEvaluated = new List<AchievementDef>();

        // 本次结算信息（供界面消费）
        public EnemyDef LastEnemy;
        public bool LastWasElite;
        public bool LastWasBoss;
        public bool RunJustEnded;

        public RunDirector(CardDatabase cards, AchievementDatabase achievements, MetaState meta, int seed)
        {
            Cards = cards ?? CardDatabase.CreateFallback();
            Achievements = achievements;
            Meta = meta ?? new MetaState();
            rng = new DeterministicRng(seed == 0 ? 20261024 : seed);
        }

        // ---------------------------------------------------------- 进度

        public int DeckCount { get { return Deck.Count; } }

        public bool AtDeckCap { get { return Deck.Count >= DeckRules.DeckCap; } }

        public MapNode CurrentNode { get { return Map != null ? Map.Get(CurrentNodeId) : null; } }

        public string ChapterName { get { return EnemyRoster.ChapterNames[EnemyRoster.ClampChapter(Chapter) - 1]; } }

        public string ChapterSubtitle { get { return EnemyRoster.ChapterSubtitles[EnemyRoster.ClampChapter(Chapter) - 1]; } }

        /// <summary>本局已推进的层数，用于 UI 显示进度。</summary>
        public int Floor
        {
            get
            {
                var node = CurrentNode;
                return node != null ? node.Floor : 0;
            }
        }

        public void StartNewRun()
        {
            Chapter = 1;
            Gold = 0;
            GoldEarned = 0;
            Phase = RunPhase.Map;
            RunJustEnded = false;
            CurrentNodeId = 0;
            Reachable.Clear();

            Deck.Clear();
            Deck.AddRange(DeckRules.BuildStarter(Cards));

            HpMax = EnemyRoster.PlayerHpForChapter(Chapter);
            Hp = HpMax;

            Meta.RunsPlayed++;
            DiscoverDeck();
            BuildChapterMap();
            SyncRun();
        }

        /// <summary>
        /// 主动放弃本局：清掉局内进度，但保留已解锁的卡牌与成就。
        /// 不清 RunDeck 的话，玩家下次点「继续冒险」会被送回刚刚放弃的那张地图。
        /// </summary>
        public void AbandonRun()
        {
            Deck.Clear();
            Meta.ClearRun();
            CurrentNodeId = 0;
            MapSeed = 0;
            Reachable.Clear();
            Map = null;
            Phase = RunPhase.Map;
            RunJustEnded = false;
        }

        public void EnsureRun()
        {
            if (Deck.Count > 0)
            {
                return;
            }

            // 有可续的局就续上
            if (Meta.RunDeck.Count > 0 && Meta.RunChapter > 0)
            {
                Chapter = EnemyRoster.ClampChapter(Meta.RunChapter);
                HpMax = Meta.RunHpMax > 0 ? Meta.RunHpMax : EnemyRoster.PlayerHpForChapter(Chapter);
                Hp = Math.Max(1, Math.Min(HpMax, Meta.RunHp));
                Gold = Meta.RunGold;
                for (int i = 0; i < Meta.RunDeck.Count; i++)
                {
                    var def = Cards.Get(Meta.RunDeck[i]);
                    if (def != null)
                    {
                        var card = CardInstance.Wrap(def);
                        // 旧存档没有等级行，长度对不上时按未升级处理
                        if (i < Meta.RunDeckLevels.Count)
                        {
                            card.Level = Math.Max(0, Meta.RunDeckLevels[i]);
                        }

                        Deck.Add(card);
                    }
                }

                BuildChapterMap(Meta.RunMapSeed);

                // 旧存档没有地图种子，读出来是 0 → 会重建一张全新的地图，
                // 原来的 NodeId 在上面可能根本不存在，那样 Reachable 会是空的，
                // 玩家就卡在一张走不动的地图上。找不到就退回起点，损失一层进度但能继续玩。
                var resume = Meta.RunNodeId != 0 ? Map.Get(Meta.RunNodeId) : null;
                CurrentNodeId = resume != null ? resume.Id : 0;
                if (resume != null)
                {
                    resume.Visited = true;
                }

                RebuildReachable();
                DiscoverDeck();
                Phase = RunPhase.Map;
                return;
            }

            StartNewRun();
            Meta.RunsPlayed--;
        }

        private void SyncRun()
        {
            Meta.RunChapter = Chapter;
            Meta.RunHp = Hp;
            Meta.RunHpMax = HpMax;
            Meta.RunGold = Gold;
            Meta.RunNodeId = CurrentNodeId;
            Meta.RunMapSeed = MapSeed;
            Meta.RunDeck.Clear();
            Meta.RunDeckLevels.Clear();
            for (int i = 0; i < Deck.Count; i++)
            {
                Meta.RunDeck.Add(Deck[i].Def.Id);
                Meta.RunDeckLevels.Add(Deck[i].Level);
            }
        }

        private void DiscoverDeck()
        {
            for (int i = 0; i < Deck.Count; i++)
            {
                Meta.Discover(Deck[i].Def.Id);
            }
        }

        private void BuildChapterMap()
        {
            BuildChapterMap(0);
        }

        /// <summary>
        /// 生成当前章节的地图。seed 为 0 时随机取一个新种子并记下来——种子必须持久化，
        /// 否则读档后 CurrentNodeId 会指向另一张地图上的节点（地图每次启动都不同）。
        /// </summary>
        private void BuildChapterMap(int seed)
        {
            if (seed == 0)
            {
                seed = (int)(rng.Next() + Chapter * 977);
            }

            MapSeed = seed;
            Map = MapGenerator.Generate(Chapter, seed);

            CurrentNodeId = 0;
            Reachable.Clear();

            // 起点：第一层的全部节点可达
            var first = Map.FloorNodes(0);
            for (int i = 0; i < first.Count; i++)
            {
                Reachable.Add(first[i].Id);
            }
        }

        private void RebuildReachable()
        {
            Reachable.Clear();

            if (CurrentNodeId == 0)
            {
                var first = Map.FloorNodes(0);
                for (int i = 0; i < first.Count; i++)
                {
                    Reachable.Add(first[i].Id);
                }

                return;
            }

            var node = Map.Get(CurrentNodeId);
            if (node == null)
            {
                return;
            }

            for (int i = 0; i < node.Next.Count; i++)
            {
                Reachable.Add(node.Next[i]);
            }
        }

        /// <summary>当前可前往的节点。</summary>
        public List<MapNode> ReachableNodes()
        {
            var result = new List<MapNode>();
            foreach (int id in Reachable)
            {
                var node = Map.Get(id);
                if (node != null)
                {
                    result.Add(node);
                }
            }

            result.Sort((a, b) =>
            {
                if (a.Floor != b.Floor) return a.Floor.CompareTo(b.Floor);
                return a.Slot.CompareTo(b.Slot);
            });

            return result;
        }

        /// <summary>
        /// 主页展示用的「下一个对手」。
        /// 不等于真实下一战：可达节点里可能只有篝火或商店，这时退化为本章普通怪，
        /// 只作氛围展示，不参与任何战斗结算。
        /// </summary>
        public EnemyDef NextEnemyPreview
        {
            get
            {
                var node = NextBattleNode();
                if (node != null)
                {
                    bool elite = node.Kind == NodeKind.Elite;
                    bool boss = node.Kind == NodeKind.Boss;
                    EnemyDef basis;
                    if (boss)
                    {
                        basis = EnemyRoster.BossOf(Chapter);
                    }
                    else
                    {
                        // 预览不能用 rng：主页每次重绘都抽一次会把随机序列打乱，
                        // 让实战的敌人生成变得不可复现。
                        var pool = elite ? EnemyRoster.ElitePool(Chapter) : EnemyRoster.NormalPool(Chapter);
                        basis = pool.Count > 0 ? pool[0] : EnemyRoster.Default;
                    }

                    return Scale(basis, Chapter, node.Floor, elite);
                }

                var fallback = EnemyRoster.NormalPool(Chapter);
                return fallback.Count > 0 ? fallback[0] : EnemyRoster.Default;
            }
        }

        /// <summary>可达节点里最靠前的一场战斗，用于预览。</summary>
        private MapNode NextBattleNode()
        {
            var nodes = ReachableNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                var kind = nodes[i].Kind;
                if (kind == NodeKind.Battle || kind == NodeKind.Elite || kind == NodeKind.Boss)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        /// <summary>进入一个节点，把阶段切到对应的流程。</summary>
        public bool EnterNode(int nodeId)
        {
            if (!Reachable.Contains(nodeId))
            {
                return false;
            }

            var node = Map.Get(nodeId);
            if (node == null)
            {
                return false;
            }

            CurrentNodeId = nodeId;
            node.Visited = true;

            switch (node.Kind)
            {
                case NodeKind.Battle:
                case NodeKind.Elite:
                case NodeKind.Boss:
                    Phase = RunPhase.Battle;
                    break;

                case NodeKind.Treasure:
                    Phase = RunPhase.Treasure;
                    break;

                case NodeKind.Rest:
                    Phase = RunPhase.Rest;
                    break;

                case NodeKind.Shop:
                    Phase = RunPhase.Shop;
                    break;

                case NodeKind.Event:
                    Phase = RunPhase.Event;
                    break;
            }

            SyncRun();
            return true;
        }

        /// <summary>离开当前节点（战斗胜利后、事件结束、商店离开）。</summary>
        public void LeaveNode()
        {
            RebuildReachable();
            Phase = RunPhase.Map;
            SyncRun();
        }

        // ---------------------------------------------------------- 战斗

        /// <summary>当前节点对应的敌人（含章节/层数缩放）。</summary>
        public EnemyDef BuildEnemy()
        {
            var node = CurrentNode;
            bool elite = node != null && node.Kind == NodeKind.Elite;
            bool boss = node != null && node.Kind == NodeKind.Boss;

            EnemyDef baseDef;
            if (boss)
            {
                baseDef = EnemyRoster.BossOf(Chapter);
            }
            else if (elite)
            {
                var pool = EnemyRoster.ElitePool(Chapter);
                baseDef = pool[rng.Range(0, pool.Count)];
            }
            else
            {
                var pool = EnemyRoster.NormalPool(Chapter);
                baseDef = pool[rng.Range(0, pool.Count)];
            }

            return Scale(baseDef, Chapter, Floor, elite);
        }

        /// <summary>按章节与层数生成一个缩放后的战斗实例（不修改模板）。</summary>
        private EnemyDef Scale(EnemyDef template, int chapter, int floor, bool elite)
        {
            var scaled = new EnemyDef
            {
                Id = template.Id,
                Name = template.Name,
                Title = template.Title,
                Family = template.Family,
                Blurb = template.Blurb,
                Sprite = template.Sprite,
                Chapter = template.Chapter,
                Accent = template.Accent,
                Seed = template.Seed,
                IsElite = elite,
                IsBoss = template.IsBoss,
                ActionsPerTurn = template.ActionsPerTurn,
                Hp = EnemyRoster.ScaleHp(template.Hp, chapter, floor, elite),
                PlayerHp = HpMax
            };

            foreach (var pair in template.StartStatuses)
            {
                scaled.StartStatuses[pair.Key] = pair.Value;
            }

            for (int i = 0; i < template.Moves.Count; i++)
            {
                var m = template.Moves[i];
                scaled.Moves.Add(new EnemyMove
                {
                    Label = m.Label,
                    Kind = m.Kind,
                    Damage = m.Damage > 0 ? EnemyRoster.ScaleDamage(m.Damage, chapter, floor, elite) : 0,
                    Hits = m.Hits,
                    Shield = m.Shield > 0 ? m.Shield + (chapter - 1) * 3 : 0,
                    Heal = m.Heal > 0 ? m.Heal + (chapter - 1) * 2 : 0,
                    Status = m.Status,
                    StatusStacks = m.StatusStacks,
                    Weight = m.Weight,
                    Cooldown = m.Cooldown,
                    OnlyBelowHpPct = m.OnlyBelowHpPct
                });
            }

            return scaled;
        }

        public BattleSession CreateBattle(int seed)
        {
            var enemy = BuildEnemy();
            LastEnemy = enemy;
            var node = CurrentNode;
            LastWasElite = node != null && node.Kind == NodeKind.Elite;
            LastWasBoss = node != null && node.Kind == NodeKind.Boss;

            var rules = BattleRules.Default();
            // 能量随章节成长。敌人血量逐章抬升，玩家如果始终只有 3 点能量，
            // 输出就不会变强，Boss 战必然变成单方面挨打。
            rules.EnergyPerTurn = 3 + (Chapter - 1);
            rules.DrawPerTurn = Chapter >= 3 ? 6 : 5;

            var session = new BattleSession(Cards, enemy, Deck, seed, rules);
            session.Chapter = Chapter;
            session.Floor = Floor;
            session.IsElite = LastWasElite;
            session.IsBoss = LastWasBoss;

            // 把跨节点保留的生命带进战斗
            session.Player.Hp = Math.Max(1, Math.Min(session.Player.HpMax, Hp));

            session.Begin();
            return session;
        }

        /// <summary>战斗结束回收血量。</summary>
        public void CapturePlayerHp(BattleSession session)
        {
            if (session == null || session.Player == null)
            {
                return;
            }

            Hp = Math.Max(0, Math.Min(HpMax, session.Player.Hp));
        }

        // ---------------------------------------------------------- 结算

        public int SettleVictory(BattleSession session)
        {
            CapturePlayerHp(session);

            var facts = BattleFacts.Capture(session, Hp);
            Meta.Wins++;

            int gold = LastWasBoss ? 80 : LastWasElite ? 45 : 20 + Floor * 2;
            Gold += gold;
            GoldEarned += gold;

            Accumulate(facts);

            RunJustEnded = false;

            if (LastWasBoss)
            {
                // 击败章节 Boss：进入下一章
                if (Chapter >= EnemyRoster.TotalChapters)
                {
                    Phase = RunPhase.Victory;
                    RunJustEnded = true;
                    Meta.Flags.Add("run_cleared");
                    Meta.RunsCleared++;
                }
                else
                {
                    Chapter++;
                    if (Chapter > Meta.BestChapter)
                    {
                        Meta.BestChapter = Chapter;
                    }

                    HpMax = EnemyRoster.PlayerHpForChapter(Chapter);
                    // 章节交界是明确的休整点：新章节的生命上限提高，并且回满。
                    // 不给这个恢复，玩家会带着 20% 残血进入数值高一档的章节，第二场必死。
                    Hp = HpMax;
                    BuildChapterMap();
                    Phase = RunPhase.Map;
                }
            }
            else
            {
                Phase = RunPhase.Reward;
            }

            SyncRun();
            return gold;
        }

        public void SettleDefeat(BattleSession session)
        {
            CapturePlayerHp(session);
            var facts = BattleFacts.Capture(session, Hp);
            Meta.Losses++;
            Accumulate(facts);

            Phase = RunPhase.Defeat;
            RunJustEnded = true;

            // 阵亡即本局结束，清掉局内存档。
            // 留着的话主页还会显示「继续冒险」，玩家点进去会带着 1 点生命
            // 站在刚才把自己打死的那一层——等于白送一次跳过这场战斗的机会。
            // 成就、解锁的卡牌与奖杯属于跨局进度，全部保留。
            Deck.Clear();
            Meta.ClearRun();
        }

        private void Accumulate(BattleFacts facts)
        {
            Meta.AddCounter("score", facts.DamageDealt);
            Meta.AddCounter("draw", facts.CardsDrawn);
            Meta.AddCounter("play", facts.CardsPlayed);
            Meta.AddCounter("energy", facts.EnergyGained);
            Meta.AddCounter("heal", facts.Healed);

            if (facts.BestTurnDamage >= 25)
            {
                Meta.Flags.Add("overheat");
            }

            if (facts.SelfDamageTaken >= 10 && facts.Victory)
            {
                Meta.Flags.Add("self_hurt");
            }

            if (facts.TurnCount <= 3 && facts.Victory)
            {
                Meta.Flags.Add("fast_kill");
            }

            if (facts.Victory && LastWasElite)
            {
                Meta.Flags.Add("elite_slayer");
            }

            if (facts.Victory && LastWasBoss)
            {
                Meta.Flags.Add("boss_slayer");
            }
        }

        // ---------------------------------------------------------- 奖励

        /// <summary>战斗奖励：普通战 3 选 1，精英战 4 选 1。</summary>
        public List<CardDef> BuildRewardChoices(int count)
        {
            var pool = DeckRules.RewardPool(Cards, Chapter, LastWasElite);
            var result = new List<CardDef>();
            if (pool.Count == 0)
            {
                return result;
            }

            // 保证系列分散（颜色拉开），避免三个都是同一系
            var groups = new List<List<CardDef>>();
            for (int i = 0; i < pool.Count; i++)
            {
                List<CardDef> group = null;
                for (int k = 0; k < groups.Count; k++)
                {
                    if (groups[k][0].Series == pool[i].Series)
                    {
                        group = groups[k];
                        break;
                    }
                }

                if (group == null)
                {
                    group = new List<CardDef>();
                    groups.Add(group);
                }

                group.Add(pool[i]);
            }

            rng.Shuffle(groups);
            for (int i = 0; i < groups.Count; i++)
            {
                rng.Shuffle(groups[i]);
            }

            for (int i = 0; i < groups.Count && result.Count < count; i++)
            {
                result.Add(groups[i][0]);
            }

            // 不够就随机补齐
            if (result.Count < count)
            {
                var bag = new List<CardDef>(pool);
                for (int i = 0; i < result.Count; i++)
                {
                    bag.Remove(result[i]);
                }

                while (bag.Count > 0 && result.Count < count)
                {
                    int index = rng.Range(0, bag.Count);
                    result.Add(bag[index]);
                    bag.RemoveAt(index);
                }
            }

            return result;
        }

        /// <summary>宝箱：直接给一张稀有度更高的牌。</summary>
        public CardDef RollTreasureCard()
        {
            var pool = new List<CardDef>();
            for (int i = 0; i < Cards.All.Count; i++)
            {
                if (Cards.All[i].Rarity >= 1)
                {
                    pool.Add(Cards.All[i]);
                }
            }

            if (pool.Count == 0)
            {
                pool.AddRange(Cards.All);
            }

            return pool.Count == 0 ? null : pool[rng.Range(0, pool.Count)];
        }

        public void MarkSeen(IEnumerable<CardDef> cards)
        {
            if (cards == null)
            {
                return;
            }

            foreach (var def in cards)
            {
                if (def != null)
                {
                    Meta.Discover(def.Id);
                }
            }
        }

        public void TakeReward(CardDef def)
        {
            if (def == null)
            {
                return;
            }

            Deck.Add(CardInstance.Wrap(def));
            Meta.Discover(def.Id);
            SyncRun();
        }

        public bool RemoveFromDeck(int cardId)
        {
            for (int i = 0; i < Deck.Count; i++)
            {
                if (Deck[i].Def.Id == cardId)
                {
                    Deck.RemoveAt(i);
                    SyncRun();
                    return true;
                }
            }

            return false;
        }

        /// <summary>升级牌组里的一张牌。</summary>
        public bool UpgradeCard(int index)
        {
            if (index < 0 || index >= Deck.Count)
            {
                return false;
            }

            var card = Deck[index];
            if (card.Level >= 1)
            {
                return false;
            }

            card.Level = 1;
            SyncRun();
            return true;
        }

        /// <summary>
        /// 篝火能回多少血（界面显示用）。与 RestHeal 共用同一个公式，
        /// 避免界面上的数字和实际回复量不一致。
        /// </summary>
        public int RestHealPreview
        {
            get { return Math.Max(30, HpMax * 60 / 100); }
        }

        /// <summary>
        /// 篝火：回复生命。
        /// 一章 12 层、每场战斗掉 8~15 血、生命上限只有 80~125，
        /// 一次篝火至少要补回「三场战斗的损耗」才可能走完一章——按 60% 给。
        /// </summary>
        public int RestHeal()
        {
            int amount = RestHealPreview;
            Hp = Math.Min(HpMax, Hp + amount);
            SyncRun();
            return amount;
        }

        public Dictionary<int, int> DeckComposition()
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < Deck.Count; i++)
            {
                int id = Deck[i].Def.Id;
                map[id] = map.ContainsKey(id) ? map[id] + 1 : 1;
            }

            return map;
        }

        // ---------------------------------------------------------- 成就

        public bool MeetsRequirement(AchievementDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (!def.IsCounter)
            {
                return Meta.Flags.Contains(def.Id);
            }

            return Meta.Counter(def.CounterKey) >= def.Threshold;
        }

        public long Progress(AchievementDef def)
        {
            if (def == null)
            {
                return 0;
            }

            return def.IsCounter ? Meta.Counter(def.CounterKey) : (Meta.Flags.Contains(def.Id) ? 1 : 0);
        }

        public bool TryPurchase(AchievementDef def, out string message)
        {
            if (def == null)
            {
                message = "成就不存在";
                return false;
            }

            if (Meta.IsUnlocked(def.Id))
            {
                message = "该成就已解锁";
                return false;
            }

            if (!MeetsRequirement(def))
            {
                message = "进度不足：" + Progress(def) + " / " + def.Threshold;
                return false;
            }

            if (Meta.Trophy < def.TrophyCost)
            {
                message = "奖杯不足（需要 " + def.TrophyCost + "）";
                return false;
            }

            Meta.Trophy -= def.TrophyCost;
            Meta.Unlocked.Add(def.Id);
            message = "成就解锁：" + def.Name;
            return true;
        }

        public List<AchievementDef> CollectNewlyReady()
        {
            JustEvaluated.Clear();
            if (Achievements == null)
            {
                return JustEvaluated;
            }

            for (int i = 0; i < Achievements.All.Count; i++)
            {
                var def = Achievements.All[i];
                if (!Meta.IsUnlocked(def.Id) && MeetsRequirement(def) && def.TrophyCost <= Meta.Trophy)
                {
                    JustEvaluated.Add(def);
                }
            }

            return JustEvaluated;
        }

        public int DiscoveredCount { get { return Meta.DiscoveredCards.Count; } }

        public int TotalCards { get { return Cards.Count; } }
    }
}
