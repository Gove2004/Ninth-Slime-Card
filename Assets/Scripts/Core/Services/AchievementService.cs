using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 成就服务实现 — 通过 IAchievementService 接口暴露功能
/// </summary>
public class AchievementService : MonoBehaviour, IAchievementService
{
    [SerializeField] private string _achievementsJsonKey = "Player_Achievements";

    private HashSet<string> _unlockedAchievements = new();

    private void Awake() => LoadAchievements();

    public void UnlockAchievement(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;

        if (_unlockedAchievements.Contains(achievementId))
        {
            Debug.Log($"[AchievementService] Achievement already unlocked: {achievementId}");
            return;
        }

        _unlockedAchievements.Add(achievementId);
        SaveAchievements();
        Debug.Log($"[AchievementService] Unlocked: {achievementId}");
    }

    public bool IsUnlocked(string achievementId) => _unlockedAchievements.Contains(achievementId);

    public int GetTotalUnlockedCount() => _unlockedAchievements.Count;

    public string[] GetAllUnlockedIds() => _unlockedAchievements.ToArray();

    public IEnumerator UnlockAchievementAsync(string achievementId, string title, string description, float displayTime = 3f)
    {
        UnlockAchievement(achievementId);

        // 这里可以添加 UI 提示动画逻辑
        Debug.Log($"[AchievementService] Showing unlock popup: {title}");

        if (displayTime > 0)
        {
            yield return new WaitForSeconds(displayTime);
        }
    }

    public void ResetAllAchievements()
    {
        _unlockedAchievements.Clear();
        SaveAchievements();
        Debug.Log("[AchievementService] All achievements reset");
    }

    private void LoadAchievements()
    {
        // 从 PlayerPrefs 或文件加载实际数据
        // string json = PlayerPrefs.GetString(_achievementsJsonKey, "[]");
        // _unlockedAchievements = new HashSet<string>(JsonUtility.FromJson<string[]>(json));
        Debug.Log("[AchievementService] Loading achievements...");
    }

    private void SaveAchievements()
    {
        // string json = JsonUtility.ToJson(_unlockedAchievements.ToArray());
        // PlayerPrefs.SetString(_achievementsJsonKey, json);
        // PlayerPrefs.Save();
        Debug.Log("[AchievementService] Saving achievements...");
    }

    // 提供给外部的事件：当有成就解锁时触发
    public System.Action<string> OnAchievementUnlocked;
    public void RaiseOnUnlocked(string id) => OnAchievementUnlocked?.Invoke(id);
}