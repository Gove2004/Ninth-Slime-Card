

using GoveKits.Runtime.Unit;

public class UseCardEffect : UnitEffect<UseCardEffect>
{
    public BaseCard Card { get; private set; }
    public BaseCharacter User { get; private set; }
    public BaseCharacter Target { get; private set; }

    public UseCardEffect Setup(BaseCard card, BaseCharacter user, BaseCharacter target)
    {
        Card = card;
        User = user;
        Target = target;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        User.UseCard(Card);
    }

    public override void OnRecycle()
    {
        Card = null;
        User = null;
        Target = null;
    }
}