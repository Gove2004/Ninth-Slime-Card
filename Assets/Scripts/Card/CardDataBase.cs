// 卡牌数据库（包含硬编码CSV解析）
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardDatabase
{
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
    
    // 硬编码的CSV相对路径
    private const string CSV_RELATIVE_PATH = "cards.csv";
    
    // 静态构造函数，自动加载数据
    static CardDatabase()
    {
        LoadCardData();
    }
    
    // 加载卡牌数据
    public static void LoadCardData()
    {
        try
        {
            string csvContent;

            #if UNITY_ANDROID && !UNITY_EDITOR
            // Android: 需要先从StreamingAssets复制到persistentDataPath
            string persistentPath = Path.Combine(Application.persistentDataPath, CSV_RELATIVE_PATH);
            
            if (!File.Exists(persistentPath))
            {
                // 确保目录存在
                string persistentDir = Application.persistentDataPath;
                if (!Directory.Exists(persistentDir))
                {
                    Directory.CreateDirectory(persistentDir);
                }
                
                // 从StreamingAssets复制文件到persistentDataPath
                string streamingPath = Path.Combine(Application.streamingAssetsPath, CSV_RELATIVE_PATH);
                WWW loadWWW = new WWW(streamingPath);
                
                // 等待加载完成（带超时保护）
                float timeout = 30f;
                float startTime = Time.realtimeSinceStartup;
                while (!loadWWW.isDone && (Time.realtimeSinceStartup - startTime) < timeout)
                {
                    System.Threading.Thread.Sleep(10);
                }
                
                if (!loadWWW.isDone)
                {
                    Debug.LogError($"加载CSV文件超时（{timeout}s）");
                    return;
                }
                
                if (!string.IsNullOrEmpty(loadWWW.error))
                {
                    Debug.LogError($"加载CSV文件失败: {loadWWW.error}");
                    return;
                }
                
                // 写入文件
                try
                {
                    File.WriteAllBytes(persistentPath, loadWWW.bytes);
                    Debug.Log($"CSV文件已复制到: {persistentPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"写入CSV文件失败: {ex.Message}");
                    return;
                }
            }
            else
            {
                Debug.Log($"CSV文件已在本地缓存: {persistentPath}");
            }
            
            csvContent = File.ReadAllText(persistentPath);
            #else
            // 其他平台: 直接从StreamingAssets读取
            string fullPath = Path.Combine(Application.streamingAssetsPath, CSV_RELATIVE_PATH);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"CSV文件不存在: {fullPath}");
                return;
            }
            csvContent = File.ReadAllText(fullPath);
            #endif
            
            if (!string.IsNullOrEmpty(csvContent))
            {
                ParseCsvContent(csvContent);
            }
            else
            {
                Debug.LogError("CSV文件内容为空");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载卡牌数据失败: {e.Message}\n{e.StackTrace}");
        }
    }
    
    // 解析CSV内容
    private static void ParseCsvContent(string csvContent)
    {
        cardDataDict.Clear();
        cardDataList.Clear();
        
        using (StringReader reader = new(csvContent))
        {
            string line;
            bool isFirstLine = true;
            
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                if (isFirstLine)
                {
                    isFirstLine = false; // 跳过标题行
                    continue;
                }
                
                // 解析CSV行
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
                    imagePath = "卡牌/" + fields[1], // 默认图片路径为 "卡牌/卡牌名称"
                    remark = fields.Length > 7 ? fields[7] : string.Empty
                };
                
                // 添加到字典
                cardDataDict[data.id] = data;
                cardDataList.Add(data);
            }
        }
        
        Debug.Log($"成功加载 {cardDataDict.Count} 张卡牌数据");
    }
    
    // 解析CSV行（简单实现）
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
    
    // 获取卡牌数据
    public static CardData GetCardData(int id)
    {
        if (cardDataDict.TryGetValue(id, out CardData data))
        {
            return data;
        }
        
        Debug.LogError($"未找到卡牌数据: ID={id}");
        return null;
    }

    public static List<CardData> GetAllCardData()
    {
        return new List<CardData>(cardDataList);
    }
}
