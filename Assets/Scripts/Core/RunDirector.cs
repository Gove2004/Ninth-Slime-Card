using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 单局（Run）流程：三幕九关廿七战，每场胜利补强牌组，打到第 27 战即通关。
    /// 牌组有容量上限——后期奖励会变成「换牌」，把弱牌换掉而不是无限堆牌。
    /// </summary>
    public sealed class RunDirector
    {
        public readonly CardDatabase Cards;
        public readonly AchievementDatabase Achievements;
        public readonly MetaState Meta;

        private readonly DeterministicRng rng;

        /// <summary>当前战序（1..27）。</summary>
        public int Battle = 1;

        public readonly List<int> DeckIds = new List<int>();

        /// <summary>上一次结算产生的成就（供界面弹提示）。</summary>
        public readonly List<AchievementDef> JustEvaluated = new List<AchievementDef>();

        public RunDirector(CardDatabase cards, AchievementDatabase achievements, MetaState meta, int seed)
        {
            Cards = cards ?? CardDatabase.CreateFallback();
            Achievements = achievements;
            Meta = meta ?? new MetaState();
            rng = new DeterministicRng(seed == 0 ? 20261001 : seed);
        }

        // ---------------------------------------------------------- 赛程进度

        /// <summary>当前关号（1..9）。</summary>
        public int Level { get { return EnemyRoster.LevelOf(Battle); } }

        /// <summary>当前关内场次（1 先锋 / 2 中坚 / 3 宗师）。</summary>
        public int Stage { get { return EnemyRoster.StageOf(Battle); } }

        /// <summary>当前是否是关底宗师战。</summary>
        public bool IsMasterStage { get { return EnemyRoster.IsMasterStage(Battle); } }

        public string StageLabel { get { return EnemyRoster.Describe(Battle); } }

        public EnemyDef CurrentEnemy { get { return EnemyRoster.ForBattle(Battle); } }

        public int DeckCount { get { return DeckIds.Count; } }

        public int DeckCap { get { return DeckRules.DeckCap; } }

        /// <summary>牌组已满，奖励必须走「替换」。</summary>
        public bool AtDeckCap { get { return DeckIds.Count >= DeckRules.DeckCap; } }

        public void StartNewRun()
        {
            Battle = 1;
            DeckIds.Clear();
            DeckIds.AddRange(DeckRules.StarterDeck);
            Meta.RunsPlayed++;
            DiscoverDeck();
            SyncRun();
        }

        public void EnsureRun()
        {
            if (DeckIds.Count > 0)
            {
                return;
            }

            // 优先续上存档里那一局——27 战的长赛程不该因为退出游戏而报废。
            if (Meta.RunDeck.Count > 0)
            {
                Battle = EnemyRoster.ClampBattle(Meta.RunBattle);
                DeckIds.AddRange(Meta.RunDeck);
                DiscoverDeck();
                return;
            }

            StartNewRun();
            Meta.RunsPlayed--; // EnsureRun 不算新开一局
        }

        /// <summary>把局内进度写回存档（每次牌组或战序变化后调用）。</summary>
        private void SyncRun()
        {
            Meta.RunBattle = Battle;
            Meta.RunDeck.Clear();
            Meta.RunDeck.AddRange(DeckIds);
        }

        private void DiscoverDeck()
        {
            for (int i = 0; i < DeckIds.Count; i++)
            {
                Meta.Discover(DeckIds[i]);
            }
        }

        public BattleSession CreateBattle(int seed)
        {
            EnsureRun();
            var session = new BattleSession(Cards, Battle, seed, DeckIds, CurrentEnemy);
            session.Begin();
            return session;
        }

        // ---------------------------------------------------------- 奖励

        /// <summary>
        /// 给出三张候选奖励牌。赛程制：击败某系宗师后，奖励以该宗师的系列为主——
        /// 对手越强，你收获的构筑方向越明确。
        /// </summary>
        public List<CardDef> BuildRewardChoices(int count)
        {
            var pool = DeckRules.RewardPool(Cards, Level);
            var result = new List<CardDef>();
            if (pool.Count == 0)
            {
                return result;
            }

            // 第一优先：刚击败宗师的系列（确保至少一半候选来自该系列）
            CardSeries? banner = LastDefeated != null ? LastDefeated.Accent : (CardSeries?)null;
            if (banner.HasValue)
            {
                var own = new List<CardDef>();
                foreach (var def in pool)
                {
                    if (def.Series == banner.Value && !result.Contains(def))
                    {
                        own.Add(def);
                    }
                }

                rng.Shuffle(own);
                for (int i = 0; i < own.Count && result.Count < (count + 1) / 2; i++)
                {
                    result.Add(own[i]);
                }
            }

            // 其余候选按系列分散（颜色拉开），排除已选与宗师系列
            var groups = new List<List<CardDef>>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (result.Contains(pool[i]) || (banner.HasValue && pool[i].Series == banner.Value))
                {
                    continue;
                }

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

            // 不够时全池随机补齐
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

        /// <summary>候选牌一旦出现就记入图鉴——单局看不到的牌，跨局也能慢慢集齐。</summary>
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

            DeckIds.Add(def.Id);
            Meta.Discover(def.Id);
            SyncRun();
        }

        /// <summary>移除牌组中的一张（替换奖励用）。</summary>
        public bool RemoveFromDeck(int cardId)
        {
            int index = DeckIds.IndexOf(cardId);
            if (index < 0)
            {
                return false;
            }

            DeckIds.RemoveAt(index);
            SyncRun();
            return true;
        }

        public Dictionary<int, int> DeckComposition()
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < DeckIds.Count; i++)
            {
                int id = DeckIds[i];
                map[id] = map.ContainsKey(id) ? map[id] + 1 : 1;
            }

            return map;
        }

        // ---------------------------------------------------------- 结算

        /// <summary>刚刚被击败的对手（奖励按其系列发放）。</summary>
        public EnemyDef LastDefeated { get; private set; }

        /// <summary>本局是否已通关（曾打穿廿七战）。</summary>
        public bool RunCleared { get { return Meta.Flags.Contains("run_cleared"); } }

        /// <summary>刚打穿廿七战（结算当帧有效）。</summary>
        public bool JustClearedRun { get; private set; }

        /// <summary>刚打完的这一场是不是关底宗师战（决定奖励轮数）。</summary>
        public bool LastWasMasterStage { get; private set; }

        /// <summary>
        /// 胜利后推进战序并结算奖杯。奖杯随战序线性增长，宗师战额外加成，
        /// 让「打穿一关」与「打穿一场」在收益上有明显区别。
        /// </summary>
        public int SettleVictory(BattleFacts facts)
        {
            LastDefeated = EnemyRoster.ForBattle(Battle);
            LastWasMasterStage = EnemyRoster.IsMasterStage(Battle);
            JustClearedRun = false;

            int reward = 10 + Battle * 4 + (LastWasMasterStage ? 40 : 0);
            Meta.Trophy += reward;
            Meta.Wins++;

            if (Battle > Meta.BestBattle)
            {
                Meta.BestBattle = Battle;
            }

            if (Level > Meta.BestLevel)
            {
                Meta.BestLevel = Level;
            }

            Accumulate(facts);

            if (Battle >= EnemyRoster.FinalBattle)
            {
                // 打穿终关：记录通关，停在终关（重开一局再战）。
                JustClearedRun = true;
                Meta.Flags.Add("run_cleared");
                Meta.BestBattle = EnemyRoster.FinalBattle;
            }
            else
            {
                Battle++;
            }

            SyncRun();
            return reward;
        }

        public void SettleDefeat(BattleFacts facts)
        {
            Meta.Losses++;
            Accumulate(facts);

            // 赛程不会因为一场失败清空：牌组与战序都留着，可以从本战重来。
            SyncRun();
        }

        private void Accumulate(BattleFacts facts)
        {
            Meta.AddCounter("score", facts.DamageDealt);
            Meta.AddCounter("draw", facts.CardsDrawn);
            Meta.AddCounter("play", facts.CardsPlayed);
            Meta.AddCounter("mana", facts.ManaGained);
            Meta.AddCounter("heal", facts.Healed);

            if (facts.CardsDrawn >= 2 && facts.CardsPlayed >= 2 && facts.BestTurnDamage >= 20)
            {
                Meta.Flags.Add("draw_draw");
            }

            if (facts.BestTurnDamage >= 30)
            {
                Meta.Flags.Add("overheat");
            }

            if (facts.SelfDamageTaken >= 10 && facts.Victory)
            {
                Meta.Flags.Add("self_hurt");
            }

            if (facts.EnemyCardsStolen > 0)
            {
                Meta.Flags.Add("double_agent");
            }

            if (facts.Victory)
            {
                // 赛程制下改用战序判定：初战 / 挺进第二幕 / 打穿廿七战。
                if (facts.Battle >= 1) Meta.Flags.Add("clear_easy");
                if (facts.Battle >= 10) Meta.Flags.Add("clear_hard");
                if (facts.Battle >= EnemyRoster.FinalBattle) Meta.Flags.Add("clear_hell");
            }
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

        /// <summary>用奖杯兑换成就。要求进度达标且奖杯足够。</summary>
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

        /// <summary>统计后自动检查是否有可解锁的成就，返回新达成的列表。</summary>
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
