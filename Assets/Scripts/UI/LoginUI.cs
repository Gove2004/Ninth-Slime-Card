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

    void Start()
    {
        loginButton.onClick.AddListener(OnLoginButtonClicked);
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


    private void OnLoginSuccess(TapTapAccount result)
    {
        TapTapSdk.Instance?.SetLoggedInAccount(result);
        string userId = result == null
            ? (TapTapSdk.Instance?.CurrentUserId ?? "unknown")
            : (!string.IsNullOrEmpty(result.openId) ? result.openId : result.unionId);
        string userName = result == null
            ? (TapTapSdk.Instance?.CurrentUserName ?? "TapTap用户")
            : (string.IsNullOrEmpty(result.name) ? "TapTap用户" : result.name);
        ApplyLoginSuccess(userId, userName, true);
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
}
