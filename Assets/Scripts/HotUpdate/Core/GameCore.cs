using TapSDK.Login;

public static class GameCore
{
    /// <summary>
    /// 本地离线模式：跳过 CDN 热更与 TapTap 联网登录。
    /// </summary>
    public static bool LocalOfflineMode { get; set; } = true;

#region 账户 Account

    public static TapTapAccount account { get; private set; }
    public static void SetAccount(TapTapAccount acc) => account = acc;

#endregion

#region 存档 Save

    public static PlayerData playerData { get; private set; }

    public static void SetPlayerData(PlayerData data)
    {
        playerData = data ?? new PlayerData();
    }

    public static void AddTrophy(int amount)
    {
        if (playerData == null)
        {
            playerData = new PlayerData();
        }

        playerData.trophy += amount;
        SaveManager.Instance?.Save();
    }

    public static bool SpendTrophy(int amount)
    {
        if (playerData == null || playerData.trophy < amount)
        {
            return false;
        }

        playerData.trophy -= amount;
        SaveManager.Instance?.Save();
        return true;
    }

    public static int GetTrophy()
    {
        return playerData?.trophy ?? 0;
    }

#endregion

#region 成就 Achievement

    public static bool IsAchievementUnlocked(string achievementId)
    {
        return playerData != null && playerData.achievementUnlocked.Contains(achievementId);
    }

    public static void UnlockAchievement(string achievementId)
    {
        if (playerData == null)
        {
            playerData = new PlayerData();
        }

        if (!playerData.achievementUnlocked.Contains(achievementId))
        {
            playerData.achievementUnlocked.Add(achievementId);
            SaveManager.Instance?.Save();
        }
    }

#endregion

#region 图鉴 Codex

    public static bool IsCardUnlocked(string cardId)
    {
        if (playerData == null) return false;
        return playerData.collection.Contains(cardId);
    }

    public static void UnlockCard(string cardId)
    {
        if (playerData == null)
        {
            playerData = new PlayerData();
        }

        if (!playerData.collection.Contains(cardId))
        {
            playerData.collection.Add(cardId);
            SaveManager.Instance?.Save();
        }
    }

#endregion

#region 局内 Run

    public static RunState runState { get; private set; }

    public static void SetRunState(RunState state)
    {
        runState = state ?? new RunState();
        runState.EnsureStarterDeck();
    }

    public static void StartNewRun()
    {
        runState = new RunState();
        runState.Reset();
        SaveRunState();
    }

    public static void SaveRunState()
    {
        if (playerData != null)
        {
            playerData.runState = runState;
            SaveManager.Instance?.Save();
        }
    }

    public static void LoadRunState()
    {
        runState = playerData?.runState;
        if (runState == null)
        {
            runState = new RunState();
        }
        runState.EnsureStarterDeck();
    }

    public static bool HasActiveRun()
    {
        return runState != null && runState.currentLv > 1;
    }

#endregion

#region 战斗 Battle

    public static string currentLevelName { get; private set; }
    public static void SetCurrentLevel(string name) => currentLevelName = name;

#endregion

}