using System.Text.RegularExpressions;
using System.Collections.Generic;

/// <summary>
/// 卡牌的基类, 定义了卡牌的基本属性和方法。
/// </summary>
public abstract class BaseCard
{
    protected abstract int id { get; }
    private static readonly Regex ValueOverOneRegex = new(@"\[数值(?<index>\d*)/1\+数值(?<index2>\d*)\]", RegexOptions.Compiled);
    private static readonly Regex ValueMultiplyRegex = new(@"\[数值(?<index>\d*)[×xX\*](?<factor>\d+)\]", RegexOptions.Compiled);
    private static readonly Regex ValueDivideRegex = new(@"\[数值(?<index>\d*)/(?<divisor>\d+)\]", RegexOptions.Compiled);
    private static readonly Regex ValueRegex = new(@"\[数值(?<index>\d*)\]", RegexOptions.Compiled);
    private readonly List<ulong> values = new();
    private string cachedDynamicDescription;
    private bool isDynamicDescriptionDirty = true;


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
            SetValues(data.values);
            Duration = data.duration;
            Description = data.effect;
            ImagePath = data.imagePath;
        }
        else
        {
            string fallbackName = GetType().Name;
            Name = string.IsNullOrWhiteSpace(fallbackName) ? "未知卡牌" : fallbackName;
            Cost = 0;
            SetValues((IEnumerable<ulong>)null);
            Duration = 0;
            Description = "无效果";
            ImagePath = CardDatabase.GetImagePathByCardName(Name);
        }

        InvalidateCachedPresentation();
    }
    
    public string Name { get; private set; }
    public ulong Cost { get; private set; }
    public ulong Value { get; private set; }
    public IReadOnlyList<ulong> Values => values;
    public int Duration { get; private set; }
    public string Description { get; private set; }
    public string ImagePath { get; private set; }
    public BaseCharacter OwningCharacter { get; private set; }
    public bool IsStolenFromOpponent { get; private set; }
    public bool PlayerAcquisitionMutationResolved { get; private set; }
    public static ulong OverclockMultiplier { get; private set; } = 1;

    public void MultiplyNumbers(ulong multiplier)
    {
        ScaleNumbers(multiplier, 1UL);
    }

    public void ScaleNumbers(ulong numerator, ulong denominator)
    {
        if (denominator == 0UL) return;
        if (numerator == denominator) return;

        Cost = ScaleValue(Cost, numerator, denominator);
        for (int i = 0; i < values.Count; i++)
        {
            values[i] = ScaleValue(values[i], numerator, denominator);
        }
        SyncPrimaryValue();
        InvalidateCachedPresentation();
    }

    public void SetCost(ulong cost)
    {
        Cost = cost;
        InvalidateCachedPresentation();
    }

    public void SetValue(ulong value)
    {
        EnsureValueSlot(0);
        values[0] = value;
        SyncPrimaryValue();
    }

    public void SetValues(IEnumerable<ulong> sourceValues)
    {
        values.Clear();
        if (sourceValues != null)
        {
            foreach (ulong value in sourceValues)
            {
                values.Add(value);
            }
        }
        SyncPrimaryValue();
    }

    public void SetValueAt(int index, ulong value)
    {
        if (index < 0) return;
        EnsureValueSlot(index);
        values[index] = value;
        SyncPrimaryValue();
    }

    public ulong GetValueAt(int index)
    {
        if (index < 0 || index >= values.Count) return 0UL;
        return values[index];
    }

    public void SetDuration(int duration)
    {
        Duration = duration;
        InvalidateCachedPresentation();
    }

    public void AddCost(ulong amount)
    {
        Cost = BaseCharacter.SaturatingAdd(Cost, amount);
        InvalidateCachedPresentation();
    }

    public void AddDuration(int amount)
    {
        if (amount == 0) return;
        Duration += amount;
        InvalidateCachedPresentation();
    }

    public void MarkStolenFromOpponent()
    {
        IsStolenFromOpponent = true;
    }

    public void MarkPlayerAcquisitionMutationResolved()
    {
        PlayerAcquisitionMutationResolved = true;
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
        if (!isDynamicDescriptionDirty)
        {
            return cachedDynamicDescription;
        }

        if (string.IsNullOrEmpty(Description))
        {
            cachedDynamicDescription = Description;
            isDynamicDescriptionDirty = false;
            return cachedDynamicDescription;
        }

        string result = Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[持续时间]", Duration.ToString());

        result = ValueOverOneRegex.Replace(result, match =>
        {
            int leftIndex = ParseValueIndex(match.Groups["index"].Value);
            int rightIndex = ParseValueIndex(match.Groups["index2"].Value);
            if (leftIndex != rightIndex)
            {
                return match.Value;
            }
            ulong value = GetValueAt(leftIndex);
            return $"{value}/{BaseCharacter.SaturatingAdd(value, 1UL)}";
        });

        result = ValueMultiplyRegex.Replace(result, match =>
        {
            if (!ulong.TryParse(match.Groups["factor"].Value, out ulong factor))
            {
                return match.Value;
            }
            ulong value = GetValueAt(ParseValueIndex(match.Groups["index"].Value));
            return BaseCharacter.SaturatingMultiply(value, factor).ToString();
        });

        result = ValueDivideRegex.Replace(result, match =>
        {
            if (!ulong.TryParse(match.Groups["divisor"].Value, out ulong divisor) || divisor == 0)
            {
                return match.Value;
            }
            ulong value = GetValueAt(ParseValueIndex(match.Groups["index"].Value));
            return (value / divisor).ToString();
        });

        cachedDynamicDescription = ValueRegex.Replace(result, match =>
        {
            ulong value = GetValueAt(ParseValueIndex(match.Groups["index"].Value));
            return value.ToString();
        });

        isDynamicDescriptionDirty = false;
        return cachedDynamicDescription;
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

    private static ulong ScaleValue(ulong value, ulong numerator, ulong denominator)
    {
        if (denominator == 0UL) return value;
        if (numerator == denominator) return value;
        if (value == 0UL || numerator == 0UL) return 0UL;

        if (numerator >= denominator)
        {
            ulong factor = numerator / denominator;
            ulong remainder = numerator % denominator;
            ulong scaled = BaseCharacter.SaturatingMultiply(value, factor);
            if (remainder == 0UL)
            {
                return scaled;
            }

            ulong fractional = BaseCharacter.SaturatingMultiply(value, remainder) / denominator;
            return BaseCharacter.SaturatingAdd(scaled, fractional);
        }

        return BaseCharacter.SaturatingMultiply(value, numerator) / denominator;
    }

    private void EnsureValueSlot(int index)
    {
        while (values.Count <= index)
        {
            values.Add(0UL);
        }
    }

    private void SyncPrimaryValue()
    {
        Value = values.Count > 0 ? values[0] : 0UL;
        InvalidateCachedPresentation();
    }

    protected void InvalidateCachedPresentation()
    {
        isDynamicDescriptionDirty = true;
    }

    private static int ParseValueIndex(string rawIndex)
    {
        if (string.IsNullOrEmpty(rawIndex)) return 0;
        return int.TryParse(rawIndex, out int parsedIndex) && parsedIndex > 0 ? parsedIndex - 1 : 0;
    }
}
