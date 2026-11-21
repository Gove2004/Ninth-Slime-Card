using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>
    /// 开屏演出。画面 = bg_title 主视觉插画（贴纸史莱姆 + 新春灯笼 + 城堡剪影夜景）
    /// 压一层轻遮罩，海报式白色大标题压在上部留空区，一条系列色短划线 + 细进度线。
    /// 所有元素分三波进场，标题落地后星尘环绕漂浮，进度线 smoothstep 充能。
    /// 时间轴（演出 2.2s，之后交棒给登录恢复）：
    ///   0.15 标题弹入 → 0.50 落地挤压 → 0.55/0.95 副标/进度线 → 0.90 星尘显形 → 2.2 转场
    ///
    /// 注意：标题描边用 UI.Shadow 四向叠加（顶点复制），不能用 TMP 的 outline
    /// keyword——运行时动态字体图集没有描边 padding，采样会取到相邻字素的像素，
    /// 字面渲染成黑色噪声块。
    /// </summary>
    public sealed class BootScreen : Screen
    {
        public override string BgKey { get { return "bg_title"; } }

        private float elapsed;
        private Image barFill;
        private bool advanced;
        private bool loginResolved;
        private bool loginOk;

        private RectTransform[] dusts;

        /// <summary>星尘的确定性分布：绕开中央史莱姆与上部标题带的两侧空域。</summary>
        private static readonly Vector2[] DustSpots =
        {
            new Vector2(-620f, 300f), new Vector2(640f, 340f),
            new Vector2(-540f, 110f), new Vector2(560f, 90f),
            new Vector2(-700f, 430f), new Vector2(710f, 450f),
            new Vector2(-460f, 190f),
        };

        private static readonly float[] DustSize = { 14f, 10f, 12f, 9f, 11f, 8f, 13f };
        private static readonly float[] DustSpeed = { 1.4f, 1.1f, 1.7f, 1.2f, 1.5f, 1.0f, 1.3f };
        private static readonly float[] DustAmp = { 16f, 12f, 18f, 11f, 15f, 10f, 17f };

        protected override void Build()
        {
            // 主视觉插画由 GameApp.SetBackground(bg_title) 铺满，这里只压一层轻遮罩
            // ——不遮也行，但插画下部饱和度高，白字会浮；24% 的黑让白字往下沉一沉。
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.24f));
            veil.raycastTarget = false;

            // 标题：第一波，从 0.5 弹到 1。白字 + 四向投影描边压插画
            var title = Ui.Label("Title", Root, "第九张史莱姆牌", 72, Theme.Text, TextAlignmentOptions.Center);
            title.characterSpacing = 10f;
            Ui.Outline(title.gameObject, new Color(0.03f, 0.08f, 0.10f, 0.85f), 3f);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 380f), new Vector2(1200f, 96f));
            // PopIn 签名是 (rt, delay, from, dur)。OutBack 曲线自带落地质感。
            Tween.PopIn(title.rectTransform, 0.15f, 0.5f, 0.3f);
            Tween.FadeIn(title, 0.30f, 0.15f, 0f, 1f);

            // 标题下只留一条系列色短划线：品牌色锚点，一行多余的字都不要
            var rule = Ui.Solid("Rule", Root, Theme.WithAlpha(Theme.Accent, 0.9f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 312f), new Vector2(88f, 5f));
            Tween.Scale(rule.rectTransform, 0f, 1f, 0.3f, Easing.OutBack, null, 0.55f);

            // 星尘：第二波，环绕主视觉两侧的呼吸小圆点，Tick 持续漂浮
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

            // 进度线：底部一条 4px 细线充能，第三波淡入；充能起点与曲线都在 Tick 里
            var barTrack = Ui.Solid("BarTrack", Root, Theme.WithAlpha(Theme.Text, 0.14f));
            Ui.Fixed(barTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 118f), new Vector2(520f, 4f));
            Tween.FadeIn(barTrack, 0.3f, 0.95f, 0f, 1f);

            barFill = Ui.Solid("BarFill", barTrack.transform, Theme.Text);
            var fillRt = barFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 0f);
            barFill.color = new Color(1f, 1f, 1f, 0f);
            Tween.FadeIn(barFill, 0.3f, 0.95f, 0f, 1f);

            // 登录恢复与演出并行：请求现在发出，结果等演出放完再落地（见 Tick）
            App.Tap.TryRestoreSession(ok =>
            {
                loginOk = ok;
                loginResolved = true;
            });
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;

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
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 520f * progress);

            if (elapsed >= 2.3f)
            {
                if (!advanced)
                {
                    advanced = true;
                    App.Audio.PlayMusic("title1");
                }

                // 登录链路合并：恢复请求在演出开始时就发出（异步），但结果必须等
                // 演出放完才落地——本地秒回也不能把 2.3s 的入场演出腰斩。
                // 恢复成功（老玩家的常态）免掉整块登录屏直接进主页；
                // 失败才落进登录屏（它内部会再试一次，网络抖动时还能兜回来）。
                if (loginResolved)
                {
                    if (loginOk)
                    {
                        App.Toast("欢迎回来 · " + App.Tap.Nickname);
                        App.Replace(new HomeScreen());
                    }
                    else
                    {
                        App.Replace(new LoginScreen());
                    }
                }
            }
        }
    }
}
