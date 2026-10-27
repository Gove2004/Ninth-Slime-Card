using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Slime.DevTools
{
    /// <summary>
    /// 命令行构建入口：<c>Unity.exe -batchmode -quit -executeMethod Slime.DevTools.CliBuild.Android</c>
    ///
    /// 走 batchmode 而不是 MCP 的 execute_code，是因为 MCP 触发时 bee 派发给 clang 的子进程
    /// 会被创建后挂起（CPU 恒为 0、线程 Suspended），构建永远停在最后几个编译目标上。
    /// 同样的 clang 命令行手动跑只要 0.2 秒 —— 问题在派发链而不在编译器。
    /// </summary>
    public static class CliBuild
    {
        [MenuItem("Tools/Build/Android (CLI)", priority = 20)]
        public static void Android()
        {
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled)
                {
                    scenes.Add(s.path);
                }
            }

            if (scenes.Count == 0)
            {
                Fail("没有启用的构建场景");
                return;
            }

            // 签名口令只在构建进程内设，不写进 ProjectSettings（那是要提交的文件）。
            // 用的是 Android 官方 debug keystore，口令本来就是公开的 android/android。
            PlayerSettings.Android.useCustomKeystore = true;
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
            {
                PlayerSettings.Android.keystorePass = "android";
            }

            if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                PlayerSettings.Android.keyaliasPass = "android";
            }

            string output = EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var sw = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            sw.Stop();

            BuildSummary sum = report.summary;
            UnityEngine.Debug.Log("[CliBuild] result=" + sum.result
                + " output=" + sum.outputPath
                + " sizeMB=" + (sum.totalSize / 1048576.0).ToString("F1")
                + " errors=" + sum.totalErrors
                + " warnings=" + sum.totalWarnings
                + " elapsed=" + sw.Elapsed.TotalMinutes.ToString("F1") + "min");

            if (sum.result != BuildResult.Succeeded)
            {
                Fail("构建失败：" + sum.result);
            }
        }

        private static void Fail(string message)
        {
            UnityEngine.Debug.LogError("[CliBuild] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
