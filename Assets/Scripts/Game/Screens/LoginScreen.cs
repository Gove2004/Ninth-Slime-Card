using Slime.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>登录屏：联网登录（TapTap）唯一入口，无游客降级。只有登录与成就会触网。</summary>
    public sealed class LoginScreen : Screen
    {
        private TextMeshProUGUI status;
        private Button loginButton;
        private bool busy;

        private readonly bool skipAutoLogin;

        /// <param name="skipAutoLogin">
        /// 开发者面板预览用：跳过"复用登录态直接进主页"。已登录的机器上这一屏会一闪而过，
        /// 平时根本看不到排版。正式流程始终是默认值 false。
        /// </param>
        public LoginScreen(bool skipAutoLogin = false)
        {
            this.skipAutoLogin = skipAutoLogin;
        }

        protected override void Build()
        {
            // 左右各占半屏：左侧品牌居左半屏中心，右侧登录面板居中于右半屏，两侧留白一致。
            var left = Ui.Node("Left", Root);
            Ui.Anchor(left, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

            var glow = Ui.Panel("Glow", left, SpriteForge.Glow(256, Theme.Accent), Theme.WithAlpha(Color.white, 0.22f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 125f), new Vector2(640f, 640f));

            var slime = Ui.Panel("Slime", left, SpriteForge.Slime(256, Theme.Accent, Theme.BgDeep, 20091, true, SlimeFace.Happy), Color.white);
            Ui.Fixed(slime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 125f), new Vector2(300f, 300f));

            var title = Ui.Label("Title", left, "第九张史莱姆牌", Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            title.characterSpacing = 6f;
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(760f, 70f));

            var panel = Ui.Rounded("LoginPanel", Root, Theme.RadiusL, Theme.WithAlpha(Theme.Panel, 0.96f), Theme.Border, 1);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(480f, 0f), new Vector2(820f, 720f));

            var column = Ui.Node("Column", panel.transform);
            Ui.Stretch(column, 52f, 52f, 52f, 52f);
            Ui.Column(column, 18, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.MiddleCenter;

            var heading = Ui.Label("Heading", column, "开 始 牌 局", Theme.H2, Theme.Text, TextAlignmentOptions.Center);
            Ui.FixedSize(heading.gameObject, 0f, 56f);

            var spacer = Ui.Node("Spacer", column);
            Ui.FixedSize(spacer.gameObject, 0f, 24f);

            loginButton = Ui.TextButton("Login", column, "TapTap 登 录", Theme.H4, Theme.AccentDeep, Theme.Text, OnLogin);
            Ui.FixedSize(loginButton.gameObject, 0f, 92f);

            status = Ui.Label("Status", column, string.Empty, Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.FixedSize(status.gameObject, 0f, 56f);

            if (skipAutoLogin)
            {
                return;
            }

            App.Tap.TryRestoreSession(ok =>
            {
                if (ok && status != null)
                {
                    status.text = "已恢复登录态 · " + App.Tap.Nickname + "，正在进入…";
                    status.color = Theme.Accent;
                    Enter();
                }
            });
        }

        private void OnLogin()
        {
            if (busy)
            {
                return;
            }

            busy = true;
            loginButton.interactable = false;
            status.text = "正在唤起 TapTap…";
            status.color = Theme.TextDim;

            App.Tap.Login((ok, error) =>
            {
                busy = false;
                if (loginButton != null)
                {
                    loginButton.interactable = true;
                }

                if (ok)
                {
                    status.text = "登录成功 · " + App.Tap.Nickname;
                    status.color = Theme.Accent;
                    App.Tap.RememberProfile();
                    App.PersistMeta();
                    Enter();
                }
                else
                {
                    status.text = "登录未完成：" + error + "\n请重试，或检查设备网络与 TapTap 客户端。";
                    status.color = Theme.Danger;
                }
            });
        }

        private void Enter()
        {
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new HomeScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }
    }
}
