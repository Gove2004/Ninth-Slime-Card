using System;
using System.Threading.Tasks;
using TapSDK.Login;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    private const string LoginAchievementId = "test";
    public Button loginButton;
    public TextMeshProUGUI loginTipText;

    void Start()
    {
        loginButton.onClick.AddListener(OnLoginButtonClicked);

        _ = ChackLoginToken();
    }

    public void OnLoginButtonClicked()
    {
        // 这里可以调用 TapTapLogin.Instance.Login() 来触发登录流程
        // 登录成功后，TapTapLogin 会通过回调通知登录结果
        loginButton.interactable = false; // 禁用登录按钮，防止重复点击
        _ = TapTapSdk.Instance.LoginAsync(OnLoginSuccess, OnLoginCancel, OnLoginFailure); // 确保 SDK 已经初始化
        Debug.Log("登录按钮被点击，正在触发登录流程...");
    }


    private async Task ChackLoginToken()
    {
        TapTapSdk.Instance?.Initialize();
        TapTapAccount account = await TapTapLogin.Instance.GetCurrentTapAccount();
        if (account == null) {
            // 用户未登录
            Debug.Log("用户未登录");
        } else {
            // 用户已登录
            OnLoginSuccess(account);
        }
    }


    private void OnLoginSuccess(TapTapAccount result)
    {
        Debug.Log($"登录成功，用户ID：{result.openId}，用户名：{result.name}");
        loginTipText.text = $"登录成功，欢迎 {result.name}！";
        TryUnlockLoginAchievement();

        this.gameObject.SetActive(false); // 隐藏登录界面
    }

    private void TryUnlockLoginAchievement()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockCustomAchievement(LoginAchievementId);
            return;
        }

        if (!TapTapSdk.IsInitialized) return;

        try
        {
            TapTapSdk.Instance.IncrementAchievement(LoginAchievementId, 3);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"登录成就发放失败：{exception.Message}");
        }
    }


    private void OnLoginFailure(Exception exception)
    {
        // 登录失败，errorCode 和 errorMsg 提供错误信息
        Debug.Log($"登录失败，出现异常：{exception}");
        loginTipText.text = $"登录失败，出现异常：{exception.Message}";
    }


    private void OnLoginCancel()
    {
        // 登录被用户取消
        Debug.Log("登录被用户取消");
        loginTipText.text = "登录被取消";
    }
}
