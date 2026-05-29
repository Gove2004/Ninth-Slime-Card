using GoveKits.Runtime.Unit;
using UnityEngine;

public class 强击 : BaseCard
{
    protected override int id => 105;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 防御 : BaseCard
{
    protected override int id => 106;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 调息 : BaseCard
{
    protected override int id => 107;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 回旋斩 : BaseCard
{
    protected override int id => 108;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 预备 : BaseCard
{
    protected override int id => 109;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        user.GainShield(Value2);
    }
}

public class 破釜 : BaseCard
{
    protected override int id => 110;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 专注 : BaseCard
{
    protected override int id => 111;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value1, 1, user).Apply(user);
    }
}

public class 应急粮 : BaseCard
{
    protected override int id => 112;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 轻击 : BaseCard
{
    protected override int id => 113;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 包扎 : BaseCard
{
    protected override int id => 114;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}

public class 防备 : BaseCard
{
    protected override int id => 115;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 整顿 : BaseCard
{
    protected override int id => 116;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 挥砍 : BaseCard
{
    protected override int id => 117;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => target;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        user.GainShield(Value2);
    }
}

public class 休整 : BaseCard
{
    protected override int id => 118;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 演练 : BaseCard
{
    protected override int id => 119;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value2, 1, user).Apply(user);
    }
}

public class 刺击 : BaseCard
{
    protected override int id => 120;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (user.CardsPlayedThisTurn == 0)
        {
            damage += Value2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}
