


using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;

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

        // Name = data.名称;
        // Cost = data.费用;
        // Value1 = data.数值1;
        // Value2 = data.数值2;
        // Value3 = data.数值3;
        // Description = data.描述;
        // ImagePath = data.图片路径;
    }


    public string Name { get; private set; }
    public ulong Cost { get; private set; }
    public string Value1 { get; private set; }
    public string Value2 { get; private set; }
    public string Value3 { get; private set; }
    public string Description { get; private set; }
    public string ImagePath { get; private set; }



    public virtual string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        string result = Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值1]", Value1)
            .Replace("[数值2]", Value2)
            .Replace("[数值3]", Value3);

        return result;
    }

}