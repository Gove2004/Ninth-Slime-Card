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
