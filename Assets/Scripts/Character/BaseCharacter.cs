using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public abstract class BaseCharacter
{
    // 目标
    public BaseCharacter Target { get; set; }

    // 基础属性
    public ulong health = 30;
    public ulong mana = 0;
    public ulong shiled = 0; // 护盾值
    public ulong autoManaPerTurn = 3;  // 每回合自动增加的法力值
    public bool IsInTurn { get; private set; }
    private bool immuneThisTurn = false;
    private bool immuneSelfDamage = false;
    private ulong cardNumberScaleNumerator = 1;
    private ulong cardNumberScaleDenominator = 1;
    protected int extraCardDuration = 0;
    private float damageTakenMultiplier = 1f;
    private static int reflectDamageContextDepth = 0;
    private static readonly List<string> nestedTriggerCardNames = new();
    public static BaseCard ActiveCardContext { get; set; }
    public static Dot ActiveDotContext { get; set; }
    public static bool IsReflectDamageContext => reflectDamageContextDepth > 0;
    public BaseCard LastPlayedCard { get; private set; }
    public BaseCard PreviousPlayedCard { get; private set; }
    public BaseCard LastDamageCard { get; private set; }
    public Dot LastDamageDot { get; private set; }
    public BaseCharacter LastDamageSource { get; private set; }
    public event Action<ulong, BaseCharacter> DamageTaken;
    public event Action<ulong, BaseCharacter> DamageDealt;
    public event Action<ulong> HealTaken;
    public event Func<ulong, BaseCharacter, ulong> IncomingDamageProcessing;
    
    public abstract void ChangeHealth(long amount);
    
    public virtual void OnBattleEnd() { }

    public virtual void ChangeMana(long amount)
    {
        if (amount >= 0)
        {
            ulong gain = (ulong)amount;
            mana = SaturatingAdd(mana, gain);
            if (gain > 0)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Mana");
                if (this is Player) EventCenter.Publish(GameEvents.PlayerGainedMana, new CharacterValueEventContext(this, gain));
            }
        }
        else
        {
            ulong loss = MagnitudeFromSigned(amount);
            mana = SaturatingSub(mana, loss);
        }
    }

    // 行动
    public IEnumerator StartTurnRoutine()
    {
        EventCenter.Publish(GameEvents.CharacterTurnStarted, new CharacterEventContext(this));

        IsInTurn = true;
        immuneThisTurn = false;
        shiled = SaturatingSub(shiled, 3);  // 另一个方案是每回合衰减3

        // Async Apply Dots
        yield return ApplyDotsRoutine();

        if (!IsInTurn) yield break;

        Action();
    }

    // Backward-compatible entry point: routes to async turn processing.
    public void StartTurn()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartCoroutine(StartTurnRoutine());
            return;
        }

        Debug.LogWarning("BattleManager.Instance is null, cannot start async turn routine.");
    }

    protected abstract void Action();

    public virtual void EndTurn()
    {
        if (!IsInTurn) return;

        IsInTurn = false;
        
        // 注意：ChangeMana 会在这里被调用，所以我们可以在这之前处理一些逻辑
        // 但对于玩家来说，我们可能需要等待 RougeUI 选择完毕后再恢复魔力
        // 不过由于 EndTurn 是同步/立即执行的，我们需要某种机制来挂起或分步执行。
        // 或者我们可以在 BattleManager 中控制 EndTurn 的流程。
        // 为了支持 RougeUI 这种特殊的等待逻辑，我们可以在 Player 中重写 EndTurn。
        
        ChangeMana((long)Math.Min(autoManaPerTurn, (ulong)long.MaxValue)); // 每回合结束增加自动法力值
        EventCenter.Publish(GameEvents.CharacterTurnEnded, new CharacterEventContext(this));
    }

    public void AbortTurn()
    {
        IsInTurn = false;
        immuneThisTurn = false;
    }

    public void RestoreTurnState(bool isInTurn)
    {
        IsInTurn = isInTurn;
        immuneThisTurn = false;
    }


    // DOT效果
    public List<Dot> dotBar = new List<Dot>();
    
    // Async version
    private IEnumerator ApplyDotsRoutine(bool fastMode = false)
    {
        var dotsToProcess = new List<Dot>(dotBar);
        if (dotsToProcess.Count > 0)
        {
            float settlementScale = GameSettings.GetSettlementDelayScale();
            float maxTotalDuration = fastMode ? 0.8f : 2.2f;
            float maxDelayPerDot = fastMode ? 0.06f : 0.15f;
            maxTotalDuration *= settlementScale;
            maxDelayPerDot *= settlementScale;
            // Keep a strict total-time cap even when DOT count is very large.
            float delay = Mathf.Min(maxDelayPerDot, maxTotalDuration / dotsToProcess.Count);
            bool showDescription = !fastMode;

            Transform targetTransform = null;
            if (DamageEffectManager.Instance != null)
            {
                 // Determine transform based on character type
                 // A bit hacky since BaseCharacter doesn't know about Transforms directly usually
                 // But we can check against BattleManager instance
                if (this == BattleManager.Instance.player) targetTransform = DamageEffectManager.Instance.playerTransform;
                else if (this == BattleManager.Instance.enemy) targetTransform = DamageEffectManager.Instance.enemyTransform;
            }

            foreach (var effect in dotsToProcess)
            {
                // Show Description
                if (showDescription && targetTransform != null && DamageEffectManager.Instance != null)
                {
                    DamageEffectManager.Instance.ShowFloatingText(targetTransform, effect.description?.Invoke() ?? "", Color.yellow);
                }

                effect.Apply();
                
                // Wait
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield return null;
                }
            }
        }
    }

    public void AddDot(Dot dotEffect)
    {
        dotBar.Add(dotEffect);
    }
    public void RemoveDot(Func<Dot, bool> condition)
    {
        foreach (var de in new List<Dot>(dotBar))
        {
            if (condition.Invoke(de))
            {
                dotBar.Remove(de);
            }
        }
    }
    public void TriggerDotsOnce()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartCoroutine(ApplyDotsRoutine(true));
            return;
        }

        Debug.LogWarning("BattleManager.Instance is null, cannot trigger async dot routine.");
    }

    public void TriggerDotsTimes(int times)
    {
        if (times <= 0) return;
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartCoroutine(TriggerDotsTimesRoutine(times));
            return;
        }

        Debug.LogWarning("BattleManager.Instance is null, cannot trigger async dot routine.");
    }

    private IEnumerator TriggerDotsTimesRoutine(int times)
    {
        // 快照当前的DOT列表，防止期间新添加的DOT被多次触发
        var snapshotDots = new List<Dot>(dotBar);
        
        for (int i = 0; i < times; i++)
        {
            yield return ApplySpecificDotsRoutine(snapshotDots, true);
        }
    }
    
    private IEnumerator ApplySpecificDotsRoutine(List<Dot> dotsToProcess, bool fastMode = false)
    {
        if (dotsToProcess.Count > 0)
        {
            float settlementScale = GameSettings.GetSettlementDelayScale();
            float maxTotalDuration = fastMode ? 0.8f : 2.2f;
            float maxDelayPerDot = fastMode ? 0.06f : 0.15f;
            maxTotalDuration *= settlementScale;
            maxDelayPerDot *= settlementScale;
            // Keep a strict total-time cap even when DOT count is very large.
            float delay = Mathf.Min(maxDelayPerDot, maxTotalDuration / dotsToProcess.Count);
            bool showDescription = !fastMode;

            Transform targetTransform = null;
            if (DamageEffectManager.Instance != null)
            {
                if (this == BattleManager.Instance.player) targetTransform = DamageEffectManager.Instance.playerTransform;
                else if (this == BattleManager.Instance.enemy) targetTransform = DamageEffectManager.Instance.enemyTransform;
            }

            foreach (var effect in dotsToProcess)
            {
                // 检查该DOT是否仍然存在于当前dotBar中（可能被驱散等移除）
                if (!dotBar.Contains(effect)) continue;
                
                if (showDescription && targetTransform != null && DamageEffectManager.Instance != null)
                {
                    DamageEffectManager.Instance.ShowFloatingText(targetTransform, effect.description?.Invoke() ?? "", Color.yellow);
                }

                effect.Apply();
                
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield return null;
                }
            }
        }
    }

    public void SetImmuneThisTurn(bool value)
    {
        immuneThisTurn = value;
    }

    public void SetImmuneSelfDamage(bool value)
    {
        immuneSelfDamage = value;
    }

    public void SetDamageTakenMultiplier(float multiplier)
    {
        damageTakenMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ApplyHealthChange(long amount, BaseCharacter source = null)
    {
        if (amount < 0 && immuneThisTurn) return;
        if (amount < 0 && source == this && immuneSelfDamage) return;

        ulong processedDamage = 0UL;
        if (amount < 0)
        {
            processedDamage = MagnitudeFromSigned(amount);
            if (damageTakenMultiplier != 1f)
            {
                double scaled = processedDamage * damageTakenMultiplier;
                if (scaled <= 0d) return;
                if (scaled >= ulong.MaxValue) processedDamage = ulong.MaxValue;
                else processedDamage = (ulong)Math.Round(scaled);
                if (processedDamage == 0) return;
            }

            processedDamage = ProcessIncomingDamage(processedDamage, source);
            if (processedDamage == 0) return;
            amount = -(long)Math.Min(processedDamage, (ulong)long.MaxValue);
        }

        if (amount < 0)
        {
            LastDamageCard = ActiveCardContext;
            LastDamageDot = ActiveDotContext;
            LastDamageSource = source;
        }

        ChangeHealth(amount);

        if (amount < 0)
        {
            DamageTaken?.Invoke(processedDamage, source);
            source?.DamageDealt?.Invoke(processedDamage, this);
        }
        else if (amount > 0)
        {
            ulong heal = (ulong)amount;
            HealTaken?.Invoke(heal);
            if (this is Player) EventCenter.Publish(GameEvents.PlayerHealed, new CharacterValueEventContext(this, heal));
        }
    }

    public void DealDamage(BaseCharacter target, ulong amount)
    {
        if (target == null || amount == 0) return;
        long signedDamage = amount >= (ulong)long.MaxValue ? -long.MaxValue : -(long)amount;
        target.ApplyHealthChange(signedDamage, this);
    }

    public void DealReflectDamage(BaseCharacter target, ulong amount)
    {
        if (target == null || amount == 0) return;
        long signedDamage = amount >= (ulong)long.MaxValue ? -long.MaxValue : -(long)amount;
        reflectDamageContextDepth++;
        try
        {
            target.ApplyHealthChange(signedDamage, this);
        }
        finally
        {
            reflectDamageContextDepth--;
        }
    }

    public static bool TryEnterNestedTrigger(BaseCard triggerCard, out BaseCard previousContext)
    {
        previousContext = ActiveCardContext;
        if (triggerCard == null) return true;

        string triggerName = triggerCard.Name;
        if (!string.IsNullOrEmpty(triggerName))
        {
            if (nestedTriggerCardNames.Contains(triggerName))
            {
                return false;
            }
            nestedTriggerCardNames.Add(triggerName);
        }

        ActiveCardContext = triggerCard;
        return true;
    }

    public static void ExitNestedTrigger(BaseCard triggerCard, BaseCard previousContext)
    {
        ActiveCardContext = previousContext;
        if (triggerCard == null) return;

        string triggerName = triggerCard.Name;
        if (string.IsNullOrEmpty(triggerName)) return;
        for (int i = nestedTriggerCardNames.Count - 1; i >= 0; i--)
        {
            if (nestedTriggerCardNames[i] != triggerName) continue;
            nestedTriggerCardNames.RemoveAt(i);
            break;
        }
    }


    public void ApplyOverclock(ulong factor)
    {
        if (factor == 1) return;
        ApplyCardNumberScale(factor, 1UL);
    }

    public void ApplyDownclock()
    {
        ApplyCardNumberScale(1UL, 2UL);
    }

    public void AddGlobalDurationBonus(int amount)
    {
        if (amount == 0) return;
        extraCardDuration += amount;
    }

    private void ApplyBuffsToCard(BaseCard card)
    {
        if (card == null) return;
        if (cardNumberScaleNumerator != cardNumberScaleDenominator)
        {
            card.ScaleNumbers(cardNumberScaleNumerator, cardNumberScaleDenominator);
        }
        if (extraCardDuration != 0)
        {
            card.AddDuration(extraCardDuration);
        }
    }



    // 卡牌逻辑
    public List<BaseCard> Cards = new List<BaseCard>();
    protected virtual int MaxHandSize => int.MaxValue;
    private bool IsHandFull => MaxHandSize > 0 && Cards.Count >= MaxHandSize;

    public void NotifyHandChanged()
    {
        if (this is Player)
        {
            EventCenter.Publish(GameEvents.PlayerHandChanged, new HandEventContext(this, Cards.Count));
        }
    }

    public BaseCard GainRandomCard()
    {
        if (IsHandFull) return null;
        BaseCard newCard = this is Player ? CardFactory.GetRandomCard() : CardFactory.GetRandomEnemyCard();
        if (newCard == null) return null;
        if (this is Player)
        {
            newCard = CardRuntimeHelper.PreparePlayerAcquiredCard(newCard);
        }
        ApplyBuffsToCard(newCard);
        newCard.SetOwningCharacter(this);
        Cards.Add(newCard);
        if (this is Player) EventCenter.Publish(GameEvents.PlayerCardGeneratedToHand, new CardEventContext(this, newCard));
        NotifyHandChanged();
        return newCard;
    }

    /// <summary>
    /// 获取指定卡牌（不走抽牌逻辑）
    /// </summary>
    /// <param name="card"></param>
    public void GainCard(BaseCard card)
    {
        if (card == null) return;
        if (IsHandFull) return;
        if (this is Player)
        {
            card = CardRuntimeHelper.PreparePlayerAcquiredCard(card);
        }
        ApplyBuffsToCard(card);
        card.SetOwningCharacter(this);
        Cards.Add(card);
        if (this is Player) EventCenter.Publish(GameEvents.PlayerCardGeneratedToHand, new CardEventContext(this, card));
        NotifyHandChanged();
    }


    public BaseCard DrawCard(ulong cost = 1)
    {
        if (IsHandFull) return null;
        if (mana < cost) return null;  // 抽卡需要消耗法力值
        ChangeMana(-(long)Math.Min(cost, (ulong)long.MaxValue));  // 抽卡消耗指定点法力值

        // 随机从牌库中抽取一张卡牌加入手牌
        BaseCard baseCard;
        if (this is Player)
        {  // 玩家从牌组中抽牌
            baseCard = CardFactory.DrawCardFromPlayerDeck();
        }
        else
        {  // 敌人直接随机生成卡牌
            baseCard = CardFactory.DrawCardFromEnemyDeck();
        }
        if (baseCard == null) return null;
        ApplyBuffsToCard(baseCard);
        baseCard.SetOwningCharacter(this);
        Cards.Add(baseCard);

        var drawContext = new CardEventContext(this, baseCard);
        EventCenter.Publish(GameEvents.BattleCardDrawnToHand, drawContext);
        if (this is Player) EventCenter.Publish(GameEvents.PlayerCardDrawnToHand, drawContext);
        NotifyHandChanged();

        return baseCard;
    }

    public ulong GetCurrentDrawCardCost()
    {
        return 1UL;
    }

    public void PlayCard(BaseCard card)
    {
        if (Cards.Contains(card) && mana >= card.Cost)
        {
            // 扣除法力值
            ChangeMana(-(long)Math.Min(card.Cost, (ulong)long.MaxValue));

            // 从手牌中移除卡牌
            Cards.Remove(card);
            NotifyHandChanged();

            PreviousPlayedCard = LastPlayedCard;
            LastPlayedCard = card;
            card.SetOwningCharacter(this);
            var playContext = new CardEventContext(this, card);

            EventCenter.Publish(GameEvents.BattleCardPlayedFromHand, playContext);
            if (this is Player)
            {
                EventCenter.Publish(GameEvents.PlayerCardPlayedFromHand, playContext);
            }

            // 使用卡牌效果
            var previousContext = ActiveCardContext;
            ActiveCardContext = card;
            card.Execute(this, Target);
            ActiveCardContext = previousContext;
            CardRuntimeHelper.TryTriggerPlayerJail(this, card);
            TransformRepriseCardsInHand(card);

            EventCenter.Publish(GameEvents.BattleCardResolved, playContext);
            if (this is Player)
            {
                EventCenter.Publish(GameEvents.PlayerCardResolved, playContext);
            }
        }
        else
        {
            Debug.LogWarning("无法使用卡牌: " + card.Name);
        }
    }

    private void TransformRepriseCardsInHand(BaseCard playedCard)
    {
        if (playedCard == null || Cards.Count == 0) return;
        for (int i = 0; i < Cards.Count; i++)
        {
            BaseCard current = Cards[i];
            if (current is not 重奏 reprise) continue;
            reprise.TransformInto(playedCard);
            if (this is Player)
            {
                EventCenter.Publish(GameEvents.PlayerCardRefreshed, new CardEventContext(this, current));
            }
        }
    }

    public static ulong SaturatingAdd(ulong left, ulong right)
    {
        ulong result = left + right;
        if (result < left) return ulong.MaxValue;
        return result;
    }

    public static ulong SaturatingSub(ulong left, ulong right)
    {
        if (right >= left) return 0;
        return left - right;
    }

    public static ulong SaturatingMultiply(ulong left, ulong right)
    {
        if (left == 0 || right == 0) return 0;
        if (left > ulong.MaxValue / right) return ulong.MaxValue;
        return left * right;
    }

    private static ulong MagnitudeFromSigned(long value)
    {
        if (value >= 0) return (ulong)value;
        if (value == long.MinValue) return (ulong)long.MaxValue + 1UL;
        return (ulong)(-value);
    }

    private void ApplyCardNumberScale(ulong numerator, ulong denominator)
    {
        if (denominator == 0UL) return;
        if (numerator == denominator) return;

        cardNumberScaleNumerator = SaturatingMultiply(cardNumberScaleNumerator, numerator);
        cardNumberScaleDenominator = SaturatingMultiply(cardNumberScaleDenominator, denominator);
        ReduceCardNumberScale();

        foreach (var card in Cards)
        {
            card.ScaleNumbers(numerator, denominator);
        }
    }

    private void ReduceCardNumberScale()
    {
        ulong gcd = GreatestCommonDivisor(cardNumberScaleNumerator, cardNumberScaleDenominator);
        if (gcd <= 1UL) return;
        cardNumberScaleNumerator /= gcd;
        cardNumberScaleDenominator /= gcd;
    }

    private static ulong GreatestCommonDivisor(ulong left, ulong right)
    {
        while (right != 0UL)
        {
            ulong remainder = left % right;
            left = right;
            right = remainder;
        }

        return left == 0UL ? 1UL : left;
    }

    private ulong ProcessIncomingDamage(ulong damage, BaseCharacter source)
    {
        if (damage == 0) return 0;
        if (IncomingDamageProcessing == null) return damage;

        Delegate[] handlers = IncomingDamageProcessing.GetInvocationList();
        ulong currentDamage = damage;
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is Func<ulong, BaseCharacter, ulong> handler)
            {
                currentDamage = handler(currentDamage, source);
                if (currentDamage == 0) break;
            }
        }

        return currentDamage;
    }

    public void RemoveCard(BaseCard card)
    {
        if (Cards.Contains(card))
        {
            Cards.Remove(card);
            if (this is Player)
            {
                EventCenter.Publish(GameEvents.PlayerCardRemovedFromHand, new CardEventContext(this, card));
            }
            NotifyHandChanged();
        }
    }
}
