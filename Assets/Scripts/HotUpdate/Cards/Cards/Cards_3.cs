public class 血契 : BaseCard
{
    protected override int id => 303;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Value3 > 0 ? Value3 : 1;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHeal, () =>
        {
            if (user.Target == null || user.CurrentEffectAmount <= 0)
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            return AttackEffect.Create().Setup(Value1, user, user.Target);
        }, duration);
    }
}

public class 鲜血 : BaseCard
{
    protected override int id => 304;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Value3 > 0 ? Value3 : 1;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenDealDamage, () =>
        {
            if (user.CurrentEffectAmount <= 0)
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            return HealEffect.Create().Setup(Value1, user, user);
        }, duration);
    }
}
