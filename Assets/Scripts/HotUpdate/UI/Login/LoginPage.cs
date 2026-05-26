

using System;
using System.Threading.Tasks;
using GoveKits.Runtime.Storage;
using TapSDK.Login;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPage : MonoBehaviour
{
    private bool isLoggingIn = false;
    public Button startButtonUI;
    public TextMeshProUGUI loginTipTextUI;


    public void Start()
    {
        startButtonUI.onClick.AddListener(OnLoginButtonClicked);

        TapTapCore.Initialize();

        _ = ChackLoginToken();
    }


    private async Task ChackLoginToken()
    {
        TapTapAccount account = await TapTapLogin.Instance.GetCurrentTapAccount();

        if (account == null)
        {
            // 用户未登录
            loginTipTextUI.text = "请先登录 TapTap 账号";
            MessageToastManager.Instance.ShowMessage("请先登录 TapTap 账号");
        }
        else
        {
            // 用户已登录
            OnLoginSuccess(account);
        }
    }


    public void OnLoginButtonClicked()
    {
        if (isLoggingIn)
        {
            // 如果已经在登录流程中，直接进入下一场景
            ResCore.LoadSceneAsync("Home");
            return;
        }

#if UNITY_EDITOR
        // 编辑器模式下直接跳过登录流程
        OnLoginSuccess(new TapTapAccount());
        // _ = TapTapCore.LoginAsync(OnLoginSuccess, OnLoginCancel, OnLoginFailure); // 确保 SDK 已经初始化
#else
        startButtonUI.interactable = false; // 禁用登录按钮，防止重复点击
        _ = TapTapCore.LoginAsync(OnLoginSuccess, OnLoginCancel, OnLoginFailure); // 确保 SDK 已经初始化
#endif
    }

    private void OnLoginSuccess(TapTapAccount result)
    {
        loginTipTextUI.text = $"登录成功，欢迎 {result.name}！";
        MessageToastManager.Instance.ShowMessage($"登录成功，欢迎 {result.name}！");

        isLoggingIn = true;
        startButtonUI.interactable = true; // 重新启用登录按钮，允许用户进入下一场景

        // 将账户信息保存到 GameCore 中，供后续使用
        GameCore.SetAccount(result);
    }

    private void OnLoginFailure(Exception exception)
    {
        // 登录失败，errorCode 和 errorMsg 提供错误信息
        loginTipTextUI.text = $"登录失败，出现异常：{exception.Message}";
        MessageToastManager.Instance.ShowMessage($"登录失败，出现异常：{exception.Message}");

        startButtonUI.interactable = true; // 重新启用登录按钮，允许用户重试
    }


    private void OnLoginCancel()
    {
        // 登录被用户取消
        loginTipTextUI.text = "登录被取消";
        MessageToastManager.Instance.ShowMessage("登录被取消");

        startButtonUI.interactable = true; // 重新启用登录按钮，允许用户重试
    }
}