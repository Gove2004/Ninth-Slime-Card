using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 模式选择。两块大选项卡：冒险 / 无限年。
    /// 只回答一个问题——玩哪种。进度信息不在这（归「存档进程确认」），说明文字不在这（没有说明文字）。
    /// </summary>
    public sealed class ModeSelectScreen : Screen
    {
        protected override void Build()
        {
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.38f));
            veil.raycastTarget = false;

            var title = Ui.Label("Title", Root, "选 择 模 式", Theme.H1, Theme.Text, TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -56f), new Vector2(600f, 74f));

            var underline = Ui.Solid("Underline", Root, Theme.WithAlpha(Theme.Accent, 0.85f));
            Ui.Fixed(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -146f), new Vector2(132f, 5f));

            var back = Ui.LinkButton("Back", Root, "返 回", Theme.Body, () => App.Pop());
            Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-72f, -62f), new Vector2(180f, 56f));

            // 两块大选项卡：左冒险（绿）右无限年（金），全屏只此两个决定
            Option("Adventure", "冒 险", -350f, Theme.Accent, () => Choose(false));
            Option("Endless", "无 限 年", 350f, Theme.Gold, () => Choose(true));
        }

        private void Option(string name, string caption, float x, Color accent, System.Action onClick)
        {
            var card = Ui.Rounded(name, Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.92f), Theme.WithAlpha(accent, 0.75f), 2, true);
            Ui.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -40f), new Vector2(620f, 380f));

            var btn = card.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = card;
            btn.onClick.AddListener(() =>
            {
                if (GameApp.I != null)
                {
                    GameApp.I.Audio.PlaySfx("ui_click");
                }

                Tween.Punch(card.rectTransform, 0.10f, 0.18f);
                onClick();
            });

            var label = Ui.Label("Caption", card.transform, caption,
                56, Theme.Text, TextAlignmentOptions.Center);
            label.characterSpacing = 8f;
            Ui.Outline(label.gameObject, new Color(0f, 0f, 0f, 0.5f), 2.5f);
            Ui.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

            // 一条系列色短划线垫在名字下，作为两块卡的视觉签名
            var rule = Ui.Solid("Rule", card.transform, Theme.WithAlpha(accent, 0.9f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -64f), new Vector2(72f, 5f));
        }

        private void Choose(bool endless)
        {
            // 手上有局才需要确认屏；空档期直接开新局进地图
            if (App.Director.DeckCount > 0)
            {
                App.Push(new RunConfirmScreen(endless));
                return;
            }

            StartAndEnter(endless);
        }

        private void StartAndEnter(bool endless)
        {
            App.Director.StartNewRun(endless);
            App.PersistMeta();
            App.Replace(new MapScreen());
        }
    }
}
