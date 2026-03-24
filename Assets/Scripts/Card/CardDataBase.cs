// 卡牌数据库（包含硬编码CSV解析）
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class CardDatabase
{
    private const string ResourcesCsvPath = "cards";
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
            cardDataDict.Clear();
            cardDataList.Clear();
            isLoaded = false;

            if (!TryReadCsvContent(out string csvContent))
            {
                Debug.LogError("未能读取卡牌CSV文件");
                return;
            }
            
            if (!string.IsNullOrEmpty(csvContent))
            {
                ParseCsvContent(csvContent);
                isLoaded = cardDataDict.Count > 0;
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
                    imagePath = "卡牌/" + ResolveImageName(fields[1]),
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
        EnsureLoaded();
        if (cardDataDict.TryGetValue(id, out CardData data))
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

    private static void EnsureLoaded()
    {
        if (!isLoaded && cardDataDict.Count == 0)
        {
            LoadCardData();
        }
    }

    private static bool TryReadCsvContent(out string csvContent)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (TryReadCsvFromPersistent(out csvContent))
        {
            return true;
        }

        if (TryReadCsvFromStreamingAssets(out csvContent))
        {
            TryWriteCsvToPersistent(csvContent);
            return true;
        }

        if (TryReadCsvFromResources(out csvContent))
        {
            TryWriteCsvToPersistent(csvContent);
            return true;
        }

        csvContent = string.Empty;
        return false;
        #else
        if (TryReadCsvFromStreamingAssets(out csvContent))
        {
            return true;
        }

        if (TryReadCsvFromResources(out csvContent))
        {
            return true;
        }

        csvContent = string.Empty;
        return false;
        #endif
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

            string persistentPath = Path.Combine(persistentDir, CSV_RELATIVE_PATH);
            File.WriteAllText(persistentPath, csvContent);
            Debug.Log($"已写入卡牌CSV缓存: {persistentPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"写入卡牌CSV缓存失败: {ex.Message}");
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
