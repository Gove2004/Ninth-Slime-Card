using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 机制说明弹层。
    ///
    /// 战斗里状态标记只能显示「中毒 6」，玩家不知道它到底什么时候掉血、
    /// 会不会被护盾挡住 —— 而这些恰好是决定要不要现在引爆的信息。
    /// 点一下标记就把词条摊开，不用退出战斗去翻图鉴。
    /// </summary>
    public static class MechanicPopup
    {
        /// <summary>弹出一条机制说明。返回弹层根节点，调用方可自行销毁。</summary>
        public static RectTransform Show(RectTransform parent, GlossaryEntry entry)
        {
            if (parent == null || entry == null)
            {
                return null;
            }

            Color accent = entry.Group == GlossaryGroup.Debuff ? Theme.Danger
                : entry.Group == GlossaryGroup.Payoff ? Theme.Gold
                : entry.Group == GlossaryGroup.Buff ? Theme.Accent
                : Theme.Mana;

            var overlay = Ui.Solid("MechanicOverlay", parent, new Color(0f, 0f, 0f, 0.78f), true);
            overlay.rectTransform.SetAsLastSibling();
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => Object.Destroy(overlay.gameObject));

            // 说明长短差别很大（一行到五行），面板高度跟着内容走：
            // 固定高度会让短词条下方留一大片空白，看起来像是没加载完。
            const float width = 880f;
            const float textWidth = 800f;
            float textH = Ui.MeasureTextHeight(entry.Text, textWidth, Theme.Body);
            // 布局自上而下：分组标签 30 + 标题 46 + 正文 textH + 按钮 56 + 上下留白。
            float panelH = 122f + textH + 30f + 56f + 40f;

            var panel = Ui.Rounded("MechanicPanel", overlay.transform, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.99f), Theme.WithAlpha(accent, 0.85f), 2, true);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(width, panelH));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            // 点面板本身不该关闭，否则读说明时误触就没了
            panel.gameObject.AddComponent<Button>().onClick.AddListener(() => { });

            var stripe = Ui.Panel("Stripe", panel.transform, SpriteForge.Solid(), accent);
            Ui.Fixed(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f, 14f), new Vector2(6f, panelH - 28f));

            var group = Ui.Label("Group", panel.transform, Glossary.GroupName(entry.Group), Theme.Caption,
                Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(group.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(400f, 24f));

            var title = Ui.Label("Title", panel.transform, entry.Name, Theme.H2,
                Theme.Lighten(accent, 0.35f), TextAlignmentOptions.Left);
            title.characterSpacing = 2f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -56f), new Vector2(600f, 46f));

            // 宽度必须显式给：Ui.Fixed 会把 anchorMax 覆写成 anchor，
            // 想靠 anchorMax=(1,1) 撑满宽度会得到 0 宽，TMP 随即把每个字单独折行。
            var text = Ui.Label("Text", panel.transform, entry.Text, Theme.Body,
                Theme.TextDim, TextAlignmentOptions.TopLeft);
            text.margin = new Vector4(40f, 0f, 40f, 0f);
            Ui.Fixed(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -122f), new Vector2(textWidth, textH + 8f));

            var close = Ui.TextButton("Close", panel.transform, "知道了", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, () => Object.Destroy(overlay.gameObject));
            Ui.Fixed(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(240f, 56f));

            return overlay.rectTransform;
        }

        /// <summary>按状态弹词条；没有对应词条时什么都不做（不要弹空面板）。</summary>
        public static void ShowStatus(RectTransform parent, StatusKind kind)
        {
            Show(parent, Glossary.ForStatus(kind));
        }
    }
}
