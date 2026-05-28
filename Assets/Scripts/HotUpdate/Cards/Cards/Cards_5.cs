public class 磐岩 : BaseCard
{
    protected override int id => 404;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 破甲 : BaseCard
{
    protected override int id => 405;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (target.GetShield() > 0)
        {
            damage *= 2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 激光 : BaseCard
{
    protected override int id => 503;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        RuntimeCost = GetCurrentCost() + 1;
        Value1 *= 2;
    }
}

public class 重奏 : BaseCard
{
    protected override int id => 507;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.SetNextCardExtraTriggers(Value1);
    }
}
