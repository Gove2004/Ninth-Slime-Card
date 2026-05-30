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
        int delay = 2;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => CallbackEffect.Create().Setup(() =>
        {
            if (delay > 0)
            {
                delay--;
                return;
            }

            HealEffect.Create().Setup(Value1, user, user).Apply(user);
        }), Value2);
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
