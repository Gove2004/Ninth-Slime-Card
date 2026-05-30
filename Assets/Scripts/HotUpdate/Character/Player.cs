using System.Collections.Generic;

public class Player : BaseCharacter
{
    public const int HandLimit = 8;

    public bool isPlayerTurn { get; private set; }

    public override void Setup()
    {
        int lv = GameCore.runState?.currentLv ?? 1;
        int maxHp = 10 + (lv - 1);
        float currentMax = Attributes.GetBaseValue(StaticString.属性.最大生命);
        if (maxHp != currentMax)
        {
            Attributes.ChangeBase(StaticString.属性.最大生命, maxHp - currentMax);
        }

        base.Setup();
    }

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

    protected override void BuildStarterDeck()
    {
        if (GameCore.runState == null)
        {
            base.BuildStarterDeck();
            return;
        }

        GameCore.runState.EnsureStarterDeck();
        foreach (int id in GameCore.runState.playerDeckIds)
        {
            BaseCard card = CardFactoryCore.CreateCard(id);
            if (card != null)
            {
                DeckCards.Add(card);
            }
        }
    }

    public void SaveCurrentDeck()
    {
        if (GameCore.runState == null)
        {
            return;
        }

        GameCore.runState.playerDeckIds = new List<int>();
        foreach (BaseCard card in DeckCards)
        {
            GameCore.runState.playerDeckIds.Add(card.Id);
        }

        foreach (BaseCard card in HandCards)
        {
            GameCore.runState.playerDeckIds.Add(card.Id);
        }

        foreach (BaseCard card in DiscardCards)
        {
            GameCore.runState.playerDeckIds.Add(card.Id);
        }
    }
}
