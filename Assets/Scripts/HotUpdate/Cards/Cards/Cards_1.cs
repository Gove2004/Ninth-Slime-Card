using GoveKits.Runtime.Unit;
using UnityEngine;

public class 普攻 : BaseCard
{
    protected override int id => 101;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 治疗 : BaseCard
{
    protected override int id => 102;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}

public class 抽牌 : BaseCard
{
    protected override int id => 103;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
    }
}

public class 吸血 : BaseCard
{
    protected override int id => 301;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        HealEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 献祭 : BaseCard
{
    protected override int id => 302;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value1, user, user).Apply(user);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 一闪 : BaseCard
{
    protected override int id => 502;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 末法 : BaseCard
{
    protected override int id => 504;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        SetManaEffect.Create().Setup(0, user).Apply(user);
        SetManaEffect.Create().Setup(0, target).Apply(target);
    }
}

public class 原子 : BaseCard
{
    protected override int id => 505;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(user.HandCards.Count, user, user).Apply(user);
    }
}

public class 急救 : BaseCard
{
    protected override int id => 508;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        GainManaEffect.Create().Setup(Value1, user).Apply(user);
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}

public class 攻骰 : BaseCard
{
    protected override int id => 601;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        AttackEffect.Create().Setup(value, user, target).Apply(target);
    }
}

public class 血骰 : BaseCard
{
    protected override int id => 602;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        HealEffect.Create().Setup(value, user, user).Apply(user);
    }
}

public class 魔骰 : BaseCard
{
    protected override int id => 603;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        GainManaEffect.Create().Setup(value, user).Apply(user);
    }
}

public class 牌骰 : BaseCard
{
    protected override int id => 604;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        DrawCardsEffect.Create().Setup(value, user).Apply(user);
    }
}
