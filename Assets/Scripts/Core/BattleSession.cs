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
        EnemyIntent = 10
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
        public int DrawPerTurn = 5;
        public int HandLimit = 10;
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

            TickStartOfTurn(Player);
            if (CheckEnd())
            {
                return;
            }

            if (SkipTurnIfIncapacitated(Player))
            {
                EndPlayerTurn();
                return;
            }

            int drawCount = rules.DrawPerTurn - Player.GetStatus(StatusKind.Blind);
            Draw(Player, Math.Max(0, drawCount));
            RefreshPlayerEnergy();

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

            Emit(EventKind.Play, def.Name, card.Cost, !self.IsPlayer);

            ResolveOps(self, foe, def.Ops, card);

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

            // 手牌超过上限的部分弃掉
            DiscardExcess(Player);

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

            TickStartOfTurn(Enemy);
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

            Emit(EventKind.EnemyIntent, Enemy.Name + " · " + intent.Describe(), intent.Value, true);

            switch (intent.Kind)
            {
                case IntentKind.Attack:
                    for (int i = 0; i < intent.Hits; i++)
                    {
                        if (CheckEnd())
                        {
                            return;
                        }

                        DealDamage(Enemy, Player, intent.Value, true);
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
                Emit(EventKind.EnemyIntent, "敌方意图：" + brain.Queue.Peek().Describe(), brain.Queue.Peek().Value, true);
            }
        }

        /// <summary>当前展示给玩家的敌方意图。</summary>
        public EnemyIntent CurrentIntent
        {
            get { return brain.Queue.Count > 0 ? brain.Queue.Peek() : null; }
        }

        // ------------------------------------------------------------ 回合钩子

        private void TickStartOfTurn(BattleUnit unit)
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

            // 护盾每回合清空（尖塔式），结界也在此时归零
            if (unit.Shield > 0)
            {
                unit.Shield = 0;
            }
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
            if (kind == StatusKind.Freeze)
            {
                unit.AddStatus(StatusKind.Freeze, -1);
            }
            else
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
                    // 重复上一张牌的伤害一次（配合 if 使用）
                    DealDamage(self, foe, op.A + AttackBonusFor(self, src), src != null && src.Def.Kind == CardKind.Attack);
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

        public void DealDamage(BattleUnit source, BattleUnit target, int raw, bool isAttack)
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

            int applied = TakeDamage(target, amount);
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
        private int TakeDamage(BattleUnit target, int amount)
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
            if (target.Shield > 0)
            {
                absorbed = Math.Min(target.Shield, amount);
                target.Shield -= absorbed;
                amount -= absorbed;
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

        private void DiscardExcess(BattleUnit unit)
        {
            while (unit.Hand.Count > rules.HandLimit)
            {
                int last = unit.Hand.Count - 1;
                var card = unit.Hand[last];
                unit.Hand.RemoveAt(last);
                unit.DiscardPile.Add(card);
                Emit(EventKind.Discard, "弃掉 " + card.Name, 1, !unit.IsPlayer);
            }
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
