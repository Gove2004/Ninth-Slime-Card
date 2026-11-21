using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 主界面。编辑式排版：**没有任何面板底**，所有元素直接落在场景背景上。
    /// 信息量刻意收窄到「做决定所需的最小集」：这一局到哪了、下一个对手是谁、
    /// 还剩多少血。身份与货币不在这里——它们在商店/图鉴里有专位，主页摆出来只是噪音。
    ///
    ///   ① 舞台：左栏文字（章节 → 标题 → 对手 → 血条 → CTA）共用一条左基线，
    ///      右侧对手立绘浮在背景上，一圈系列色光晕给落点
    ///   ② 页脚：四个纯文字导航入口，无按钮壳
    /// </summary>
    public sealed class HomeScreen : Screen
    {
        /// <summary>左栏文字块的公共左缘。</summary>
        private const float LeftX = -860f;

        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            // 均匀暗幕只压背景不动 UI：左栏小字与对手名压在城堡剪影上时对比度不够
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.38f));
            veil.raycastTarget = false;

            BuildStage();
            BuildFooter();

            // 新春装饰最后挂到 Root（渲染在最上层），只占屏幕四角的空地，
            // 左栏与立绘所在的中轴区不碰；窗口外或玩家在设置里关掉就整层不建。
            if (Festival.IsActive)
            {
                BuildFestivalDecor();
            }
        }

        /// <summary>
        /// 新春装饰：左右春联、檐口双灯笼、顶部福字。Art/Decor 真图优先，缺图回退 SpriteForge 程序化。
        /// 只占屏幕四角的空地，左栏与立绘所在的中轴区一个像素都不碰。
        /// </summary>
        private void BuildFestivalDecor()
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

            // ---- 春联：左右两条红底金字的竖批 ----
            BuildCouplet(-878f, "辞\n旧\n迎\n新", 0.05f);
            BuildCouplet(878f, "万\n事\n胜\n意", 0.14f);

            // ---- 双灯笼：挂在檐口（屏幕顶缘），带垂绳 ----
            BuildLantern(-640f, 0f);
            BuildLantern(640f, 0.12f);

            // ---- 福字：一枚红底金字的菱印 ----
            var fuArt = ArtLib.Decor("fu");
            RectTransform fu;
            if (fuArt != null)
            {
                var fuImg = Ui.Panel("Fu", Root, fuArt, Color.white);
                fuImg.raycastTarget = false;
                fuImg.preserveAspect = true;
                fu = fuImg.rectTransform;
            }
            else
            {
                var plaque = Ui.Rounded("Fu", Root, Theme.RadiusS,
                    Theme.WithAlpha(Theme.Darken(red, 0.2f), 0.94f), Theme.WithAlpha(gold, 0.85f), 2);
                var fuText = Ui.Label("FuText", plaque.transform, "福", Theme.H3, gold, TextAlignmentOptions.Center);
                Ui.Fixed(fuText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(130f, 130f));
                fuText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                fu = plaque.rectTransform;
                fu.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 福字：当左栏章节行末尾的「落款印章」
            Ui.Fixed(fu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320f, 240f), new Vector2(84f, 84f));
            Tween.PopIn(fu, 0.32f, 0.5f, 0.4f);
        }

        private void BuildCouplet(float x, string verticalText, float delay)
        {
            Color red = Theme.SeriesSpring;
            var scrollArt = ArtLib.Decor("couplet");

            RectTransform strip;
            if (scrollArt != null)
            {
                // 真图含上下木轴，整体略宽于纸面；竖向轻拉到 560 高，装饰条上看不出变形
                var img = Ui.Panel("Couplet", Root, scrollArt, Color.white);
                img.raycastTarget = false;
                strip = img.rectTransform;
                Ui.Fixed(strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 60f), new Vector2(150f, 560f));
            }
            else
            {
                var rounded = Ui.Rounded("Couplet", Root, Theme.RadiusS,
                    Theme.WithAlpha(Theme.Darken(red, 0.15f), 0.94f), Theme.WithAlpha(Theme.Gold, 0.6f), 2);
                strip = rounded.rectTransform;
                Ui.Fixed(strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 60f), new Vector2(64f, 560f));
            }
            Tween.SlideIn(strip, new Vector2(x > 0 ? 120f : -120f, 0f), 0.42f, delay);

            // 字永远由 UI 叠：AI 写中文常出错别字，真图只画空白纸面
            var text = Ui.Label("Text", strip.transform, verticalText, Theme.Body,
                Theme.Gold, TextAlignmentOptions.Center);
            Ui.Stretch(text.rectTransform, 4f, 8f, 4f, 8f);
            Tween.FadeIn(text, 0.4f, delay + 0.16f, 0f, 1f);
        }

        private void BuildLantern(float x, float delay)
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

            // 垂绳：从屏幕顶缘垂到灯笼
            var rope = Ui.Solid("Rope", Root, Theme.WithAlpha(gold, 0.55f));
            rope.raycastTarget = false;
            Ui.Fixed(rope.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -34f), new Vector2(2.5f, 68f));

            var lanternArt = ArtLib.Decor("lantern");
            var lantern = Ui.Panel("Lantern", Root,
                lanternArt != null
                    ? lanternArt
                    : SpriteForge.Lantern(160, red, gold),
                Color.white);
            lantern.raycastTarget = false;
            // 真图自带流苏与挂绳头，整体比程序化版大一号、略往下沉
            Ui.Fixed(lantern.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, lanternArt != null ? -150f : -122f),
                lanternArt != null ? new Vector2(160f, 160f) : new Vector2(124f, 148f));
            lantern.preserveAspect = true;
            Tween.SlideIn(lantern.rectTransform, new Vector2(0f, 110f), 0.5f, delay);
        }

        // ---------------------------------------------------------------- 舞台

        /// <summary>
        /// 左栏文字共一条左基线（距底坐标）：
        ///   236  章节行（小字 + 福印落点）
        ///   158  大标题（H1）
        ///   108  系列色短划线
        ///    58  对手名（系列色）
        ///    -2  细血条 + 读数
        ///  -122  主 CTA（实心金块 400×92）
        /// 右侧立绘 430 见方，中心 (460, 40)，光晕垫底。
        /// </summary>
        private void BuildStage()
        {
            var director = App.Director;
            var info = director.NextEnemyPreview;
            Color accent = Theme.SeriesColor(info.Accent);

            // ---- 右侧：对手立绘直接浮在背景上 ----
            var glow = Ui.Panel("Glow", Root, SpriteForge.Glow(256, accent),
                Theme.WithAlpha(Color.white, 0.32f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(460f, 40f), new Vector2(600f, 600f));

            var portrait = Ui.Panel("Enemy", Root, null, Color.white);
            Sprite foeArt = ArtLib.Monster(info.Sprite);
            portrait.sprite = foeArt != null
                ? foeArt
                : SpriteForge.Slime(256, accent, Theme.BgDeep, info.Seed, true, SlimeFace.Fierce);
            portrait.preserveAspect = true;
            Ui.Fixed(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(460f, 40f), new Vector2(430f, 430f));
            Tween.PopIn(portrait.rectTransform, 0.10f, 0.85f);

            // ---- 左栏：编辑式排版 ----
            var chapter = Ui.Label("Chapter", Root,
                director.ChapterName + " · 第 " + (director.Floor + 1) + " 层",
                Theme.Caption, Theme.TextDim, TextAlignmentOptions.Left);
            chapter.characterSpacing = 3f;
            Ui.Outline(chapter.gameObject, new Color(0f, 0f, 0f, 0.65f), 2.5f);
            Ui.Fixed(chapter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, 236f), new Vector2(900f, 34f));

            var title = Ui.Label("Title", Root, director.ChapterSubtitle, Theme.H1, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, 158f), new Vector2(960f, 74f));
            Tween.SlideIn(title.rectTransform, new Vector2(-50f, 0f), 0.32f, 0.05f);

            var rule = Ui.Solid("Rule", Root, Theme.WithAlpha(accent, 0.9f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, 108f), new Vector2(72f, 4f));

            var foe = Ui.Label("Foe", Root, info.Name, Theme.H3, accent, TextAlignmentOptions.Left);
            Ui.Outline(foe.gameObject, new Color(0f, 0f, 0f, 0.7f), 3f);
            Ui.Fixed(foe.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, 58f), new Vector2(600f, 40f));

            // 细血条：一条线 + 一个读数，不再做成大板
            float hpPct = director.HpMax <= 0 ? 0f : (float)director.Hp / director.HpMax;
            const float hpW = 340f;
            var hpTrack = Ui.Rounded("HpTrack", Root, 6,
                Theme.WithAlpha(Theme.PanelSunken, 0.96f), Theme.BorderSoft, 1);
            Ui.Fixed(hpTrack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, -2f), new Vector2(hpW, 14f));

            var hpFill = Ui.Rounded("HpFill", hpTrack.transform, 5,
                hpPct > 0.5f ? Theme.Hp : Theme.Danger, Color.clear, 0);
            Ui.Fixed(hpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(Mathf.Max(6f, (hpW - 4f) * hpPct), 10f));

            var hpText = Ui.Label("HpText", Root, director.Hp + " / " + director.HpMax,
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Outline(hpText.gameObject, new Color(0f, 0f, 0f, 0.6f), 2.5f);
            Ui.Fixed(hpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX + hpW + 24f, -2f), new Vector2(220f, 32f));

            // 主 CTA：整屏唯一的实心亮色块
            var start = Ui.PrimaryButton("Start", Root, "继 续 冒 险", Theme.H3, StartRun);
            Ui.Fixed(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX, -122f), new Vector2(400f, 92f));
        }

        // ---------------------------------------------------------------- 页脚

        /// <summary>页脚：四个纯文字导航入口，无按钮壳，靠间距读成一行而不是四个格子。</summary>
        private void BuildFooter()
        {
            const float step = 256f;
            float cx = -step * 1.5f;   // 最左入口的中心

            FooterLink("Codex", "牌 库", cx, () => App.Push(new CodexScreen(CodexScreen.Tab.Cards)));
            FooterLink("Endless", "无限年", cx + step, ConfirmEndless);
            FooterLink("Settings", "设 置", cx + step * 2f, () => App.Push(new SettingsScreen()));
            FooterLink("Restart", "重开一局", cx + step * 3f, RestartRun);
        }

        private void FooterLink(string name, string caption, float x, System.Action onClick)
        {
            var btn = Ui.LinkButton(name, Root, caption, Theme.Body, onClick);
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -462f), new Vector2(200f, 56f));
        }

        // ---------------------------------------------------------------- 行为

        private void StartRun()
        {
            App.Audio.PlaySfx("ui_click");
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new MapScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        private void RestartRun()
        {
            // 重开会清掉当前局，属于不可撤回操作，必须二次确认
            App.ShowConfirm("重开一局会放弃当前进度\n（奖杯与图鉴记录保留）", "确认重开", "取消", () =>
            {
                App.Audio.PlaySfx("reward");
                App.Director.StartNewRun();
                App.PersistMeta();
                App.Toast("已重开一局");
                App.Replace(new HomeScreen());
            });
        }

        private void ConfirmEndless()
        {
            // 手上有一局没打完才需要警告；空档期直接进
            if (App.Director.DeckCount > 0)
            {
                App.ShowConfirm("进入无限年会放弃当前进度\n（奖杯与图鉴记录保留）", "进入", "取消", StartEndless);
                return;
            }

            StartEndless();
        }

        private void StartEndless()
        {
            App.Audio.PlaySfx("reward");
            App.Director.StartNewRun(true);
            App.PersistMeta();
            App.Toast("无限年 · 第 1 层");
            App.Replace(new HomeScreen());
        }
    }
}
