using System.Collections.Generic;
using UnityEngine;

public static class CardFactory
{
    private static readonly Dictionary<string, System.Func<BaseCard>> cardFactories = new()
    {
        { nameof(流血), () => new 流血() },
        { nameof(恢复), () => new 恢复() },
        { nameof(入魔), () => new 入魔() },
        { nameof(抽牌), () => new 抽牌() },
        { nameof(驱散), () => new 驱散() },
        { nameof(吸血), () => new 吸血() },
        { nameof(加速), () => new 加速() },
        { nameof(延续), () => new 延续() },
        { nameof(超频), () => new 超频() },
        { nameof(结算), () => new 结算() },
        { nameof(急救), () => new 急救() },
        { nameof(闪击), () => new 闪击() },
        { nameof(重奏), () => new 重奏() },
        { nameof(七罪), () => new 七罪() },
        { nameof(暴怒), () => new 暴怒() },
        { nameof(傲慢), () => new 傲慢() },
        { nameof(嫉妒), () => new 嫉妒() },
        { nameof(贪婪), () => new 贪婪() },
        { nameof(懒惰), () => new 懒惰() },
        { nameof(色欲), () => new 色欲() },
        { nameof(暴食), () => new 暴食() },
        { nameof(种子), () => new 种子() },
        { nameof(骰子), () => new 骰子() },
        { nameof(命签), () => new 命签() },
        { nameof(运势), () => new 运势() },
        { nameof(赌徒), () => new 赌徒() },
        { nameof(轮盘), () => new 轮盘() },
        { nameof(苦修), () => new 苦修() },
        { nameof(献祭), () => new 献祭() },
        { nameof(卖血), () => new 卖血() },
        { nameof(反伤), () => new 反伤() },
        { nameof(血契), () => new 血契() },
        { nameof(偷窃), () => new 偷窃() },
        { nameof(偷魔), () => new 偷魔() },
        { nameof(偷月), () => new 偷月() },
        { nameof(未来), () => new 未来() },
        { nameof(破甲), () => new 破甲() },
        { nameof(羽化), () => new 羽化() },
        { nameof(制衡), () => new 制衡() },
        { nameof(诅咒), () => new 诅咒() },
        { nameof(镜像), () => new 镜像() },
        { nameof(激光), () => new 激光() },
        { nameof(视界), () => new 视界() },
        { nameof(奇点), () => new 奇点() },
        { nameof(传送), () => new 传送() }
    };
    private static readonly List<string> allCardKeys = new(cardFactories.Keys);
    private static readonly string[] initialPlayerDeckKeys =
    {
        nameof(流血), nameof(流血), nameof(流血),
        nameof(恢复), nameof(恢复), nameof(恢复),
        nameof(入魔), nameof(入魔),
        nameof(抽牌), nameof(抽牌)
    };
    private static readonly string[] initialEnemyDeckKeys =
    {
        nameof(流血),
        nameof(入魔)
    };
    private static readonly string[] resetEnemyDeckKeys =
    {
        nameof(流血),
        nameof(恢复),
        nameof(抽牌),
        nameof(入魔)
    };
    private static readonly List<BaseCard> allCards = CreateAllCards();
    private static List<BaseCard> playerDeck = CreateDeck(initialPlayerDeckKeys);
    private static List<BaseCard> enemyDeck = CreateDeck(initialEnemyDeckKeys);

    private static BaseCard CreateCardInstance(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return null;
        return cardFactories.TryGetValue(cardName, out var factory) ? factory() : null;
    }

    private static List<BaseCard> CreateAllCards()
    {
        List<BaseCard> cards = new(cardFactories.Count);
        foreach (string cardKey in allCardKeys)
        {
            BaseCard card = CreateCardInstance(cardKey);
            if (card != null) cards.Add(card);
        }
        return cards;
    }

    private static List<BaseCard> CreateDeck(IReadOnlyList<string> cardKeys)
    {
        List<BaseCard> deck = new(cardKeys.Count);
        for (int i = 0; i < cardKeys.Count; i++)
        {
            BaseCard card = CreateCardInstance(cardKeys[i]);
            if (card != null) deck.Add(card);
        }
        return deck;
    }


    /// <summary>
    /// 获取指定名称的卡牌
    /// </summary>
    /// <param name="cardName">卡牌名称</param>
    /// <returns></returns>
    public static BaseCard GetThisCard(string cardName)
    {
        return CreateCardInstance(cardName);
    }

    public static List<BaseCard> GetAllCards() => allCards;

    /// <summary>
    /// 随机获取一张卡牌
    /// </summary>
    /// <returns></returns>
    public static BaseCard GetRandomCard()
    {
        if (allCardKeys.Count == 0) return null;
        int index = Random.Range(0, allCardKeys.Count);
        return CreateCardInstance(allCardKeys[index]);
    }

    private static HashSet<string> GetEnemyBannedCards()
    {
        return new HashSet<string>
        {
            "偷窃", "偷月", "偷魔",
            "反伤", "傲慢", "嫉妒", "视界"
        };
    }

    /// <summary>
    /// 随机获取一张敌人可用的卡牌（排除偷窃、无敌与高风险特殊牌）
    /// </summary>
    /// <returns></returns>
    public static BaseCard GetRandomEnemyCard()
    {
        if (allCards.Count == 0) return null;
        
        var bannedCards = GetEnemyBannedCards();
        var validCardKeys = new List<string>();
        foreach (string cardKey in allCardKeys)
        {
            if (!bannedCards.Contains(cardKey))
            {
                validCardKeys.Add(cardKey);
            }
        }
        
        if (validCardKeys.Count == 0) return null;

        int index = Random.Range(0, validCardKeys.Count);
        return CreateCardInstance(validCardKeys[index]);
    }
    // 从中抽取一张
    public static BaseCard DrawCardFromPlayerDeck()
    {
        if (playerDeck.Count == 0) return null;
        int index = Random.Range(0, playerDeck.Count);
        var card = playerDeck[index];
        return CreateCardInstance(card.Name);
    }

    // 用一张卡牌替换其中一张
    public static void ReplaceCardInPlayerDeck(BaseCard newCard, int index)
    {
        if (index < 0 || index >= playerDeck.Count) return;

        playerDeck[index] = newCard;
    }

    /// <summary>
    /// 重置玩家牌组为初始状态
    /// </summary>
    public static void ResetPlayerDeck()
    {
        playerDeck = CreateDeck(initialPlayerDeckKeys);
    }

    // 获取玩家牌组
    public static List<BaseCard> GetPlayerDeck() => playerDeck;

    public static void ResetEnemyDeck()
    {
        enemyDeck = CreateDeck(resetEnemyDeckKeys);
    }

    public static BaseCard DrawCardFromEnemyDeck()
    {
        if (enemyDeck.Count == 0) return null;
        int index = Random.Range(0, enemyDeck.Count);
        var card = enemyDeck[index];
        return CreateCardInstance(card.Name);
    }

    public static List<BaseCard> GetDeckSnapshot(BaseCharacter character)
    {
        List<BaseCard> sourceDeck = ResolveDeck(character);
        List<BaseCard> result = new List<BaseCard>(sourceDeck.Count);
        foreach (var card in sourceDeck)
        {
            if (card == null) continue;
            BaseCard clone = CreateCardInstance(card.Name);
            if (clone != null) result.Add(clone);
        }
        return result;
    }

    private static List<BaseCard> ResolveDeck(BaseCharacter character)
    {
        if (character is Player) return playerDeck;
        if (character is EnemyBoss) return enemyDeck;
        if (BattleManager.Instance != null)
        {
            if (ReferenceEquals(character, BattleManager.Instance.player)) return playerDeck;
            if (ReferenceEquals(character, BattleManager.Instance.enemy)) return enemyDeck;
        }
        return playerDeck;
    }

    public static void AddRandomCardToEnemyDeck()
    {
        // 根据难度等级添加更多卡牌
        for (int i = 0; i < GameManager.Instance.difficultyLevel; i++)
        {
            var card = GetRandomEnemyCard();
            if (card == null) return;
            enemyDeck.Add(card);
        }
    }

}
