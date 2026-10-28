using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Slime.Core;
using TapSDK.Achievement;
using TapSDK.Core;
using TapSDK.Login;
using UnityEngine;

namespace Slime.Game
{
    /// <summary>
    /// TapTap 联网能力封装：登录 + 成就。这是本工程唯一的联网路径。
    /// 正式包只提供 TapTap 登录，不做任何游客降级——登录失败就停在登录页重试。
    /// 编辑器/开发包额外留了一个仅用于离线调试的本地身份（见 DebugSignIn）。
    /// </summary>
    public sealed class TapService
    {
        private const string ClientId = "irmjeyzoxpztwlne5z";
        private const string ClientToken = "kXbnn4wKsOA4Rcd5wPLnEWLIgecN8pW5Dpk6ov2E";
        private const string ClientPublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA7Pfrav0TTq4fHVkLrj/IJd/q/lpVJGeZ7jWVIgjeW9CeKi8zs46Uk+9Jyzd3Jmc8xG/sUb0gS2ZGMHSuZNHXV+IhC4MD2nqjW68yEuCGbgWuzkebFPGRsRwAFLk6MhsUoW+30f9TCHB5w/qnsmEwcXiko5H8+Gjp+vRCY4/ojTXBHpAegm7lqTh2cL15nYuzNdCZEZ6cqVNkJkLSgkkevq1rLZknznHZpymYlGCqHYcVsR1kJBcIL+kE/rqxHihOUILEZSstbHD8Ru8NZDieaP+Sz76t0f/3aqWOiJbWPEngofvOSEpdJaiGzoc2m6DTAsmErIZMZgiJ80uztVi/lQIDAQAB";

        public bool SdkReady { get; private set; }
        public bool IsLoggedIn { get; private set; }
        public string UnionId { get; private set; } = string.Empty;
        public string Nickname { get; private set; } = string.Empty;
        public string AvatarUrl { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;

        /// <summary>编辑器/开发包专用的本地入口是否已启用。</summary>
        public bool LocalDebugMode { get; private set; }

        /// <summary>可以进入主界面：要么真的登录了，要么是编辑器调试身份。</summary>
        public bool CanEnterGame { get { return IsLoggedIn || LocalDebugMode; } }

        /// <summary>界面上显示的称呼：没登录时给一个默认名，别让名牌空着。</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Nickname))
                {
                    return Nickname;
                }

                string saved = SaveService.Current != null ? SaveService.Current.nickname : string.Empty;
                return string.IsNullOrEmpty(saved) ? "史莱姆训练家" : saved;
            }
        }

        private AchievementBridge bridge;

        public void Initialize()
        {
            if (SdkReady)
            {
                return;
            }

            try
            {
                var core = new TapTapSdkOptions
                {
                    clientId = ClientId,
                    clientToken = ClientToken,
                    region = TapTapRegionType.CN,
                    clientPublicKey = ClientPublicKey,
                    screenOrientation = 1,
                    enableLog = false
                };

                var achievement = new TapTapAchievementOptions { enableToast = false };

                TapTapSDK.Init(core, new TapTapSdkBaseOptions[] { achievement });

                bridge = new AchievementBridge();
                TapTapAchievement.RegisterCallBack(bridge);

                SdkReady = true;
            }
            catch (Exception e)
            {
                SdkReady = false;
                LastError = e.Message;
                Debug.LogWarning("[Tap] SDK init failed, falling back to guest: " + e.Message);
            }
        }

        public void Login(Action<bool, string> completed)
        {
            LoginRoutine(completed);
        }

        private async void LoginRoutine(Action<bool, string> completed)
        {
            Initialize();

            if (!SdkReady)
            {
                LastError = string.IsNullOrEmpty(LastError) ? "TapSDK 不可用" : LastError;
                completed?.Invoke(false, LastError);
                return;
            }

            try
            {
                var scopes = new List<string> { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE };
                TapTapAccount account = await TapTapLogin.Instance.LoginWithScopes(scopes.ToArray());
                ApplyAccount(account);
                completed?.Invoke(true, string.Empty);
            }
            catch (TaskCanceledException)
            {
                IsLoggedIn = false;
                LastError = "已取消授权";
                completed?.Invoke(false, LastError);
            }
            catch (Exception e)
            {
                IsLoggedIn = false;
                LastError = e.Message;
                completed?.Invoke(false, LastError);
            }
        }

        /// <summary>尝试复用已有登录态，避免每次都弹授权。</summary>
        public void TryRestoreSession(Action<bool> done)
        {
            TryRestoreRoutine(done);
        }

        private async void TryRestoreRoutine(Action<bool> done)
        {
            Initialize();
            if (!SdkReady)
            {
                done?.Invoke(false);
                return;
            }

            try
            {
                var account = await TapTapLogin.Instance.GetCurrentTapAccount();
                if (account != null && !string.IsNullOrEmpty(account.unionId))
                {
                    ApplyAccount(account);
                    done?.Invoke(true);
                    return;
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }

            done?.Invoke(false);
        }

        private void ApplyAccount(TapTapAccount account)
        {
            IsLoggedIn = true;
            LocalDebugMode = false;
            UnionId = account != null ? account.unionId ?? string.Empty : string.Empty;
            Nickname = account != null ? account.name ?? string.Empty : string.Empty;
            AvatarUrl = account != null ? account.avatar ?? string.Empty : string.Empty;
            LastError = string.Empty;
        }

        /// <summary>登录成功后的本地落盘（昵称写进存档，下次冷启动也能显示）。</summary>
        public void RememberProfile()
        {
            if (!string.IsNullOrEmpty(Nickname))
            {
                SaveService.Current.nickname = Nickname;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 仅编辑器/开发包可用：跳过 TapTap 登录，用一个本地调试身份进入游戏。
        /// 正式包不会编译出这个方法，因此不存在任何绕过登录的路径。
        /// </summary>
        public void DebugSignIn()
        {
            IsLoggedIn = false;
            LocalDebugMode = true;
            UnionId = "editor-debug";
            Nickname = string.IsNullOrEmpty(SaveService.Current.nickname)
                ? "调试训练家"
                : SaveService.Current.nickname;
            AvatarUrl = string.Empty;
            LastError = string.Empty;
        }
#endif

        /// <summary>本会话内已经报过的成就。TapTap 侧没有查询接口，重复上报只能本地拦。</summary>
        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 上报成就。失败静默，不阻断游戏。
        ///
        /// TapTap 后台每个成就都有一个「成就步数」：步数 1 的调 Unlock，
        /// 多步的必须调 Increment 把进度补到满。对多步成就硬调 Unlock，
        /// 服务端会拒成 80020 并原样丢掉这次上报——本作的成就大量是
        /// 100 / 1 万 / 1 千万这类累计值，一律走 Unlock 等于全部上报失败。
        /// </summary>
        public void ReportAchievement(string achievementId, long totalSteps)
        {
            if (!SdkReady || !IsLoggedIn || string.IsNullOrEmpty(achievementId))
            {
                return;
            }

            if (!reported.Add(achievementId))
            {
                return;
            }

            try
            {
                if (totalSteps <= 1)
                {
                    TapTapAchievement.Unlock(achievementId: achievementId);
                    return;
                }

                // 本地只在进度达标后才解锁，所以这里一次补齐到满步即可。
                // step 是增量、类型是 int，超过上限的步数夹到上限（服务端到顶即解锁）。
                int step = totalSteps > int.MaxValue ? int.MaxValue : (int)totalSteps;
                TapTapAchievement.Increment(achievementId: achievementId, step: step);
            }
            catch (Exception e)
            {
                reported.Remove(achievementId);
                Debug.LogWarning("[Tap] achievement report failed: " + achievementId + " / " + e.Message);
            }
        }

        public void Shutdown()
        {
            try
            {
                if (bridge != null)
                {
                    TapTapAchievement.UnRegisterCallBack(bridge);
                }
            }
            catch (Exception)
            {
                // 关闭期异常忽略
            }

            bridge = null;
            SdkReady = false;
        }

        private sealed class AchievementBridge : ITapAchievementCallback
        {
            /// <summary>同一条失败只留一行日志，批量上报失败时否则会刷满整个 Console。</summary>
            private readonly HashSet<string> failed = new HashSet<string>(StringComparer.Ordinal);

            public void OnAchievementSuccess(int code, TapAchievementResult result)
            {
                Debug.Log("[Tap] achievement success, code = " + code);
            }

            public void OnAchievementFailure(string achievementId, int errorCode, string errorMsg)
            {
                if (!failed.Add(achievementId + "|" + errorCode))
                {
                    return;
                }

                // 80020 是无效请求：要么成就 ID 在后台不存在，要么后台记的「成就步数」
                // 与调用方式不符（多步成就必须 Increment）。这两种都只能在
                // 开发者中心核对，代码侧无从修正，所以把话说到点上。
                string hint = errorCode == TapTapAchievementConstants.INVALID_REQUEST
                    ? "（到开发者中心核对该成就的「成就步数」是否与 CSV 一致）"
                    : string.Empty;

                Debug.LogWarning("[Tap] achievement failure: " + achievementId + " (" + errorCode + ") " + errorMsg + hint);
            }
        }
    }
}
