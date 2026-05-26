


using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using UnityEngine;

public abstract class BaseCard
{
    protected abstract int id { get; }

    public BaseCard()
    {
        CardConfigData data = ConfigCore.LoadOne<CardConfigData>((dt) => dt.id == this.id);

        if (data == null)
        {
            LogCore.Error("BaseCard", $"未找到id为{this.id}的卡牌数据");
            return;
        }

        Name = data.名称;
        Cost = data.费用;
        Value1 = data.数值;
        // Value2 = data.数值2;
        // Value3 = data.数值3;
        Description = data.效果;
        Image = ResCore.LoadAssetSync<Sprite>($"Card_{data.名称}").GetAssetObject<Sprite>();
    }


    public string Name { get; private set; }
    public int Cost { get; private set; }
    public int Value1 { get; private set; }
    public int Value2 { get; private set; }
    public int Value3 { get; private set; }
    private string Description { get; set; }
    public Sprite Image { get; private set; }



    public virtual string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        string result = Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值]", Value1.ToString())
            .Replace("[数值1]", Value1.ToString())
            .Replace("[数值2]", Value2.ToString())
            .Replace("[数值3]", Value3.ToString());

        return result;
    }

}