using System;
using System.Collections.Generic;
using GoveKits.Runtime.Core;

public static class CardFactoryCore
{
    private static readonly Dictionary<int, Type> cardTypesById = new Dictionary<int, Type>();
    private static readonly Dictionary<string, int> cardIdsByName = new Dictionary<string, int>();

    static CardFactoryCore()
    {
        var allTypes = typeof(BaseCard).Assembly.GetTypes();
        foreach (var type in allTypes)
        {
            if (!type.IsSubclassOf(typeof(BaseCard)) || type.IsAbstract)
            {
                continue;
            }

            var card = (BaseCard)Activator.CreateInstance(type);
            cardTypesById[card.Id] = type;
            cardIdsByName[card.Name] = card.Id;
        }

        LogCore.Success("CardFactory", $"已加载 {cardTypesById.Count} 张卡牌");
    }

    public static BaseCard CreateCard(int id)
    {
        if (!cardTypesById.TryGetValue(id, out var type))
        {
            LogCore.Error("CardFactory", $"未找到id为{id}的卡牌类型");
            return null;
        }

        return (BaseCard)Activator.CreateInstance(type);
    }

    public static BaseCard CreateCard(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return cardIdsByName.TryGetValue(name, out var id) ? CreateCard(id) : null;
    }
}
