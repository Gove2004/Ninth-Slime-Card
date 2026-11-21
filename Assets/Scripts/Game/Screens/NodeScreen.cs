using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 非战斗节点屏的共用骨架：全屏沉浸构图，没有中央面板。
    /// 标题左上大字（与地图头部同一语言），内容直接落背景场景，操作按钮沉底。
    /// 篝火 / 宝箱 / 商店 / 事件四个屏都从这里派生，保证它们的排版完全一致。
    /// </summary>
    public abstract class NodeScreen : Screen
    {
        /// <summary>节点标题。</summary>
        protected abstract string Title { get; }

        /// <summary>强调色。</summary>
        protected abstract Color Accent { get; }

        /// <summary>构建内容。body 是避开标题后的全屏区域，坐标以自身中心为原点。</summary>
        protected abstract void BuildContent(RectTransform body);

        protected override void Build()
        {
            App.Audio.PlayMusic("title2");

            // 一团强调色光晕垫在场景中上部：给内容一个「从暗场里亮出来」的落点
            var glow = Ui.Panel("Glow", Root, SpriteForge.Glow(512, Accent), Theme.WithAlpha(Color.white, 0.16f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 80f), new Vector2(1400f, 1400f));

            var title = Ui.Label("Title", Root, Title, Theme.H1, Accent, TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -56f), new Vector2(900f, 74f));

            // 标题下一段短下划线：唯一的装饰，替代原来整块面板的框定感
            var underline = Ui.Solid("Underline", Root, Theme.WithAlpha(Accent, 0.85f));
            Ui.Fixed(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -146f), new Vector2(132f, 5f));

            var body = Ui.Node("Body", Root);
            Ui.Stretch(body, 56f, 176f, 56f, EdgeMargin);
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

        /// <summary>沉底主按钮：全屏构图下唯一的大行动点，居中或左右分立由 centerX 决定。</summary>
        protected Button BottomButton(RectTransform parent, string caption, float centerX, Vector2 size, Color fill)
        {
            var btn = Ui.TextButton("Action", parent, caption, Theme.H3, fill, Theme.Text, null);
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(centerX, 56f), size);
            return btn;
        }
    }
}
