// 卡牌数据库（包含硬编码CSV解析）
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class CardDatabase
{
    private const string ResourcesCsvPath = "cards";
    private const int MinimumValidCardCount = 20;
    private static readonly int[] RequiredCardIds = { 1000, 1001, 1002, 1003, 1201, 1500, 1702 };
    private static readonly string EmbeddedFallbackCsv = @"id,名称,系列,费用,数值,持续时间,效果,趣闻
1000,流血,基础,1,3,3,每回合造成[数值]点伤害，持续[持续时间]回合,众所周知，史莱姆是不会流血的
1001,恢复,基础,1,3,5,每回合恢复[数值]点生命，持续[持续时间]回合,即便是史莱姆之神也需要喘息
1002,入魔,基础,1,1,3,每回合获得[数值]点魔力，持续[持续时间]回合,你的头脑变得麻木...
1003,抽牌,基础,1,1,5,每回合抽取[数值]张卡牌，持续[持续时间]回合,这是一个卡牌游戏
1007,驱散,基础,1,1,0,随机驱散敌方[数值]个效果,阴森的声音在你耳边萦绕不绝...
1100,吸血,进阶,2,0,3,当你造成伤害后，恢复等量的生命值，持续[持续时间]回合,打完身上没一滴血是自己的
1101,加速,进阶,0,1,3,每回合造成[数值]点伤害，此伤害每回合+[数值×2]，持续[持续时间]回合,我说，我已启动，你耳朵懂吗
1102,延续,进阶,1,1,0,使自身的所有效果延长[数值]回合,老板，能不能再加个钟
1103,超频,进阶,1,2,0,使你所有手牌的数值和费用x[数值],超频提升角色和武器战力的核心机制
1104,结算,进阶,1,1,0,立即结算[数值]次双方身上的所有效果,你的月付账单已生成
1105,急救,进阶,0,1,0,立即获得[数值]点魔力并恢复[数值×3]点生命,我觉得我还可以再抢救一下
1106,闪击,进阶,0,5,0,立即造成[数值]点伤害,你甚至没看清刀光，一切就结束了
1110,重奏,进阶,0,0,0,本牌在你手牌中时，当你使用一张卡牌后，变形为它的复制,人类的本质是复读机
1200,七罪,原罪,7,7,7,获得全部[数值]张“七宗罪”卡牌，此后每回合随机获得一张，持续[持续时间]回合,二手全新，拆开概不退换
1201,暴怒,原罪,0,7,0,造成[数值]点伤害，有[数值]%概率受到同等伤害,怒火烧尽敌人，同时燃尽自我
1202,傲慢,原罪,0,7,1,免疫[数值]次伤害，持续[持续时间]回合,巴尔泽布，我已登神
1203,嫉妒,原罪,0,7,0,消耗至多[数值]点魔力值，随机偷取敌人等量的手牌、魔力、效果或生命,你的才是我的
1204,贪婪,原罪,0,7,0,弃置至多[数值]张牌，获得等量的魔力值，并对敌人造成等量的伤害,你的也是我的
1205,懒惰,原罪,0,7,0,消耗至多[数值]点法力值并抽等量张牌，结束此回合,今天有点累了，明天再更新吧
1206,色欲,原罪,0,7,0,随机使场上一个效果的持续时间+[数值],直播间由于太涩情被封禁了
1207,暴食,原罪,0,7,0,吃掉至多[数值]张手牌，每吃掉一张，恢复[数值]点生命,拜拜甜甜圈，珍珠奶茶大盘鸡
1300,种子,随机,0,0,0,重置随机数种子，随机获得一张牌,何意味
1301,骰子,随机,1,12,5,每回合随机造成0-[数值]点伤害，持续1-[持续时间]回合,骰子已经掷下
1302,命签,随机,1,12,5,每回合随机恢复0-[数值]点生命，持续1-[持续时间]回合,应天涉远的卜者
1303,运势,随机,1,6,5,每回合随机获得0-[数值]点魔力，持续1-[持续时间]回合,本座偏要，执此一签，观照诸天
1304,赌徒,随机,1,1,0,""[数值/1+数值]概率获得1张""""随机""""牌并再次触发此卡效果"",一无所有？或者，赢下所有
1305,轮盘,随机,1,6,6,5/6概率对自己造成[数值/6]伤害，1/6概率对敌人造成[数值×6]点伤害，持续[持续时间]回合,咔哒。咔哒。咔哒。砰
1400,苦修,鲜血,1,1,5,你对自己造成伤害时，获得[数值]点魔力，持续[持续时间]回合,人生是一场修行
1401,献祭,鲜血,1,1,5,每回合对双方造成[数值]点伤害，本局对战内“献祭”伤害永久增加一点，持续[持续时间]回合,复活吧，我的爱人
1402,卖血,鲜血,0,2,5,对自己造成[数值]点伤害，抽[数值/2]张牌，持续[持续时间]回合,许三观卖血记读后感1000字（通用8篇）-- 作文人网
1403,反伤,鲜血,1,0,5,受伤后，对敌造成等量伤害，持续[持续时间]回合,用毒蛇的毒毒毒蛇，毒蛇会不会被毒死
1404,血契,鲜血,2,0,3,当你恢复生命后，对敌人造成等量伤害，持续[持续时间]回合,一起成为马猴烧酒吧
1500,偷窃,暗影,2,1,3,每回合偷取敌人[数值]张卡牌，持续[持续时间]回合,小时偷针
1501,偷魔,暗影,2,1,3,每回合偷取敌人[数值]点魔力，持续[持续时间]回合,大时偷金
1502,偷月,暗影,2,1,3,每回合偷取敌人[数值]个效果，持续[持续时间]回合,老时偷月
1503,未来,暗影,1,15,3,延迟三回合后，每回合恢复[数值]点生命，持续[持续时间]回合,我（莫）把希望寄托在明天的自己
1504,破甲,暗影,2,2,3,敌方受到的伤害翻[数值]倍，持续[持续时间]回合,我给大家找了点穿甲弹[图片][图片]
1505,羽化,暗影,0,0,0,弃掉手牌，获得等量魔力和生命,观前脑子存放处
1506,制衡,暗影,0,0,0,弃掉手牌，抽取等量卡牌,牌再烂也不能掀桌子啊
1508,诅咒,暗影,1,1,5,每当敌方使用卡牌，其受到[数值]点伤害，持续[持续时间]回合,赛博功德+1
1700,镜像,科技,1,1,5,你每打出一张牌，对敌人造成[数值]点伤害，持续[持续时间]回合,自我之相，犹在镜中
1702,激光,科技,1,1,0,造成[数值]点伤害，本局游戏中你的“激光”伤害永久增加一点,这期神了，神在哪
1800,视界,科技,0,1,0,将你的手牌替换为对手牌库的复制，使用[数值]张牌或回合结束时换回,这和重开有什么区别啊喂
1801,奇点,科技,1,1,0,将双方当前魔力值和每回合获得的魔力值都重置为[数值]点,重开一下，我兄弟难产了
1802,传送,科技,1,1,3,存储你的所有手牌，三回合后，每回合获得[数值]份复制，持续3回合,要是能重来，我要选李白";
    private static readonly Dictionary<string, string> CardImageNameAliases = new()
    {
        { "视界", "黑洞" },
        { "传送", "时域" }
    };

    [Serializable]
    public class CardData
    {
        public int id;
        public string name;
        public string series;
        public ulong cost;
        public ulong value;
        public int duration;
        public string effect;
        public string imagePath;
        public string remark;
    }
    
    private static readonly Dictionary<int, CardData> cardDataDict = new();
    private static readonly List<CardData> cardDataList = new();
    private static bool isLoaded;
    private const string CSV_RELATIVE_PATH = "cards.csv";
    private const string CSV_BACKUP_RELATIVE_PATH = "cards.backup.csv";
    private static bool runtimeReloadAttempted;

    static CardDatabase()
    {
        LoadCardData();
    }

    public static void LoadCardData()
    {
        try
        {
            cardDataDict.Clear();
            cardDataList.Clear();
            isLoaded = false;

            if (!TryLoadCardDataFromSources())
            {
                Debug.LogError("未能加载有效的卡牌CSV文件");
                return;
            }

            isLoaded = true;
            runtimeReloadAttempted = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"加载卡牌数据失败: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void ParseCsvContent(string csvContent)
    {
        using (StringReader reader = new(csvContent))
        {
            string line;
            bool isFirstLine = true;
            
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue;
                }
                
                string[] fields = ParseCsvLine(line);
                if (fields.Length < 7)
                {
                    Debug.LogWarning($"CSV行字段不足，跳过: {line}");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(fields[0])) continue;
                
                // 创建卡牌数据对象
                if (!int.TryParse(fields[0], out int idValue))
                {
                    Debug.LogWarning($"卡牌ID解析失败: {fields[0]}");
                    continue;
                }
                if (!ulong.TryParse(fields[3], out ulong costValue))
                {
                    Debug.LogWarning($"卡牌成本解析失败，ID={idValue}: {fields[3]}");
                    continue;
                }
                if (!ulong.TryParse(fields[4], out ulong valueValue))
                {
                    Debug.LogWarning($"卡牌价值解析失败，ID={idValue}: {fields[4]}");
                    continue;
                }
                if (!int.TryParse(fields[5], out int durationValue))
                {
                    Debug.LogWarning($"卡牌持续时间解析失败，ID={idValue}: {fields[5]}");
                    continue;
                }
                
                CardData data = new()
                {
                    id = idValue,
                    name = fields[1],
                    series = fields[2],
                    cost = costValue,
                    value = valueValue,
                    duration = durationValue,
                    effect = fields[6],
                    imagePath = "卡牌/" + ResolveImageName(fields[1]),
                    remark = fields.Length > 7 ? fields[7] : string.Empty
                };
                
                cardDataDict[data.id] = data;
                cardDataList.Add(data);
            }
        }
        
        Debug.Log($"成功加载 {cardDataDict.Count} 张卡牌数据");
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        string currentField = string.Empty;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentField += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.Trim());
                currentField = string.Empty;
            }
            else
            {
                currentField += c;
            }
        }
        
        fields.Add(currentField.Trim());
        return fields.ToArray();
    }

    public static CardData GetCardData(int id)
    {
        EnsureLoaded();
        if (cardDataDict.TryGetValue(id, out CardData data))
        {
            return data;
        }

        TryRuntimeReload(id);
        if (cardDataDict.TryGetValue(id, out data))
        {
            return data;
        }
        
        Debug.LogError($"未找到卡牌数据: ID={id}");
        return null;
    }

    public static List<CardData> GetAllCardData()
    {
        EnsureLoaded();
        return new List<CardData>(cardDataList);
    }

    public static string GetImagePathByCardName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return "卡牌/default";
        }

        return "卡牌/" + ResolveImageName(cardName);
    }

    private static void EnsureLoaded()
    {
        if (!isLoaded && cardDataDict.Count == 0)
        {
            LoadCardData();
        }
    }

    private static void TryRuntimeReload(int missingId)
    {
        if (runtimeReloadAttempted)
        {
            return;
        }

        runtimeReloadAttempted = true;
        Debug.LogWarning($"检测到卡牌数据缺失，尝试重新载入卡牌库。缺失ID={missingId}");
        LoadCardData();
    }

    private delegate bool CsvReader(out string csvContent);

    private static bool TryLoadCardDataFromSources()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (TryLoadValidatedSource("StreamingAssets", TryReadCsvFromStreamingAssets, cacheToPersistent: true))
        {
            return true;
        }

        if (TryLoadValidatedSource("Resources", TryReadCsvFromResources, cacheToPersistent: true))
        {
            return true;
        }

        if (TryLoadValidatedSource("本地缓存", TryReadCsvFromPersistent, cacheToPersistent: false))
        {
            return true;
        }

        if (TryLoadValidatedSource("兜底备份", TryReadCsvFromBackup, cacheToPersistent: false))
        {
            return true;
        }

        if (TryLoadValidatedSource("代码内置卡牌表", TryReadEmbeddedFallback, cacheToPersistent: true))
        {
            return true;
        }

        return false;
        #else
        if (TryLoadValidatedSource("StreamingAssets", TryReadCsvFromStreamingAssets, cacheToPersistent: true))
        {
            return true;
        }

        if (TryLoadValidatedSource("Resources", TryReadCsvFromResources, cacheToPersistent: true))
        {
            return true;
        }

        if (TryLoadValidatedSource("本地缓存", TryReadCsvFromPersistent, cacheToPersistent: false))
        {
            return true;
        }

        if (TryLoadValidatedSource("兜底备份", TryReadCsvFromBackup, cacheToPersistent: false))
        {
            return true;
        }

        if (TryLoadValidatedSource("代码内置卡牌表", TryReadEmbeddedFallback, cacheToPersistent: true))
        {
            return true;
        }

        return false;
        #endif
    }

    private static bool TryLoadValidatedSource(string sourceName, CsvReader reader, bool cacheToPersistent)
    {
        if (!reader(out string csvContent))
        {
            return false;
        }

        if (!TryParseValidatedCsv(csvContent, sourceName))
        {
            return false;
        }

        TryWriteCsvBackup(csvContent);
        if (cacheToPersistent)
        {
            TryWriteCsvToPersistent(csvContent);
        }

        Debug.Log($"卡牌数据源校验通过: {sourceName}");
        return true;
    }

    private static bool TryParseValidatedCsv(string csvContent, string sourceName)
    {
        cardDataDict.Clear();
        cardDataList.Clear();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            Debug.LogWarning($"{sourceName}中的卡牌CSV为空");
            return false;
        }

        ParseCsvContent(csvContent);
        if (IsLoadedCardDataValid())
        {
            return true;
        }

        Debug.LogWarning($"{sourceName}中的卡牌CSV校验失败，已忽略该数据源");
        cardDataDict.Clear();
        cardDataList.Clear();
        return false;
    }

    private static bool IsLoadedCardDataValid()
    {
        if (cardDataDict.Count < MinimumValidCardCount)
        {
            return false;
        }

        for (int i = 0; i < RequiredCardIds.Length; i++)
        {
            if (!cardDataDict.TryGetValue(RequiredCardIds[i], out CardData data))
            {
                return false;
            }

            if (data == null || string.IsNullOrWhiteSpace(data.name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadCsvFromPersistent(out string csvContent)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, CSV_RELATIVE_PATH);
        if (!File.Exists(persistentPath))
        {
            csvContent = string.Empty;
            return false;
        }

        csvContent = File.ReadAllText(persistentPath);
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            Debug.LogWarning($"本地缓存的CSV为空: {persistentPath}");
            return false;
        }

        Debug.Log($"从本地缓存加载卡牌CSV: {persistentPath}");
        return true;
    }

    private static bool TryReadCsvFromBackup(out string csvContent)
    {
        string backupPath = Path.Combine(Application.persistentDataPath, CSV_BACKUP_RELATIVE_PATH);
        if (!File.Exists(backupPath))
        {
            csvContent = string.Empty;
            return false;
        }

        csvContent = File.ReadAllText(backupPath);
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            Debug.LogWarning($"兜底备份CSV为空: {backupPath}");
            return false;
        }

        Debug.Log($"从兜底备份加载卡牌CSV: {backupPath}");
        return true;
    }

    private static bool TryReadEmbeddedFallback(out string csvContent)
    {
        csvContent = EmbeddedFallbackCsv;
        return !string.IsNullOrWhiteSpace(csvContent);
    }

    private static bool TryReadCsvFromStreamingAssets(out string csvContent)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, CSV_RELATIVE_PATH);
        #if UNITY_ANDROID && !UNITY_EDITOR
        UnityWebRequest request = UnityWebRequest.Get(fullPath);
        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            csvContent = string.Empty;
            request.Dispose();
            return false;
        }

        csvContent = request.downloadHandler.text;
        request.Dispose();
        #else
        if (!File.Exists(fullPath))
        {
            csvContent = string.Empty;
            return false;
        }

        csvContent = File.ReadAllText(fullPath);
        #endif
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            Debug.LogWarning($"StreamingAssets中的CSV为空: {fullPath}");
            return false;
        }

        Debug.Log($"从StreamingAssets加载卡牌CSV: {fullPath}");
        return true;
    }

    private static bool TryReadCsvFromResources(out string csvContent)
    {
        TextAsset csvAsset = Resources.Load<TextAsset>(ResourcesCsvPath);
        if (csvAsset == null)
        {
            csvContent = string.Empty;
            return false;
        }

        csvContent = csvAsset.text;
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            Debug.LogWarning("Resources中的卡牌CSV为空");
            return false;
        }

        Debug.Log("从Resources加载卡牌CSV");
        return true;
    }

    private static void TryWriteCsvToPersistent(string csvContent)
    {
        TryWriteCsvFile(CSV_RELATIVE_PATH, csvContent, "卡牌CSV缓存");
    }

    private static void TryWriteCsvBackup(string csvContent)
    {
        TryWriteCsvFile(CSV_BACKUP_RELATIVE_PATH, csvContent, "卡牌CSV兜底备份");
    }

    private static void TryWriteCsvFile(string fileName, string csvContent, string label)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(csvContent))
            {
                return;
            }

            string persistentDir = Application.persistentDataPath;
            if (!Directory.Exists(persistentDir))
            {
                Directory.CreateDirectory(persistentDir);
            }

            string persistentPath = Path.Combine(persistentDir, fileName);
            File.WriteAllText(persistentPath, csvContent);
            Debug.Log($"已写入{label}: {persistentPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"写入{label}失败: {ex.Message}");
        }
    }

    private static string ResolveImageName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName)) return cardName;
        if (CardImageNameAliases.TryGetValue(cardName, out string aliasName))
        {
            return aliasName;
        }
        return cardName;
    }
}
