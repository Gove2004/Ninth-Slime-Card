using UnityEngine;

public class 止血 : BaseCard
{
    protected override int id => 305;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        user.GainShield(Value2);
    }
}

public class 血怒 : BaseCard
{
    protected override int id => 306;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value1, 1, user).Apply(user);
    }
}

public class 猩红雨 : BaseCard
{
    protected override int id => 307;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        HealEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 血泉 : BaseCard
{
    protected override int id => 308;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () => HealEffect.Create().Setup(Value1, user, user), Value3);
    }
}

public class 血瓶 : BaseCard
{
    protected override int id => 309;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}

public class 血涌 : BaseCard
{
    protected override int id => 310;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 血债 : BaseCard
{
    protected override int id => 311;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenDealDamage, () => GainManaEffect.Create().Setup(Value1, user), Value3);
    }
}

public class 血肉铠 : BaseCard
{
    protected override int id => 312;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
        user.GainShield(Value1);
    }
}

public class 血疗 : BaseCard
{
    protected override int id => 313;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value1, user, user).Apply(user);
        HealEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 裂伤 : BaseCard
{
    protected override int id => 314;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (user.GetHealth() * 2 <= user.GetMaxHealth())
        {
            damage += Value2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 血税 : BaseCard
{
    protected override int id => 315;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value1, user, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 凝血 : BaseCard
{
    protected override int id => 316;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        user.GainShield(Value2);
    }
}

public class 血咒 : BaseCard
{
    protected override int id => 317;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () => GainManaEffect.Create().Setup(Value1, user), Mathf.Max(1, Value3));
    }
}

public class 血宴 : BaseCard
{
    protected override int id => 318;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value1, user, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
        HealEffect.Create().Setup(2, user, user).Apply(user);
    }
}

public class 血刺 : BaseCard
{
    protected override int id => 319;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        if (user.DamageTakenThisTurn > 0)
        {
            HealEffect.Create().Setup(Value2, user, user).Apply(user);
        }
    }
}

public class 血偿 : BaseCard
{
    protected override int id => 320;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        if (user.DamageTakenThisTurn > 0)
        {
            DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
        }
    }
}
