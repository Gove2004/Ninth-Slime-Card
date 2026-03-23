using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TapSDK.Achievement;
using TapSDK.Core;
using TapSDK.Login;
using UnityEngine;

public class TapTapSdk : MonoBehaviour
{
    /// <summary>
    /// 简单的单例实现，方便在游戏中任何地方通过 TapTapSdk.Instance 来访问 SDK 功能
    /// </summary>
    public static TapTapSdk Instance { get; private set; }

    private const string ClientId = "irmjeyzoxpztwlne5z";
    private const string ClientToken = "kXbnn4wKsOA4Rcd5wPLnEWLIgecN8pW5Dpk6ov2E";
    private const string ClientPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7Pfrav0TTq4fHVkLrj/IJd/q/lpVJGeZ7jWVIgjeW9CeKi8zs46Uk+9Jyzd3Jmc8xG/sUb0gS2ZGMHSuZNHXV+IhC4MD2nqjW68yEuCGbgWuzkebFPGRsRwAFLk6MhsUoW+30f9TCHB5w/qnsmEwcXiko5H8+Gjp+vRCY4/ojTXBHpAegm7lqTh2cL15nYuzNdCZEZ6cqVNkJkLSgkkevq1rLZknznHZpymYlGCqHYcVsR1kJBcIL+kE/rqxHihOUILEZSstbHD8Ru8NZDieaP+Sz76t0f/3aqWOiJbWPEngofvOSEpdJaiGzoc2m6DTAsmErIZMZgiJ80uztVi/lQIDAQAB";
    private AchievementCallback achievementCallback;
    
    void Awake()
    {
        // 实现单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 切换场景时不销毁
        }
        else
        {
            Destroy(gameObject); // 已经存在实例，销毁重复的对象
        }
    }

    void Start()
    {
        Initialize();
    }

    /// <summary>
    /// 初始化 TapTap SDK，建议在游戏启动时调用一次
    /// </summary>
    public void Initialize()
    {
        // 核心配置
        TapTapSdkOptions coreOptions = new TapTapSdkOptions
        {
            // 客户端 ID，开发者后台获取
            clientId = ClientId,
            // 客户端令牌，开发者后台获取
            clientToken = ClientToken,
            // 地区，CN 为国内，Overseas 为海外
            region = TapTapRegionType.CN,
            // 客户端 PC 平台公钥，开发者后台获取，仅接入 TapTap PC 客户端需要
            clientPublicKey = ClientPublicKey,
            // 屏幕方向：0-竖屏 1-横屏，仅移动端生效
            screenOrientation = 1,
            // 是否开启日志，Release 版本请设置为 false
            enableLog = true
        };

        // 成就配置
        TapTapAchievementOptions achievementOptions = new TapTapAchievementOptions
        {
            // 成就达成时 SDK 是否需要展示一个气泡弹窗提示
            enableToast = true
        };
        achievementCallback ??= new AchievementCallback();
        TapTapAchievement.RegisterCallBack(achievementCallback);

        // 其他模块配置项
        TapTapSdkBaseOptions[] otherOptions = new TapTapSdkBaseOptions[]
        {
            achievementOptions
        };
        // TapSDK 初始化
        TapTapSDK.Init(coreOptions, otherOptions);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (achievementCallback != null)
        {
            TapTapAchievement.UnRegisterCallBack(achievementCallback);
        }
    }


    public async Task LoginAsync(
        Action<TapTapAccount> onLoginSuccess,
        Action onLoginCancel,
        Action<Exception> onLoginError
    )
    {
        try
        {
            // 定义授权范围
            List<string> scopes = new List<string>
            {
                TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE
            };
            // 发起 Tap 登录
            var userInfo = await TapTapLogin.Instance.LoginWithScopes(scopes.ToArray());
            Debug.Log($"登录成功，当前用户 ID：{userInfo.unionId}");

            onLoginSuccess?.Invoke(userInfo);
        }
        catch (TaskCanceledException)
        {
            onLoginCancel?.Invoke();
            Debug.Log("用户取消登录");
        }
        catch (Exception exception)
        {
            onLoginError?.Invoke(exception);
            Debug.Log($"登录失败，出现异常：{exception}");
        }
    }



    // public void Logout()
    // {
    //     TapTapLogin.Instance.Logout();
    //     Debug.Log("用户已退出登录");
    // }

    /// <summary>
    /// 解锁成就
    /// </summary>
    /// <param name="achievementId"></param>
    public void UnlockAchievement(string achievementId)
    {
        TapTapAchievement.Unlock(achievementId : achievementId);
    }
    
    /// <summary>
    /// 增加成就步长
    /// </summary>
    /// <param name="achievementId"></param>
    /// <param name="step"></param>
    public void IncrementAchievement(string achievementId, int step)
    {
        TapTapAchievement.Increment(achievementId : achievementId, step : step);
    }
}




class AchievementCallback : ITapAchievementCallback
{

    public AchievementCallback(){}
  
    public void OnAchievementSuccess(int code, TapAchievementResult result)
    {
        // 成就状态更新成功
        // code 70001 解锁成就成功
        // code 70002 增加步长成功
        // result 成就数据详情

        Debug.Log($"成就状态更新成功，code：{code}，achievementId：{result.AchievementId}，currentSteps：{result.CurrentSteps}");
    }
  
    public void OnAchievementFailure(string achievementId, int errorCode, string errorMsg)
    {
        // 成就状态更新失败或其他错误
        // achievementId 触发失败的成就 ID， 如果调用的是 [ShowAchievements] 接口，则为 "" 空字符串。
        // errorCode 错误码
        // errorMsg 错误描述
        Debug.Log($"成就状态更新失败，achievementId：{achievementId}，errorCode：{errorCode}，errorMsg：{errorMsg}");
	}

}
