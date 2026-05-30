using UnityEngine;

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

public class 盾骰 : BaseCard
{
    protected override int id => 605;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = UnityEngine.Random.Range(0, Value1 + 1);
        user.GainShield(value);
    }
}

public class 骰骰 : BaseCard
{
    protected override int id => 606;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int[] ids = { 601, 602, 603, 604, 605, 606 };
        int count = Random.Range(0, Value1 + 1);
        for (int i = 0; i < count; i++)
        {
            int randomId = ids[Random.Range(0, ids.Length)];
            BaseCard card = CardFactoryCore.CreateCard(randomId);
            user.AddCardToHand(card);
        }
    }
}

public class 愈种 : BaseCard
{
    protected override int id => 607;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        user.GainShield(Value2);
    }
}

public class 爆种 : BaseCard
{
    protected override int id => 608;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        AttackEffect.Create().Setup(value, user, target).Apply(target);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 蓝种 : BaseCard
{
    protected override int id => 609;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        GainManaEffect.Create().Setup(value, user).Apply(user);
    }
}

public class 甲种 : BaseCard
{
    protected override int id => 610;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = Random.Range(0, Value1 + 1);
        user.GainShield(value);
    }
}

public class 连种 : BaseCard
{
    protected override int id => 611;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int[] ids = { 601, 602, 603, 604, 605, 606, 607, 608, 609, 610, 611, 612, 613, 614 };
        for (int i = 0; i < Value1; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(ids[Random.Range(0, ids.Length)]);
            user.AddCardToHand(card);
        }
    }
}

public class 丰收 : BaseCard
{
    protected override int id => 612;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        BaseCard card = CardFactoryCore.CreateCard(601 + Random.Range(0, 6));
        user.AddCardToHand(card);
    }
}

public class 混沌芽 : BaseCard
{
    protected override int id => 613;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int option = Random.Range(0, 4);
        switch (option)
        {
            case 0:
                AttackEffect.Create().Setup(Value1, user, target).Apply(target);
                break;
            case 1:
                HealEffect.Create().Setup(Value1, user, user).Apply(user);
                break;
            case 2:
                GainManaEffect.Create().Setup(Value1, user).Apply(user);
                break;
            default:
                user.GainShield(Value1);
                break;
        }
    }
}

public class 发芽 : BaseCard
{
    protected override int id => 614;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => DrawCardsEffect.Create().Setup(Value1, user), Value2);
    }
}

public class 生芽 : BaseCard
{
    protected override int id => 615;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int option = Random.Range(0, 3);
        switch (option)
        {
            case 0: HealEffect.Create().Setup(1, user, user).Apply(user); break;
            case 1: GainManaEffect.Create().Setup(1, user).Apply(user); break;
            default: user.GainShield(1); break;
        }
    }
}

public class 变种 : BaseCard
{
    protected override int id => 616;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Random.Range(0, Value1 + 1);
        int heal = Random.Range(0, Value2 + 1);
        AttackEffect.Create().Setup(damage, user, target).Apply(target);
        HealEffect.Create().Setup(heal, user, user).Apply(user);
    }
}

public class 蔓延 : BaseCard
{
    protected override int id => 617;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int[] ids = { 601, 602, 603, 604, 605, 606, 607, 608, 609, 610, 611, 612, 613, 614, 615, 616, 617, 618, 619 };
        int count = Value1;
        bool hasSeed = false;
        foreach (BaseCard card in user.HandCards)
        {
            if (card != this && card.Series == "种子")
            {
                hasSeed = true;
                break;
            }
        }
        if (hasSeed)
        {
            count += 1;
        }
        for (int i = 0; i < count; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(ids[Random.Range(0, ids.Length)]);
            user.AddCardToHand(card);
        }
    }
}

public class 成熟 : BaseCard
{
    protected override int id => 618;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => DrawCardsEffect.Create().Setup(Value1, user), 1);
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => GainManaEffect.Create().Setup(Value2, user), 1);
    }
}

public class 异变 : BaseCard
{
    protected override int id => 619;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int option = Random.Range(0, 4);
        switch (option)
        {
            case 0: AttackEffect.Create().Setup(Value1, user, target).Apply(target); break;
            case 1: HealEffect.Create().Setup(Value1, user, user).Apply(user); break;
            case 2: GainManaEffect.Create().Setup(Value2, user).Apply(user); break;
            default: user.GainShield(Value1); break;
        }
    }
}
