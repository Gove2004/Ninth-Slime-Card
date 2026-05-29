using UnityEngine;

public class 入魔 : BaseCard
{
    protected override int id => 104;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenEndTurn, () => GainManaEffect.Create().Setup(Value1, user), Mathf.Max(1, Value3));
    }
}

public class 罪罚 : BaseCard
{
    protected override int id => 201;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int[] ids = { 201, 202, 203, 204, 205, 206, 207, 208 };
        for (int i = 0; i < Value1; i++)
        {
            int randomId = ids[Random.Range(0, ids.Length)];
            BaseCard card = CardFactoryCore.CreateCard(randomId);
            if (card != null)
            {
                card.RuntimeCost = 0;
                user.AddCardToHand(card);
            }
        }
    }
}

public class 暴怒 : BaseCard
{
    protected override int id => 202;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 嫉妒 : BaseCard
{
    protected override int id => 204;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int enemyMana = target.GetMana();
        user.SetMana(enemyMana);
    }
}

public class 贪婪 : BaseCard
{
    protected override int id => 205;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value1, target).Apply(target);
    }
}

public class 懒惰 : BaseCard
{
    protected override int id => 206;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        GainManaEffect.Create().Setup(Value1, user).Apply(user);
        BattleManager.Instance?.RequestEndTurn();
    }
}

public class 硬化 : BaseCard
{
    protected override int id => 401;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Mathf.Max(1, Value3 == 0 ? 1 : Value3);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value1, duration, user).Apply(user);
    }
}

public class 钻石 : BaseCard
{
    protected override int id => 403;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害百分比提升, 1, 1, user).Apply(user);
    }
}

public class 电光 : BaseCard
{
    protected override int id => 506;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < Value1; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(502);
            user.AddCardToHand(card);
        }
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

public class 傲慢 : BaseCard
{
    protected override int id => 203;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddTargetedImmunity(Value1);
    }
}

public class 色欲 : BaseCard
{
    protected override int id => 207;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        if (user.HandCards.Count <= 1)
        {
            return;
        }

        System.Collections.Generic.List<BaseCard> candidates = new System.Collections.Generic.List<BaseCard>();
        foreach (BaseCard card in user.HandCards)
        {
            if (card != this)
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        int index = Random.Range(0, candidates.Count);
        BaseCard randomCard = candidates[index];
        user.SetNextCardExtraTriggers(Value1);
        user.UseCard(randomCard, user.Target);
    }
}

public class 暴食 : BaseCard
{
    protected override int id => 208;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int consumedCount = user.HandCards.Count;
        user.ConsumeAllHandCardsToDiscard(true);
        HealEffect.Create().Setup(consumedCount * Value2, user, user).Apply(user);
    }

    public override void PostUse(BaseCharacter user, BaseCharacter target)
    {
    }
}
