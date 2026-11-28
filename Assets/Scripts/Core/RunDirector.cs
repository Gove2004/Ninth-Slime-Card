using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>一局（Run）的当前状态。</summary>
    public enum RunPhase
    {
        Chapter = 0,    // 在章节卡牌里选一张
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
    /// 一局流程：每层发一组遭遇卡，玩家选一张进入对应内容，结算完推进到下一层。
    /// 层号是唯一进度，摆在面前的牌只由 (CardSeed, Chapter, Floor) 决定。
    /// </summary>
    public sealed class RunDirector
    {
        public readonly CardDatabase Cards;
        public readonly AchievementDatabase Achievements;
        public readonly MetaState Meta;

        private readonly DeterministicRng rng;

        public readonly List<CardInstance> Deck = new List<CardInstance>();

        public int Chapter = 1;
        /// <summary>当前层号（0 起）。选完一张卡、内容结算完才 +1。</summary>
        public int Floor;
        /// <summary>整局的发牌种子，随存档保存，读档后复现同一组遭遇卡。</summary>
        public int CardSeed;
        /// <summary>本层摆在面前的遭遇卡。</summary>
        public readonly List<EncounterCard> Hand = new List<EncounterCard>();

        /// <summary>已选中的那张卡；Hand 会随层数重建，内容结算期间得靠这三个值记住选了什么。</summary>
        public NodeKind ActiveKind;
        public int ActiveSeed;
        public int ActiveSlot = -1;
        /// <summary>遭遇卡上写着的对手。BuildEnemy 按它生成，不二次抽取。</summary>
        public string ActiveEnemyId;

        public RunPhase Phase = RunPhase.Chapter;

        /// <summary>当前局是否为无限年模式（由 Meta.RunMode 持久化）。</summary>
        public bool Endless { get { return Meta != null && Meta.RunMode == 1; } }

        /// <summary>
        /// 无限年的难度阶梯：每 10 层 +1 档（虚拟章节）。普通模式恒等于 Chapter。
        /// </summary>
        public int VirtualChapter
        {
            get { return Endless ? EnemyRoster.TotalChapters + Floor / ChapterCardDealer.EndlessSegment : Chapter; }
        }

        /// <summary>金币。商店与事件用。</summary>
        public int Gold;

        /// <summary>玩家当前生命（跨层保留，篝火与事件可以回复）。</summary>
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
        /// <summary>刚结束的一局是不是无限年。结算会把 RunMode 清零，界面展示只能从这里读。</summary>
        public bool LastRunWasEndless;
        /// <summary>刚结束的一局到达的层数（1 起）。无限年结算屏展示用。</summary>
        public int LastRunFloor;

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

        public string ChapterName
        {
            get
            {
                return Endless
                    ? "无限年"
                    : EnemyRoster.ChapterNames[EnemyRoster.ClampChapter(Chapter) - 1];
            }
        }

        public string ChapterSubtitle
        {
            get
            {
                return Endless
                    ? "越走越深，敌人无限变强"
                    : EnemyRoster.ChapterSubtitles[EnemyRoster.ClampChapter(Chapter) - 1];
            }
        }

        public void StartNewRun()
        {
            StartNewRun(false);
        }

        /// <summary>
        /// 开新局。<paramref name="endless"/> = true 进入无限年：
        /// 新春构筑起手，Chapter 固定在第四章（全部怪池可用），难度由 VirtualChapter 接管。
        /// </summary>
        public void StartNewRun(bool endless)
        {
            Meta.RunMode = endless ? 1 : 0;
            Chapter = endless ? EnemyRoster.TotalChapters : 1;
            Gold = 0;
            GoldEarned = 0;
            Phase = RunPhase.Chapter;
            RunJustEnded = false;
            Floor = 0;

            Deck.Clear();
            if (endless)
            {
                Deck.AddRange(DeckRules.BuildEndlessStarter(Cards));
            }
            else
            {
                Deck.AddRange(DeckRules.BuildStarter(Cards));
            }

            HpMax = EnemyRoster.PlayerHpForChapter(Chapter);
            Hp = HpMax;

            Meta.RunsPlayed++;
            DiscoverDeck();
            NewCardSeed();
            DealHand();
            SyncRun();
        }

        /// <summary>
        /// 主动放弃本局：清掉局内进度，但保留已解锁的卡牌与成就。
        /// 不清 RunDeck 的话，玩家下次点「继续冒险」会被送回刚放弃的那一层。
        /// </summary>
        public void AbandonRun()
        {
            Deck.Clear();
            Meta.ClearRun();
            Floor = 0;
            CardSeed = 0;
            Hand.Clear();
            ActiveSlot = -1;
            Phase = RunPhase.Chapter;
            RunJustEnded = false;
        }

        public void EnsureRun()
        {
            if (Deck.Count > 0)
            {
                return;
            }

            // 有可续的局就续上。发牌只认 (种子, 章节, 层号)，所以不需要存整组卡。
            if (Meta.RunDeck.Count > 0 && Meta.RunChapter > 0)
            {
                Chapter = EnemyRoster.ClampChapter(Meta.RunChapter);
                HpMax = Meta.RunHpMax > 0 ? Meta.RunHpMax : EnemyRoster.PlayerHpForChapter(Chapter);
                Hp = Math.Max(1, Math.Min(HpMax, Meta.RunHp));
                Gold = Meta.RunGold;
                Floor = Math.Max(0, Meta.RunFloor);
                CardSeed = Meta.RunCardSeed;
                for (int i = 0; i < Meta.RunDeck.Count; i++)
                {
                    var def = Cards.Get(Meta.RunDeck[i]);
                    if (def != null)
                    {
                        var card = CardInstance.Wrap(def);
                        if (i < Meta.RunDeckLevels.Count)
                        {
                            card.Level = Math.Max(0, Meta.RunDeckLevels[i]);
                        }

                        Deck.Add(card);
                    }
                }

                if (CardSeed == 0)
                {
                    NewCardSeed();
                }

                DealHand();
                DiscoverDeck();
                Phase = RunPhase.Chapter;
                return;
            }

            StartNewRun();
            Meta.RunsPlayed--;
        }

        private void NewCardSeed()
        {
            CardSeed = (int)(rng.Next() + Chapter * 977);
            if (CardSeed == 0)
            {
                CardSeed = 20261024;
            }
        }

        private void DealHand()
        {
            Hand.Clear();
            Hand.AddRange(ChapterCardDealer.Deal(Chapter, Floor, CardSeed, Endless));
            ActiveSlot = -1;
            ActiveSeed = 0;
            ActiveKind = NodeKind.Battle;
            ActiveEnemyId = null;
        }

        private void SyncRun()
        {
            Meta.RunChapter = Chapter;
            Meta.RunHp = Hp;
            Meta.RunHpMax = HpMax;
            Meta.RunGold = Gold;
            Meta.RunFloor = Floor;
            Meta.RunCardSeed = CardSeed;
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

        /// <summary>
        /// 选中本层的一张遭遇卡并进入对应内容。选完不推进层数——
        /// 战斗、事件、商店都可能在半途退出，层数留到 Advance 才加。
        /// </summary>
        public bool ChooseCard(int slot)
        {
            if (Phase != RunPhase.Chapter || slot < 0 || slot >= Hand.Count)
            {
                return false;
            }

            var card = Hand[slot];
            ActiveSlot = slot;
            ActiveKind = card.Kind;
            ActiveSeed = card.Seed;
            ActiveEnemyId = card.EnemyId;

            switch (card.Kind)
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

        /// <summary>当前这张卡结算完毕，推进到下一层并重新发牌。</summary>
        public void Advance()
        {
            Floor++;
            DealHand();
            Phase = RunPhase.Chapter;
            SyncRun();
        }

        /// <summary>本层卡组里第一场战斗的对手，用于预览展示。</summary>
        public EnemyDef NextEnemyPreview
        {
            get
            {
                bool elite = false;
                bool boss = false;
                string enemyId = null;
                for (int i = 0; i < Hand.Count; i++)
                {
                    if (string.IsNullOrEmpty(Hand[i].EnemyId))
                    {
                        continue;
                    }

                    elite = Hand[i].Kind == NodeKind.Elite;
                    boss = Hand[i].Kind == NodeKind.Boss;
                    enemyId = Hand[i].EnemyId;
                    break;
                }

                EnemyDef basis;
                if (boss)
                {
                    basis = Endless ? EnemyRoster.EndlessBossFor(Floor) : EnemyRoster.BossOf(Chapter);
                }
                else if (enemyId != null)
                {
                    basis = EnemyRoster.Get(enemyId);
                }
                else
                {
                    var pool = elite ? EnemyRoster.ElitePool(Chapter) : EnemyRoster.NormalPool(Chapter);
                    basis = pool.Count > 0 ? pool[0] : EnemyRoster.Default;
                }

                return Scale(basis, VirtualChapter, Floor, elite);
            }
        }

        // ---------------------------------------------------------- 战斗

        /// <summary>选中遭遇卡对应的敌人（含章节/层数缩放）。</summary>
        public EnemyDef BuildEnemy()
        {
            bool elite = ActiveKind == NodeKind.Elite;
            bool boss = ActiveKind == NodeKind.Boss;

            EnemyDef baseDef;
            if (boss)
            {
                baseDef = Endless ? EnemyRoster.EndlessBossFor(Floor) : EnemyRoster.BossOf(Chapter);
            }
            else if (!string.IsNullOrEmpty(ActiveEnemyId))
            {
                // 卡面写了谁就打谁。EnemyRoster.Get 找不到时回退到蓝色史莱姆，不会返回 null。
                baseDef = EnemyRoster.Get(ActiveEnemyId);
            }
            else if (elite)
            {
                var pool = EnemyRoster.ElitePool(Chapter);
                baseDef = pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : EnemyRoster.Default;
            }
            else
            {
                var pool = EnemyRoster.NormalPool(Chapter);
                baseDef = pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : EnemyRoster.Default;
            }

            return Scale(baseDef, VirtualChapter, Floor, elite);
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
            LastWasElite = ActiveKind == NodeKind.Elite;
            LastWasBoss = ActiveKind == NodeKind.Boss;

            var rules = BattleRules.Default();
            // 能量随章节成长。敌人血量逐章抬升，玩家如果始终只有 3 点能量，
            // 输出就不会变强，Boss 战必然变成单方面挨打。
            // 无限年按虚拟章节继续涨，封顶 10——能量无上限会让深层变成一回合清屏表演。
            rules.EnergyPerTurn = Math.Min(3 + (VirtualChapter - 1), 10);
            // 手牌目标同样随章节 +1：后期牌库更杂，多一张选择才撑得住场面。
            // 注意这是「补齐到几张」，不是「抽几张」——见 BattleRules.HandTarget。
            rules.HandTarget = VirtualChapter >= 3 ? 6 : 5;

            var session = new BattleSession(Cards, enemy, Deck, seed, rules);
            session.Chapter = Chapter;
            session.Floor = Floor;
            session.IsElite = LastWasElite;
            session.IsBoss = LastWasBoss;

            // 把跨层保留的生命带进战斗
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

            if (Endless)
            {
                int reached = Floor + 1;
                if (reached > Meta.BestFloor)
                {
                    Meta.BestFloor = reached;
                }

                LastRunWasEndless = true;
                LastRunFloor = reached;

                if (LastWasBoss)
                {
                    // 段终：小抬生命上限并回复三成，接着继续往下发牌。
                    HpMax += 5;
                    Hp = Math.Min(HpMax, Hp + HpMax * 30 / 100);
                    Advance();
                }
                else
                {
                    Phase = RunPhase.Reward;
                }

                SyncRun();
                return gold;
            }

            if (LastWasBoss)
            {
                // 击败章节 Boss：进入下一章
                if (Chapter >= EnemyRoster.TotalChapters)
                {
                    Phase = RunPhase.Victory;
                    RunJustEnded = true;
                    Meta.Flags.Add("run_cleared");
                    Meta.Flags.Add("clear_hell");
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
                    Floor = 0;
                    DealHand();
                    Phase = RunPhase.Chapter;
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

            // 无限年阵亡也要记层数：死在第 9 层就是到达过第 9 层。
            if (Endless)
            {
                int reached = Floor + 1;
                if (reached > Meta.BestFloor)
                {
                    Meta.BestFloor = reached;
                }

                LastRunWasEndless = true;
                LastRunFloor = reached;
            }
            else
            {
                LastRunWasEndless = false;
            }

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
            // 键名必须是 mana：成就表里能量类成就写的是 counter:mana，
            // 这里写成 energy 的话那 8 条成就读的是一个永远为 0 的计数器，一辈子解不开。
            Meta.AddCounter("mana", facts.EnergyGained);
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

            // 下面几条写的是成就 ID，不是语义名：MeetsRequirement 直接拿 def.Id
            // 去 Flags 里找，写成别的名字成就就永远解不开（这六条一度就是这样失联的）。
            if (facts.CardsDrawn >= 16 && facts.CardsPlayed >= 12 && facts.BestTurnDamage >= 25)
            {
                Meta.Flags.Add("draw_draw");
            }

            if (facts.EnemyDebuffKinds >= 3)
            {
                Meta.Flags.Add("double_agent");
            }

            if (facts.Victory && Meta.Wins == 1)
            {
                Meta.Flags.Add("clear_easy");
            }

            if (facts.Victory && Hp * 10 <= HpMax)
            {
                Meta.Flags.Add("clear_hard");
            }

            // 「dddd」是后台早先建的一条测试成就，成就表已经不能再改，只能给它一个
            // 真实条件。绑在 3 回合内取胜上：它的成就步数也正好是 3。
            if (facts.TurnCount <= 3 && facts.Victory)
            {
                Meta.Flags.Add("test");
            }

            // 无限年里程碑。这两个 flag 将来绑成就，后台建成就行时要用同名 ID。
            if (Endless && Floor + 1 >= 10)
            {
                Meta.Flags.Add("endless_10");
            }

            if (Endless && Floor + 1 >= 30)
            {
                Meta.Flags.Add("endless_30");
            }
        }

        // ---------------------------------------------------------- 奖励

        /// <summary>战斗奖励：普通战 3 选 1，精英战 4 选 1。</summary>
        public List<CardDef> BuildRewardChoices(int count)
        {
            var pool = Endless
                ? DeckRules.EndlessRewardPool(Cards, Floor, LastWasElite)
                : DeckRules.RewardPool(Cards, Chapter, LastWasElite);
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

            // 无限年：新春系列锁定一个奖励位（池里有时必出现），
            // 这是模式给新春构筑的兑现通道，其余位仍走全池分散。
            if (Endless)
            {
                List<CardDef> spring = null;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i][0].Series == CardSeries.Spring)
                    {
                        spring = groups[i];
                        groups.RemoveAt(i);
                        break;
                    }
                }

                if (spring != null && result.Count < count)
                {
                    result.Add(spring[0]);
                }
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
            get { return Math.Max(26, HpMax * 45 / 100); }
        }

        /// <summary>
        /// 篝火：回复生命。
        ///
        /// 45% 而不是更高：一章 12 层里有 2~3 处篝火，回得太多就等于没有资源压力 ——
        /// 实测 60% 时打完整章还剩一半血，血条形同虚设。降到 45% 后，
        /// 篝火回到「补三场损耗」的量级，玩家必须开始考虑「这场值不值得打」。
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
