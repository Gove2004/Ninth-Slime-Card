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
    private ulong overclockMultiplier = 1;
    protected int extraCardDuration = 0;
    private float damageTakenMultiplier = 1f;
    public static BaseCard ActiveCardContext { get; set; }
    public static Dot ActiveDotContext { get; set; }
    public BaseCard LastDamageCard { get; private set; }
    public Dot LastDamageDot { get; private set; }
    public BaseCharacter LastDamageSource { get; private set; }
    public event Action<ulong, BaseCharacter> DamageTaken;
    public event Action<ulong, BaseCharacter> DamageDealt;
    public event Action<ulong> HealTaken;
    
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
                if (this is Player) EventCenter.Publish("Player_GainMana", gain);
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
        EventCenter.Publish("TurnStart", this);

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
        IsInTurn = false;
        
        // 注意：ChangeMana 会在这里被调用，所以我们可以在这之前处理一些逻辑
        // 但对于玩家来说，我们可能需要等待 RougeUI 选择完毕后再恢复魔力
        // 不过由于 EndTurn 是同步/立即执行的，我们需要某种机制来挂起或分步执行。
        // 或者我们可以在 BattleManager 中控制 EndTurn 的流程。
        // 为了支持 RougeUI 这种特殊的等待逻辑，我们可以在 Player 中重写 EndTurn。
        
        ChangeMana((long)Math.Min(autoManaPerTurn, (ulong)long.MaxValue)); // 每回合结束增加自动法力值
        EventCenter.Publish("CharacterEndedTurn");
    }

    public void AbortTurn()
    {
        IsInTurn = false;
        immuneThisTurn = false;
    }


    // DOT效果
    public List<Dot> dotBar = new List<Dot>();
    
    // Async version
    private IEnumerator ApplyDotsRoutine()
    {
        var dotsToProcess = new List<Dot>(dotBar);
        if (dotsToProcess.Count > 0)
        {
            float maxTotalDuration = 10f;
            float delay = Mathf.Min(0.5f, maxTotalDuration / dotsToProcess.Count);

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
                if (targetTransform != null && DamageEffectManager.Instance != null)
                {
                    DamageEffectManager.Instance.ShowFloatingText(targetTransform, effect.description?.Invoke() ?? "", Color.yellow);
                }

                effect.Apply();
                
                // Wait
                yield return new WaitForSeconds(delay);
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
            BattleManager.Instance.StartCoroutine(ApplyDotsRoutine());
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
        for (int i = 0; i < times; i++)
        {
            yield return ApplyDotsRoutine();
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

        if (amount < 0 && damageTakenMultiplier != 1f)
        {
            ulong damage = MagnitudeFromSigned(amount);
            double scaled = damage * damageTakenMultiplier;
            if (scaled <= 0d) return;
            if (scaled >= ulong.MaxValue) damage = ulong.MaxValue;
            else damage = (ulong)Math.Round(scaled);
            if (damage == 0) return;
            amount = -(long)Math.Min(damage, (ulong)long.MaxValue);
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
            ulong damage = MagnitudeFromSigned(amount);
            DamageTaken?.Invoke(damage, source);
            source?.DamageDealt?.Invoke(damage, this);
        }
        else if (amount > 0)
        {
            ulong heal = (ulong)amount;
            HealTaken?.Invoke(heal);
            if (this is Player) EventCenter.Publish("Player_Heal", heal);
        }
    }

    public void DealDamage(BaseCharacter target, ulong amount)
    {
        if (target == null || amount == 0) return;
        long signedDamage = amount >= (ulong)long.MaxValue ? -long.MaxValue : -(long)amount;
        target.ApplyHealthChange(signedDamage, this);
    }


    public void ApplyOverclock(ulong factor)
    {
        if (factor == 1) return;
        overclockMultiplier = SaturatingMultiply(overclockMultiplier, factor);


        // 新的超频只对玩家手牌生效
        foreach (var card in Cards)
        {
            card.MultiplyNumbers(factor);
        }
    }

    public void AddGlobalDurationBonus(int amount)
    {
        if (amount == 0) return;
        extraCardDuration += amount;
    }

    private void ApplyBuffsToCard(BaseCard card)
    {
        if (card == null) return;
        if (overclockMultiplier != 1)
        {
            // card.MultiplyNumbers(overclockMultiplier);  // // 新的超频只对玩家手牌生
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


    public BaseCard GainRandomCard()
    {
        if (IsHandFull) return null;
        BaseCard newCard = this is Player ? CardFactory.GetRandomCard() : CardFactory.GetRandomEnemyCard();
        if (newCard == null) return null;
        ApplyBuffsToCard(newCard);
        Cards.Add(newCard);
        if (this is Player) EventCenter.Publish("Player_DrawCard", newCard);
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
        ApplyBuffsToCard(card);
        Cards.Add(card);
        if (this is Player) EventCenter.Publish("Player_DrawCard", card);
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
        Cards.Add(baseCard);

        EventCenter.Publish("CardDrawn", baseCard);

        return baseCard;
    }

    public void PlayCard(BaseCard card)
    {
        if (Cards.Contains(card) && mana >= card.Cost)
        {
            // 扣除法力值
            ChangeMana(-(long)Math.Min(card.Cost, (ulong)long.MaxValue));

            // 从手牌中移除卡牌
            Cards.Remove(card);

            // 使用卡牌效果
            var previousContext = ActiveCardContext;
            ActiveCardContext = card;
            card.Execute(this, Target);
            ActiveCardContext = previousContext;

            EventCenter.Publish("CardPlayed", card);
            if (this is Player)
            {
                EventCenter.Publish("Player_PlayCardExecuted", card);
            }
        }
        else
        {
            Debug.LogWarning("无法使用卡牌: " + card.Name);
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

    public void RemoveCard(BaseCard card)
    {
        if (Cards.Contains(card))
        {
            Cards.Remove(card);
            if (this is Player)
            {
                EventCenter.Publish("Player_RemoveCard", card);
            }
        }
    }
}
