using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 非战斗节点屏的共用骨架：中间一块面板，往上放标题、说明与操作区。
    /// 篝火 / 宝箱 / 商店 / 事件四个屏都从这里派生，保证它们的排版完全一致。
    /// </summary>
    public abstract class NodeScreen : Screen
    {
        protected const float PanelW = 1080f;
        protected const float PanelH = 720f;

        /// <summary>节点标题。</summary>
        protected abstract string Title { get; }

        /// <summary>标题下的一句说明。</summary>
        protected abstract string Subtitle { get; }

        /// <summary>强调色。</summary>
        protected abstract Color Accent { get; }

        /// <summary>构建面板内容。y 从 0 向下递减。</summary>
        protected abstract void BuildContent(RectTransform panel);

        protected override void Build()
        {
            App.Audio.PlayMusic("title2");

            var glow = Ui.Panel("Glow", Root, SpriteForge.Glow(512, Accent), Theme.WithAlpha(Color.white, 0.18f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(1100f, 1100f));

            var panel = Ui.Rounded("Panel", Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.97f), Theme.WithAlpha(Accent, 0.6f), 2);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(PanelW, PanelH));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            var title = Ui.Label("Title", panel.transform, Title, Theme.H1, Accent, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(PanelW - 80f, 62f));

            var sub = Ui.Label("Sub", panel.transform, Subtitle, Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -114f), new Vector2(PanelW - 100f, 30f));
            Tween.FadeIn(sub, 0.3f, 0.1f, 0f, 1f);

            var body = Ui.Node("Body", panel.transform);
            Ui.Stretch(body, 40f, 160f, 40f, 40f);
            BuildContent(body);
        }

        /// <summary>统一的「离开这个节点、回地图」出口。</summary>
        protected void Leave()
        {
            App.Director.LeaveNode();
            App.PersistMeta();
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new MapScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        protected Button ActionButton(RectTransform parent, string caption, Vector2 pos, Vector2 size, Color fill)
        {
            var btn = Ui.TextButton("Action", parent, caption, Theme.H3, fill, Theme.Text, null);
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, size);
            return btn;
        }
    }
}
