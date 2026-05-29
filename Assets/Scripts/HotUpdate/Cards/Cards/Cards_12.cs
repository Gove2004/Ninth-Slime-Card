using UnityEngine;

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
