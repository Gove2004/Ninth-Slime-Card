
using TapSDK.Login;

public static class GameCore
{
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

#region 战斗 Battle

    public static string currentLevelName { get; private set; }
    public static void SetCurrentLevel(string name) => currentLevelName = name;

#endregion

}
