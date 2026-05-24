using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GoveKits.Runtime;
using GoveKits.Runtime.Storage;

public class Login : MonoBehaviour
{
    private enum LoginState
    {
        WaitingForLogin,
        LoggingIn,
        ReadyToEnter,
        EnteringHome
    }

    public GameObject loginPanelUI;
    public Button startButtonUI;
    public TextMeshProUGUI loginTipTextUI;

    private string loginTipText = string.Empty;
    public string LoginTipText
    {
        get => loginTipText;
        set
        {
            loginTipText = value;
            if (loginTipTextUI != null)
            {
                loginTipTextUI.text = loginTipText;
            }
        }
    }

    private LoginState state = LoginState.WaitingForLogin;
    private bool sceneLoading;

    private void Awake()
    {
        ValidateReferences();
        BindUI();
    }

    private void OnEnable()
    {
        state = LoginState.WaitingForLogin;
        sceneLoading = false;
        LoginTipText = "请先登录 TapTap 账号。";
        RefreshButtonState();
    }

    private void OnDestroy()
    {
        if (startButtonUI != null)
        {
            startButtonUI.onClick.RemoveListener(OnStartButtonClicked);
        }
    }

    public void OnStartButtonClicked()
    {
        if (state == LoginState.WaitingForLogin)
        {
            _ = LoginAsync();
            return;
        }

        if (state != LoginState.ReadyToEnter)
        {
            return;
        }

        if (sceneLoading)
        {
            return;
        }

        sceneLoading = true;
        state = LoginState.EnteringHome;
        RefreshButtonState();
        LoginTipText = "正在进入游戏...";
        ResCore.LoadSceneAsync("Home");
    }

    private async Task LoginAsync()
    {
        if (state != LoginState.WaitingForLogin)
        {
            return;
        }

        state = LoginState.LoggingIn;
        RefreshButtonState();
        LoginTipText = "正在登录 TapTap...";

#if UNITY_EDITOR
        await Task.Yield();
        state = LoginState.ReadyToEnter;
        LoginTipText = "编辑器模式已跳过登录，点击按钮进入游戏。";
        RefreshButtonState();
#else
        TapTapSdk.LoginResult result = await TapTapSdk.LoginAsync(
            onLoginSuccess: account => LoginTipText = $"登录成功，欢迎 {account.name}。",
            onLoginCancel: () => LoginTipText = "已取消登录，请重试。",
            onLoginError: exception => LoginTipText = $"登录失败: {exception.Message}"
        );

        if (result.IsSuccess)
        {
            state = LoginState.ReadyToEnter;
            RefreshButtonState();
            return;
        }

        state = LoginState.WaitingForLogin;
        RefreshButtonState();
#endif
    }

    private void BindUI()
    {
        startButtonUI.onClick.AddListener(OnStartButtonClicked);
        LoginTipText = loginTipText;
        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        if (startButtonUI == null)
        {
            return;
        }

        startButtonUI.interactable = state == LoginState.WaitingForLogin || state == LoginState.ReadyToEnter;
    }

    private void ValidateReferences()
    {
        if (loginPanelUI == null)
        {
            throw new InvalidOperationException("Login 缺少 loginPanelUI 引用。");
        }

        if (startButtonUI == null)
        {
            throw new InvalidOperationException("Login 缺少 startButtonUI 引用。");
        }

        if (loginTipTextUI == null)
        {
            throw new InvalidOperationException("Login 缺少 loginTipTextUI 引用。");
        }
    }
}
