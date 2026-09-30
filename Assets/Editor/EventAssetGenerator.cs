using UnityEngine;

/// <summary>
/// 资产生成工具 — 在编辑器中右键点击菜单可以快速创建常用SO资产
/// Only for Editor usage (放在Editor文件夹下实际会更好，但这里作为示例)
/// </summary>
#if UNITY_EDITOR
using UnityEditor;

public class EventAssetGenerator
{
    [MenuItem("Assets/Create/Events/Battle Started")]
    static public GameEventSO CreateBattleStartedEvent()
    {
        return CreateEventSO("Battle_Started");
    }

    [MenuItem("Assets/Create/Events/Damage Taken")]
    static public GameEventSO CreateDamageTakenEvent()
    {
        return CreateEventSO("Damage_Taken");
    }

    [MenuItem("Assets/Create/Events/Card Played")]
    static public GameEventSO CreateCardPlayedEvent()
    {
        return CreateEventSO("Card_Played");
    }

    [MenuItem("Assets/Create/Events/Achievement Unlocked")]
    static public GameEventSO CreateAchievementUnlockedEvent()
    {
        return CreateEventSO("Achievement_Unlocked");
    }

    [MenuItem("Assets/Create/Variables/Trophy Count")]
    static public IntVariable CreateTrophyVariable()
    {
        IntVariable var = ScriptableObject.CreateInstance<IntVariable>();
        var.name = "Trophy_Count";
        var.Value = 0;
        AssetUtility.CreateAsset(var);
        return var;
    }

    [MenuItem("Assets/Create/Variables/Player Health")]
    static public FloatVariable CreateHealthVariable()
    {
        FloatVariable var = ScriptableObject.CreateInstance<FloatVariable>();
        var.name = "Player_Health";
        var.Value = 100f;
        AssetUtility.CreateAsset(var);
        return var;
    }

    private static GameEventSO CreateEventSO(string eventName)
    {
        GameEventSO eventSO = ScriptableObject.CreateInstance<GameEventSO>();
        eventSO.name = eventName;
        AssetUtility.CreateAsset(eventSO);
        return eventSO;
    }
}

public static class AssetUtility
{
    public static void CreateAsset<T>(T asset) where T : ScriptableObject
    {
        string path = "Assets/New " + asset.GetType().Name + ".asset";
        AssetDatabase.CreateAsset(asset, path);
    }
}
#endif