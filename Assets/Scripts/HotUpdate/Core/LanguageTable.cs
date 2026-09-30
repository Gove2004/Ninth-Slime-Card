using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 静态语言表服务 —— 纯 HotUpdate 代码，不依赖 UnityEditor
/// 通过遍历场景中的 Text/TMP_Text 组件进行文本替换
/// </summary>
public static class LanguageTable
{
    // 翻译键值表: { "btn_start": {"zh":"开始游戏","en":"Start Game"} }
    private static readonly Dictionary<string, Dictionary<string, string>> s_Table = new();

    /// <summary>
    /// 添加一条翻译记录
    /// </summary>
    public static void AddEntry(string key, string zh, string en)
    {
        if (!s_Table.ContainsKey(key))
        {
            s_Table[key] = new Dictionary<string, string>();
        }
        s_Table[key]["zh"] = zh;
        s_Table[key]["en"] = en;
    }

    /// <summary>
    /// 应用指定语言到所有可识别的 UI 文本节点
    /// </summary>
    public static void ApplyLanguage(string lang)
    {
        var texts = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(UnityEngine.FindObjectsSortMode.None);
        foreach (var text in texts)
        {
            if (text == null) continue;
            // 查找节点上是否有保存 key 的字段或属性
            // 这里采用简单策略：如果 text 的 initial text 是一个键名（如 "btn_start"），则替换为对应语言的翻译
            // 实际项目中建议为每个可翻译文本增加一个 [SerializeField] string translationKey 字段更精确
            if (text.text.Length > 0 && s_Table.ContainsKey(text.text))
            {
                text.text = s_Table[text.text][lang];
            }
        }

        // 同样处理旧版 UI Text（若有）
        var texts2D = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(UnityEngine.FindObjectsSortMode.None);
        foreach (var text in texts2D)
        {
            if (text == null) continue;
            if (text.text.Length > 0 && s_Table.ContainsKey(text.text))
            {
                text.text = s_Table[text.text][lang];
            }
        }
    }

    /// <summary>
    /// 初始化默认翻译表（在 Editor 中调用一次，或在游戏启动时加载）
    /// </summary>
    public static void InitDefaultEntries()
    {
        AddEntry("btn_start", "开始游戏", "Start Game");
        AddEntry("btn_continue", "继续 Lv.{Lv}", "Continue Lv.{Lv}");
        AddEntry("btn_settings", "设置", "Settings");
        AddEntry("panel_title", "设置面板", "Settings Panel");
        AddEntry("vol_label", "音量", "Volume");
        AddEntry("lang_label", "语言", "Language");
    }

    /// <summary>
    /// 获取翻译后的文本（用于动态设置）
    /// </summary>
    public static string GetText(string key, string lang)
    {
        if (s_Table.TryGetValue(key, out var langs) && langs.TryGetValue(lang, out var txt))
        {
            return txt;
        }
        return key; // 找不到原键作为回退
    }
}
