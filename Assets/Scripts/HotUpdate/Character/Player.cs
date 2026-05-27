public class Player : BaseCharacter
{
    public const int HandLimit = 8;

    public bool isPlayerTurn { get; private set; }

    public override void StartTurn()
    {
        isPlayerTurn = true;
        base.StartTurn();
    }

    public override void EndTurn()
    {
        isPlayerTurn = false;
        base.EndTurn();
    }

    public override void DrawCards(int count)
    {
        int available = HandLimit - HandCards.Count;
        if (available <= 0)
        {
            return;
        }

        base.DrawCards(UnityEngine.Mathf.Min(count, available));
    }
}
