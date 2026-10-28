using System;
using System.Collections.Generic;

namespace Slime.Core
{
    public enum BattlePhase
    {
        Setup = 0,
        PlayerTurn = 1,
        EnemyTurn = 2,
        Ended = 3
    }

    public enum EventKind
    {
        Info = 0,
        Damage = 1,
        Heal = 2,
        Shield = 3,
        Energy = 4,
        Draw = 5,
        Play = 6,
        Status = 7,
        Debuff = 8,
        Discard = 9,
        EnemyIntent = 10,

        /// <summary>
        /// 兑现时刻：把攒了几回合的层数/护盾一次性换成伤害。
        /// 单独一个类型，是因为它必须被强烈地表现出来 —— 玩家如果看不到
        /// 「6 层冻结炸了 36 点」，就不会明白自己刚才那一手为什么强。
        /// </summary>
        Payoff = 11
    }

    /// <summary>一条战斗播报。视图层消费它做飘字、震屏与提示。</summary>
    public struct BattleEvent
    {
        public EventKind Kind;
        public string Text;
        public int Value;
        /// <summary>true 表示作用于敌方。</summary>
        public bool OnEnemy;
    }

    /// <summary>
    /// 战斗配置。把回合抽牌、手牌上限、能量等从常量里解放出来，
    /// 让遗物、事件、Boss 机制都能改写规则。
    /// </summary>
    public sealed class BattleRules
    {
        /// <summary>
        /// 手牌目标张数。每回合开始**补齐到这个数**，而不是固定抽 N 张。
        ///
        /// 两种做法的差别是玩法层面的：
        /// - 固定抽 N 张：回合末弃光 → 每回合永远是同一个开局手感，上回合打掉几张毫无记忆点；
        /// - 补齐到 N 张：开局 5 张，打出 3 张后回合结束，下回合只补 3 张。
        ///   上一回合剩多少，直接决定这一回合有多少选择，玩家会开始为「留着不打」付代价，
        ///   也会为「这回合必须打完，好把好牌洗回去」做计算。
        ///
        /// 配合回合末弃牌，手牌永远不会堆积，也不会出现「每回合都满手、永远出不完」的拖沓感。
        /// </summary>
        public int HandTarget = 5;

        /// <summary>单张牌的效果最多能把抽牌推到多少张（防止「抽到死」类死循环）。</summary>
        public int HandLimit = 10;

        /// <summary>开局手牌数。</summary>
        public int StartingHand = 5;

        public int EnergyPerTurn = 3;

        /// <summary>超过这个回合数后双方开始流失生命，防止无限拉锯。</summary>
        public int SoftTurnLimit = 30;

        public static BattleRules Default()
        {
            return new BattleRules();
        }
    }

    /// <summary>
    /// 一局 1v1 回合制卡牌对决。
    ///
    /// 规则要点：
    /// - 每回合能量回满（不是累加），抽 / 打 / 弃构成牌库循环，弃牌堆耗尽时洗回；
    /// - 敌人有「可读意图」，玩家回合开始时就能看到它下回合要做什么；
    /// - 状态是唯一的力量来源，全部按层数计量并在回合结束统一衰减。
    /// </summary>
    public sealed class BattleSession
    {
        private readonly CardDatabase db;
        private readonly DeterministicRng rng;
        private readonly List<BattleEvent> events = new List<BattleEvent>();

        private readonly BattleRules rules;
        private readonly EnemyDef enemyDef;
        private readonly EnemyBrain brain;

        public readonly BattleUnit Player;
        public readonly BattleUnit Enemy;

        public int TurnNumber;
        public BattlePhase Phase = BattlePhase.Setup;
        public bool Victory;
        public bool Defeat;

        /// <summary>本场战斗的关卡信息（地图节点带来）。</summary>
        public int Chapter = 1;
        public int Floor;
        public bool IsElite;
        public bool IsBoss;

        public BattleRules Rules { get { return rules; } }

        public EnemyDef EnemyInfo { get { return enemyDef; } }

        public int DamageDealtByPlayer { get { return Player.DamageDealtTotal; } }
        public int CardsPlayedByPlayer { get { return Player.CardsPlayedTotal; } }
        public int CardsDrawnTotal { get; private set; }
        public int ManaGainedTotal { get; private set; }
        public int HealedTotal { get { return Player.HealedTotal; } }
        public int BestTurnDamage { get; private set; }

        /// <summary>玩家在本场战斗中对敌人施加过的不同减益种类数（同一状态重复施加只算一种）。</summary>
        public int EnemyDebuffKinds { get { return enemyDebuffsApplied.Count; } }

        private readonly HashSet<StatusKind> enemyDebuffsApplied = new HashSet<StatusKind>();

        public BattleSession(CardDatabase database, EnemyDef enemy, IEnumerable<CardInstance> deck, int seed, BattleRules battleRules = null)
        {
            db = database ?? CardDatabase.CreateFallback();
            enemyDef = enemy ?? EnemyRoster.Default;
            rules = battleRules ?? BattleRules.Default();
            rng = new DeterministicRng(seed == 0 ? 20261024 : seed);

            Player = new BattleUnit { IsPlayer = true, Name = "勇者" };
            Enemy = new BattleUnit { IsPlayer = false, Name = enemyDef.Name };

            BuildPlayer(deck);
            BuildEnemy();
            brain = new EnemyBrain(enemyDef, rng);
        }

        // ------------------------------------------------------------ 初始化

        private void BuildPlayer(IEnumerable<CardInstance> deck)
        {
            Player.HpMax = enemyDef.PlayerHp;
            Player.Hp = Player.HpMax;
            Player.EnergyMax = rules.EnergyPerTurn;
            Player.Energy = 0;

            var cards = new List<CardInstance>();
            if (deck != null)
            {
                foreach (var card in deck)
                {
                    if (card != null)
                    {
                        cards.Add(card.Clone());
                    }
                }
            }

            if (cards.Count == 0)
            {
                cards.AddRange(DeckRules.BuildStarter(db));
            }

            Player.LoadDeck(cards);
            rng.Shuffle(Player.DrawPile);
        }

        private void BuildEnemy()
        {
            Enemy.HpMax = enemyDef.Hp;
            Enemy.Hp = Enemy.HpMax;
            Enemy.EnergyMax = 3;
            Enemy.Energy = 0;

            // 敌人不再持有实体牌组——它靠 EnemyBrain 出招，牌面只是展示。
            // 这样敌人的行为可以精确设计（多段、附状态、蓄力），而不是从卡池里随机抽。
            foreach (var status in enemyDef.StartStatuses)
            {
                Enemy.SetStatus(status.Key, status.Value);
            }
        }

        /// <summary>开局：先手抽牌，玩家先行动。</summary>
        public void Begin()
        {
            Phase = BattlePhase.PlayerTurn;
            TurnNumber = 1;
            Player.ResetTurnStats();
            Enemy.ResetTurnStats();

            Draw(Player, rules.StartingHand);
            RefreshPlayerEnergy();
            PlanEnemyIntent();

            Emit(EventKind.Info, "第 1 回合 · 你的行动", 0, false);
        }

        // ------------------------------------------------------------ 玩家回合

        /// <summary>开始新的玩家回合（第一回合由 Begin 处理）。</summary>
        public void StartPlayerTurn()
        {
            if (Phase == BattlePhase.Ended)
            {
                return;
            }

            TurnNumber++;
            Phase = BattlePhase.PlayerTurn;
            Player.ResetTurnStats();
            Enemy.ResetTurnStats();

            TickStartOfTurn(Player, Enemy);
            if (CheckEnd())
            {
                return;
            }

            if (SkipTurnIfIncapacitated(Player))
            {
                EndPlayerTurn();
                return;
            }

            // 补牌式：把手牌补齐到目标张数，而不是固定抽 N 张。
            // 上一回合剩几张，这一回合就只补几张 —— 打出多少、补回多少。
            // 致盲扣的是「目标张数」，不是「抽取张数」，所以致盲期间手牌会更薄。
            int target = rules.HandTarget - Player.GetStatus(StatusKind.Blind);
            int drawCount = Math.Max(0, target) - Player.Hand.Count;
            Draw(Player, Math.Max(0, drawCount));
            RefreshPlayerEnergy();

            // 能力牌在本回合的抽牌与能量之后结算：turn:energy 与 turn:draw
            // 才能在本回合就生效，而不是等下一回合。
            TickPowers(Player, Enemy);
            if (CheckEnd())
            {
                return;
            }

            PlanEnemyIntent();
            Emit(EventKind.Info, "第 " + TurnNumber + " 回合 · 你的行动", 0, false);
        }

        private void RefreshPlayerEnergy()
        {
            Player.Energy = Player.EnergyMax;
            Emit(EventKind.Energy, "能量回满 " + Player.Energy, Player.Energy, false);
        }

        public bool CanPlay(CardInstance card)
        {
            return card != null && Phase == BattlePhase.PlayerTurn && card.Cost <= Player.Energy;
        }

        public bool PlayCard(int handIndex)
        {
            if (Phase != BattlePhase.PlayerTurn)
            {
                return false;
            }

            if (handIndex < 0 || handIndex >= Player.Hand.Count)
            {
                return false;
            }

            var card = Player.Hand[handIndex];
            if (!CanPlay(card))
            {
                return false;
            }

            PlayInternal(Player, Enemy, handIndex);
            CheckEnd();
            return true;
        }

        private void PlayInternal(BattleUnit self, BattleUnit foe, int handIndex)
        {
            var card = self.Hand[handIndex];
            var def = card.Def;

            self.Energy -= card.Cost;
            self.Hand.RemoveAt(handIndex);

            self.CardsPlayedThisTurn++;
            self.CardsPlayedTotal++;
            if (def.Kind == CardKind.Attack)
            {
                self.AttacksPlayedThisTurn++;
                self.PlayedAttackThisTurn = true;
            }
            else if (def.Kind == CardKind.Skill)
            {
                self.SkillsPlayedThisTurn++;
            }
            else if (def.Kind == CardKind.Spell)
            {
                self.SpellsPlayedThisTurn++;
            }

            Emit(EventKind.Play, def.Name, card.Cost, !self.IsPlayer);

            // 能力牌拆成两半：带 turn: 前缀的 op 登记下来每回合重放，
            // 其余（加力量、加护盾这类一次性效果）照常当场结算。
            var instantOps = new List<EffectOp>();
            var perTurnOps = new List<EffectOp>();
            var onPlayOps = new List<EffectOp>();
            SplitOps(def.Ops, instantOps, perTurnOps, onPlayOps);

            if (perTurnOps.Count > 0)
            {
                self.Powers.Add(new PowerRuntime { Def = def, Src = card, PerTurn = perTurnOps });
            }

            if (onPlayOps.Count > 0)
            {
                self.Powers.Add(new PowerRuntime
                {
                    Def = def,
                    Src = card,
                    OnPlay = onPlayOps,
                    ExpireEndOfTurn = def.Kind != CardKind.Power
                });
            }

            ResolveOps(self, foe, instantOps, card);

            // 本回合有效的 onplay 效果要在出牌后立即触发一次 —— 打出这张牌的
            // 动作本身就是「打出一张牌」，不触发的话第一张永远白给。
            TickOnPlay(self, foe);

            // 能力牌（Power）打出后整场生效，不进弃牌堆
            bool exhaust = card.Exhaust || def.Exhaust || def.Kind == CardKind.Power;
            if (exhaust)
            {
                self.ExhaustPile.Add(card);
            }
            else
            {
                self.DiscardPile.Add(card);
            }
        }

        public void EndPlayerTurn()
        {
            if (Phase != BattlePhase.PlayerTurn)
            {
                return;
            }

            int thisTurn = Player.DamageDealtThisTurn;
            if (thisTurn > BestTurnDamage)
            {
                BestTurnDamage = thisTurn;
            }

            // 回合结束**保留手牌**，只把超出的部分裁掉。
            //
            // 这是补牌式抽牌成立的前提：如果回合末整手弃光，「补齐到 5 张」就等价于
            // 「每回合固定抽 5 张」，两种做法没有区别。保留手牌之后，
            // 打出去几张、下回合就只补几张，手牌数量被钉在目标张数上，
            // 既不会堆积（不用囤牌），也不会出现「永远满手、永远出不完」的拖沓。
            //
            // 裁到 HandTarget 而不是 HandLimit：效果抽牌允许临时超出手牌目标，
            // 但超出部分不该跨回合留下来变成永久牌库。
            TrimHand(Player, rules.HandTarget);
            ExpireOnPlayPowers(Player);

            TickEndOfTurn(Player);
            if (CheckEnd())
            {
                return;
            }

            Phase = BattlePhase.EnemyTurn;
        }

        // ------------------------------------------------------------ 敌人回合

        public void StartEnemyTurn()
        {
            if (Phase == BattlePhase.Ended || Phase == BattlePhase.PlayerTurn)
            {
                return;
            }

            Phase = BattlePhase.EnemyTurn;
            Enemy.ResetTurnStats();

            TickStartOfTurn(Enemy, Player);
            if (CheckEnd())
            {
                return;
            }

            TickPowers(Enemy, Player);
            if (CheckEnd())
            {
                return;
            }

            if (SkipTurnIfIncapacitated(Enemy))
            {
                FinishEnemyTurn();
            }
        }

        /// <summary>
        /// 执行敌人的意图。返回 false 表示这个回合的动作已经做完。
        /// 视图层逐段调用以便逐条演出。
        /// </summary>
        public bool EnemyActOnce()
        {
            if (Phase != BattlePhase.EnemyTurn || CheckEnd())
            {
                return false;
            }

            int guard = 0;
            while (guard++ < 8)
            {
                if (TurnNumber <= rules.SoftTurnLimit)
                {
                    // 每回合多次意图：Boss 可连续行动
                    if (brain.Queue.Count == 0)
                    {
                        return false;
                    }

                    var intent = brain.Queue.Dequeue();
                    ExecuteIntent(intent);
                    if (CheckEnd())
                    {
                        return false;
                    }

                    return brain.Queue.Count > 0;
                }

                return false;
            }

            return false;
        }

        private void FinishEnemyTurn()
        {
            if (Phase == BattlePhase.Ended)
            {
                return;
            }

            TickEndOfTurn(Enemy);
            if (CheckEnd())
            {
                return;
            }

            ApplyAttrition();
            Phase = BattlePhase.PlayerTurn;
        }

        /// <summary>敌人回合并发执行（视图层不必逐段演出时使用）。</summary>
        public void RunEnemyTurn()
        {
            if (Phase != BattlePhase.EnemyTurn)
            {
                return;
            }

            int guard = 0;
            while (brain.Queue.Count > 0 && guard++ < 12)
            {
                if (CheckEnd())
                {
                    return;
                }

                var intent = brain.Queue.Dequeue();
                ExecuteIntent(intent);
            }

            FinishEnemyTurn();
        }

        private void ExecuteIntent(EnemyIntent intent)
        {
            if (intent == null)
            {
                return;
            }

            Emit(EventKind.EnemyIntent, Enemy.Name + " · " + intent.Describe(Enemy), intent.EffectiveDamage(Enemy), true);

            switch (intent.Kind)
            {
                case IntentKind.Attack:
                    // 力量必须在这里加进去。敌人攒「力量」的招式（低吼、战吼、龙威……）
                    // 本来就是为了让后面的攻击变疼，不加的话那些招式等于空过一回合。
                    int strike = intent.EffectiveDamage(Enemy);
                    for (int i = 0; i < intent.Hits; i++)
                    {
                        if (CheckEnd())
                        {
                            return;
                        }

                        DealDamage(Enemy, Player, strike, true);
                    }

                    break;

                case IntentKind.Defend:
                    GainShield(Enemy, intent.Value);
                    break;

                case IntentKind.Buff:
                    if (intent.AppliesStatus != StatusKind.None)
                    {
                        Enemy.AddStatus(intent.AppliesStatus, intent.StatusStacks);
                        Emit(EventKind.Status, Enemy.Name + " 获得 " + Statuses.DisplayName(intent.AppliesStatus) + " " + intent.StatusStacks, intent.StatusStacks, true);
                    }

                    break;

                case IntentKind.Debuff:
                    if (intent.AppliesStatus != StatusKind.None)
                    {
                        ApplyStatus(Enemy, Player, intent.AppliesStatus, intent.StatusStacks);
                    }

                    break;

                case IntentKind.AttackDebuff:
                    DealDamage(Enemy, Player, intent.Value, true);
                    if (!CheckEnd() && intent.AppliesStatus != StatusKind.None)
                    {
                        ApplyStatus(Enemy, Player, intent.AppliesStatus, intent.StatusStacks);
                    }

                    break;

                case IntentKind.Heal:
                    Heal(Enemy, intent.Value);
                    break;
            }
        }

        /// <summary>根据敌人定义生成本回合意图（玩家回合开始时调用，供 UI 展示）。</summary>
        private void PlanEnemyIntent()
        {
            brain.Plan(Enemy, Player, TurnNumber, IsElite, IsBoss);
            if (brain.Queue.Count > 0)
            {
                Emit(EventKind.EnemyIntent, "敌方意图：" + brain.Queue.Peek().Describe(Enemy), brain.Queue.Peek().EffectiveDamage(Enemy), true);
            }
        }

        /// <summary>当前展示给玩家的敌方意图。</summary>
        public EnemyIntent CurrentIntent
        {
            get { return brain.Queue.Count > 0 ? brain.Queue.Peek() : null; }
        }

        // ------------------------------------------------------------ 回合钩子

        private void TickStartOfTurn(BattleUnit unit, BattleUnit foe)
        {
            // 控制免疫只覆盖「刚醒来的那一回合」，这里先清掉
            if (unit.ControlImmunity > 0)
            {
                unit.ControlImmunity--;
            }

            // 中毒：回合开始失去生命
            int poison = unit.GetStatus(StatusKind.Poison);
            if (poison > 0)
            {
                LoseHp(unit, poison);
                Emit(EventKind.Debuff, unit.Name + " 中毒 -" + poison, poison, !unit.IsPlayer);
                unit.AddStatus(StatusKind.Poison, -1);
            }

            // 回灵：回合开始回血
            int aura = unit.GetStatus(StatusKind.RegenAura);
            if (aura > 0)
            {
                Heal(unit, aura);
            }

            // 护盾清空放在自己回合的**最后**：此时敌人的整个回合已经打完，
            // 盾该吃的伤害都吃到了，剩下的才是真正「没用上」的部分。
            //
            // 不能放在回合结束（TickEndOfTurn）：那发生在玩家交牌之后、敌人动手之前，
            // 盾会在敌人出招前归零 —— 玩家叠的盾一次都用不上。
            // 也不能放在回合开始的第一行：中毒无视护盾，先散盾会让「护盾撑过了敌方回合」
            // 这条信息丢失，结算顺序也变得无法验证。
            //
            // 对称性：敌人自己叠的盾同样保留到玩家的回合打完，
            // 玩家看到的「护盾 8」是实打实会吃掉 8 点伤害的。
            if (unit.Shield > 0)
            {
                Emit(EventKind.Shield, unit.Name + " 护盾消散 " + unit.Shield, unit.Shield, !unit.IsPlayer);
                unit.Shield = 0;
            }
        }

        private void TickEndOfTurn(BattleUnit unit)
        {
            // 再生：回合结束回血
            int regen = unit.GetStatus(StatusKind.Regen);
            if (regen > 0)
            {
                Heal(unit, regen);
            }

            // 灼烧：回合结束结算后减半
            int burn = unit.GetStatus(StatusKind.Burn);
            if (burn > 0)
            {
                LoseHp(unit, burn);
                Emit(EventKind.Debuff, unit.Name + " 灼烧 -" + burn, burn, !unit.IsPlayer);
            }

            unit.DecayStatuses();

            // 护盾不在这里清。清在这里等于「敌人还没动手，盾先没了」，
            // 正确时机是持有者下一个回合开始时（见 TickStartOfTurn）。
        }

        /// <summary>返回 true 表示本回合因控制效果被跳过。</summary>
        private bool SkipTurnIfIncapacitated(BattleUnit unit)
        {
            if (!unit.IsIncapacitated)
            {
                return false;
            }

            var kind = unit.HasStatus(StatusKind.Stun) ? StatusKind.Stun : StatusKind.Freeze;
            Emit(EventKind.Debuff, unit.Name + " 被" + Statuses.DisplayName(kind) + "，跳过本回合", 0, !unit.IsPlayer);

            // 这里不再手动扣冻结层数：本回合结束的 DecayStatuses 已经会 -1。
            // 两处都扣的话，2 层冻结和 1 层完全等价（跳过时 -1、结束时再 -1 就归零），
            // 卡面写着「冻结 2 回合」实际只锁 1 回合，多给的层数白费。
            if (kind == StatusKind.Stun)
            {
                unit.SetStatus(StatusKind.Stun, 0);
            }

            // 醒来后给一回合免疫，避免被同一招连续锁住
            unit.ControlImmunity = 1;
            return true;
        }

        /// <summary>拉锯保险：超过软上限后双方每回合流失递增生命。</summary>
        private void ApplyAttrition()
        {
            if (TurnNumber <= rules.SoftTurnLimit)
            {
                return;
            }

            int pressure = TurnNumber - rules.SoftTurnLimit;
            LoseHp(Player, pressure);
            LoseHp(Enemy, pressure);
            Emit(EventKind.Debuff, "牌局拖太久：双方流失 " + pressure + " 点生命", pressure, false);
            CheckEnd();
        }

        // ------------------------------------------------------------ 效果解释

        private void ResolveOps(BattleUnit self, BattleUnit foe, List<EffectOp> ops, CardInstance src)
        {
            if (ops == null)
            {
                return;
            }

            for (int i = 0; i < ops.Count; i++)
            {
                if (Phase == BattlePhase.Ended)
                {
                    return;
                }

                Exec(self, foe, ops[i], src);
            }
        }

        /// <summary>
        /// 把一条效果链拆成「打出时结算」与「每回合重放」两份。
        /// 条件块不能整个丢进任一边：turn:if:lowhp|heal:v1 这种写法里，
        /// 条件本身要留到每回合执行时才判断，而 if 里可能同时混着两类 op，
        /// 所以遇到 if 就递归拆开，再各自拼回一个带原条件的副本。
        /// </summary>
        private static void SplitOps(List<EffectOp> ops, List<EffectOp> instant, List<EffectOp> perTurn,
            List<EffectOp> onPlay = null)
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

                if (op.PerTurn)
                {
                    perTurn.Add(op);
                    continue;
                }

                if (op.OnPlay)
                {
                    if (onPlay != null)
                    {
                        onPlay.Add(op);
                    }

                    continue;
                }

                if (!op.IsIf)
                {
                    instant.Add(op);
                    continue;
                }

                var thenInstant = new List<EffectOp>();
                var thenTurn = new List<EffectOp>();
                var thenPlay = new List<EffectOp>();
                var elseInstant = new List<EffectOp>();
                var elseTurn = new List<EffectOp>();
                var elsePlay = new List<EffectOp>();
                SplitOps(op.Then, thenInstant, thenTurn, thenPlay);
                SplitOps(op.Else, elseInstant, elseTurn, elsePlay);

                if (thenInstant.Count > 0 || elseInstant.Count > 0)
                {
                    instant.Add(RebuildIf(op, thenInstant, elseInstant));
                }

                if (thenTurn.Count > 0 || elseTurn.Count > 0)
                {
                    perTurn.Add(RebuildIf(op, thenTurn, elseTurn));
                }

                if (onPlay != null && (thenPlay.Count > 0 || elsePlay.Count > 0))
                {
                    onPlay.Add(RebuildIf(op, thenPlay, elsePlay));
                }
            }
        }

        private static EffectOp RebuildIf(EffectOp src, List<EffectOp> then, List<EffectOp> els)
        {
            var copy = src.ShallowClone();
            copy.Then = then;
            copy.Else = els.Count > 0 ? els : null;
            return copy;
        }

        /// <summary>回合开始时重放所有能力牌。</summary>
        private void TickPowers(BattleUnit unit, BattleUnit foe)
        {
            for (int i = 0; i < unit.Powers.Count; i++)
            {
                var power = unit.Powers[i];
                for (int j = 0; j < power.PerTurn.Count; j++)
                {
                    if (Phase == BattlePhase.Ended)
                    {
                        return;
                    }

                    Exec(unit, foe, power.PerTurn[j], power.Src);
                }
            }
        }

        /// <summary>
        /// 出牌后的触发点。每打出一张牌跑一次，让「本回合每出一张牌就抽一张」
        /// 这类效果真正跟着出牌节奏走，而不是只在回合开始跑一次。
        /// </summary>
        private void TickOnPlay(BattleUnit unit, BattleUnit foe)
        {
            for (int i = 0; i < unit.Powers.Count; i++)
            {
                var power = unit.Powers[i];
                for (int j = 0; j < power.OnPlay.Count; j++)
                {
                    if (Phase == BattlePhase.Ended)
                    {
                        return;
                    }

                    Exec(unit, foe, power.OnPlay[j], power.Src);
                }
            }
        }

        /// <summary>清掉只在本回合有效的 onplay 效果（技能牌打出的临时钩子）。</summary>
        private static void ExpireOnPlayPowers(BattleUnit unit)
        {
            for (int i = unit.Powers.Count - 1; i >= 0; i--)
            {
                if (unit.Powers[i].ExpireEndOfTurn)
                {
                    unit.Powers.RemoveAt(i);
                }
            }
        }

        private void Exec(BattleUnit self, BattleUnit foe, EffectOp op, CardInstance src)
        {
            if (op == null)
            {
                return;
            }

            if (op.IsIf)
            {
                bool passed = Test(self, foe, op);
                var branch = passed ? op.Then : op.Else;
                if (branch != null)
                {
                    for (int i = 0; i < branch.Count; i++)
                    {
                        Exec(self, foe, branch[i], src);
                    }
                }

                return;
            }

            int v1 = src != null ? src.Value1 : 0;
            int v2 = src != null ? src.Value2 : 0;
            int v3 = src != null ? src.Value3 : 0;

            bool isSpell = src != null && src.Def.Kind == CardKind.Spell;
            int bonus = SpellBonus(self, isSpell);

            switch (op.Kind)
            {
                case "dmg":
                    DealDamage(self, foe, op.A + AttackBonusFor(self, src), src != null && src.Def.Kind == CardKind.Attack);
                    break;

                // 穿心：无视护盾，直接打生命。走 DealDamage 的 ignoreShield 分支
                // 而不是 LoseHp，是为了仍吃到力量/专注/虚弱/易伤的修正 ——
                // 它是一记攻击，只是绕开了盾。
                case "pierce":
                    DealDamage(self, foe, op.A + AttackBonusFor(self, src),
                        src != null && src.Def.Kind == CardKind.Attack, ignoreShield: true);
                    break;

                // 斩铁：这一击对护盾双倍，溢出部分按原值进生命。
                // 结算时先把目标当前护盾记下来，伤害过盾时按两倍扣。
                case "shieldbreak":
                    DealDamage(self, foe, op.A + AttackBonusFor(self, src),
                        src != null && src.Def.Kind == CardKind.Attack, shieldDamageMultiplier: 2);
                    break;

                case "vdmg":
                    DealDamage(self, foe, op.A + AttackBonusFor(self, src), src != null && src.Def.Kind == CardKind.Attack);
                    break;

                case "spdmg":
                    DealDamage(self, foe, op.A + bonus, false);
                    break;

                case "selfdmg":
                    LoseHp(self, op.A);
                    break;

                case "heal":
                    Heal(self, op.A);
                    break;

                case "shield":
                    GainShield(self, op.A);
                    break;

                case "draw":
                    Draw(self, op.A);
                    break;

                case "discard":
                    DiscardRandom(self, op.A);
                    break;

                case "energy":
                    GainEnergy(self, op.A);
                    break;

                case "status":
                    ApplyStatus(self, foe, op.StatusKind, op.A);
                    break;

                case "sstatus":
                    ApplyStatus(self, self, op.StatusKind, op.A);
                    break;

                case "estatus":
                    ApplyStatus(self, foe, op.StatusKind, op.A);
                    break;

                case "heal_self":
                    Heal(self, op.A);
                    break;

                case "addcard":
                    AddCardToHand(self, op.A, op.B);
                    break;

                case "addrandom":
                    AddRandomToHand(self, Series.FromName(op.Text), op.A);
                    break;

                case "losehp":
                    LoseHp(self, op.A);
                    break;

                case "double_shield":
                    GainShield(self, self.Shield);
                    break;

                case "double_poison":
                    ApplyStatus(self, foe, StatusKind.Poison, foe.GetStatus(StatusKind.Poison));
                    break;

                case "multiply":
                    // 剑舞：重复「本回合已出牌数」次。
                    // 注意计数在 PlayInternal 里已经 ++ 过，所以这里要把自己减掉，
                    // 否则 3 费牌打出第 1 张就成了 2 段，和卡面「本回合已出牌数」不符。
                    {
                        int repeats = Math.Max(1, self.CardsPlayedThisTurn - 1);
                        for (int i = 0; i < repeats && !foe.IsDead; i++)
                        {
                            DealDamage(self, foe, op.A + AttackBonusFor(self, src),
                                src != null && src.Def.Kind == CardKind.Attack);
                        }
                    }
                    break;

                case "exhaust_hand":
                    ExhaustHand(self, op.A);
                    break;

                case "scry":
                    Scry(self, op.A);
                    break;

                case "cleanse":
                    self.ClearStatuses();
                    Emit(EventKind.Status, self.Name + " 净化了所有状态", 0, !self.IsPlayer);
                    break;

                // ---- 兑现类：把已经堆上去的层数换成伤害 ----
                // 这一类是「流派」的黏合剂。没有它，施加中毒/灼烧/冻结的牌只是
                // 一堆平行的数值选项，玩家永远攒不出 combo。
                case "detonate":
                    Detonate(self, foe, op.StatusKind, op.A);
                    break;

                case "consume_shield":
                    ConsumeShieldForDamage(self, foe, op.A);
                    break;

                default:
                    // 未知 op 静默忽略，避免旧数据炸掉整场战斗
                    break;
            }
        }

        private static int AttackBonusFor(BattleUnit unit, CardInstance src)
        {
            if (src != null && src.Def.Kind == CardKind.Attack)
            {
                return unit.AttackBonus;
            }

            return 0;
        }

        private static int SpellBonus(BattleUnit unit, bool isSpell)
        {
            return isSpell ? unit.SpellBonus : 0;
        }

        private bool Test(BattleUnit self, BattleUnit foe, EffectOp op)
        {
            switch (op.Cond)
            {
                case Condition.Always:
                    return true;

                case Condition.FirstCard:
                    return self.CardsPlayedThisTurn == 0;

                case Condition.ThirdCard:
                    return self.CardsPlayedThisTurn >= 2;

                case Condition.HalfHp:
                    return self.Hp * 2 <= self.HpMax;

                case Condition.LowHp:
                    return self.Hp * 10 <= self.HpMax * 3;

                case Condition.HasShield:
                    return self.Shield > 0;

                case Condition.EnemyHasShield:
                    return foe != null && foe.Shield > 0;

                case Condition.EnemyPoisoned:
                    return foe != null && foe.GetStatus(StatusKind.Poison) > 0;

                case Condition.EnemyVulnerable:
                    return foe != null && foe.GetStatus(StatusKind.Vulnerable) > 0;

                case Condition.EnemyBurning:
                    return foe != null && foe.GetStatus(StatusKind.Burn) > 0;

                case Condition.EnemyFrozen:
                    return foe != null && foe.GetStatus(StatusKind.Freeze) > 0;

                case Condition.EnemyLowHp:
                    return foe != null && foe.Hp * 10 <= foe.HpMax * 3;

                case Condition.SpellPlayed:
                    return self.SpellsPlayedThisTurn > 0;

                case Condition.HasStatus:
                    return self.GetStatus((StatusKind)op.CondValue) > 0;

                case Condition.HandCount:
                    return self.Hand.Count >= op.CondValue;

                case Condition.EnergyZero:
                    return self.Energy <= 0;

                case Condition.TookDamage:
                    return self.TookDamageThisTurn;

                case Condition.DrawPileLow:
                    return self.DrawPile.Count <= op.CondValue;

                case Condition.EveryOtherTurn:
                    return TurnNumber % 2 == 0;

                default:
                    return true;
            }
        }

        // ------------------------------------------------------------ 数值管线

        public void DealDamage(BattleUnit source, BattleUnit target, int raw, bool isAttack,
            bool ignoreShield = false, int shieldDamageMultiplier = 1)
        {
            if (raw <= 0 || target == null || Phase == BattlePhase.Ended)
            {
                return;
            }

            int amount = raw;

            // 虚弱：输出 -25%
            if (source.HasStatus(StatusKind.Weak))
            {
                amount = amount * 3 / 4;
            }

            // 易伤：受伤 +50%
            if (target.HasStatus(StatusKind.Vulnerable))
            {
                amount = amount * 3 / 2;
            }

            if (amount < 1)
            {
                amount = 1;
            }

            int applied = TakeDamage(target, amount, ignoreShield, shieldDamageMultiplier);
            source.DamageDealtTotal += applied;
            source.DamageDealtThisTurn += applied;

            if (applied > 0)
            {
                target.TookDamageThisTurn = true;
            }

            // 荆棘：受到攻击时反弹
            if (isAttack && applied > 0)
            {
                int thorns = target.GetStatus(StatusKind.Thorns);
                if (thorns > 0 && source != null && !source.IsDead)
                {
                    LoseHp(source, thorns);
                    Emit(EventKind.Damage, target.Name + " 荆棘反弹 " + thorns, thorns, source.IsPlayer);
                }
            }
        }

        /// <summary>伤害结算：结界 → 护盾 → 生命。</summary>
        private int TakeDamage(BattleUnit target, int amount, bool ignoreShield = false,
            int shieldDamageMultiplier = 1)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int barrier = target.GetStatus(StatusKind.Barrier);
            if (barrier > 0)
            {
                int absorbedBarrier = Math.Min(barrier, amount);
                target.AddStatus(StatusKind.Barrier, -absorbedBarrier);
                amount -= absorbedBarrier;
                Emit(EventKind.Shield, "结界抵挡 " + absorbedBarrier, absorbedBarrier, !target.IsPlayer);
                if (amount <= 0)
                {
                    return absorbedBarrier;
                }
            }

            int absorbed = 0;
            if (!ignoreShield && target.Shield > 0)
            {
                // 破盾类攻击：每 1 点伤害打掉 2 点护盾。
                // 按「护盾被扣多少」反推「消耗掉多少伤害」，而不是直接把伤害翻倍 ——
                // 后者会让 1 点残余护盾也把整发攻击翻倍，溢出到生命上。
                int maxDestroy = amount * shieldDamageMultiplier;
                int destroyed = Math.Min(target.Shield, maxDestroy);
                absorbed = destroyed;
                target.Shield -= destroyed;
                amount -= (destroyed + shieldDamageMultiplier - 1) / shieldDamageMultiplier;
                if (amount < 0)
                {
                    amount = 0;
                }
            }

            int hpLoss = 0;
            if (amount > 0)
            {
                hpLoss = Math.Min(target.Hp, amount);
                target.Hp -= hpLoss;
                target.DamageTakenThisTurn += hpLoss;
                target.DamageTakenTotal += hpLoss;
            }

            Emit(EventKind.Damage, "-" + hpLoss, hpLoss, !target.IsPlayer);
            return absorbed + hpLoss;
        }

        /// <summary>无视护盾的直接生命损失（中毒 / 灼烧 / 自伤 / 流失）。</summary>
        private void LoseHp(BattleUnit unit, int amount)
        {
            if (amount <= 0 || unit == null)
            {
                return;
            }

            int lost = Math.Min(unit.Hp, amount);
            unit.Hp -= lost;
            unit.DamageTakenThisTurn += lost;
            unit.DamageTakenTotal += lost;
            unit.TookDamageThisTurn = true;
            Emit(EventKind.Damage, "-" + lost, lost, !unit.IsPlayer);
        }

        public void Heal(BattleUnit unit, int amount)
        {
            if (amount <= 0 || unit == null)
            {
                return;
            }

            int before = unit.Hp;
            unit.Hp = Math.Min(unit.HpMax, unit.Hp + amount);
            int gained = unit.Hp - before;
            unit.HealedTotal += gained;
            if (gained > 0)
            {
                Emit(EventKind.Heal, "+" + gained + " 生命", gained, !unit.IsPlayer);
            }
        }

        public void GainShield(BattleUnit unit, int amount)
        {
            if (amount <= 0 || unit == null)
            {
                return;
            }

            amount += unit.ShieldBonus;

            if (unit.HasStatus(StatusKind.Frail))
            {
                amount = amount * 3 / 4;
            }

            if (amount <= 0)
            {
                return;
            }

            unit.Shield += amount;
            Emit(EventKind.Shield, "+" + amount + " 护盾", amount, !unit.IsPlayer);
        }

        public void GainEnergy(BattleUnit unit, int amount)
        {
            if (amount <= 0 || unit == null)
            {
                return;
            }

            unit.Energy += amount;
            if (unit.IsPlayer)
            {
                ManaGainedTotal += amount;
            }

            Emit(EventKind.Energy, "+" + amount + " 能量", amount, !unit.IsPlayer);
        }

        /// <summary>
        /// 引爆：清空目标身上的指定状态，按「层数 × 每层伤害」结算一次伤害。
        /// 走 LoseHp 而不是 DealDamage —— 引爆不该再被护盾/易伤二次放大，
        /// 否则一堆叠层 + 一发引爆就能越过整场战斗的数值设计。
        /// </summary>
        private void Detonate(BattleUnit self, BattleUnit foe, StatusKind kind, int perStack)
        {
            if (foe == null || kind == StatusKind.None || perStack <= 0)
            {
                return;
            }

            int stacks = foe.GetStatus(kind);
            if (stacks <= 0)
            {
                Emit(EventKind.Status, foe.Name + " 身上没有" + Statuses.DisplayName(kind) + "，引爆落空", 0, !foe.IsPlayer);
                return;
            }

            int dmg = stacks * perStack;
            foe.AddStatus(kind, -stacks);
            Emit(EventKind.Payoff, "引爆 " + stacks + " 层" + Statuses.DisplayName(kind), stacks, !foe.IsPlayer);
            LoseHp(foe, dmg);
        }

        /// <summary>
        /// 把当前护盾转换成伤害。护盾清零（打完就没得挡），
        /// 每点护盾折算 <paramref name="perPoint"/> 点伤害。
        /// </summary>
        private void ConsumeShieldForDamage(BattleUnit self, BattleUnit foe, int perPoint)
        {
            if (self == null || foe == null || perPoint <= 0)
            {
                return;
            }

            int shield = self.Shield;
            if (shield <= 0)
            {
                Emit(EventKind.Status, self.Name + " 没有护盾可转化为伤害", 0, !self.IsPlayer);
                return;
            }

            self.Shield = 0;
            int dmg = shield * perPoint;
            Emit(EventKind.Payoff, "倾泻 " + shield + " 点护盾", shield, !self.IsPlayer);
            LoseHp(foe, dmg);
        }

        private void ApplyStatus(BattleUnit source, BattleUnit target, StatusKind kind, int stacks)
        {
            if (kind == StatusKind.None || stacks <= 0 || target == null)
            {
                return;
            }

            // 控制免疫窗口内不吃冻/晕。窗口在脱离控制后的那一回合生效，
            // 否则「每回合都施加控制」会把对方永久锁死。
            if ((kind == StatusKind.Freeze || kind == StatusKind.Stun) && target.HasControlImmunity)
            {
                Emit(EventKind.Status, target.Name + " 免疫了" + Statuses.DisplayName(kind), 0, !target.IsPlayer);
                return;
            }

            // 只统计「玩家施加到敌人身上」的不同减益：敌人自己叠的、施加给玩家的都不算。
            if (source != null && source.IsPlayer && !target.IsPlayer && !Statuses.IsBuff(kind))
            {
                enemyDebuffsApplied.Add(kind);
            }

            target.AddStatus(kind, stacks);
            Emit(EventKind.Status,
                target.Name + " 获得 " + Statuses.DisplayName(kind) + " " + stacks,
                stacks, !target.IsPlayer);
        }

        public void Draw(BattleUnit unit, int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (unit.Hand.Count >= rules.HandLimit)
                {
                    break;
                }

                if (unit.DrawPile.Count == 0)
                {
                    Reshuffle(unit);
                }

                if (unit.DrawPile.Count == 0)
                {
                    break;
                }

                var card = unit.DrawPile[unit.DrawPile.Count - 1];
                unit.DrawPile.RemoveAt(unit.DrawPile.Count - 1);
                unit.Hand.Add(card);
                drawn++;
            }

            if (drawn > 0)
            {
                if (unit.IsPlayer)
                {
                    CardsDrawnTotal += drawn;
                }

                Emit(EventKind.Draw, "抽 " + drawn + " 张", drawn, !unit.IsPlayer);
            }
        }

        /// <summary>弃牌堆洗回抽牌堆。洗牌时打出一次「洗牌」播报，让玩家知道循环了一圈。</summary>
        private void Reshuffle(BattleUnit unit)
        {
            if (unit.DiscardPile.Count == 0)
            {
                return;
            }

            unit.DrawPile.AddRange(unit.DiscardPile);
            unit.DiscardPile.Clear();
            rng.Shuffle(unit.DrawPile);
            Emit(EventKind.Draw, "弃牌堆洗回", unit.DrawPile.Count, !unit.IsPlayer);
        }

        /// <summary>
        /// 回合结束把手牌裁到 <paramref name="keep"/> 张，多出来的从**右侧**（最新拿到的）开始弃。
        ///
        /// 弃右侧而不是左侧：左侧是最早留下的牌，玩家把它们留着不打通常是有意的
        /// （等能量、等配套牌）；右侧则是本回合用效果临时抽进来的，弃掉损失最小。
        /// </summary>
        private void TrimHand(BattleUnit unit, int keep)
        {
            if (keep < 0)
            {
                keep = 0;
            }

            int excess = unit.Hand.Count - keep;
            if (excess <= 0)
            {
                return;
            }

            for (int i = 0; i < excess; i++)
            {
                int last = unit.Hand.Count - 1;
                unit.DiscardPile.Add(unit.Hand[last]);
                unit.Hand.RemoveAt(last);
            }

            Emit(EventKind.Discard, "弃掉 " + excess + " 张手牌", excess, !unit.IsPlayer);
        }

        private void DiscardRandom(BattleUnit unit, int count)
        {
            for (int i = 0; i < count && unit.Hand.Count > 0; i++)
            {
                int index = rng.Range(0, unit.Hand.Count);
                var card = unit.Hand[index];
                unit.Hand.RemoveAt(index);
                unit.DiscardPile.Add(card);
                Emit(EventKind.Discard, "弃掉 " + card.Name, 1, !unit.IsPlayer);
            }
        }

        private void ExhaustHand(BattleUnit unit, int count)
        {
            for (int i = 0; i < count && unit.Hand.Count > 0; i++)
            {
                int index = rng.Range(0, unit.Hand.Count);
                var card = unit.Hand[index];
                unit.Hand.RemoveAt(index);
                unit.ExhaustPile.Add(card);
                Emit(EventKind.Discard, "消耗 " + card.Name, 1, !unit.IsPlayer);
            }
        }

        /// <summary>观星：查看抽牌堆顶若干张，把不想要的弃掉。</summary>
        private void Scry(BattleUnit unit, int count)
        {
            int discarded = 0;
            for (int i = 0; i < count && unit.DrawPile.Count > 0; i++)
            {
                var card = unit.DrawPile[unit.DrawPile.Count - 1];
                unit.DrawPile.RemoveAt(unit.DrawPile.Count - 1);
                unit.DiscardPile.Add(card);
                discarded++;
            }

            if (discarded > 0)
            {
                Emit(EventKind.Discard, "观星弃掉 " + discarded + " 张", discarded, !unit.IsPlayer);
            }
        }

        private void AddCardToHand(BattleUnit unit, int cardId, int count)
        {
            var def = db.Get(cardId);
            if (def == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (unit.Hand.Count >= rules.HandLimit)
                {
                    return;
                }

                unit.Hand.Add(CardInstance.Wrap(def));
            }
        }

        private void AddRandomToHand(BattleUnit unit, CardSeries series, int count)
        {
            var pool = db.OfSeries(series);
            if (pool.Count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (unit.Hand.Count >= rules.HandLimit)
                {
                    return;
                }

                unit.Hand.Add(CardInstance.Wrap(pool[rng.Range(0, pool.Count)]));
            }
        }

        // ------------------------------------------------------------ 结算

        public bool CheckEnd()
        {
            if (Phase == BattlePhase.Ended)
            {
                return true;
            }

            if (Enemy.IsDead)
            {
                Phase = BattlePhase.Ended;
                Victory = true;
                Emit(EventKind.Info, Enemy.Name + " 被击败", 0, true);
                return true;
            }

            if (Player.IsDead)
            {
                Phase = BattlePhase.Ended;
                Defeat = true;
                Emit(EventKind.Info, "你倒下了", 0, false);
                return true;
            }

            return false;
        }

        public bool Finished { get { return Phase == BattlePhase.Ended; } }

        // ------------------------------------------------------------ 事件流

        private void Emit(EventKind kind, string text, int value, bool onEnemy)
        {
            events.Add(new BattleEvent { Kind = kind, Text = text, Value = value, OnEnemy = onEnemy });
        }

        public List<BattleEvent> TakeEvents()
        {
            if (events.Count == 0)
            {
                return null;
            }

            var copy = new List<BattleEvent>(events);
            events.Clear();
            return copy;
        }

        public int PendingEventCount { get { return events.Count; } }

        // ------------------------------------------------------------ 调试

        public void DebugGrantEnergy(int amount)
        {
            GainEnergy(Player, amount);
        }

        public void DebugSetHp(int hp)
        {
            Player.Hp = Math.Max(1, Math.Min(Player.HpMax, hp));
        }

        public string DeckSummary()
        {
            return "抽 " + Player.DrawPile.Count + " / 手 " + Player.Hand.Count + " / 弃 " + Player.DiscardPile.Count;
        }
    }
}
