


using System.Collections.Generic;
using GoveKits.Runtime.Core;

public static class CardFactoryCore
{
    private static Dictionary<string, BaseCard> _cardDict = new Dictionary<string, BaseCard>();

    // 扫描所有继承自BaseCard的类，并创建实例
    static CardFactoryCore()
    {
        var cardTypes = typeof(BaseCard).Assembly.GetTypes();
        foreach (var type in cardTypes)
        {
            if (type.IsSubclassOf(typeof(BaseCard)) && !type.IsAbstract)
            {
                var cardInstance = (BaseCard)System.Activator.CreateInstance(type);
                _cardDict[cardInstance.Name] = cardInstance;
            }
        }

        LogCore.Success("CardFactory", $"已加载 {_cardDict.Count} 张卡牌");
    }
}