using UnityEngine;

public class 偷窃 : BaseCard
{
    protected override int id => 1501;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => StealRandomCardEffect.Create().Setup(Value1, user, target), Value2);
    }
}

public class 偷魔 : BaseCard
{
    protected override int id => 1502;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => StealManaEffect.Create().Setup(Value1, user, target), Value2);
    }
}

public class 偷月 : BaseCard
{
    protected override int id => 1503;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => DispelRandomEffect.Create().Setup(Value1, user, target), Value2);
    }
}

public class 未来 : BaseCard
{
    protected override int id => 1504;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => DelayedHealEffect.Create().Setup(Value1, user, 2), Value2 + 2);
    }
}

public class 破甲暗影 : BaseCard
{
    protected override int id => 1505;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害百分比减免, -Value1, Value2, target).Apply(target);
    }
}

public class 羽化 : BaseCard
{
    protected override int id => 1506;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int count = user.HandCards.Count - 1;
        user.ConsumeAllHandCardsToDiscard(false);
        GainManaEffect.Create().Setup(count, user).Apply(user);
        HealEffect.Create().Setup(count, user, user).Apply(user);
    }

    public override void PostUse(BaseCharacter user, BaseCharacter target)
    {
    }
}

public class 制衡 : BaseCard
{
    protected override int id => 1507;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int count = user.HandCards.Count - 1;
        user.ConsumeAllHandCardsToDiscard(false);
        DrawCardsEffect.Create().Setup(count, user).Apply(user);
    }

    public override void PostUse(BaseCharacter user, BaseCharacter target)
    {
    }
}

public class 诅咒 : BaseCard
{
    protected override int id => 1508;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        target.AddHookEffect(BaseCharacter.HookTiming.WhenUseCard, () => HurtEffect.Create().Setup(Value1, user, target), Value2);
    }
}
