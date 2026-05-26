using GoveKits.Runtime.Storage;


[ConfigPath("CardConfigData", "csv")]
public class CardConfigData : IConfigData
{
    // id,名称,系列,费用,数值,持续时间,效果,趣闻,备注
    public int id;
    public string 名称;
    public string 系列;
    public int 费用;
    public int 数值;
    public int 持续时间;
    public string 效果;
    public string 趣闻;
    public string 备注;

    // public string ImagePath;  // 就是 "Card_" + 卡牌名称 + ".png"
}