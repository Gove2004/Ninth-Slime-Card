using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using UnityEngine;

public abstract class BaseCard
{
    protected abstract int id { get; }

    protected BaseCard()
    {
        CardConfigData data = ConfigCore.LoadOne<CardConfigData>((dt) => dt.id == id);

        if (data == null)
        {
            LogCore.Error("BaseCard", $"未找到id为{id}的卡牌数据");
            return;
        }

        Name = data.名称;
        Series = data.系列;
        Cost = data.费用;
        Value1 = data.数值1;
        Value2 = data.数值2;
        Value3 = data.数值3;
        description = data.描述;
        Intresting = data.趣闻;
        Image = LoadCardSprite(data.名称);
    }

    public int Id => id;
    public string Name { get; private set; }
    public string Series { get; private set; }
    public int Cost { get; protected set; }
    public int RuntimeCost { get; set; } = -1;
    public int Value1 { get; protected set; }
    public int Value2 { get; protected set; }
    public int Value3 { get; protected set; }
    private string description { get; set; }
    public string Intresting { get; private set; }
    public Sprite Image { get; private set; }

    public virtual string Description()
    {
        if (string.IsNullOrEmpty(description)) return description;
        return description
            .Replace("[费用]", GetCurrentCost().ToString())
            .Replace("[数值]", Value1.ToString())
            .Replace("[数值1]", Value1.ToString())
            .Replace("[数值2]", Value2.ToString())
            .Replace("[数值3]", Value3.ToString());
    }

    public virtual int GetCurrentCost()
    {
        return RuntimeCost >= 0 ? RuntimeCost : Cost;
    }

    public virtual bool IsTargetedEffect(BaseCharacter user, BaseCharacter target)
    {
        return true;
    }

    public virtual BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return target ?? user?.Target;
    }

    public virtual bool CanUse(BaseCharacter user, BaseCharacter target)
    {
        if (user == null) return false;
        if (!user.CanSpendMana(GetCurrentCost())) return false;
        return ResolveTarget(user, target) != null;
    }

    public virtual void PreUse(BaseCharacter user, BaseCharacter target)
    {
        SpendManaEffect.Create().Setup(GetCurrentCost(), user).Apply(user);
    }

    public abstract void OnUse(BaseCharacter user, BaseCharacter target);

    public virtual void PostUse(BaseCharacter user, BaseCharacter target)
    {
        user.DiscardCard(this);
    }

    private Sprite LoadCardSprite(string cardName)
    {
        var handle = ResCore.LoadAssetSync<Sprite>($"Card_{cardName}");
        return handle?.GetAssetObject<Sprite>();
    }
}
