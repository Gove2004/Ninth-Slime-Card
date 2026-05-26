

public class 普通攻击 : BaseCard
{
    protected override int id => 101;

    public override void Use(BaseCharacter user, BaseCharacter target)
    {
        target.Health.Change(x => x - Value1); // 造成Value1点伤害
    }
}


public class 治疗术 : BaseCard
{
    protected override int id => 102;

    public override void Use(BaseCharacter user, BaseCharacter target)
    {
        // 治疗术的逻辑，比如恢复生命等
        target.Health.Change(x => x + Value1); // 恢复Value1点生命
    }
}



