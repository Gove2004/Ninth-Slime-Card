using GoveKits.Runtime.Unit;
using UnityEngine;
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

        Target.TakeDamage(HurtAmount);
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

        Target.Heal(HealAmount);
    }

    public override void OnRecycle()
    {
        HealAmount = 0;
        User = null;
        Target = null;
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
