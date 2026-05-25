using System.Text.RegularExpressions;

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
            string fallbackName = GetType().Name;
            Name = string.IsNullOrWhiteSpace(fallbackName) ? "未知卡牌" : fallbackName;
            Cost = 0;
            Value = 0;
            Duration = 0;
            Description = "无效果";
            ImagePath = CardDatabase.GetImagePathByCardName(Name);
        }
    }
    
    public string Name { get; private set; }
    public ulong Cost { get; private set; }
    public ulong Value { get; private set; }
    public int Duration { get; private set; }
    public string Description { get; private set; }
    public string ImagePath { get; private set; }
    public BaseCharacter OwningCharacter { get; private set; }
    public bool IsStolenFromOpponent { get; private set; }
    public static ulong OverclockMultiplier { get; private set; } = 1;

    public void MultiplyNumbers(ulong multiplier)
    {
        if (multiplier == 1) return;
        Cost = BaseCharacter.SaturatingMultiply(Cost, multiplier);
        Value = BaseCharacter.SaturatingMultiply(Value, multiplier);
    }

    public void SetCost(ulong cost)
    {
        Cost = cost;
    }

    public void SetValue(ulong value)
    {
        Value = value;
    }

    public void SetDuration(int duration)
    {
        Duration = duration;
    }

    public void AddCost(ulong amount)
    {
        Cost = BaseCharacter.SaturatingAdd(Cost, amount);
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

    public void SetOwningCharacter(BaseCharacter character)
    {
        OwningCharacter = character;
    }

    public static void ResetOverclock()
    {
        OverclockMultiplier = 1;
        foreach (var card in CardFactory.GetAllCards())
        {
            card.ReloadCardData();
        }
    }

    public static void ApplyOverclock(ulong factor)
    {
        if (factor == 1) return;
        OverclockMultiplier = BaseCharacter.SaturatingMultiply(OverclockMultiplier, factor);
        foreach (var card in CardFactory.GetAllCards())
        {
            card.MultiplyNumbers(factor);
        }
    }

    public virtual string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        string result = Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值/1+数值]", $"{Value}/{BaseCharacter.SaturatingAdd(Value, 1)}")
            .Replace("[数值]", Value.ToString())
            .Replace("[持续时间]", Duration.ToString());

        result = Regex.Replace(result, @"\[数值[×xX\*](\d+)\]", match =>
        {
            if (!ulong.TryParse(match.Groups[1].Value, out ulong factor))
            {
                return match.Value;
            }
            return BaseCharacter.SaturatingMultiply(Value, factor).ToString();
        });

        result = Regex.Replace(result, @"\[数值/(\d+)\]", match =>
        {
            if (!ulong.TryParse(match.Groups[1].Value, out ulong divisor) || divisor == 0)
            {
                return match.Value;
            }
            return (Value / divisor).ToString();
        });

        return result;
    }

    public virtual string GetDisplayName()
    {
        return Name;
    }

    public virtual ulong GetDisplayCost()
    {
        return Cost;
    }

    public virtual string GetDisplayImagePath()
    {
        return ImagePath;
    }

    public virtual bool IsMirageCard => false;

    /// <summary>
    /// 使用卡牌的效果
    /// </summary>
    /// <param name="user">使用卡牌的角色。</param>
    /// <param name="target">卡牌的目标角色。</param>
    public abstract void Execute(BaseCharacter user, BaseCharacter target);
}
