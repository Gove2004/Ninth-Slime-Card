public class 盾骰 : BaseCard
{
    protected override int id => 605;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int value = UnityEngine.Random.Range(0, Value1 + 1);
        user.GainShield(value);
    }
}
