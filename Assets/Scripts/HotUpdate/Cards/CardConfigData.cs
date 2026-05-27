using GoveKits.Runtime.Storage;


[ConfigPath("第九张史莱姆牌-工作表1", "csv")]
public class CardConfigData : IConfigData
{
    // id,名称,系列,费用,数值,持续时间,效果,趣闻,备注
    public int id;
    public string 名称;
    public string 系列;
    public int 费用;
    public int 数值1;
    public int 数值2;
    public int 数值3;
    public string 描述;
    public string 趣闻;
    public string 备注;
}