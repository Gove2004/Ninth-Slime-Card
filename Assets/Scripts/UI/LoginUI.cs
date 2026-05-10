using System;
using System.Threading.Tasks;
using TapSDK.Login;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    private const string LoginAchievementId = "login";
    public Button loginButton;
    public TextMeshProUGUI loginTipText;

    private async void Start()
    {
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
            loginButton.interactable = false;
        }

        if (loginTipText != null)
        {
            loginTipText.text = "检查登录状态中...";
        }

        await TryAutoLoginAsync();
    }

    public void OnLoginButtonClicked()
    {
        if (loginButton == null) return;
        loginButton.interactable = false; // 禁用登录按钮，防止重复点击
        _ = TapTapSdk.Instance.LoginAsync(OnLoginSuccess, OnLoginCancel, OnLoginFailure); // 确保 SDK 已经初始化
        if (loginTipText != null)
        {
            loginTipText.text = "登录中...";
        }
        Debug.Log("登录按钮被点击，正在触发登录流程...");
    }

    private async Task TryAutoLoginAsync()
    {
        if (TapTapSdk.Instance == null)
        {
            if (loginButton != null) loginButton.interactable = true;
            if (loginTipText != null) loginTipText.text = "点击登录";
            return;
        }

        try
        {
            TapTapAccount result = await TapTapSdk.Instance.TryRestoreLoginAsync();
            if (result != null)
            {
                ApplyLoginSuccess(ResolveUserId(result), ResolveUserName(result), false);
                return;
            }

            if (loginButton != null) loginButton.interactable = true;
            if (loginTipText != null) loginTipText.text = "点击登录";
        }
        catch (Exception exception)
        {
            Debug.Log($"自动登录失败，出现异常：{exception}");
            TapTapSdk.Instance?.ClearLoggedInAccount();
            if (loginButton != null) loginButton.interactable = true;
            if (loginTipText != null) loginTipText.text = "自动登录失败，请手动登录";
        }
    }

    private void OnLoginSuccess(TapTapAccount result)
    {
        TapTapSdk.Instance?.SetLoggedInAccount(result);
        ApplyLoginSuccess(ResolveUserId(result), ResolveUserName(result), true);
    }

    private void ApplyLoginSuccess(string userId, string userName, bool unlockAchievement)
    {
        Debug.Log($"登录成功，用户ID：{userId}，用户名：{userName}");
        if (loginTipText != null)
        {
            loginTipText.text = $"登录成功，欢迎 {userName}！";
        }

        if (unlockAchievement)
        {
            TryUnlockLoginAchievement();
        }

        gameObject.SetActive(false); // 隐藏登录界面
    }

    private string ResolveUserId(TapTapAccount account)
    {
        return account == null
            ? (TapTapSdk.Instance?.CurrentUserId ?? "unknown")
            : (!string.IsNullOrEmpty(account.openId) ? account.openId : account.unionId);
    }

    private string ResolveUserName(TapTapAccount account)
    {
        return account == null
            ? (TapTapSdk.Instance?.CurrentUserName ?? "TapTap用户")
            : (string.IsNullOrEmpty(account.name) ? "TapTap用户" : account.name);
    }

    private void TryUnlockLoginAchievement()
    {
        if (AchievementManager.Instance == null) return;
        if (!AchievementManager.Instance.HasAchievement(LoginAchievementId)) return;
        AchievementManager.Instance.UnlockCustomAchievement(LoginAchievementId);
    }


    private void OnLoginFailure(Exception exception)
    {
        TapTapSdk.Instance?.ClearLoggedInAccount();
        Debug.Log($"登录失败，出现异常：{exception}");
        if (loginButton != null) loginButton.interactable = true;
        if (loginTipText != null) loginTipText.text = $"登录失败，出现异常：{exception.Message}";
    }


    private void OnLoginCancel()
    {
        TapTapSdk.Instance?.ClearLoggedInAccount();
        Debug.Log("登录被用户取消");
        if (loginButton != null) loginButton.interactable = true;
        if (loginTipText != null) loginTipText.text = "登录被取消";
    }

    private void OnDestroy()
    {
        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(OnLoginButtonClicked);
        }
    }
}
