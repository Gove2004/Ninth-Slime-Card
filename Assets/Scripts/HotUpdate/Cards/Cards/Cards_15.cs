using UnityEngine;

public class 影袭 : BaseCard
{
    protected override int id => 1509;

    public override bool CanUse(BaseCharacter user, BaseCharacter target)
    {
        return base.CanUse(user, target) && user.HasPlayedAnotherCardThisTurn(this);
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 窃盾 : BaseCard
{
    protected override int id => 1510;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int stolen = Mathf.Min(Value1, target.GetShield());
        target.Attributes.ChangeBase(StaticString.属性.护盾, -stolen);
        user.GainShield(stolen);
    }
}

public class 影遁 : BaseCard
{
    protected override int id => 1511;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddTargetedImmunity(Value1);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 借命 : BaseCard
{
    protected override int id => 1512;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value1, user, target).Apply(target);
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}

public class 移祸 : BaseCard
{
    protected override int id => 1513;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () => HurtEffect.Create().Setup(Value1, user, user.Target), 1);
    }
}

public class 潜谋 : BaseCard
{
    protected override int id => 1514;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < Value1; i++)
        {
            string cardName = target.PeekRandomCardNameFromDeck();
            BaseCard card = CardFactoryCore.CreateCard(cardName);
            user.AddCardToHand(card);
        }
    }
}

public class 夜幕 : BaseCard
{
    protected override int id => 1515;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, -Value1, Value2, target).Apply(target);
    }
}

public class 侵蚀 : BaseCard
{
    protected override int id => 1516;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        target.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => ErodeManaEffect.Create().Setup(Value1, target), Value2);
    }
}
