using System;
using System.Threading.Tasks;
using GoveKits.Runtime;
using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;
using HybridCLR;

public class Boot : MonoSingleton<Boot>
{
    private enum BootState
    {
        None,
        Initializing,
        UpdatingPackage,
        LoadingHotUpdate,
        LoadingGameServices,
        Failed
    }

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            SetState(BootState.Initializing);

            YooAssets.Initialize(new YooLogger());
            InitializeCoreServices();

            SetState(BootState.UpdatingPackage);
            await ResCore.PackageWorkflowAsync(
                new AutoHostPackageConfig
                (
                    "DefaultPackage",
                    "http://106.13.26.185:10413/NinthSlimeCard"
                ),
                new UpdateCallbacks
                {
                    OnCheckVersionBegin = OnCheckVersionBegin,
                    OnCheckVersionSuccess = OnCheckVersionSuccess,
                    OnCheckVersionFailed = OnCheckVersionFailed,
                    OnUpdateManifestBegin = OnUpdateManifestBegin,
                    OnUpdateManifestSuccess = OnUpdateManifestSuccess,
                    OnUpdateManifestFailed = OnUpdateManifestFailed,
                    OnDownloadBegin = OnDownloadBegin,
                    OnDownloadFileBegin = OnDownloadFileBegin,
                    OnDownloadUpdate = OnDownloadUpdate,
                    OnDownloadError = OnDownloadError,
                    OnDownloadFinish = OnDownloadFinish
                }
            );
        }
        catch (Exception e)
        {
            HandleStartupFailure("初始化失败", e);
        }
    }

    private void InitializeCoreServices()
    {
        LogCore.InfuseLogger(new UnityLogger());
        RandomCore.Initialize(new NormalRNG(Environment.TickCount));
        TimeCore.Initialize(16, 128);
        TimeCore.RigisterWheel(TimeCore.NormalWheelName, 0.05f, 512);
        TimeCore.RigisterWheel(TimeCore.UnscaledWheelName, 0.05f, 512);
    }

    private async Task ContinueStartupAsync()
    {
        if (startupContinuationStarted || state == BootState.Failed)
        {
            return;
        }

        startupContinuationStarted = true;

        try
        {
            SetState(BootState.LoadingHotUpdate);
            TipText = "正在加载热更新...";
            await LoadHotUpdateAsync();

            SetState(BootState.LoadingGameServices);
            TipText = "正在初始化游戏服务...";
            LoadAsset();

            ResCore.LoadSceneAsync("Login");
        }
        catch (Exception e)
        {
            HandleStartupFailure("加载失败", e);
        }
    }

    private void Update()
    {
        TimeCore.Update(TimeCore.NormalWheelName, Time.deltaTime);
        TimeCore.Update(TimeCore.UnscaledWheelName, Time.unscaledDeltaTime);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    private void OnApplicationQuit()
    {
        YooAssets.Destroy();
    }

    #endregion

    #region 热更新

    public void OnCheckVersionBegin()
    {
        TipText = "正在检查版本...";
        Progress = 0f;
    }

    public void OnCheckVersionSuccess(string version) => TipText = $"检查版本成功，版本号: {version}.";

    public void OnCheckVersionFailed(string error) => TipText = $"检查版本失败，错误信息: {error}.";

    public void OnUpdateManifestBegin() => TipText = "正在更新清单...";

    public void OnUpdateManifestSuccess() => TipText = "更新清单成功。";

    public void OnUpdateManifestFailed(string error) => TipText = $"更新清单失败，错误信息: {error}.";

    public void OnDownloadBegin(int totalCount, long totalBytes) => TipText = $"开始下载，文件数量: {totalCount}, 总大小: {totalBytes / 1024 / 1024:F2} MB.";

    public void OnDownloadFileBegin(DownloadFileData data) => Progress = 0f;

    public void OnDownloadUpdate(DownloadUpdateData data) => Progress = data.Progress;

    public void OnDownloadError(DownloadErrorData data) => TipText = $"下载错误，文件: {data.FileName}, 错误信息: {data.ErrorInfo}.";

    public void OnDownloadFinish(DownloaderFinishData data)
    {
        TipText = "下载完成...";
        Progress = 1f;
        _ = ContinueStartupAsync();
    }

    public async Task LoadHotUpdateAsync()
    {
        await HotfixCore.LoadAotMetadataAsync(AOTGenericReferences.PatchedAOTAssemblyList);
        await HotfixCore.LoadHotfixAssemblyAsync("HotUpdate.dll");
    }

    public void LoadAsset()
    {
        ConfigCore.InfuseParser(new JsonConfigParser());
        ConfigCore.InfuseParser(new CsvConfigParser());
        ConfigCore.Initialize();
        AudioCore.Initialize(16);
        SaveCore.Initialize(new JsonSerializer());
    }

    #endregion

    #region 字段

    private string tipText = "资源加载中...";
    public string TipText
    {
        get => tipText;
        set
        {
            tipText = value;
            if (tipTextUI != null)
            {
                tipTextUI.text = tipText;
            }
        }
    }

    private float progress;
    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            if (progressSliderUI != null)
            {
                progressSliderUI.value = progress;
            }

            if (progressTextUI != null)
            {
                progressTextUI.text = $"{(int)(progress * 100)}%";
            }
        }
    }

    private bool startupContinuationStarted;
    private BootState state = BootState.None;

    #endregion

    #region UI

    public GameObject loadingPanelUI;
    public TextMeshProUGUI tipTextUI;
    public TextMeshProUGUI progressTextUI;
    public Slider progressSliderUI;

    private void SetState(BootState nextState)
    {
        state = nextState;
    }

    private void HandleStartupFailure(string message, Exception exception)
    {
        state = BootState.Failed;
        TipText = message;
        Debug.LogException(exception);
        LogCore.Error(nameof(Boot), $"{message}: {exception}");
    }

    #endregion
}
