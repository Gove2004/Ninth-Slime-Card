using System.Collections;
using UnityEngine;

/// <summary>
/// 成就系统的接口定义 — 替代直接在代码中操作 GameCore.Achievements
/// 解耦成就解锁逻辑与显示、保存、UI更新等关注点
/// </summary>
public interface IAchievementService
{
    /// <summary>解锁成就（同步）</summary>
    void UnlockAchievement(string achievementId);

    /// <summary>检查成就是否已解锁</summary>
    bool IsUnlocked(string achievementId);

    /// <summary>获取已解锁的成就数量</summary>
    int GetTotalUnlockedCount();

    /// <summary>获取所有解锁的成就ID列表</summary>
    string[] GetAllUnlockedIds();

    /// <summary>异步解锁成就（用于带动画/提示的场景）</summary>
    IEnumerator UnlockAchievementAsync(string achievementId, string title, string description, float displayTime = 3f);

    /// <summary>重置所有成就（调试用）</summary>
    void ResetAllAchievements();
}