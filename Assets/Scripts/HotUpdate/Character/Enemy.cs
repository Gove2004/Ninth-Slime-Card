using System.Collections.Generic;
using GoveKits.Runtime.Storage;
using UnityEngine;

public class Enemy : BaseCharacter
{
    public override void Setup()
    {
        int lv = GameCore.runState?.currentLv ?? 1;
        int maxHp = 10 + (lv - 1) * 5;
        int manaRegen = 1 + lv / 3;

        Attributes.ChangeBase(StaticString.属性.最大生命, maxHp - Attributes.GetBaseValue(StaticString.属性.最大生命));
        Attributes.ChangeBase(StaticString.属性.每回合自动恢复, manaRegen - Attributes.GetBaseValue(StaticString.属性.每回合自动恢复));

        base.Setup();
    }

    protected override void BuildStarterDeck()
    {
        int lv = GameCore.runState?.currentLv ?? 1;
        int deckSize = 5 + (lv - 1);
        List<string> allowedSeries = GetAllowedSeries(lv);

        List<CardConfigData> allCards = ConfigCore.LoadAll<CardConfigData>();
        List<CardConfigData> attackPool = new();
        List<CardConfigData> fullPool = new();
        foreach (CardConfigData card in allCards)
        {
            if (card.系列 != null && allowedSeries.Contains(card.系列))
            {
                fullPool.Add(card);
                if (card.费用 <= 2 && card.数值1 >= 2)
                {
                    attackPool.Add(card);
                }
            }
        }

        int guaranteedAttacks = Mathf.Max(2, deckSize / 3);
        for (int i = 0; i < guaranteedAttacks && attackPool.Count > 0; i++)
        {
            CardConfigData config = attackPool[Random.Range(0, attackPool.Count)];
            BaseCard card = CardFactoryCore.CreateCard(config.id);
            if (card != null)
            {
                DeckCards.Add(card);
            }
        }

        int remaining = deckSize - DeckCards.Count;
        List<CardConfigData> pool = fullPool.Count > 0 ? fullPool : attackPool;
        for (int i = 0; i < remaining && pool.Count > 0; i++)
        {
            CardConfigData config = pool[Random.Range(0, pool.Count)];
            BaseCard card = CardFactoryCore.CreateCard(config.id);
            if (card != null)
            {
                DeckCards.Add(card);
            }
        }

        if (DeckCards.Count == 0)
        {
            base.BuildStarterDeck();
        }
    }

    private static List<string> GetAllowedSeries(int lv)
    {
        List<string> series = new() { "初始" };
        if (lv >= 2) series.Add("七罪");
        if (lv >= 3) series.Add("血族");
        if (lv >= 4) series.Add("坚固");
        if (lv >= 5) { series.Add("科技"); series.Add("种子"); series.Add("暗影"); series.Add("时序"); }
        return series;
    }

    public override void StartTurn()
    {
        base.StartTurn();
    }

    public override void EndTurn()
    {
        base.EndTurn();
    }

    public string TryActOnce()
    {
        if (Target == null || IsDead)
        {
            return null;
        }

        for (int i = 0; i < HandCards.Count; i++)
        {
            BaseCard card = HandCards[i];
            if (CanUseCard(card, Target))
            {
                if (UseCard(card, Target))
                {
                    return card.Name;
                }
            }
        }

        return null;
    }
}
