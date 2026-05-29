using UnityEngine;

public class 狂喜 : BaseCard
{
    protected override int id => 209;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
        GainManaEffect.Create().Setup(Value1, user).Apply(user);
    }
}

public class 亵渎 : BaseCard
{
    protected override int id => 210;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.LoseAllMana();
        target.LoseAllMana();
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
    }
}

public class 压榨 : BaseCard
{
    protected override int id => 211;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int before = target.GetMana();
        target.SetMana(Mathf.Max(0, before - Value1));
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 赌命 : BaseCard
{
    protected override int id => 212;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
    }
}

public class 放纵 : BaseCard
{
    protected override int id => 213;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 枷锁 : BaseCard
{
    protected override int id => 214;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
        DrawCardsEffect.Create().Setup(Value1, target).Apply(target);
        AttackEffect.Create().Setup(Value2, user, target).Apply(target);
    }
}

public class 奢靡 : BaseCard
{
    protected override int id => 215;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int[] ids = { 201, 202, 203, 204, 205, 206, 207, 208 };
        for (int i = 0; i < Value1; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(ids[Random.Range(0, ids.Length)]);
            if (card != null)
            {
                user.AddCardToHand(card);
            }
        }
    }
}

public class 妄念 : BaseCard
{
    protected override int id => 216;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetNextCardExtraTriggers(Value1);
        HurtEffect.Create().Setup(Value2, user, user).Apply(user);
    }
}

public class 虚荣 : BaseCard
{
    protected override int id => 217;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddTargetedImmunity(Value1);
        user.GainShield(Value2);
    }
}

public class 掠夺 : BaseCard
{
    protected override int id => 218;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int stolen = Mathf.Min(Value1, target.GetMana());
        target.SetMana(target.GetMana() - stolen);
        GainManaEffect.Create().Setup(Value2, user).Apply(user);
    }
}

public class 狂噬 : BaseCard
{
    protected override int id => 219;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        BaseCard discarded = null;
        System.Collections.Generic.List<BaseCard> candidates = new System.Collections.Generic.List<BaseCard>();
        foreach (BaseCard card in user.HandCards)
        {
            if (card != this)
            {
                candidates.Add(card);
            }
        }

        if (candidates.Count > 0)
        {
            discarded = candidates[Random.Range(0, candidates.Count)];
            user.DiscardCard(discarded);
        }

        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        if (discarded != null && discarded.Series == "七罪")
        {
            GainManaEffect.Create().Setup(Value2, user).Apply(user);
        }
    }
}

public class 怒火中烧 : BaseCard
{
    protected override int id => 220;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (user.DamageTakenThisTurn > 0)
        {
            damage += Value2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 诱导 : BaseCard
{
    protected override int id => 221;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
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
            DrawCardsEffect.Create().Setup(1, user).Apply(user);
            return;
        }

        BaseCard randomCard = candidates[Random.Range(0, candidates.Count)];
        user.SetNextCardExtraTriggers(Value1);
        user.UseCard(randomCard, user.Target);
    }
}

public class 妒火 : BaseCard
{
    protected override int id => 222;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (target.GetMana() > user.GetMana())
        {
            damage += Value2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 贪杯 : BaseCard
{
    protected override int id => 223;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
        DrawCardsEffect.Create().Setup(1, user).Apply(user);
        DrawCardsEffect.Create().Setup(1, target).Apply(target);
    }
}

public class 怠惰 : BaseCard
{
    protected override int id => 224;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => CallbackEffect.Create().Setup(() =>
        {
            DrawCardsEffect.Create().Setup(Value1, user).Apply(user);
            GainManaEffect.Create().Setup(Value2, user).Apply(user);
        }), 1);
        BattleManager.Instance?.RequestEndTurn();
    }
}
