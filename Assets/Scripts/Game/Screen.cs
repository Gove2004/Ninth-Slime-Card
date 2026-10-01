using UnityEngine;

namespace Slime.Game
{
    /// <summary>屏幕基类。每个屏幕自我构建，不依赖任何场景预制体。</summary>
    public abstract class Screen
    {
        /// <summary>标题栏与内容区的统一外边距：四边一致，所有屏共用。</summary>
        protected const float TopMargin = 40f;
        protected const float SideMargin = 40f;
        protected const float HeaderH = 96f;

        public RectTransform Root { get; private set; }

        protected GameApp App { get { return GameApp.I; } }

        public void Attach(RectTransform parent)
        {
            Root = Ui.Node(GetType().Name, parent);
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

        /// <summary>顶部标题栏（统一风格）。</summary>
        protected RectTransform BuildHeader(string title, string subtitle, string backCaption)
        {
            var header = Ui.Rounded("Header", Root, Theme.RadiusM, Theme.WithAlpha(Theme.Panel, 0.92f), Theme.Border, 1);
            Ui.Fixed(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopMargin), new Vector2(1840f, 96f));

            var titleLabel = Ui.Label("Title", header.transform, title, Theme.H2, Theme.Text, TMPro.TextAlignmentOptions.Left);
            titleLabel.characterSpacing = 4f;
            Ui.Fixed(titleLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 12f), new Vector2(900f, 48f));

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Ui.Label("Sub", header.transform, subtitle, Theme.Caption, Theme.TextDim, TMPro.TextAlignmentOptions.Left);
                Ui.Fixed(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, -22f), new Vector2(1200f, 30f));
            }

            if (!string.IsNullOrEmpty(backCaption))
            {
                var back = Ui.TextButton("Back", header.transform, backCaption, Theme.Body, Theme.PanelRaised, Theme.TextDim, () => App.Pop());
                Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(180f, 60f));
            }

            return header.rectTransform;
        }

        /// <summary>内容区域（自动避开标题栏，左右下三边与标题栏同边距）。</summary>
        protected RectTransform BuildBody(float topInset)
        {
            var body = Ui.Node("Body", Root);
            Ui.Stretch(body, SideMargin, topInset, SideMargin, SideMargin);
            return body;
        }
    }
}
