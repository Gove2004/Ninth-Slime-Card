public class Enemy : BaseCharacter
{
    public override void StartTurn()
    {
        base.StartTurn();
    }

    public override void EndTurn()
    {
        base.EndTurn();
    }

    public bool TryAct()
    {
        if (Target == null || IsDead)
        {
            return false;
        }

        for (int i = 0; i < HandCards.Count; i++)
        {
            BaseCard card = HandCards[i];
            if (CanUseCard(card, Target))
            {
                return UseCard(card, Target);
            }
        }

        return false;
    }
}
