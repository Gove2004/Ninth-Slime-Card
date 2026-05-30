using System.Collections.Generic;
using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using TapSDK.Achievement;

public class AchievementManager : MonoSingleton<AchievementManager>
{
    public List<AchievementConfigData> GetAllAchievements()
    {
        return ConfigCore.LoadAll<AchievementConfigData>();
    }

    public bool TryPurchase(AchievementConfigData config)
    {
        if (config == null)
        {
            return false;
        }

        if (GameCore.IsAchievementUnlocked(config.成就ID))
        {
            MessageToastManager.Instance.ShowMessage("成就已解锁");
            return false;
        }

        if (!GameCore.SpendTrophy(config.奖杯需求))
        {
            MessageToastManager.Instance.ShowMessage("奖杯不足");
            return false;
        }

        GameCore.UnlockAchievement(config.成就ID);

        TapTapAchievement.Unlock(config.成就ID);

        MessageToastManager.Instance.ShowMessage($"成就解锁：{config.名称}");
        return true;
    }

    public bool IsUnlocked(string achievementId)
    {
        return GameCore.IsAchievementUnlocked(achievementId);
    }
}
