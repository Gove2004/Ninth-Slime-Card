using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TapSDK.Achievement;
using TapSDK.Core;
using TapSDK.Login;
using UnityEngine;

public class TapTapSdk : MonoBehaviour
{
    public const bool EnableTapTapAchievements = false;
    public string CurrentUserId { get; private set; }
    public string CurrentUserName { get; private set; }
    /// <summary>
    /// 简单的单例实现，方便在游戏中任何地方通过 TapTapSdk.Instance 来访问 SDK 功能
    /// </summary>
    public static TapTapSdk Instance { get; private set; }
    public static bool IsInitialized { get; private set; }
    public static bool IsLoggedIn { get; private set; }
    public static event Action<TapAchievementResult> AchievementSucceeded;
    public static event Action<string, int, string> AchievementFailed;
    public static event Action<TapTapAccount> LoginSucceeded;

    private const string ClientId = "irmjeyzoxpztwlne5z";
    private const string ClientToken = "kXbnn4wKsOA4Rcd5wPLnEWLIgecN8pW5Dpk6ov2E";
    private const string ClientPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7Pfrav0TTq4fHVkLrj/IJd/q/lpVJGeZ7jWVIgjeW9CeKi8zs46Uk+9Jyzd3Jmc8xG/sUb0gS2ZGMHSuZNHXV+IhC4MD2nqjW68yEuCGbgWuzkebFPGRsRwAFLk6MhsUoW+30f9TCHB5w/qnsmEwcXiko5H8+Gjp+vRCY4/ojTXBHpAegm7lqTh2cL15nYuzNdCZEZ6cqVNkJkLSgkkevq1rLZknznHZpymYlGCqHYcVsR1kJBcIL+kE/rqxHihOUILEZSstbHD8Ru8NZDieaP+Sz76t0f/3aqWOiJbWPEngofvOSEpdJaiGzoc2m6DTAsmErIZMZgiJ80uztVi/lQIDAQAB";
    private MethodInfo setAchievementStepsMethod;
    private bool hasResolvedSetAchievementStepsMethod;
    private bool isShuttingDown;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 初始化 TapTap SDK，建议在游戏启动时调用一次
    /// </summary>
    public void Initialize()
    {
        if (IsInitialized)
        {
            ConfigureAndroidLandscapeAutoRotation();
            return;
        }

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
            enableLog = false
        };

        // 禁用成就模块初始化，避免登录时触发成就同步与回调风暴。
        TapTapSdkBaseOptions[] otherOptions = Array.Empty<TapTapSdkBaseOptions>();
        // TapSDK 初始化
        TapTapSDK.Init(coreOptions, otherOptions);
        IsInitialized = true;
        ConfigureAndroidLandscapeAutoRotation();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            return;
        }

        ConfigureAndroidLandscapeAutoRotation();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            return;
        }

        ConfigureAndroidLandscapeAutoRotation();
    }

    private void OnDestroy()
    {
        isShuttingDown = true;
        if (Instance == this)
        {
            Instance = null;
            IsInitialized = false;
            IsLoggedIn = false;
        }

    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private static void ConfigureAndroidLandscapeAutoRotation()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
#endif
    }


    public async Task LoginAsync(
        Action<TapTapAccount> onLoginSuccess,
        Action onLoginCancel,
        Action<Exception> onLoginError
    )
    {
        try
        {
            Initialize();

            // 定义授权范围
            List<string> scopes = new List<string>
            {
                TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE
            };
            // 发起 Tap 登录
            var userInfo = await TapTapLogin.Instance.LoginWithScopes(scopes.ToArray());
            if (isShuttingDown) return;
            Debug.Log($"登录成功，当前用户 ID：{userInfo.unionId}");

            SetLoggedInAccount(userInfo);
            onLoginSuccess?.Invoke(userInfo);
        }
        catch (TaskCanceledException)
        {
            if (isShuttingDown) return;
            onLoginCancel?.Invoke();
            Debug.Log("用户取消登录");
        }
        catch (Exception exception)
        {
            if (isShuttingDown) return;
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
        if (!EnableTapTapAchievements) return;
        TapTapAchievement.Unlock(achievementId : achievementId);
    }
    
    /// <summary>
    /// 增加成就步长
    /// </summary>
    /// <param name="achievementId"></param>
    /// <param name="step"></param>
    public void IncrementAchievement(string achievementId, int step)
    {
        if (!EnableTapTapAchievements) return;
        TapTapAchievement.Increment(achievementId : achievementId, step : step);
    }

    public void SetLoggedInAccount(TapTapAccount account)
    {
        IsLoggedIn = account != null;
        CurrentUserId = account == null ? null : (!string.IsNullOrEmpty(account.openId) ? account.openId : account.unionId);
        CurrentUserName = account?.name;
        if (account != null)
        {
            LoginSucceeded?.Invoke(account);
        }
    }

    public void ClearLoggedInAccount()
    {
        IsLoggedIn = false;
        CurrentUserId = null;
        CurrentUserName = null;
    }

    public bool TrySetAchievementSteps(string achievementId, int step)
    {
        if (!EnableTapTapAchievements) return false;
        if (string.IsNullOrEmpty(achievementId) || step < 0)
        {
            return false;
        }

        MethodInfo method = ResolveSetAchievementStepsMethod();
        if (method == null)
        {
            return false;
        }

        try
        {
            method.Invoke(null, new object[] { achievementId, step });
            return true;
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private MethodInfo ResolveSetAchievementStepsMethod()
    {
        if (hasResolvedSetAchievementStepsMethod)
        {
            return setAchievementStepsMethod;
        }

        hasResolvedSetAchievementStepsMethod = true;
        Type achievementType = typeof(TapTapAchievement);
        setAchievementStepsMethod =
            FindStaticAchievementMethod(achievementType, "MakeSteps") ??
            FindStaticAchievementMethod(achievementType, "SetSteps") ??
            FindStaticAchievementMethod(achievementType, "makeSteps");
        return setAchievementStepsMethod;
    }

    private static MethodInfo FindStaticAchievementMethod(Type achievementType, string methodName)
    {
        if (achievementType == null || string.IsNullOrEmpty(methodName))
        {
            return null;
        }

        return achievementType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(int) },
            null
        );
    }

    internal static void NotifyAchievementSuccess(TapAchievementResult result)
    {
        if (!EnableTapTapAchievements) return;
        AchievementSucceeded?.Invoke(result);
    }

    internal static void NotifyAchievementFailure(string achievementId, int errorCode, string errorMsg)
    {
        if (!EnableTapTapAchievements) return;
        AchievementFailed?.Invoke(achievementId, errorCode, errorMsg);
    }
}
