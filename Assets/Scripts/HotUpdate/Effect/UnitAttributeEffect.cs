using GoveKits.Runtime.Unit;
using UnityEngine;

public class AttackEffect : UnitEffect<AttackEffect>
{
    public int Damage { get; private set; }
    public BaseCharacter User { get; private set; }
    public BaseCharacter Target { get; private set; }

    public AttackEffect Setup(int damage, BaseCharacter user, BaseCharacter target)
    {
        Damage = damage;
        User = user;
        Target = target;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        Damage = Mathf.Max(0, Damage);
        Damage = Mathf.RoundToInt(Damage * (1 + User.Attributes.GetValue(StaticString.属性.伤害百分比提升)));
        Damage = Mathf.RoundToInt(Damage + User.Attributes.GetValue(StaticString.属性.伤害固定提升));
        Damage = Mathf.Max(0, Damage);

        User.SetEffectContext(User.CurrentResolvingCard, User, Target, Damage);
        HurtEffect.Create().Setup(Damage, User, Target).Apply(Target);
    }

    public override void OnRecycle()
    {
        Damage = 0;
        User = null;
        Target = null;
    }
}

public class HurtEffect : UnitEffect<HurtEffect>
{
    public int HurtAmount { get; private set; }
    public BaseCharacter User { get; private set; }
    public BaseCharacter Target { get; private set; }

    public HurtEffect Setup(int hurtAmount, BaseCharacter user, BaseCharacter target)
    {
        HurtAmount = hurtAmount;
        User = user;
        Target = target;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        HurtAmount = Mathf.Max(0, HurtAmount);
        HurtAmount = Mathf.RoundToInt(HurtAmount * (1 - Target.Attributes.GetValue(StaticString.属性.伤害百分比减免)));
        HurtAmount = Mathf.RoundToInt(HurtAmount - Target.Attributes.GetValue(StaticString.属性.伤害固定减免));
        HurtAmount = Mathf.Max(0, HurtAmount);

        Target.SetEffectContext(User.CurrentResolvingCard, User, Target, HurtAmount);
        Target.TakeDamage(HurtAmount);
        User.SetEffectContext(User.CurrentResolvingCard, User, Target, HurtAmount);
        User.TriggerHookEffect(BaseCharacter.HookTiming.WhenDealDamage);
    }

    public override void OnRecycle()
    {
        HurtAmount = 0;
        User = null;
        Target = null;
    }
}

public class HealEffect : UnitEffect<HealEffect>
{
    public int HealAmount { get; private set; }
    public BaseCharacter User { get; private set; }
    public BaseCharacter Target { get; private set; }

    public HealEffect Setup(int healAmount, BaseCharacter user, BaseCharacter target)
    {
        HealAmount = healAmount;
        User = user;
        Target = target;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        HealAmount = Mathf.Max(0, HealAmount);
        HealAmount = Mathf.RoundToInt(HealAmount * (1 + User.Attributes.GetValue(StaticString.属性.治疗百分比提升)));
        HealAmount = Mathf.RoundToInt(HealAmount + User.Attributes.GetValue(StaticString.属性.治疗追加));
        HealAmount = Mathf.Max(0, HealAmount);

        Target.SetEffectContext(User.CurrentResolvingCard, User, Target, HealAmount);
        Target.Heal(HealAmount);
    }

    public override void OnRecycle()
    {
        HealAmount = 0;
        User = null;
        Target = null;
    }
}

public class TemporaryAttributeEffect : UnitEffect<TemporaryAttributeEffect>
{
    public string AttributeTag { get; private set; }
    public int DeltaValue { get; private set; }
    public int DurationTurns { get; private set; }
    public BaseCharacter User { get; private set; }
    public BaseCharacter.HookTiming ExpireTiming { get; private set; }

    public TemporaryAttributeEffect Setup(string attributeTag, int deltaValue, int durationTurns, BaseCharacter user, BaseCharacter.HookTiming expireTiming = BaseCharacter.HookTiming.WhenEndTurn)
    {
        AttributeTag = attributeTag;
        DeltaValue = deltaValue;
        DurationTurns = Mathf.Max(1, durationTurns);
        User = user;
        ExpireTiming = expireTiming;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        if (User == null || string.IsNullOrEmpty(AttributeTag) || DeltaValue == 0)
        {
            return;
        }

        User.Attributes.ChangeBase(AttributeTag, DeltaValue);
        User.AddHookEffect(ExpireTiming, () => RemoveTemporaryAttributeEffect.Create().Setup(AttributeTag, DeltaValue, User), DurationTurns);
    }

    public override void OnRecycle()
    {
        AttributeTag = null;
        DeltaValue = 0;
        DurationTurns = 0;
        User = null;
        ExpireTiming = BaseCharacter.HookTiming.WhenEndTurn;
    }
}

public class RemoveTemporaryAttributeEffect : UnitEffect<RemoveTemporaryAttributeEffect>
{
    public string AttributeTag { get; private set; }
    public int DeltaValue { get; private set; }
    public BaseCharacter User { get; private set; }

    public RemoveTemporaryAttributeEffect Setup(string attributeTag, int deltaValue, BaseCharacter user)
    {
        AttributeTag = attributeTag;
        DeltaValue = deltaValue;
        User = user;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        if (User == null || string.IsNullOrEmpty(AttributeTag) || DeltaValue == 0)
        {
            return;
        }

        User.Attributes.ChangeBase(AttributeTag, -DeltaValue);
    }

    public override void OnRecycle()
    {
        AttributeTag = null;
        DeltaValue = 0;
        User = null;
    }
}

public class GainManaEffect : UnitEffect<GainManaEffect>
{
    public int ManaAmount { get; private set; }
    public BaseCharacter User { get; private set; }

    public GainManaEffect Setup(int manaAmount, BaseCharacter user)
    {
        ManaAmount = manaAmount;
        User = user;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        ManaAmount = Mathf.Max(0, ManaAmount);
        User.GainMana(ManaAmount);
    }

    public override void OnRecycle()
    {
        ManaAmount = 0;
        User = null;
    }
}

public class DrawCardsEffect : UnitEffect<DrawCardsEffect>
{
    public int DrawCount { get; private set; }
    public BaseCharacter User { get; private set; }

    public DrawCardsEffect Setup(int drawCount, BaseCharacter user)
    {
        DrawCount = drawCount;
        User = user;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        DrawCount = Mathf.Max(0, DrawCount);
        User.DrawCards(DrawCount);
    }

    public override void OnRecycle()
    {
        DrawCount = 0;
        User = null;
    }
}

public class SetManaEffect : UnitEffect<SetManaEffect>
{
    public int ManaValue { get; private set; }
    public BaseCharacter User { get; private set; }

    public SetManaEffect Setup(int manaValue, BaseCharacter user)
    {
        ManaValue = manaValue;
        User = user;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        User.SetMana(ManaValue);
    }

    public override void OnRecycle()
    {
        ManaValue = 0;
        User = null;
    }
}

public class SpendManaEffect : UnitEffect<SpendManaEffect>
{
    public int ManaCost { get; private set; }
    public BaseCharacter User { get; private set; }

    public SpendManaEffect Setup(int manaCost, BaseCharacter user)
    {
        ManaCost = manaCost;
        User = user;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        ManaCost = Mathf.Max(0, ManaCost);
        User.SpendMana(ManaCost);
    }

    public override void OnRecycle()
    {
        ManaCost = 0;
        User = null;
    }
}

