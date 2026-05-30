using System;
using System.Collections.Generic;
using GoveKits.Runtime.Unit;
using UnityEngine;

public abstract class BaseCharacter : UnitBehaviour
{
    private const int StartingHandCount = 1;

    public BaseCharacter Target;

    public BaseCard CurrentResolvingCard { get; private set; }
    public BaseCharacter CurrentSourceCharacter { get; private set; }
    public BaseCharacter CurrentTargetCharacter { get; private set; }
    public int CurrentEffectAmount { get; private set; }
    public int PendingExtraCardTriggers { get; private set; }
    public int TargetedImmunityCharges { get; private set; }
    public int CardsPlayedThisTurn { get; private set; }
    public int TechCardsPlayedThisTurn { get; private set; }
    public int DamageTakenThisTurn { get; private set; }
    public BaseCharacter LastDamageSourceThisTurn { get; private set; }
    public int PendingNextCardDamageBonus { get; private set; }
    public int PendingNextCardCostReduction { get; private set; }
    public int PendingNextTechCardExtraTriggers { get; private set; }
    public int PendingTechCardDamageBonus { get; private set; }
    public int PendingTechCardDamageBonusUses { get; private set; }
    private bool isResolvingHookEffect;

    public List<BaseCard> HandCards { get; } = new List<BaseCard>();
    public List<BaseCard> DeckCards { get; } = new List<BaseCard>();
    public List<BaseCard> DiscardCards { get; } = new List<BaseCard>();

    public bool IsDead => Attributes.GetBaseValue(StaticString.属性.生命) <= 0;

    public Action<BaseCharacter> BeforeActionTurn;
    public Action<BaseCharacter> AfterActionTurn;
    public event Action<BaseCharacter> OnStatsChanged;
    public event Action<BaseCharacter> OnHandChanged;

    public override void InitAttributes()
    {
        base.InitAttributes();
        Attributes.Add(StaticString.属性.生命, 10);
        Attributes.Add(StaticString.属性.最大生命, 10);
        Attributes.Add(StaticString.属性.法力, 0);
        Attributes.Add(StaticString.属性.每回合自动恢复, 2);
        Attributes.Add(StaticString.属性.护盾, 0);
        Attributes.Add(StaticString.属性.伤害百分比提升, 0);
        Attributes.Add(StaticString.属性.伤害固定提升, 0);
        Attributes.Add(StaticString.属性.伤害百分比减免, 0);
        Attributes.Add(StaticString.属性.伤害固定减免, 0);
        Attributes.Add(StaticString.属性.治疗百分比提升, 0);
        Attributes.Add(StaticString.属性.治疗追加, 0);

        Attributes.BeforeValueChange += OnBeforeAttributeChange;
        Attributes.AfterValueChange += OnAfterAttributeChange;
    }

    public float HealthPercent
    {
        get
        {
            float maxHealth = Attributes.GetValue(StaticString.属性.最大生命);
            if (maxHealth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Attributes.GetValue(StaticString.属性.生命) / maxHealth);
        }
    }

    public virtual bool CanUseCard(BaseCard card, BaseCharacter target = null)
    {
        if (card == null) return false;
        if (IsDead) return false;
        if (!HandCards.Contains(card)) return false;
        return card.CanUse(this, target ?? Target);
    }

    public bool CanSpendMana(int amount)
    {
        amount = Mathf.Max(0, amount);
        return Attributes.GetBaseValue(StaticString.属性.法力) >= amount;
    }

    public void GainMana(int amount)
    {
        if (amount <= 0) return;
        Attributes.ChangeBase(StaticString.属性.法力, amount);
    }

    public bool SpendMana(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (!CanSpendMana(amount)) return false;
        Attributes.ChangeBase(StaticString.属性.法力, -amount);
        return true;
    }

    public void TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;

        int shield = GetShield();
        if (shield > 0)
        {
            int absorbed = Mathf.Min(shield, amount);
            Attributes.ChangeBase(StaticString.属性.护盾, -absorbed);
            amount -= absorbed;
        }

        if (amount <= 0)
        {
            return;
        }

        Attributes.ChangeBase(StaticString.属性.生命, -amount);
        DamageTakenThisTurn += amount;
        LastDamageSourceThisTurn = CurrentSourceCharacter;
        TriggerHookEffect(HookTiming.WhenHurt);
    }

    public void Heal(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;
        Attributes.ChangeBase(StaticString.属性.生命, amount);
        TriggerHookEffect(HookTiming.WhenHeal);
    }

    public bool UseCard(BaseCard card, BaseCharacter target = null)
    {
        BaseCharacter resolvedTarget = target ?? Target;
        if (!CanUseCard(card, resolvedTarget))
        {
            return false;
        }

        resolvedTarget = card.ResolveTarget(this, resolvedTarget);
        int extraTriggers = PendingExtraCardTriggers;
        PendingExtraCardTriggers = 0;

        int bonusDamage = PendingNextCardDamageBonus;
        PendingNextCardDamageBonus = 0;

        int costReduction = PendingNextCardCostReduction;
        PendingNextCardCostReduction = 0;

        if (card.Series == "科技")
        {
            extraTriggers += PendingNextTechCardExtraTriggers;
            PendingNextTechCardExtraTriggers = 0;

            if (PendingTechCardDamageBonusUses > 0)
            {
                bonusDamage += PendingTechCardDamageBonus;
                PendingTechCardDamageBonusUses--;
                if (PendingTechCardDamageBonusUses <= 0)
                {
                    PendingTechCardDamageBonus = 0;
                }
            }
        }

        int originalRuntimeCost = card.RuntimeCost;
        if (costReduction > 0)
        {
            card.RuntimeCost = Mathf.Max(0, card.GetCurrentCost() - costReduction);
        }

        CurrentResolvingCard = card;
        CurrentSourceCharacter = this;
        CurrentTargetCharacter = resolvedTarget;
        CurrentEffectAmount = bonusDamage;
        card.PreUse(this, resolvedTarget);

        if (card.IsTargetedEffect(this, resolvedTarget) && resolvedTarget != null && resolvedTarget.TryConsumeTargetedImmunity())
        {
            card.RuntimeCost = originalRuntimeCost;
            card.PostUse(this, resolvedTarget);
            ClearEffectContext();
            NotifyHandChanged();
            return true;
        }

        card.OnUse(this, resolvedTarget);
        for (int i = 0; i < extraTriggers; i++)
        {
            card.OnUse(this, resolvedTarget);
        }
        CardsPlayedThisTurn++;
        if (card.Series == "科技")
        {
            TechCardsPlayedThisTurn++;
        }
        TriggerHookEffect(HookTiming.WhenUseCard);
        card.PostUse(this, resolvedTarget);
        card.RuntimeCost = originalRuntimeCost;
        ClearEffectContext();
        NotifyHandChanged();
        return true;
    }

    public void DiscardCard(BaseCard card)
    {
        if (card == null) return;
        if (HandCards.Remove(card))
        {
            DiscardCards.Add(card);
            NotifyHandChanged();
        }
    }

    public virtual void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (DeckCards.Count == 0)
            {
                ShuffleDiscardIntoDeck();
            }

            if (DeckCards.Count == 0)
            {
                break;
            }

            BaseCard card = DeckCards[0];
            DeckCards.RemoveAt(0);
            HandCards.Add(card);
        }

        NotifyHandChanged();
    }

    public void AddCardToHand(BaseCard card)
    {
        if (card == null)
        {
            return;
        }

        HandCards.Add(card);
        NotifyHandChanged();
    }

    public void AddCardToDeck(BaseCard card, bool shuffle = false)
    {
        if (card == null)
        {
            return;
        }

        DeckCards.Add(card);
        if (shuffle)
        {
            ShuffleDeck();
        }
    }

    public void SetMana(int amount)
    {
        SetBaseAttribute(StaticString.属性.法力, Mathf.Max(0, amount));
    }

    public void LoseAllMana()
    {
        SetMana(0);
    }

    public int GetMana()
    {
        return Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.法力));
    }

    public int GetHealth()
    {
        return Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.生命));
    }

    public int GetMaxHealth()
    {
        return Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.最大生命));
    }

    public int GetShield()
    {
        return Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.护盾));
    }

    public void GainShield(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;
        Attributes.ChangeBase(StaticString.属性.护盾, amount);
    }

    public void SetNextCardExtraTriggers(int extraTimes)
    {
        PendingExtraCardTriggers = Mathf.Max(0, extraTimes);
    }

    public void SetNextCardDamageBonus(int amount)
    {
        PendingNextCardDamageBonus = Mathf.Max(0, amount);
    }

    public void SetNextCardCostReduction(int amount)
    {
        PendingNextCardCostReduction = Mathf.Max(0, amount);
    }

    public void SetNextTechCardExtraTriggers(int extraTimes)
    {
        PendingNextTechCardExtraTriggers = Mathf.Max(0, extraTimes);
    }

    public void SetTechCardDamageBonus(int amount, int uses)
    {
        PendingTechCardDamageBonus = Mathf.Max(0, amount);
        PendingTechCardDamageBonusUses = Mathf.Max(0, uses);
    }

    public void AddTargetedImmunity(int charges)
    {
        TargetedImmunityCharges += Mathf.Max(0, charges);
    }

    public bool TryConsumeTargetedImmunity()
    {
        if (TargetedImmunityCharges <= 0)
        {
            return false;
        }

        TargetedImmunityCharges--;
        return true;
    }

    public void ConsumeAllHandCardsToDiscard(bool includeCurrentCard)
    {
        List<BaseCard> cards = new List<BaseCard>(HandCards);
        foreach (BaseCard card in cards)
        {
            if (!includeCurrentCard && card == CurrentResolvingCard)
            {
                continue;
            }

            DiscardCard(card);
        }
    }

    public bool HasPlayedAnotherCardThisTurn(BaseCard currentCard)
    {
        return CardsPlayedThisTurn > 0 || (currentCard != null && HandCards.Contains(currentCard) && DiscardCards.Contains(currentCard));
    }

    public bool HasPlayedAnotherTechCardThisTurn(BaseCard currentCard)
    {
        bool currentIsTech = currentCard != null && currentCard.Series == "科技";
        return TechCardsPlayedThisTurn > 0 || (currentIsTech && currentCard != null && DiscardCards.Contains(currentCard));
    }

    public void ReplaceHand(System.Collections.Generic.IEnumerable<BaseCard> cards)
    {
        HandCards.Clear();
        if (cards != null)
        {
            HandCards.AddRange(cards);
        }
        NotifyHandChanged();
    }

    public void ExtendHookEffects(int extraTimes)
    {
        if (extraTimes <= 0)
        {
            return;
        }

        foreach (var pair in HookEffects)
        {
            foreach (var effect in pair.Value)
            {
                if (effect.Times > 0)
                {
                    effect.Times += extraTimes;
                }
            }
        }
    }

    public string PeekRandomCardNameFromDeckOrDiscard()
    {
        List<BaseCard> pool = new List<BaseCard>();
        pool.AddRange(DeckCards);
        pool.AddRange(DiscardCards);
        if (pool.Count == 0)
        {
            return null;
        }

        int index = UnityEngine.Random.Range(0, pool.Count);
        return pool[index].Name;
    }

    public string PeekRandomCardNameFromDeck()
    {
        if (DeckCards.Count == 0)
        {
            return null;
        }

        int index = UnityEngine.Random.Range(0, DeckCards.Count);
        return DeckCards[index].Name;
    }

    public void SetEffectContext(BaseCard card, BaseCharacter source, BaseCharacter target, int amount)
    {
        CurrentResolvingCard = card;
        CurrentSourceCharacter = source;
        CurrentTargetCharacter = target;
        CurrentEffectAmount = amount;
    }

    public void ClearEffectContext()
    {
        CurrentResolvingCard = null;
        CurrentSourceCharacter = null;
        CurrentTargetCharacter = null;
        CurrentEffectAmount = 0;
    }

    public void ShuffleDiscardIntoDeck()
    {
        if (DiscardCards.Count == 0)
        {
            return;
        }

        DeckCards.AddRange(DiscardCards);
        DiscardCards.Clear();

        ShuffleDeck();
    }

    public virtual void StartTurn()
    {
        CardsPlayedThisTurn = 0;
        TechCardsPlayedThisTurn = 0;
        DamageTakenThisTurn = 0;
        LastDamageSourceThisTurn = null;
        BeforeActionTurn?.Invoke(this);
        TriggerHookEffect(HookTiming.WhenStartTurn);
        GainMana(Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.每回合自动恢复)));
        DrawCards(2);
    }

    public virtual void EndTurn()
    {
        TriggerHookEffect(HookTiming.WhenEndTurn);
        AfterActionTurn?.Invoke(this);
    }

    public virtual void Setup()
    {
        HandCards.Clear();
        DeckCards.Clear();
        DiscardCards.Clear();

        SetBaseAttribute(StaticString.属性.生命, Attributes.GetBaseValue(StaticString.属性.最大生命));
        SetBaseAttribute(StaticString.属性.法力, 0);

        BuildStarterDeck();
        ShuffleDeck();
        DrawCards(StartingHandCount);
        NotifyStatsChanged();
    }

    protected virtual void BuildStarterDeck()
    {
        AddStarterCard(101, 3);
        AddStarterCard(102, 2);
    }

    protected void AddStarterCard(int id, int count)
    {
        for (int i = 0; i < count; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(id);
            if (card != null)
            {
                DeckCards.Add(card);
            }
        }
    }

    protected void ShuffleDeck()
    {
        for (int i = 0; i < DeckCards.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, DeckCards.Count);
            (DeckCards[i], DeckCards[swapIndex]) = (DeckCards[swapIndex], DeckCards[i]);
        }
    }

    private void SetBaseAttribute(string tag, float targetValue)
    {
        float current = Attributes.GetBaseValue(tag);
        Attributes.ChangeBase(tag, targetValue - current);
    }

    private float OnBeforeAttributeChange(GoveKits.Runtime.Unit.UnitTag tag, float expectedValue)
    {
        if (tag == StaticString.属性.最大生命)
        {
            return Mathf.Max(0f, expectedValue);
        }

        if (tag == StaticString.属性.生命)
        {
            float maxHealth = Attributes.GetBaseValue(StaticString.属性.最大生命);
            return Mathf.Clamp(expectedValue, 0f, Mathf.Max(0f, maxHealth));
        }

        if (tag == StaticString.属性.法力)
        {
            return Mathf.Max(0f, expectedValue);
        }

        return expectedValue;
    }

    private void OnAfterAttributeChange(GoveKits.Runtime.Unit.UnitTag tag, float oldValue, float newValue)
    {
        NotifyStatsChanged();
    }

    private void NotifyStatsChanged()
    {
        OnStatsChanged?.Invoke(this);
    }

    private void NotifyHandChanged()
    {
        OnHandChanged?.Invoke(this);
    }

    #region Hook Timing Effect

    public enum HookTiming
    {
        WhenStartTurn,
        WhenEndTurn,
        WhenUseCard,
        WhenHeal,
        WhenHurt,
        WhenDealDamage,
    }

    public class WhenTimingFunction
    {
        public int Times;
        public Func<UnitEffect> EffectFunc;

        public WhenTimingFunction(Func<UnitEffect> effectFunc, int times = 1)
        {
            Times = times;
            EffectFunc = effectFunc;
        }

        public void Invoke(BaseCharacter character)
        {
            if (Times > 0)
            {
                EffectFunc().Apply(character);
                Times--;
            }
        }
    }

    public Dictionary<HookTiming, List<WhenTimingFunction>> HookEffects = new();

    public void AddHookEffect(HookTiming timing, Func<UnitEffect> effect, int times = 1)
    {
        if (!HookEffects.ContainsKey(timing))
        {
            HookEffects[timing] = new List<WhenTimingFunction>();
        }
        HookEffects[timing].Add(new WhenTimingFunction(effect, times));
    }

    public void TriggerHookEffect(HookTiming timing)
    {
        if (isResolvingHookEffect || !HookEffects.ContainsKey(timing))
        {
            return;
        }

        isResolvingHookEffect = true;
        foreach (var effect in HookEffects[timing])
        {
            effect.Invoke(this);
        }
        isResolvingHookEffect = false;

        for (int i = HookEffects[timing].Count - 1; i >= 0; i--)
        {
            if (HookEffects[timing][i].Times <= 0)
            {
                HookEffects[timing].RemoveAt(i);
            }
        }
    }

    #endregion
}
