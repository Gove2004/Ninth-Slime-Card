using System;
using System.Collections.Generic;
using GoveKits.Runtime.Unit;
using UnityEngine;

public abstract class BaseCharacter : UnitBehaviour
{
    private const int StartingHandCount = 5;

    public BaseCharacter Target;

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
        Attributes.ChangeBase(StaticString.属性.生命, -amount);
        TriggerHookEffect(HookTiming.WhenHurt);
    }

    public void Heal(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;
        Attributes.ChangeBase(StaticString.属性.生命, amount);
        TriggerHookEffect(HookTiming.WhenHeal);
    }

    public virtual bool UseCard(BaseCard card, BaseCharacter target = null)
    {
        BaseCharacter resolvedTarget = target ?? Target;
        if (!CanUseCard(card, resolvedTarget))
        {
            return false;
        }

        resolvedTarget = card.ResolveTarget(this, resolvedTarget);
        card.PreUse(this, resolvedTarget);
        card.OnUse(this, resolvedTarget);
        TriggerHookEffect(HookTiming.WhenUseCard);
        card.PostUse(this, resolvedTarget);
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
        BeforeActionTurn?.Invoke(this);
        TriggerHookEffect(HookTiming.WhenStartTurn);
        GainMana(Mathf.RoundToInt(Attributes.GetBaseValue(StaticString.属性.每回合自动恢复)));
        DrawCards(1);
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
        if (!HookEffects.ContainsKey(timing))
        {
            return;
        }

        foreach (var effect in HookEffects[timing])
        {
            effect.Invoke(this);
        }

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
