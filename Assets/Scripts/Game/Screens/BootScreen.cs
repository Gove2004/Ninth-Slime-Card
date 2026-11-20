using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>
    /// 开屏演出。不是「Logo 挂在那里等进度条」——所有元素分四波进场，
    /// LOGO 落地后保持呼吸，星尘环绕漂浮，光晕脉动，进度条 smoothstep 充能。
    /// 时间轴（演出 2.2s，之后交棒给登录恢复）：
    ///   0.08 光晕展开 → 0.15 LOGO 弹入 → 0.50 落地挤压 → 0.55/0.75 标题/副标题滑入
    ///   → 0.90 星尘显形 → 0.95 进度条充能 → 2.2 完成转场
    /// </summary>
    public sealed class BootScreen : Screen
    {
        private float elapsed;
        private Image barFill;
        private TextMeshProUGUI tip;
        private bool advanced;

        private Image slime;
        private Image glow;
        private RectTransform[] dusts;

        /// <summary>星尘的确定性分布：绕 LOGO 的椭圆环，避开中轴标题区。</summary>
        private static readonly Vector2[] DustSpots =
        {
            new Vector2(-430f, 330f), new Vector2(410f, 360f),
            new Vector2(-520f, 150f), new Vector2(510f, 130f),
            new Vector2(-350f, 430f), new Vector2(330f, 445f),
            new Vector2(-260f, 240f),
        };

        private static readonly float[] DustSize = { 14f, 10f, 12f, 9f, 11f, 8f, 13f };
        private static readonly float[] DustSpeed = { 1.4f, 1.1f, 1.7f, 1.2f, 1.5f, 1.0f, 1.3f };
        private static readonly float[] DustAmp = { 16f, 12f, 18f, 11f, 15f, 10f, 17f };

        protected override void Build()
        {
            var backdrop = Ui.Solid("Backdrop", Root, Theme.BgDeep);
            backdrop.raycastTarget = false;

            // 光晕：第一波进场，从 0.4 倍展开 + 淡入，之后 Tick 持续脉动
            glow = Ui.Panel("Glow", Root, SpriteForge.Glow(256, Theme.Accent), Theme.WithAlpha(Color.white, 0f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 140f), new Vector2(760f, 760f));
            glow.type = Image.Type.Simple;
            glow.rectTransform.localScale = Vector3.one * 0.4f;
            Tween.Scale(glow.rectTransform, 0.4f, 1f, 0.55f, Easing.OutCubic, null, 0.08f);
            Tween.FadeIn(glow, 0.55f, 0.08f, 0f, 0.30f);

            // LOGO：第二波，从 0.3 弹到 1，落地瞬间挤压一次
            slime = Ui.Panel("Slime", Root,
                SpriteForge.Slime(256, Theme.Accent, Theme.BgDeep, 20091, true, SlimeFace.Happy), Color.white);
            slime.rectTransform.localScale = Vector3.one * 0.3f;
            Ui.Fixed(slime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 150f), new Vector2(300f, 300f));
            Tween.PopIn(slime.rectTransform, 0.15f, 0.3f, 0.5f);
            Tween.Punch(slime.rectTransform, 0.10f, 0.30f, 0.62f);

            var title = Ui.Label("Title", Root, "第九张史莱姆牌", Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            title.characterSpacing = 6f;
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f), new Vector2(1200f, 70f));
            Tween.SlideIn(title.rectTransform, new Vector2(0f, 46f), 0.42f, 0.55f);
            Tween.FadeIn(title, 0.42f, 0.55f, 0f, 1f);

            var sub = Ui.Label("Sub", Root, "一 对 一 · 卡 牌 对 决", Theme.H4, Theme.Accent, TextAlignmentOptions.Center);
            sub.characterSpacing = 3f;
            Ui.Fixed(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -140f), new Vector2(900f, 40f));
            Tween.SlideIn(sub.rectTransform, new Vector2(0f, 32f), 0.42f, 0.75f);
            Tween.FadeIn(sub, 0.42f, 0.75f, 0f, 1f);

            // 星尘：第三波，环绕 LOGO 的呼吸小圆点，Tick 持续漂浮
            dusts = new RectTransform[DustSpots.Length];
            for (int i = 0; i < DustSpots.Length; i++)
            {
                Color c = i % 2 == 0 ? Theme.Accent : Theme.Gold;
                var dot = Ui.Panel("Dust" + i, Root, SpriteForge.Glow(64, c), Theme.WithAlpha(Color.white, 0f));
                Ui.Fixed(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    DustSpots[i], new Vector2(DustSize[i], DustSize[i]));
                dot.rectTransform.localScale = Vector3.one * 0.6f;
                Tween.FadeIn(dot, 0.4f, 0.90f + 0.06f * i, 0f, 0.34f);
                Tween.Scale(dot.rectTransform, 0.6f, 1f, 0.4f, Easing.OutBack, null, 0.90f + 0.06f * i);
                dusts[i] = dot.rectTransform;
            }

            // 进度条：第四波，充能起点与充能曲线都在 Tick 里
            var barBg = Ui.Rounded("BarBg", Root, 10, Theme.PanelSunken, Theme.Border, 1);
            Ui.Fixed(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(640f, 22f));
            Tween.FadeIn(barBg, 0.3f, 0.95f, 0f, 1f);

            barFill = Ui.Rounded("BarFill", barBg.transform, 10, Theme.Accent, Theme.Accent, 0);
            var fillRt = barFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(3f, -3f);
            fillRt.sizeDelta = new Vector2(0f, -6f);
            barFill.color = new Color(1f, 1f, 1f, 0f);
            Tween.FadeIn(barFill, 0.3f, 0.95f, 0f, 1f);

            tip = Ui.Label("Tip", Root, "正在洗牌…", Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 84f), new Vector2(1200f, 30f));
            Tween.FadeIn(tip, 0.3f, 0.95f, 0f, 1f);
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            // LOGO 呼吸：等弹入（0.15+0.5s）结束后由 Tick 接管缩放，
            // 与 Tween 的一次性补间不再抢同一个通道
            if (elapsed > 0.75f && slime != null)
            {
                float breathe = 1f + Mathf.Sin((elapsed - 0.75f) * 3.0f) * 0.025f;
                slime.rectTransform.localScale = Vector3.one * breathe;
            }

            // 光晕脉动：alpha 在 0.23~0.37 之间往复
            if (glow != null)
            {
                glow.color = Theme.WithAlpha(Color.white, 0.30f + Mathf.Sin(elapsed * 1.8f) * 0.07f);
            }

            // 星尘漂浮：围绕初始点位做正弦升降，相位与速度各不相同
            if (dusts != null)
            {
                for (int i = 0; i < dusts.Length; i++)
                {
                    dusts[i].anchoredPosition = DustSpots[i] +
                        new Vector2(0f, Mathf.Sin(elapsed * DustSpeed[i] + i * 1.7f) * DustAmp[i]);
                }
            }

            if (advanced)
            {
                return;
            }

            // 充能从 0.95s 起、1.2s 走完，smoothstep 前快后稳
            float t = Mathf.Clamp01((elapsed - 0.95f) / 1.2f);
            float progress = t * t * (3f - 2f * t);
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

            if (elapsed >= 2.3f)
            {
                advanced = true;
                App.Audio.PlayMusic("title1");

                // 登录链路合并：启动条走完直接在这层试恢复登录态——
                // 恢复成功（老玩家的常态）免掉整块登录屏直接进主页；
                // 失败才落进登录屏（它内部会再试一次，网络抖动时还能兜回来）。
                tip.text = "正在恢复登录态…";
                App.Tap.TryRestoreSession(ok =>
                {
                    if (ok)
                    {
                        App.Toast("欢迎回来 · " + App.Tap.Nickname);
                        App.Replace(new HomeScreen());
                    }
                    else
                    {
                        App.Replace(new LoginScreen());
                    }
                });
            }
        }
    }
}
