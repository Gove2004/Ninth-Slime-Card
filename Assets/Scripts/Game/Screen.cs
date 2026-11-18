using UnityEngine;

namespace Slime.Game
{
    /// <summary>屏幕基类。每个屏幕自我构建，不依赖任何场景预制体。</summary>
    public abstract class Screen
    {
        /// <summary>
        /// 屏幕四边的安全内边距。所有屏共用同一套数值，别在单个屏里另写一套。
        /// 40 是「元素到屏幕边缘」的绝对下限；真机刘海与手势条再叠一层（见 Attach 里的 safeArea）。
        /// </summary>
        protected const float EdgeMargin = 48f;

        /// <summary>标题栏占位：标题栏本身的高度 + 它与下方内容之间的呼吸。</summary>
        protected const float HeaderH = 96f;
        protected const float HeaderGap = 28f;

        /// <summary>内容区上边界 = 边距 + 标题栏 + 间隔。BuildBody 默认值。</summary>
        protected const float BodyTop = EdgeMargin + HeaderH + HeaderGap;

        // 兼容旧名字：这两处改动过含义，直接跟着 EdgeMargin 走，避免两套数值打架。
        protected const float TopMargin = EdgeMargin;
        protected const float SideMargin = EdgeMargin;

        /// <summary>标题栏实际宽度 = 画布宽 - 左右边距。留 3% 余量给不同宽高比。</summary>
        private const float HeaderW = 1792f;

        public RectTransform Root { get; private set; }

        /// <summary>
        /// 本屏的场景背景图 key（Art/Bg 下）。null = 默认主城背景。
        /// 由 GameApp 在 Push/Pop/PopTo 时统一应用，屏幕自己不用管切换时机。
        /// </summary>
        public virtual string BgKey { get { return null; } }

        protected GameApp App { get { return GameApp.I; } }

        public void Attach(RectTransform parent)
        {
            Root = Ui.Node(GetType().Name, parent);

            // 安全区必须在建任何子节点之前套上：之后所有 Fixed/Stretch 都是相对 Root 的，
            // 先套安全区等于给整屏留出了刘海与手势条的位置，子节点无需各自处理。
            Ui.ApplySafeArea(Root, 1920f, 1080f);

            Build();
        }

        protected abstract void Build();

        public virtual void Tick(float deltaTime)
        {
        }

        public virtual void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        public void Destroy()
        {
            OnDestroyed();
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        protected virtual void OnDestroyed()
        {
        }

        /// <summary>
        /// 顶部标题栏（统一风格）。
        /// 只保留三项：主标题、一行副标题、返回。副标题超过一行就是在抢内容的注意力。
        /// </summary>
        protected RectTransform BuildHeader(string title, string subtitle, string backCaption)
        {
            var header = Ui.Rounded("Header", Root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.92f), Theme.Border, 1);
            Ui.Fixed(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -EdgeMargin), new Vector2(HeaderW, HeaderH));

            bool hasSub = !string.IsNullOrEmpty(subtitle);

            var titleLabel = Ui.Label("Title", header.transform, title, Theme.H2, Theme.Text,
                TMPro.TextAlignmentOptions.Left);
            titleLabel.characterSpacing = 3f;
            Ui.Fixed(titleLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(32f, hasSub ? 11f : 0f), new Vector2(1100f, 50f));

            if (hasSub)
            {
                var sub = Ui.Label("Sub", header.transform, subtitle, Theme.Caption, Theme.TextDim,
                    TMPro.TextAlignmentOptions.Left);
                Ui.Fixed(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(32f, -24f), new Vector2(1200f, 30f));
            }

            if (!string.IsNullOrEmpty(backCaption))
            {
                var back = Ui.TextButton("Back", header.transform, backCaption, Theme.Body,
                    Theme.PanelRaised, Theme.TextDim, () => App.Pop());
                Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-20f, 0f), new Vector2(184f, 64f));
            }

            return header.rectTransform;
        }

        /// <summary>内容区域：四边与标题栏对齐，自动避开标题栏。</summary>
        protected RectTransform BuildBody(float topInset = 0f)
        {
            var body = Ui.Node("Body", Root);
            float top = topInset > 0f ? topInset : BodyTop;
            Ui.Stretch(body, EdgeMargin, top, EdgeMargin, EdgeMargin);
            return body;
        }
    }
}
