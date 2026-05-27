using GoveKits.Runtime.Unit;

public class 普通攻击 : BaseCard
{
    protected override int id => 101;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 治疗术 : BaseCard
{
    protected override int id => 102;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        HealEffect.Create().Setup(Value1, user, user).Apply(user);
    }
}
