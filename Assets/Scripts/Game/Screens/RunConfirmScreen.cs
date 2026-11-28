using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 存档进程确认。**游戏进度信息只在这一屏出现**：
    /// 当前局是什么模式、打到第几层、还剩多少血。
    ///
    /// 同模式进来：继续 / 重开 / 返回；
    /// 跨模式进来：一行警示 + 开始新冒险 / 返回（换模式会放弃当前进度）。
    /// </summary>
    public sealed class RunConfirmScreen : Screen
    {
        private readonly bool wantEndless;

        public RunConfirmScreen(bool wantEndless)
        {
            this.wantEndless = wantEndless;
        }

        protected override void Build()
        {
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.38f));
            veil.raycastTarget = false;

            var title = Ui.Label("Title", Root, "存 档 进 程", Theme.H1, Theme.Text, TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -56f), new Vector2(600f, 74f));

            var underline = Ui.Solid("Underline", Root, Theme.WithAlpha(Theme.Accent, 0.85f));
            Ui.Fixed(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -146f), new Vector2(132f, 5f));

            var back = Ui.LinkButton("Back", Root, "返 回", Theme.Body, () => App.Pop());
            Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-72f, -62f), new Vector2(180f, 56f));

            var director = App.Director;
            bool sameMode = director.Endless == wantEndless;

            // ---- 进程三行：模式 / 章节·层 / 生命。全部大字，无 caption ----
            Color accent = director.Endless ? Theme.Gold : Theme.Accent;

            var mode = Ui.Label("Mode", Root, director.Endless ? "无 限 年" : "冒 险",
                Theme.H2, accent, TextAlignmentOptions.Center);
            mode.characterSpacing = 6f;
            Ui.Fixed(mode.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f), new Vector2(600f, 56f));

            var progress = Ui.Label("Progress", Root,
                director.ChapterName + " · 第 " + (director.Floor + 1) + " 层",
                Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            Ui.Outline(progress.gameObject, new Color(0f, 0f, 0f, 0.5f), 2.5f);
            Ui.Fixed(progress.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 84f), new Vector2(1000f, 74f));

            var hp = Ui.Label("Hp", Root, director.Hp + " / " + director.HpMax,
                Theme.H2, Theme.Hp, TextAlignmentOptions.Center);
            Ui.Fixed(hp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 12f), new Vector2(500f, 56f));

            // ---- 行动 ----
            if (sameMode)
            {
                var cont = Ui.PrimaryButton("Continue", Root, "继 续 冒 险", Theme.H3, Continue);
                Ui.Fixed(cont.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -130f), new Vector2(420f, 96f));

                var restart = Ui.TextButton("Restart", Root, "重 开 一 局", Theme.H3,
                    Theme.PanelRaised, Theme.Text, ConfirmRestart);
                Ui.Fixed(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-160f, -280f), new Vector2(280f, 76f));

                var home = Ui.LinkButton("Home", Root, "返 回", Theme.Body, () => App.Pop());
                Ui.Fixed(home.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(160f, -280f), new Vector2(180f, 56f));
            }
            else
            {
                // 换模式 = 当前进度不保。警示一行用 H4（不是小字），颜色用警示红。
                var warn = Ui.Label("Warn", Root, "开始将放弃当前进度",
                    Theme.H4, Theme.Danger, TextAlignmentOptions.Center);
                Ui.Fixed(warn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -100f), new Vector2(800f, 40f));

                var start = Ui.PrimaryButton("Start", Root, "开 始 新 冒 险", Theme.H3, StartFresh);
                Ui.Fixed(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -210f), new Vector2(420f, 96f));

                var home = Ui.LinkButton("Home", Root, "返 回", Theme.Body, () => App.Pop());
                Ui.Fixed(home.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -330f), new Vector2(180f, 56f));
            }
        }

        private void Continue()
        {
            App.Audio.PlaySfx("ui_click");
            App.Replace(new ChapterScreen());
        }

        private void ConfirmRestart()
        {
            // 重开会清掉当前局，属于不可撤回操作，必须二次确认
            App.ShowConfirm("重开一局会放弃当前进度\n（奖杯与图鉴记录保留）", "确认重开", "取消", StartFresh);
        }

        private void StartFresh()
        {
            App.Audio.PlaySfx("reward");
            App.Director.StartNewRun(wantEndless);
            App.PersistMeta();
            App.Toast("新的冒险开始了");
            App.Replace(new ChapterScreen());
        }
    }
}
