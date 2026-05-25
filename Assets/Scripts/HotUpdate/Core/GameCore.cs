
using TapSDK.Login;

public static class GameCore
{
#region 账户 Account

    public static TapTapAccount account { get; private set; }
    public static void SetAccount(TapTapAccount acc) => account = acc;

#endregion




#region 战斗 Battle

    public static string currentLevelName { get; private set; }
    public static void SetCurrentLevel(string name) => currentLevelName = name;





#endregion

}