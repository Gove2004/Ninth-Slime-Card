using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>
    /// 开屏演出。画面 = bg_title 主视觉插画（贴纸史莱姆 + 新春灯笼 + 城堡剪影夜景）
    /// 压一层轻遮罩，海报式金字标题压在上部留空区，副标题金框徽章紧随其下。
    /// 所有元素分三波进场，标题落地后星尘环绕漂浮，进度条 smoothstep 充能。
    /// 时间轴（演出 2.2s，之后交棒给登录恢复）：
    ///   0.15 标题弹入 → 0.50 落地挤压 → 0.55/0.95 徽章/进度条 → 0.90 星尘显形 → 2.2 转场
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
        private TextMeshProUGUI tip;
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
            // ——不遮也行，但插画下部饱和度高，白字会浮；22% 的黑让金字往下沉一沉。
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.22f));
            veil.raycastTarget = false;

            // 标题：第一波，从 0.3 弹到 1，落地瞬间挤压一次。金字 + 四向投影描边压插画
            var title = Ui.Label("Title", Root, "第九张史莱姆牌", 62, Theme.Gold, TextAlignmentOptions.Center);
            title.characterSpacing = 8f;
            AddOutline(title.gameObject, new Color(0.03f, 0.08f, 0.10f, 0.85f), 3f);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 386f), new Vector2(1200f, 84f));
            // PopIn 签名是 (rt, delay, from, dur)。落地挤压不再叠 Punch——
            // Punch 在 Build 期捕获 baseScale（此刻 PopIn 刚把缩放压到 from），
            // 延迟触发后会把标题按 from 倍永久钉死，标题只剩徽章大小。
            // OutBack 曲线自带落地质感，无需叠加。
            Tween.PopIn(title.rectTransform, 0.15f, 0.5f, 0.3f);
            Tween.FadeIn(title, 0.30f, 0.15f, 0f, 1f);

            // 副标题徽章：金框石板胶囊，第二波淡入
            var badge = Ui.Rounded("Badge", Root, 12,
                Theme.WithAlpha(Theme.PanelSunken, 0.88f), Theme.WithAlpha(Theme.Gold, 0.85f), 2);
            Ui.Fixed(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 296f), new Vector2(380f, 52f));
            Tween.FadeIn(badge, 0.35f, 0.55f, 0f, 1f);

            var sub = Ui.Label("Sub", badge.transform, "一 对 一 · 卡 牌 对 决",
                Theme.H4, Theme.Gold, TextAlignmentOptions.Center);
            sub.characterSpacing = 3f;
            Ui.Stretch(sub.rectTransform, 0f, 0f, 0f, 0f);

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

            // 进度条：金框石板底 + 金色充能，第三波淡入；充能起点与曲线都在 Tick 里
            Sprite barSkin = ArtLib.UiArt("ui_panel", new Vector4(16f, 16f, 16f, 16f));
            Image barBg = barSkin != null
                ? Ui.Panel("BarBg", Root, barSkin, Color.white)
                : Ui.Rounded("BarBg", Root, 10, Theme.PanelSunken, Theme.Border, 1);
            barBg.type = Image.Type.Sliced;
            Ui.Fixed(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(640f, 26f));
            Tween.FadeIn(barBg, 0.3f, 0.95f, 0f, 1f);

            barFill = Ui.Rounded("BarFill", barBg.transform, 8, Theme.Gold, Theme.Gold, 0);
            var fillRt = barFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = new Vector2(5f, 5f);
            fillRt.offsetMax = new Vector2(5f, -5f);
            fillRt.sizeDelta = new Vector2(0f, -10f);
            barFill.color = new Color(1f, 1f, 1f, 0f);
            Tween.FadeIn(barFill, 0.3f, 0.95f, 0f, 1f);

            tip = Ui.Label("Tip", Root, "正在洗牌…", Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 78f), new Vector2(1200f, 30f));
            Tween.FadeIn(tip, 0.3f, 0.95f, 0f, 1f);

            // 登录恢复与演出并行：请求现在发出，结果等演出放完再落地（见 Tick）
            App.Tap.TryRestoreSession(ok =>
            {
                loginOk = ok;
                loginResolved = true;
            });
        }

        /// <summary>
        /// 四向 Shadow 叠加模拟描边。纯顶点偏移复制，不触碰字体图集——
        /// TMP 的 OUTLINE keyword 在运行时动态图集上会渲染成噪声（见类注释）。
        /// </summary>
        private static void AddOutline(GameObject go, Color color, float dist)
        {
            var offsets = new Vector2[]
            {
                new Vector2(dist, 0f), new Vector2(-dist, 0f),
                new Vector2(0f, dist), new Vector2(0f, -dist),
            };
            foreach (var off in offsets)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = color;
                shadow.effectDistance = off;
                shadow.useGraphicAlpha = true;
            }
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
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 630f * progress);

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
                if (!advanced)
                {
                    advanced = true;
                    App.Audio.PlayMusic("title1");
                    tip.text = "正在恢复登录态…";
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
