using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 主界面 = 标题屏。**零进度信息**——这一局打到哪、对手是谁、剩多少血，
    /// 全部归「存档进程确认」屏；模式归「模式选择」屏。
    /// 主界面只回答一个问题：这是什么游戏，从哪进。
    ///
    ///   标题（居中大字 + 系列色短划线）→ 进入游戏（实心金块）→ 页脚两个文字入口
    /// </summary>
    public sealed class HomeScreen : Screen
    {
        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            // 均匀暗幕只压背景不动 UI
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.38f));
            veil.raycastTarget = false;

            var title = Ui.Label("Title", Root, "第九张史莱姆牌", 72, Theme.Text, TextAlignmentOptions.Center);
            title.characterSpacing = 10f;
            Ui.Outline(title.gameObject, new Color(0f, 0f, 0f, 0.6f), 3f);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(1200f, 96f));
            Tween.PopIn(title.rectTransform, 0.10f, 0.6f, 0.32f);
            Tween.FadeIn(title, 0.30f, 0.10f, 0f, 1f);

            var rule = Ui.Solid("Rule", Root, Theme.WithAlpha(Theme.Accent, 0.9f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 116f), new Vector2(88f, 5f));
            Tween.Scale(rule.rectTransform, 0f, 1f, 0.3f, Easing.OutBack, null, 0.42f);

            var start = Ui.PrimaryButton("Start", Root, "进 入 游 戏", Theme.H3, EnterGame);
            Ui.Fixed(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(420f, 96f));
            Tween.PopIn(start.GetComponent<RectTransform>(), 0.30f, 0.8f, 0.32f);

            BuildFooter();

            // 新春装饰最后挂到 Root（渲染在最上层），只占屏幕四角的空地。
            if (Festival.IsActive)
            {
                BuildFestivalDecor();
            }
        }

        /// <summary>页脚：纯文字导航，无按钮壳。</summary>
        private void BuildFooter()
        {
            const float step = 256f;

            FooterLink("Codex", "牌 库", -step * 0.5f, () => App.Push(new CodexScreen(CodexScreen.Tab.Cards)));
            FooterLink("Settings", "设 置", step * 0.5f, () => App.Push(new SettingsScreen()));
        }

        private void FooterLink(string name, string caption, float x, System.Action onClick)
        {
            var btn = Ui.LinkButton(name, Root, caption, Theme.Body, onClick);
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -462f), new Vector2(200f, 56f));
        }

        // ---------------------------------------------------------------- 行为

        private void EnterGame()
        {
            App.Audio.PlaySfx("ui_click");
            App.Push(new ModeSelectScreen());
        }

        // ---------------------------------------------------------------- 新春装饰

        private void BuildFestivalDecor()
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

            BuildCouplet(-878f, "辞\n旧\n迎\n新", 0.05f);
            BuildCouplet(878f, "万\n事\n胜\n意", 0.14f);
            BuildLantern(-640f, 0f);
            BuildLantern(640f, 0.12f);

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

            Ui.Fixed(fu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320f, 150f), new Vector2(84f, 84f));
            Tween.PopIn(fu, 0.32f, 0.5f, 0.4f);
        }

        private void BuildCouplet(float x, string verticalText, float delay)
        {
            Color red = Theme.SeriesSpring;
            var scrollArt = ArtLib.Decor("couplet");

            RectTransform strip;
            if (scrollArt != null)
            {
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

            var text = Ui.Label("Text", strip.transform, verticalText, Theme.Body,
                Theme.Gold, TextAlignmentOptions.Center);
            Ui.Stretch(text.rectTransform, 4f, 8f, 4f, 8f);
            Tween.FadeIn(text, 0.4f, delay + 0.16f, 0f, 1f);
        }

        private void BuildLantern(float x, float delay)
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

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
            Ui.Fixed(lantern.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, lanternArt != null ? -150f : -122f),
                lanternArt != null ? new Vector2(160f, 160f) : new Vector2(124f, 148f));
            lantern.preserveAspect = true;
            Tween.SlideIn(lantern.rectTransform, new Vector2(0f, 110f), 0.5f, delay);
        }
    }
}
