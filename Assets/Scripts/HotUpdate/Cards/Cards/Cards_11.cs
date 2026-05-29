public class 充能 : BaseCard
{
    protected override int id => 509;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        GainManaEffect.Create().Setup(Value1, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 校准 : BaseCard
{
    protected override int id => 510;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value1, 1, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 复写 : BaseCard
{
    protected override int id => 511;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < Value1; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(502);
            user.AddCardToHand(card);
        }
    }
}

public class 速算 : BaseCard
{
    protected override int id => 512;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value2, 1, user).Apply(user);
    }
}

public class 超频 : BaseCard
{
    protected override int id => 513;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetNextCardExtraTriggers(Value1);
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 过载 : BaseCard
{
    protected override int id => 514;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        GainManaEffect.Create().Setup(Value1, user).Apply(user);
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 检修 : BaseCard
{
    protected override int id => 515;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        user.GainShield(Value2);
    }
}

public class 算法 : BaseCard
{
    protected override int id => 516;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        if (user.HandCards.Count >= Value2)
        {
            DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        }
        else
        {
            GainManaEffect.Create().Setup(1, user).Apply(user);
        }
    }
}

public class 预装 : BaseCard
{
    protected override int id => 517;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        user.SetNextCardDamageBonus(Value2);
    }
}

public class 连发 : BaseCard
{
    protected override int id => 518;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        if (user.CardsPlayedThisTurn > 0)
        {
            AttackEffect.Create().Setup(Value2, user, target).Apply(target);
        }
    }
}

public class 备份 : BaseCard
{
    protected override int id => 519;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int count = Value1;
        if (user.TechCardsPlayedThisTurn > 0)
        {
            count += 1;
        }

        for (int i = 0; i < count; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(502);
            user.AddCardToHand(card);
        }
    }
}

public class 校验 : BaseCard
{
    protected override int id => 520;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        if (user.HandCards.Count >= Value2)
        {
            DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        }
        else
        {
            GainManaEffect.Create().Setup(1, user).Apply(user);
        }
    }
}

public class 迭代 : BaseCard
{
    protected override int id => 521;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetNextTechCardExtraTriggers(Value1);
        DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 快照 : BaseCard
{
    protected override int id => 522;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        if (user.CardsPlayedThisTurn > 0)
        {
            GainManaEffect.Create().Setup(Value2, user).Apply(user);
        }
    }
}

public class 拼装 : BaseCard
{
    protected override int id => 523;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < Value2; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(502);
            user.AddCardToHand(card);
        }
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value1, 1, user).Apply(user);
    }
}

public class 压缩 : BaseCard
{
    protected override int id => 524;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetNextCardCostReduction(Value1);
    }
}

public class 并联 : BaseCard
{
    protected override int id => 525;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetTechCardDamageBonus(Value1, Value2);
    }
}
