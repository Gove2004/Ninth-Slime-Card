public class 反伤 : BaseCard
{
    protected override int id => 402;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Value3 > 0 ? Value3 : 1;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () =>
        {
            if (user.CurrentSourceCharacter == null || user.CurrentSourceCharacter == user)
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            return HurtEffect.Create().Setup(Value1, user, user.CurrentSourceCharacter).ApplyWrapper(user.CurrentSourceCharacter);
        }, duration);
    }
}

public class 镜像 : BaseCard
{
    protected override int id => 501;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Value3 > 0 ? Value3 : 1;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenUseCard, () =>
        {
            if (user.Target == null || user.CurrentResolvingCard == null || user.CurrentResolvingCard == this)
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            if (user.CurrentResolvingCard.Series != "科技")
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            return AttackEffect.Create().Setup(Value1, user, user.Target);
        }, duration);
    }
}

internal static class EffectCardExtensions
{
    public static DummyApplyEffect ApplyWrapper(this HurtEffect effect, BaseCharacter target)
    {
        return DummyApplyEffect.Create().Setup(() => effect.Apply(target));
    }
}

public class DummyApplyEffect : GoveKits.Runtime.Unit.UnitEffect<DummyApplyEffect>
{
    private System.Action action;

    public DummyApplyEffect Setup(System.Action value)
    {
        action = value;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        action?.Invoke();
    }

    public override void OnRecycle()
    {
        action = null;
    }
}
