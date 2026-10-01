using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>启动屏：Logo + 进度条，随后进入登录。</summary>
    public sealed class BootScreen : Screen
    {
        private float elapsed;
        private Image barFill;
        private TextMeshProUGUI tip;
        private bool advanced;

        protected override void Build()
        {
            var backdrop = Ui.Solid("Backdrop", Root, Theme.BgDeep);
            backdrop.raycastTarget = false;

            var glow = Ui.Panel("Glow", Root, SpriteForge.Glow(256, Theme.Accent), Theme.WithAlpha(Color.white, 0.28f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(760f, 760f));
            glow.type = Image.Type.Simple;

            var slime = Ui.Panel("Slime", Root, SpriteForge.Slime(256, Theme.Accent, Theme.BgDeep, 20091, true, SlimeFace.Happy), Color.white);
            Ui.Fixed(slime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(300f, 300f));

            var title = Ui.Label("Title", Root, "第九张史莱姆牌", Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            title.characterSpacing = 6f;
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1200f, 70f));

            var sub = Ui.Label("Sub", Root, "一 对 一 · 卡 牌 对 决", Theme.H4, Theme.Accent, TextAlignmentOptions.Center);
            sub.characterSpacing = 3f;
            Ui.Fixed(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(900f, 40f));

            var barBg = Ui.Rounded("BarBg", Root, 10, Theme.PanelSunken, Theme.Border, 1);
            Ui.Fixed(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(640f, 22f));

            barFill = Ui.Rounded("BarFill", barBg.transform, 10, Theme.Accent, Theme.Accent, 0);
            var fillRt = barFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(3f, -3f);
            fillRt.sizeDelta = new Vector2(0f, -6f);

            tip = Ui.Label("Tip", Root, "正在洗牌…", Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(1200f, 30f));
        }

        public override void Tick(float deltaTime)
        {
            if (advanced)
            {
                return;
            }

            elapsed += deltaTime;

            float progress = Mathf.Clamp01(elapsed / 1.6f);
            var rt = barFill.rectTransform;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 634f * progress);

            if (progress > 0.45f && progress < 0.5f)
            {
                tip.text = "正在展开牌桌…";
            }
            else if (progress > 0.8f)
            {
                tip.text = "准备就绪";
            }

            if (elapsed >= 1.8f)
            {
                advanced = true;
                App.Audio.PlayMusic("title1");
                App.Replace(new LoginScreen());
            }
        }
    }
}
