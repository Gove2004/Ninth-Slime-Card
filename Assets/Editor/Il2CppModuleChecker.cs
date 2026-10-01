using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android IL2CPP 构建需要 Unity Hub 里的「Windows Build Support (IL2CPP)」模块。
/// 该模块会把宿主 IL2CPP 工具链落在
/// &lt;Editor 内容根&gt;/PlaybackEngines/windowsstandalonesupport/Variations/il2cpp。
/// </summary>
public static class Il2CppModuleChecker
{
    /// <summary>
    /// 相对 EditorApplication.applicationContentsPath 的探测路径。
    ///
    /// 注意：Windows 上 applicationContentsPath 已经是「&lt;安装根&gt;/Editor/Data」，
    /// macOS 上是「Unity.app/Contents」——两者的 PlaybackEngines 都**直接**位于其下。
    /// 所以这里不能再写 "Editor/Data/" 前缀：旧代码多拼了一层，
    /// 会得到「.../Editor/Data/Editor/Data/PlaybackEngines/...」这种不存在的路径，
    /// 结果模块装好了也永远判定为缺失。
    /// </summary>
    private static readonly string[] ModuleProbePaths =
    {
        "PlaybackEngines/windowsstandalonesupport/Variations/il2cpp", // 模块落地：宿主 IL2CPP 工具链
        "il2cpp",                                                     // 兜底：部分安装把工具链直接放在内容根下
    };

    private static string VersionLabel => "Unity " + Application.unityVersion;

    private static string ContentsRoot => EditorApplication.applicationContentsPath;

    [MenuItem("Tools/Build/Check IL2CPP Module", priority = 0)]
    public static void CheckFromMenu()
    {
        bool ok = IsWindowsIl2CppModuleInstalled();
        EditorUtility.DisplayDialog(
            ok ? "IL2CPP 模块已就绪" : "IL2CPP 模块缺失",
            Describe(),
            "OK");

        if (!ok)
        {
            ShowInstallDialog();
        }
    }

    [MenuItem("Tools/Build/Install Windows IL2CPP Module (Unity Hub)", priority = 1)]
    public static void OpenInstallPage()
    {
        try
        {
            System.Diagnostics.Process.Start("unityhub://");
        }
        catch
        {
            // ignore
        }

        EditorUtility.DisplayDialog(
            "IL2CPP 模块安装",
            "请在 Unity Hub 中安装模块（必须手动操作一次）：\n\n" +
            "1. 打开 Unity Hub → Installs（安装）\n" +
            "2. 找到 " + VersionLabel + " → 右侧齿轮 → Add modules\n" +
            "   （务必选对版本：装在别的版本上，当前编辑器仍会报缺失）\n" +
            "3. 勾选「Windows Build Support (IL2CPP)」\n" +
            "4. 点 Done / Install，等待下载完成\n" +
            "5. 关闭并重新打开 Unity，再 Build Android\n\n" +
            "本工程编辑器装在：\n" + EditorApplication.applicationPath,
            "OK");
    }

    /// <summary>模块是否已安装（内容根 + 探测路径全部按平台正确拼接）。</summary>
    public static bool IsWindowsIl2CppModuleInstalled()
    {
        foreach (string relative in ModuleProbePaths)
        {
            if (Directory.Exists(Path.Combine(ContentsRoot, relative)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 诊断报告：把探测到的事实全摊开，避免再出现「明明装了却报没装」这种无法自证的情况。
    /// </summary>
    public static string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine(VersionLabel);
        sb.AppendLine("Editor: " + EditorApplication.applicationPath);
        sb.AppendLine("内容根: " + ContentsRoot);
        sb.AppendLine();
        sb.AppendLine("IL2CPP 模块探测:");
        foreach (string relative in ModuleProbePaths)
        {
            string full = Path.Combine(ContentsRoot, relative);
            sb.AppendLine("  " + (Directory.Exists(full) ? "[有] " : "[无] ") + relative);
        }

        sb.AppendLine();
        sb.AppendLine("Android 目标可用: " + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android));
        sb.AppendLine("Android 脚本后端: " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
        return sb.ToString();
    }

    public static void ShowInstallDialog()
    {
        bool openHub = EditorUtility.DisplayDialog(
            "缺少 IL2CPP 模块",
            "当前 Unity 未安装「Windows Build Support (IL2CPP)」，无法打 Android IL2CPP 包。\n\n" +
            "请任选一种方式安装：\n" +
            "1. Unity Hub → " + VersionLabel + " → Add modules → Windows Build Support (IL2CPP)\n" +
            "2. 菜单 Tools/Build/Install Windows IL2CPP Module 下载安装包\n\n" +
            "装好后用 Tools/Build/Check IL2CPP Module 复查（会列出实际探测的目录）。",
            "打开下载页",
            "我知道了");
        if (openHub)
        {
            OpenInstallPage();
        }
    }
}

/// <summary>
/// 只拦「Android + IL2CPP」这一种构建：Mono 后端或其它平台用不到该模块，不该被拦下。
/// </summary>
public class Il2CppModuleBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
        {
            return;
        }

        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
        {
            return;
        }

        if (Il2CppModuleChecker.IsWindowsIl2CppModuleInstalled())
        {
            return;
        }

        Il2CppModuleChecker.ShowInstallDialog();
        throw new BuildFailedException(
            "IL2CPP module not found for Editor " + Application.unityVersion
            + ". Probed under: " + EditorApplication.applicationContentsPath);
    }
}
