using GoveKits.Runtime.Storage;

[ConfigPath("第九张史莱姆牌-工作表2", "csv")]
public class AchievementConfigData : IConfigData
{
    public int 排序;
    public string 名称;
    public string 成就ID;
    public string 成就描述;
    public int 成就步数;
    public int 奖杯需求;
}
