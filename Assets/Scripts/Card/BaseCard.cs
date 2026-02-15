

/// <summary>
/// 卡牌的基类, 定义了卡牌的基本属性和方法。
/// </summary>
public abstract class BaseCard
{
    protected abstract int id { get; }


    public BaseCard()
    {
        ReloadCardData();
    }
    
    protected void ReloadCardData()
    {
        // 从数据库获取卡牌数据
        var data = CardDatabase.GetCardData(id);
        
        if (data != null)
        {
            Name = data.name;
            Cost = data.cost;
            Value = data.value;
            Duration = data.duration;
            Description = data.effect;
            ImagePath = data.imagePath;
        }
        else
        {
            // 设置默认值
            Name = "未知卡牌";
            Cost = 0;
            Value = 0;
            Duration = 0;
            Description = "无效果";
            ImagePath = "卡牌/default";
        }
    }
    
    public string Name { get; private set; }
    public int Cost { get; private set; }
    public int Value { get; private set; }
    public int Duration { get; private set; }
    public string Description { get; private set; }
    public string ImagePath { get; private set; }
    public bool IsStolenFromOpponent { get; private set; }
    public static int OverclockMultiplier { get; private set; } = 1;

    public void MultiplyNumbers(int multiplier)
    {
        if (multiplier == 1) return;
        Cost *= multiplier;
        Value *= multiplier;
    }

    public void AddDuration(int amount)
    {
        if (amount == 0) return;
        Duration += amount;
    }

    public void MarkStolenFromOpponent()
    {
        IsStolenFromOpponent = true;
    }

    public static void ResetOverclock()
    {
        OverclockMultiplier = 1;
        foreach (var card in CardFactory.GetAllCards())
        {
            card.ReloadCardData();
        }
    }

    public static void ApplyOverclock(int factor)
    {
        if (factor == 1) return;
        OverclockMultiplier *= factor;
        foreach (var card in CardFactory.GetAllCards())
        {
            card.MultiplyNumbers(factor);
        }
    }

    public virtual string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        return Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值×20]", (Value * 20).ToString())
            .Replace("[数值×2]", (Value * 2).ToString())
            .Replace("[数值/10]", (Value / 10).ToString())
            .Replace("[数值]", Value.ToString())
            .Replace("[持续时间]", Duration.ToString());
    }

    /// <summary>
    /// 使用卡牌的效果
    /// </summary>
    /// <param name="user">使用卡牌的角色。</param>
    /// <param name="target">卡牌的目标角色。</param>
    public abstract void Execute(BaseCharacter user, BaseCharacter target);
}
