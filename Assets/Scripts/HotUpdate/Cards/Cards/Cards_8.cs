using UnityEngine;

public class 流血 : BaseCard
{
    protected override int id => 1801;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        target.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => HurtEffect.Create().Setup(Value1, user, target), Value2);
    }
}

public class 恢复 : BaseCard
{
    protected override int id => 1802;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => HealEffect.Create().Setup(Value1, user, user), Value2);
    }
}

public class 入魔时序 : BaseCard
{
    protected override int id => 1803;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => GainManaEffect.Create().Setup(Value1, user), Value2);
    }
}

public class 抽牌机 : BaseCard
{
    protected override int id => 1804;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => DrawCardsEffect.Create().Setup(Value1, user), Value2);
    }
}

public class 驱散 : BaseCard
{
    protected override int id => 1805;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        DispelRandomEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 加速 : BaseCard
{
    protected override int id => 1806;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int currentDamage = Value1;
        target.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => CallbackEffect.Create().Setup(() =>
        {
            AttackEffect.Create().Setup(currentDamage, user, target).Apply(target);
            currentDamage += 2;
        }), Value2);
    }
}

public class 延续 : BaseCard
{
    protected override int id => 1807;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.ExtendHookEffects(Mathf.Max(1, Value1));
    }
}

public class 结算 : BaseCard
{
    protected override int id => 1808;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < Value1; i++)
        {
            user.TriggerHookEffect(BaseCharacter.HookTiming.WhenStartTurn);
            user.TriggerHookEffect(BaseCharacter.HookTiming.WhenEndTurn);
            target.TriggerHookEffect(BaseCharacter.HookTiming.WhenStartTurn);
            target.TriggerHookEffect(BaseCharacter.HookTiming.WhenEndTurn);
        }
    }
}

public class 倒计时 : BaseCard
{
    protected override int id => 1809;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        target.AddHookEffect(BaseCharacter.HookTiming.WhenEndTurn, () => HurtEffect.Create().Setup(Value1, user, target), Value2);
    }
}

public class 余温 : BaseCard
{
    protected override int id => 1810;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.治疗追加, Value1, Value2, user).Apply(user);
    }
}

public class 余震 : BaseCard
{
    protected override int id => 1811;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定提升, Value1, Value2, user).Apply(user);
    }
}

public class 早熟 : BaseCard
{
    protected override int id => 1812;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.TriggerHookEffect(BaseCharacter.HookTiming.WhenStartTurn);
    }
}

public class 奇点 : BaseCard
{
    protected override int id => 1813;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetMana(Value1);
        target.SetMana(Value1);
    }
}

public class 传送 : BaseCard
{
    protected override int id => 1814;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        System.Collections.Generic.List<string> storedCardNames = new System.Collections.Generic.List<string>();
        foreach (BaseCard card in user.HandCards)
        {
            if (card != this)
            {
                storedCardNames.Add(card.Name);
            }
        }

        if (storedCardNames.Count == 0)
        {
            return;
        }

        int remainingDelay = 3;
        int remainingDuration = Mathf.Max(1, Value2);
        user.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => CallbackEffect.Create().Setup(() =>
        {
            if (remainingDelay > 0)
            {
                remainingDelay--;
                return;
            }

            if (remainingDuration <= 0)
            {
                return;
            }

            foreach (string cardName in storedCardNames)
            {
                for (int i = 0; i < Value1; i++)
                {
                    BaseCard copy = CardFactoryCore.CreateCard(cardName);
                    user.AddCardToHand(copy);
                }
            }

            remainingDuration--;
        }), 3 + Mathf.Max(1, Value2));
    }
}

public class 视界 : BaseCard
{
    protected override int id => 1815;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        System.Collections.Generic.List<BaseCard> originalHand = new System.Collections.Generic.List<BaseCard>(user.HandCards);
        originalHand.Remove(this);

        int replacementCount = Mathf.Min(originalHand.Count, target.DeckCards.Count);
        System.Collections.Generic.List<BaseCard> replacementHand = new System.Collections.Generic.List<BaseCard>();
        for (int i = 0; i < replacementCount; i++)
        {
            BaseCard card = CardFactoryCore.CreateCard(target.DeckCards[i].Id);
            if (card != null)
            {
                replacementHand.Add(card);
            }
        }

        user.DiscardCard(this);
        user.ReplaceHand(replacementHand);

        int remainingUses = Mathf.Max(1, Value1);
        bool restored = false;

        void RestoreHand()
        {
            if (restored)
            {
                return;
            }

            restored = true;
            user.ReplaceHand(originalHand);
        }

        user.AddHookEffect(BaseCharacter.HookTiming.WhenUseCard, () => CallbackEffect.Create().Setup(() =>
        {
            if (restored)
            {
                return;
            }

            remainingUses--;
            if (remainingUses <= 0)
            {
                RestoreHand();
            }
        }), remainingUses);

        user.AddHookEffect(BaseCharacter.HookTiming.WhenEndTurn, () => CallbackEffect.Create().Setup(RestoreHand), 1);
    }

    public override void PostUse(BaseCharacter user, BaseCharacter target)
    {
    }
}

public class 宿命 : BaseCard
{
    protected override int id => 1816;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        target.AddHookEffect(BaseCharacter.HookTiming.WhenStartTurn, () => FateEffect.Create().Setup(Value1, user, target), Value2);
    }
}
