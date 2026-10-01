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
        Mana = 4,
        Draw = 5,
        Play = 6,
        Hook = 7,
        Debuff = 8
    }

    /// <summary>一条战斗播报。视图层消费它来做飘字、震屏与提示。</summary>
    public struct BattleEvent
    {
        public EventKind Kind;
        public string Text;
        public int Value;
        /// <summary>true 表示作用于敌方。</summary>
        public bool OnEnemy;
    }

    /// <summary>
    /// 一局 1v1 卡牌对决。规则完整沿用旧版《第九张史莱姆牌》：
    /// 每回合恢复魔力、抽 2 张牌；卡牌按费用出手；护盾先抵伤害；
    /// 持续效果以"回合数"计时并在各自回合结束时递减。
    /// </summary>
    public sealed class BattleSession
    {
        public const int DrawPerTurn = 2;
        public const int StartingHand = 1;
        /// <summary>超过这个回合数后开始结算"牌局衰减"，防止无限拉锯。</summary>
        public const int SoftTurnLimit = 20;

        private readonly CardDatabase db;
        private readonly DeterministicRng rng;
        private readonly List<BattleEvent> events = new List<BattleEvent>();

        private readonly List<CardInstance> resolving = new List<CardInstance>();
        private bool resolvingHooks;
        private int pendingHookDelay;
        private int damageAtTurnStart;

        public readonly BattleUnit Player;
        public readonly BattleUnit Enemy;

        public int Battle = 1;
        /// <summary>关号（1..9），由战序推导。</summary>
        public int Level;
        /// <summary>关内场次（1 先锋 / 2 中坚 / 3 宗师）。</summary>
        public int Stage = 1;
        public int TurnNumber;
        public BattlePhase Phase = BattlePhase.Setup;
        public bool Victory;
        public bool Defeat;
        /// <summary>本回合因卡牌效果被要求立刻结束（懒惰 / 怠惰）。</summary>
        public bool EndTurnRequested;

        public int DamageDealtByPlayer { get { return Player.DamageDealtTotal; } }
        public int CardsPlayedByPlayer { get { return playedTotal; } }

        private int playedTotal;
        private int drawnTotal;
        private int manaGainedTotal;
        private int healedTotal;
        private int bestTurnDamage;

        public int BestTurnDamage { get { return bestTurnDamage; } }
        public int DrawnTotal { get { return drawnTotal; } }
        public int ManaGainedTotal { get { return manaGainedTotal; } }
        public int HealedTotal { get { return healedTotal; } }

        private EnemyDef enemyDef = EnemyRoster.All[0];

        public BattleSession(CardDatabase database, int battle, int seed, IReadOnlyList<int> playerDeck, EnemyDef enemy)
        {
            db = database ?? CardDatabase.CreateFallback();
            Battle = EnemyRoster.ClampBattle(battle);
            Level = EnemyRoster.LevelOf(Battle);
            Stage = EnemyRoster.StageOf(Battle);
            rng = new DeterministicRng(seed == 0 ? 20261001 + Battle * 977 : seed);
            enemyDef = enemy ?? EnemyRoster.ForBattle(Battle);

            Player = new BattleUnit { IsPlayer = true, Name = "你" };
            Enemy = new BattleUnit { IsPlayer = false, Name = enemyDef.Name };

            BuildPlayer(playerDeck);
            BuildEnemy();
        }

        public EnemyDef EnemyInfo { get { return enemyDef; } }

        // ------------------------------------------------------------ 初始化

        private void BuildPlayer(IReadOnlyList<int> ids)
        {
            Player.HpMax = EnemyRoster.PlayerHpFor(Battle);
            Player.Hp = Player.HpMax;
            Player.Mana = 0;
            Player.ManaRegen = 2;

            var source = ids != null && ids.Count > 0 ? ids : DeckRules.StarterDeck;
            for (int i = 0; i < source.Count; i++)
            {
                var def = db.Get(source[i]);
                if (def != null)
                {
                    Player.Deck.Add(CardInstance.Wrap(def));
                }
            }

            if (Player.Deck.Count == 0)
            {
                for (int i = 0; i < DeckRules.StarterDeck.Length; i++)
                {
                    var def = db.Get(DeckRules.StarterDeck[i]);
                    if (def != null)
                    {
                        Player.Deck.Add(CardInstance.Wrap(def));
                    }
                }
            }

            rng.Shuffle(Player.Deck);
        }

        private void BuildEnemy()
        {
            Enemy.HpMax = EnemyRoster.EnemyHpFor(Battle);
            Enemy.Hp = Enemy.HpMax;
            Enemy.Mana = 0;
            Enemy.ManaRegen = EnemyRoster.EnemyManaRegenFor(Battle);

            int deckSize = EnemyRoster.EnemyDeckSizeFor(Battle);
            var unlocked = EnemyRoster.UnlockedSeries(Level);
            var pool = db.OfSeries(unlocked);

            var attacks = new List<CardDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Cost <= 2 && pool[i].V1 >= 2)
                {
                    attacks.Add(pool[i]);
                }
            }

            int guaranteed = Math.Max(2, deckSize / 3);
            for (int i = 0; i < guaranteed && attacks.Count > 0; i++)
            {
                Enemy.Deck.Add(CardInstance.Wrap(attacks[rng.Range(0, attacks.Count)]));
            }

            var fallbackPool = pool.Count > 0 ? pool : attacks;
            while (Enemy.Deck.Count < deckSize && fallbackPool.Count > 0)
            {
                Enemy.Deck.Add(CardInstance.Wrap(fallbackPool[rng.Range(0, fallbackPool.Count)]));
            }

            if (Enemy.Deck.Count == 0)
            {
                var def = db.Get(101);
                if (def != null)
                {
                    Enemy.Deck.Add(CardInstance.Wrap(def));
                }
            }

            rng.Shuffle(Enemy.Deck);
        }

        /// <summary>开局：双方各摸起手牌，玩家先行动。</summary>
        public void Begin()
        {
            Draw(Player, StartingHand, false);
            Draw(Enemy, StartingHand, false);
            PlayerTurnStart();
        }

        // ------------------------------------------------------------ 回合流程

        public void PlayerTurnStart()
        {
            if (Phase == BattlePhase.Ended)
            {
                return;
            }

            TurnNumber++;
            Phase = BattlePhase.PlayerTurn;
            EndTurnRequested = false;
            damageAtTurnStart = Player.DamageDealtTotal;

            Player.ResetTurnFlags();
            Player.ClearPending();

            FireHooks(Player, Enemy, HookTiming.StartTurn);
            AddTurnMana(Player);
            Draw(Player, DrawPerTurn, false);

            if (CheckEnd())
            {
                return;
            }

            Emit(EventKind.Info, "第 " + TurnNumber + " 回合 · 你的行动", 0, false);
        }

        public void PlayerEndTurn()
        {
            if (Phase != BattlePhase.PlayerTurn)
            {
                return;
            }

            FireHooks(Player, Enemy, HookTiming.EndTurn);

            int thisTurn = Player.DamageDealtTotal - damageAtTurnStart;
            if (thisTurn > bestTurnDamage)
            {
                bestTurnDamage = thisTurn;
            }

            Player.TickDurations();
            Phase = BattlePhase.EnemyTurn;
        }

        public void EnemyTurnStart()
        {
            if (Phase == BattlePhase.Ended)
            {
                return;
            }

            Enemy.ResetTurnFlags();
            Enemy.ClearPending();

            FireHooks(Enemy, Player, HookTiming.StartTurn);
            AddTurnMana(Enemy);
            Draw(Enemy, DrawPerTurn, false);
        }

        /// <summary>敌方行动一次。返回 false 表示无牌可出，回合结束。</summary>
        public bool EnemyActOnce()
        {
            if (Phase != BattlePhase.EnemyTurn || CheckEnd())
            {
                return false;
            }

            int chosen = -1;
            int fallback = -1;

            for (int i = 0; i < Enemy.Hand.Count; i++)
            {
                var card = Enemy.Hand[i];
                if (card.Cost > Enemy.Mana)
                {
                    continue;
                }

                bool aggressive = IsAggressive(card.Def);
                if (aggressive)
                {
                    chosen = i;
                    break;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }
            }

            if (chosen < 0)
            {
                chosen = fallback;
            }

            if (chosen < 0)
            {
                return false;
            }

            bool ok = PlayInternal(Enemy, Player, chosen, true);
            return ok;
        }

        public void EnemyTurnEnd()
        {
            if (Phase == BattlePhase.Ended)
            {
                return;
            }

            FireHooks(Enemy, Player, HookTiming.EndTurn);
            Enemy.TickDurations();
            ApplyAttrition();
        }

        /// <summary>
        /// 拉锯保险：第 SoftTurnLimit 回合之后双方每回合固定流失递增的生命，
        /// 保证任何牌组组合的对局都能在有限回合内收敛（不会被无限治疗拖死）。
        /// </summary>
        private void ApplyAttrition()
        {
            if (TurnNumber <= SoftTurnLimit)
            {
                return;
            }

            int pressure = TurnNumber - SoftTurnLimit;
            HurtDirect(Player, pressure);
            HurtDirect(Enemy, pressure);
            Emit(EventKind.Debuff, "牌局拖太久：双方每回合流失 " + pressure + " 点生命", pressure, false);
            CheckEnd();
        }

        private static bool IsAggressive(CardDef def)
        {
            if (def == null)
            {
                return false;
            }

            string text = def.Raw ?? string.Empty;
            return text.Contains("造成") && text.Contains("伤害") && !text.Contains("对自身");
        }

        // ------------------------------------------------------------ 玩家出牌

        public bool CanPlay(CardInstance card)
        {
            if (card == null || Phase != BattlePhase.PlayerTurn)
            {
                return false;
            }

            return card.Cost <= Player.Mana;
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

            if (!CanPlay(Player.Hand[handIndex]))
            {
                return false;
            }

            return PlayInternal(Player, Enemy, handIndex, false);
        }

        private bool PlayInternal(BattleUnit self, BattleUnit foe, int handIndex, bool enemySide)
        {
            var card = self.Hand[handIndex];
            var def = card.Def;

            int extraTriggers = self.PendingExtraTriggers + card.ExtraTriggers;
            int bonusDamage = self.PendingDamageBonus;
            int costReduction = self.PendingCostReduction;

            self.PendingExtraTriggers = 0;
            self.PendingDamageBonus = 0;
            self.PendingCostReduction = 0;
            card.ExtraTriggers = 0;

            if (card.IsTech)
            {
                extraTriggers += self.PendingTechExtraTriggers;
                self.PendingTechExtraTriggers = 0;

                if (self.PendingTechDamageBonusUses > 0)
                {
                    bonusDamage += self.PendingTechDamageBonus;
                    self.PendingTechDamageBonusUses--;
                    if (self.PendingTechDamageBonusUses <= 0)
                    {
                        self.PendingTechDamageBonus = 0;
                    }
                }
            }

            int payCost = card.Cost;
            if (costReduction > 0)
            {
                payCost = Math.Max(0, payCost - costReduction);
            }

            if (payCost > self.Mana)
            {
                return false;
            }

            self.Mana -= payCost;

            self.Hand.RemoveAt(handIndex);
            resolving.Add(card);

            Emit(EventKind.Play, (enemySide ? "敌方 · " : string.Empty) + def.Name, card.Def.Cost, enemySide);

            bool blocked = false;
            if (IsTargeted(def) && foe.ImmunityCharges > 0)
            {
                foe.ImmunityCharges--;
                blocked = true;
                Emit(EventKind.Debuff, foe.Name + " 免疫了 " + def.Name, 0, !enemySide);
            }

            if (!blocked)
            {
                ResolveOps(self, foe, def.Ops, card, bonusDamage);
                for (int i = 0; i < extraTriggers; i++)
                {
                    if (CheckEnd())
                    {
                        break;
                    }

                    ResolveOps(self, foe, def.Ops, card, bonusDamage);
                }
            }

            self.CardsPlayedThisTurn++;
            playedTotal += enemySide ? 0 : 1;
            if (card.IsTech)
            {
                self.TechCardsPlayedThisTurn++;
            }

            FireHooks(self, foe, HookTiming.UseCard);
            if (card.IsTech)
            {
                FireHooks(self, foe, HookTiming.UseCardTech);
            }

            FireHooks(foe, self, HookTiming.EnemyUseCard);

            resolving.Remove(card);
            self.Discard.Add(card);

            CheckEnd();
            return true;
        }

        /// <summary>卡牌是否指向对手（用于免疫判定）。</summary>
        private static bool IsTargeted(CardDef def)
        {
            if (def == null)
            {
                return false;
            }

            for (int i = 0; i < def.Ops.Count; i++)
            {
                string k = def.Ops[i].Kind;
                if (k == "dmg" || k == "dmgadd" || k == "drain" || k == "ssteal"
                    || k == "emanalse" || k == "esteal" || k == "stealcard"
                    || k == "purge" || k == "etakenmul" || k == "edmgdown"
                    || k == "grow" || k == "randdmg" || k == "randomplay")
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------ 效果解释

        private void ResolveOps(BattleUnit self, BattleUnit foe, List<CardOp> ops, CardInstance src, int bonusDamage)
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

                if (EndTurnRequested && self.IsPlayer)
                {
                    // 懒惰/怠惰：结束回合，但钩子类 op 仍需挂上
                    if (ops[i].Kind != "hook")
                    {
                        continue;
                    }
                }

                Exec(self, foe, ops[i], src, bonusDamage);
            }
        }

        private void Exec(BattleUnit self, BattleUnit foe, CardOp op, CardInstance src, int bonusDamage)
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
                        Exec(self, foe, branch[i], src, bonusDamage);
                    }
                }

                return;
            }

            if (op.IsHook)
            {
                var created = self.AddHook(op.Timing, op.Hook, op.HookTurns, HookLabel(op));
                if (created != null && pendingHookDelay > 0)
                {
                    created.Delay = pendingHookDelay;
                }

                pendingHookDelay = 0;

                Emit(EventKind.Hook, self.Name + " 获得持续效果：" + HookLabel(op) + " (" + op.HookTurns + " 回合)", op.HookTurns, !self.IsPlayer);
                return;
            }

            switch (op.Kind)
            {
                case "dmg":
                    DealDamage(self, foe, op.A + bonusDamage);
                    break;

                case "dmgadd":
                    DealDamage(self, foe, op.A);
                    break;

                case "selfdmg":
                    TakeDamage(self, self, op.A, false);
                    break;

                case "heal":
                    Heal(self, op.A);
                    break;

                case "shield":
                    GainShield(self, op.A);
                    break;

                case "draw":
                    Draw(self, op.A, true);
                    break;

                case "edraw":
                    Draw(foe, op.A, true);
                    break;

                case "mana":
                    GainMana(self, op.A);
                    break;

                case "manalose":
                    self.Mana = Math.Max(0, self.Mana - op.A);
                    Emit(EventKind.Mana, self.Name + " 失去 " + op.A + " 魔力", -op.A, !self.IsPlayer);
                    break;

                case "emanalse":
                    foe.Mana = Math.Max(0, foe.Mana - op.A);
                    Emit(EventKind.Debuff, foe.Name + " 失去 " + op.A + " 魔力", -op.A, !foe.IsPlayer);
                    break;

                case "esteal":
                    foe.Mana = Math.Max(0, foe.Mana - op.A);
                    GainMana(self, op.A);
                    break;

                case "manaloseall":
                    self.Mana = 0;
                    Emit(EventKind.Mana, self.Name + " 的魔力被清空", 0, !self.IsPlayer);
                    break;

                case "manalosealle":
                    foe.Mana = 0;
                    Emit(EventKind.Mana, foe.Name + " 的魔力被清空", 0, !foe.IsPlayer);
                    break;

                case "manaswap":
                    self.Mana = foe.Mana;
                    Emit(EventKind.Mana, self.Name + " 的魔力变为 " + self.Mana, self.Mana, !self.IsPlayer);
                    break;

                case "setmana":
                    self.Mana = Math.Max(0, op.A);
                    break;

                case "setmanaboth":
                    self.Mana = Math.Max(0, op.A);
                    foe.Mana = Math.Max(0, op.A);
                    Emit(EventKind.Info, "双方魔力重置为 " + op.A, op.A, false);
                    break;

                case "setregenboth":
                    self.ManaRegen = Math.Max(0, op.A);
                    foe.ManaRegen = Math.Max(0, op.A);
                    Emit(EventKind.Info, "双方每回合魔力重置为 " + op.A, op.A, false);
                    break;

                case "ssteal":
                    {
                        int stolen = Math.Min(foe.Shield, op.A);
                        foe.Shield -= stolen;
                        GainShield(self, stolen);
                        Emit(EventKind.Debuff, self.Name + " 偷取 " + stolen + " 点护盾", stolen, !self.IsPlayer);
                        break;
                    }

                case "drain":
                    {
                        int lost = Math.Min(foe.Hp, op.A);
                        HurtDirect(foe, lost);
                        Heal(self, lost);
                        break;
                    }

                case "healhand":
                    Heal(self, Math.Max(0, self.Hand.Count) * op.A);
                    break;

                case "randdmg":
                    DealDamage(self, foe, rng.Range(0, op.A + 1));
                    break;

                case "randheal":
                    Heal(self, rng.Range(0, op.A + 1));
                    break;

                case "randmana":
                    GainMana(self, rng.Range(0, op.A + 1));
                    break;

                case "randshield":
                    GainShield(self, rng.Range(0, op.A + 1));
                    break;

                case "randdraw":
                    Draw(self, rng.Range(0, op.A + 1), true);
                    break;

                case "randadd":
                    AddRandomCards(self, Series.FromName(op.Text), Math.Max(0, op.B), op.Free);
                    break;

                case "addrandom":
                    AddRandomCards(self, Series.FromName(op.Text), Math.Max(0, op.B), op.Free);
                    break;

                case "addcard":
                    AddCard(self, db.Get(op.A), op.B, false);
                    break;

                case "copydraw":
                    AddRandomCards(self, PickSeries(foe), Math.Max(1, op.A), false);
                    break;

                case "randpick":
                    {
                        var names = (op.Text ?? string.Empty).Split('~');
                        if (names.Length > 0)
                        {
                            string pick = names[rng.Range(0, names.Length)].Trim();
                            ApplySimple(self, foe, pick, op.B);
                        }

                        break;
                    }

                case "randpick4":
                    {
                        var items = (op.Text ?? string.Empty).Split('~');
                        if (items.Length > 0)
                        {
                            string pick = items[rng.Range(0, items.Length)].Trim();
                            var kv = pick.Split('=');
                            if (kv.Length == 2)
                            {
                                int value;
                                int.TryParse(kv[1].Trim(), out value);
                                ApplySimple(self, foe, kv[0].Trim(), value);
                            }
                        }

                        break;
                    }

                case "discardall":
                    DiscardAll(self, foe, op);
                    break;

                case "discardrand":
                    DiscardRandom(self, foe, op.A, op.B);
                    break;

                case "randomplay":
                    {
                        int index = RandomOtherHandIndex(self);
                        if (index >= 0)
                        {
                            var target = self.Hand[index];
                            target.ExtraTriggers += op.A;
                            Emit(EventKind.Info, "随机打出：" + target.Name, 0, !self.IsPlayer);
                            PlayInternal(self, foe, index, !self.IsPlayer);
                        }

                        break;
                    }

                case "randombuff":
                    {
                        int index = RandomOtherHandIndex(self);
                        if (index >= 0)
                        {
                            self.Hand[index].ExtraTriggers += op.A;
                            Emit(EventKind.Info, self.Hand[index].Name + " 本回合额外触发 " + op.A + " 次", op.A, !self.IsPlayer);
                        }

                        break;
                    }

                case "nextxtra":
                    self.PendingExtraTriggers += op.A;
                    Emit(EventKind.Info, "下一张牌额外触发 " + op.A + " 次", op.A, !self.IsPlayer);
                    break;

                case "nextdmg":
                    self.PendingDamageBonus += op.A;
                    break;

                case "nextcost":
                    self.PendingCostReduction += op.A;
                    break;

                case "techxtra":
                    self.PendingTechExtraTriggers += op.A;
                    break;

                case "techdmg":
                    self.PendingTechDamageBonus = op.A;
                    self.PendingTechDamageBonusUses = op.B;
                    break;

                case "immunity":
                    self.ImmunityCharges += op.A;
                    Emit(EventKind.Shield, self.Name + " 免疫 " + op.A + " 次指向效果", op.A, !self.IsPlayer);
                    break;

                case "dodge":
                    self.DodgeCharges += op.A;
                    Emit(EventKind.Shield, self.Name + " 闪避 " + op.A + " 次伤害", op.A, !self.IsPlayer);
                    break;

                case "transfer":
                    self.TransferPool += op.A;
                    Emit(EventKind.Info, self.Name + " 转移接下来 " + op.A + " 点伤害", op.A, !self.IsPlayer);
                    break;

                case "dmgup":
                    self.AddBuff(BuffKind.DmgFlatUp, op.A, op.B > 0 ? op.B : 1);
                    Emit(EventKind.Info, "伤害提高 " + op.A + (op.B > 1 ? "（" + op.B + " 回合）" : ""), op.A, !self.IsPlayer);
                    break;

                case "healup":
                    self.AddBuff(BuffKind.HealFlatUp, op.A, op.B > 0 ? op.B : 1);
                    break;

                case "dmgdown":
                    self.AddBuff(BuffKind.DmgFlatDown, op.A, op.B > 0 ? op.B : 1);
                    Emit(EventKind.Shield, "获得 " + op.A + " 点伤害减免", op.A, !self.IsPlayer);
                    break;

                case "edmgdown":
                    foe.AddBuff(BuffKind.DmgFlatDown, op.A, op.B > 0 ? op.B : 1);
                    break;

                case "etakenmul":
                    foe.AddBuff(BuffKind.DmgTakenPctUp, op.A * 100, op.B > 0 ? op.B : 1);
                    Emit(EventKind.Debuff, foe.Name + " 受到的伤害提高 " + (op.A * 100) + "%", op.A, !foe.IsPlayer);
                    break;

                case "dmgmul":
                    self.AddBuff(BuffKind.DmgOutPctUp, (op.A - 1) * 100, op.B > 0 ? op.B : 1);
                    Emit(EventKind.Info, "本回合伤害 ×" + op.A, op.A, !self.IsPlayer);
                    break;

                case "purge":
                    PurgeBuffs(foe, op.A);
                    break;

                case "extend":
                    ExtendOwnEffects(self, op.A);
                    break;

                case "settle":
                    for (int i = 0; i < op.A; i++)
                    {
                        FireHooks(self, foe, HookTiming.EndTurn);
                    }

                    break;

                case "stealcard":
                    {
                        for (int i = 0; i < op.A && foe.Deck.Count > 0; i++)
                        {
                            var stolen = foe.Deck[0];
                            foe.Deck.RemoveAt(0);
                            self.Hand.Add(stolen);
                        }

                        Emit(EventKind.Debuff, self.Name + " 偷取 " + op.A + " 张牌", op.A, !self.IsPlayer);
                        break;
                    }

                case "storehand":
                    {
                        int keep = self.Hand.Count;
                        Draw(self, keep, true);
                        self.AddHook(HookTiming.EndTurn, new List<CardOp> { new CardOp { Kind = "draw", A = op.A } }, op.B, "传送");
                        Emit(EventKind.Hook, "手牌被封存，每回合复制 " + op.A + " 份", op.B, !self.IsPlayer);
                        break;
                    }

                case "mirrorhand":
                    {
                        self.Hand.Clear();
                        AddRandomCards(self, PickSeries(foe), Math.Max(1, op.A), true);
                        Emit(EventKind.Info, "手牌被替换", op.A, !self.IsPlayer);
                        break;
                    }

                case "rampdmg":
                    DealDamage(self, foe, op.A);
                    break;

                case "grow":
                    {
                        int mul = src != null ? src.PowerMul : 1;
                        DealDamage(self, foe, op.A * mul);
                        if (src != null)
                        {
                            src.PowerMul *= 2;
                            src.CostDelta += 1;
                        }

                        break;
                    }

                case "delayed":
                    pendingHookDelay = op.A;
                    Emit(EventKind.Hook, "延迟 " + op.A + " 回合后生效", op.A, !self.IsPlayer);
                    break;

                case "endturn":
                    EndTurnRequested = true;
                    break;
            }
        }

        /// <summary>条件判定。对应旧版卡面里"若……则……"的分支。</summary>
        private bool Test(BattleUnit self, BattleUnit foe, CardOp op)
        {
            switch (op.Test)
            {
                case OpTest.Always:
                    return true;

                case OpTest.FirstCard:
                    return self.CardsPlayedThisTurn == 0;

                case OpTest.PlayedOther:
                    return self.CardsPlayedThisTurn > 0;

                case OpTest.PlayedTech:
                    return self.TechCardsPlayedThisTurn > 0;

                case OpTest.HalfHp:
                    return self.Hp * 2 <= self.HpMax;

                case OpTest.HasShield:
                    return self.Shield > 0;

                case OpTest.EnemyHasShield:
                    return foe != null && foe.Shield > 0;

                case OpTest.TookDamage:
                    return self.TookDamageThisTurn;

                case OpTest.HandGte:
                    return self.Hand.Count >= op.TestValue;

                case OpTest.EnemyManaHigher:
                    return foe != null && foe.Mana > self.Mana;

                case OpTest.EnemyAttackedMe:
                    return self.EnemyAttackedMeThisTurn;

                case OpTest.HasSeed:
                    for (int i = 0; i < self.Hand.Count; i++)
                    {
                        if (self.Hand[i].Def.Series == CardSeries.Seed)
                        {
                            return true;
                        }
                    }

                    return false;

                case OpTest.NoOtherHand:
                    return self.Hand.Count == 0;

                default:
                    return true;
            }
        }

        private static string HookLabel(CardOp op)
        {
            switch (op.Timing)
            {
                case HookTiming.StartTurn: return "回合开始";
                case HookTiming.EndTurn: return "回合结束";
                case HookTiming.UseCard: return "出牌后";
                case HookTiming.UseCardTech: return "科技牌触发";
                case HookTiming.EnemyUseCard: return "敌方出牌时";
                case HookTiming.Heal: return "恢复时";
                case HookTiming.Hurt: return "受伤时";
                case HookTiming.DealDamage: return "造成伤害时";
                default: return "持续效果";
            }
        }

        private CardSeries PickSeries(BattleUnit source)
        {
            if (source == Enemy)
            {
                var unlocked = EnemyRoster.UnlockedSeries(Level);
                return unlocked[rng.Range(0, unlocked.Count)];
            }

            return CardSeries.Starter;
        }

        private int RandomOtherHandIndex(BattleUnit unit)
        {
            return unit.Hand.Count == 0 ? -1 : rng.Range(0, unit.Hand.Count);
        }

        private void ApplySimple(BattleUnit self, BattleUnit foe, string name, int value)
        {
            switch (name)
            {
                case "dmg": DealDamage(self, foe, value); break;
                case "heal": Heal(self, value); break;
                case "mana": GainMana(self, value); break;
                case "shield": GainShield(self, value); break;
                case "draw": Draw(self, value, true); break;
            }
        }

        private void DiscardAll(BattleUnit self, BattleUnit foe, CardOp op)
        {
            string mode = op.Text ?? string.Empty;
            int count = self.Hand.Count;
            self.Hand.Clear();

            switch (mode)
            {
                case "heal":
                    Heal(self, count * Math.Max(1, op.B));
                    break;
                case "mana":
                    GainMana(self, count);
                    break;
                case "draw":
                    Draw(self, count, true);
                    break;
                case "manaheal":
                    GainMana(self, count);
                    Heal(self, count);
                    break;
            }

            Emit(EventKind.Info, "弃掉 " + count + " 张手牌", count, !self.IsPlayer);
        }

        private void DiscardRandom(BattleUnit self, BattleUnit foe, int healAmount, int sinMana)
        {
            if (self.Hand.Count == 0)
            {
                Heal(self, healAmount);
                return;
            }

            int index = rng.Range(0, self.Hand.Count);
            var card = self.Hand[index];
            bool wasSin = card.Def.Series == CardSeries.Sin;
            self.Hand.RemoveAt(index);
            self.Discard.Add(card);

            Heal(self, healAmount);
            if (wasSin)
            {
                GainMana(self, sinMana);
            }

            Emit(EventKind.Info, "弃掉 " + card.Name + "，恢复 " + healAmount + " 点生命", healAmount, !self.IsPlayer);
        }

        private void PurgeBuffs(BattleUnit target, int count)
        {
            int removed = 0;
            for (int i = 0; i < count && target.Buffs.Count > 0; i++)
            {
                int index = rng.Range(0, target.Buffs.Count);
                Emit(EventKind.Debuff, "驱散：" + target.Buffs[index].Describe(), 0, !target.IsPlayer);
                target.Buffs.RemoveAt(index);
                removed++;
            }

            if (target.Hooks.Count > 0 && removed < count)
            {
                target.Hooks.RemoveAt(rng.Range(0, target.Hooks.Count));
                removed++;
            }
        }

        private void ExtendOwnEffects(BattleUnit unit, int turns)
        {
            for (int i = 0; i < unit.Buffs.Count; i++)
            {
                unit.Buffs[i].TurnsLeft += turns;
            }

            for (int i = 0; i < unit.Hooks.Count; i++)
            {
                unit.Hooks[i].Times += turns;
            }

            Emit(EventKind.Hook, "所有持续效果延长 " + turns + " 回合", turns, !unit.IsPlayer);
        }

        private void AddCard(BattleUnit unit, CardDef def, int count, bool free)
        {
            if (def == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var inst = CardInstance.Wrap(def);
                if (free)
                {
                    inst.FreeTurns = 1;
                }

                unit.Hand.Add(inst);
            }
        }

        private void AddRandomCards(BattleUnit unit, CardSeries series, int count, bool free)
        {
            var pool = db.OfSeries(series);
            if (pool.Count == 0)
            {
                pool = db.OfSeries(EnemyRoster.UnlockedSeries(Level));
            }

            if (pool.Count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                AddCard(unit, pool[rng.Range(0, pool.Count)], 1, free);
            }
        }

        // ------------------------------------------------------------ 数值管线

        private void AddTurnMana(BattleUnit unit)
        {
            GainMana(unit, unit.ManaRegen);
        }

        public void GainMana(BattleUnit unit, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            unit.Mana += amount;
            unit.ManaGainedTotal += amount;
            if (unit.IsPlayer)
            {
                manaGainedTotal += amount;
            }

            Emit(EventKind.Mana, "+" + amount + " 魔力", amount, !unit.IsPlayer);
        }

        public void GainShield(BattleUnit unit, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            unit.Shield += amount;
            Emit(EventKind.Shield, "+" + amount + " 护盾", amount, !unit.IsPlayer);
        }

        public void Heal(BattleUnit unit, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            amount += unit.BuffValue(BuffKind.HealFlatUp);
            int before = unit.Hp;
            unit.Hp = Math.Min(unit.HpMax, unit.Hp + amount);
            int gained = unit.Hp - before;
            unit.HealedTotal += gained;
            if (unit.IsPlayer)
            {
                healedTotal += gained;
            }

            Emit(EventKind.Heal, "+" + gained + " 生命", gained, !unit.IsPlayer);
            if (gained > 0)
            {
                FireHooks(unit, unit == Player ? Enemy : Player, HookTiming.Heal);
            }
        }

        /// <summary>攻击管线：加成 → 百分比 → 目标减免。</summary>
        public void DealDamage(BattleUnit source, BattleUnit target, int raw)
        {
            if (raw <= 0 || target == null || Phase == BattlePhase.Ended)
            {
                return;
            }

            int amount = raw + source.BuffValue(BuffKind.DmgFlatUp);
            int pctUp = source.BuffValue(BuffKind.DmgOutPctUp);
            if (pctUp > 0)
            {
                amount = amount * (100 + pctUp) / 100;
            }

            amount -= target.BuffValue(BuffKind.DmgFlatDown);
            int takenPct = target.BuffValue(BuffKind.DmgTakenPctUp);
            if (takenPct > 0)
            {
                amount = amount * (100 + takenPct) / 100;
            }

            if (amount < 1)
            {
                amount = 1;
            }

            int applied = TakeDamage(target, source, amount, true);
            source.DamageDealtTotal += applied;
            if (applied > 0 && target.IsPlayer)
            {
                target.EnemyAttackedMeThisTurn = true;
            }

            if (applied > 0)
            {
                FireHooks(source, target, HookTiming.DealDamage);
            }
        }

        /// <summary>伤害结算：闪避 → 转移 → 护盾 → 生命。</summary>
        private int TakeDamage(BattleUnit target, BattleUnit source, int amount, bool fromCombat)
        {
            if (amount <= 0)
            {
                return 0;
            }

            if (fromCombat && target.DodgeCharges > 0)
            {
                target.DodgeCharges--;
                Emit(EventKind.Shield, target.Name + " 闪避了这次伤害", 0, !target.IsPlayer);
                return 0;
            }

            if (fromCombat && target.TransferPool > 0)
            {
                int moved = Math.Min(target.TransferPool, amount);
                target.TransferPool -= moved;
                amount -= moved;
                var other = target == Player ? Enemy : Player;
                HurtDirect(other, moved);
                Emit(EventKind.Damage, "伤害转移 " + moved + " 点", moved, other.IsPlayer);
                if (amount <= 0)
                {
                    return moved;
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
                target.TookDamageThisTurn = true;
            }

            int total = absorbed + hpLoss;
            Emit(EventKind.Damage,
                (hpLoss > 0 ? "-" + hpLoss : string.Empty) + (absorbed > 0 ? (hpLoss > 0 ? " " : "") + "护盾-" + absorbed : string.Empty),
                total, !target.IsPlayer);

            if (hpLoss > 0)
            {
                FireHooks(target, source, HookTiming.Hurt);
            }

            return total;
        }

        /// <summary>无视护盾的直接生命损失（吸血 / 转移 / 借命）。</summary>
        private void HurtDirect(BattleUnit unit, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int lost = Math.Min(unit.Hp, amount);
            unit.Hp -= lost;
            unit.DamageTakenThisTurn += lost;
            unit.TookDamageThisTurn = true;
            Emit(EventKind.Damage, "-" + lost, lost, !unit.IsPlayer);
        }

        public void Draw(BattleUnit unit, int count, bool announce)
        {
            int limit = unit.IsPlayer ? DeckRules.HandLimit : 12;
            for (int i = 0; i < count; i++)
            {
                if (unit.Hand.Count >= limit)
                {
                    break;
                }

                if (unit.Deck.Count == 0)
                {
                    if (unit.Discard.Count == 0)
                    {
                        break;
                    }

                    unit.Deck.AddRange(unit.Discard);
                    unit.Discard.Clear();
                    rng.Shuffle(unit.Deck);
                }

                if (unit.Deck.Count == 0)
                {
                    break;
                }

                var card = unit.Deck[0];
                unit.Deck.RemoveAt(0);
                unit.Hand.Add(card);
            }

            if (announce)
            {
                Emit(EventKind.Draw, "抽牌", count, !unit.IsPlayer);
            }
        }

        // ------------------------------------------------------------ 钩子

        private void FireHooks(BattleUnit unit, BattleUnit foe, HookTiming timing)
        {
            if (resolvingHooks || unit.Hooks.Count == 0)
            {
                return;
            }

            resolvingHooks = true;
            var snapshot = new List<Hook>(unit.Hooks);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var hook = snapshot[i];
                if (hook.Timing != timing || hook.Times <= 0 || hook.Delay > 0 || !unit.Hooks.Contains(hook))
                {
                    continue;
                }

                hook.Fired++;
                for (int k = 0; k < hook.Ops.Count; k++)
                {
                    var op = hook.Ops[k];
                    if (op.Kind == "rampdmg")
                    {
                        var ramped = new CardOp
                        {
                            Kind = "rampdmg",
                            A = op.A + op.B * (hook.Fired - 1)
                        };
                        Exec(unit, foe, ramped, null, 0);
                    }
                    else
                    {
                        Exec(unit, foe, op, null, 0);
                    }
                }
            }

            resolvingHooks = false;
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

        public void DebugGrantMana(int amount)
        {
            GainMana(Player, amount);
        }

        public void DebugSetMana(int amount)
        {
            Player.Mana = amount;
        }

        public string DeckSummary()
        {
            return "牌库 " + Player.Deck.Count + " / 手牌 " + Player.Hand.Count + " / 弃牌 " + Player.Discard.Count;
        }
    }
}
