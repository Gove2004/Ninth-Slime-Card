using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>档案屏：卡牌图鉴（按八系列分组）+ 机制说明 + 成就档案（用奖杯兑换）。</summary>
    public sealed class CodexScreen : Screen
    {
        public enum Tab
        {
            Cards = 0,
            Mechanics = 1,
            Achievements = 2
        }

        private readonly Tab initialTab;
        private RectTransform body;

        public CodexScreen(Tab tab)
        {
            initialTab = tab;
        }

        protected override void Build()
        {
            // 去石板头部：标题落景左上，返回沉右上——与节点屏同一构图语言。
            var title = Ui.Label("Title", Root, "档 案 室", Theme.H1, Theme.Text, TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -56f), new Vector2(600f, 74f));

            var underline = Ui.Solid("Underline", Root, Theme.WithAlpha(Theme.Accent, 0.85f));
            Ui.Fixed(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -146f), new Vector2(132f, 5f));

            var back = Ui.GhostButton("Back", Root, "返 回", Theme.Body, () => App.Pop());
            Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-56f, -56f), new Vector2(220f, 68f));

            body = Ui.Node("Body", Root);
            Ui.Stretch(body, 56f, 176f, 56f, EdgeMargin);
            Show(initialTab);
        }

        private void Show(Tab tab)
        {
            Ui.ClearChildren(body);

            var tabBar = Ui.Node("TabBar", body);
            Ui.Fixed(tabBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -6f), new Vector2(1400f, 66f));

            var row = Ui.Row(tabBar, 12, new RectOffset(0, 0, 0, 0));
            row.childForceExpandWidth = true;

            var cards = Ui.TextButton("TabCards", tabBar,
                "卡牌图鉴  " + App.Meta.DiscoveredCards.Count + " / " + App.Cards.Count,
                Theme.Body, tab == Tab.Cards ? Theme.AccentDeep : Theme.Panel,
                tab == Tab.Cards ? Theme.Text : Theme.TextDim, () => Show(Tab.Cards));
            Ui.FixedSize(cards.gameObject, 320f, 62f);

            var mech = Ui.TextButton("TabMech", tabBar, "机制说明",
                Theme.Body, tab == Tab.Mechanics ? Theme.AccentDeep : Theme.Panel,
                tab == Tab.Mechanics ? Theme.Text : Theme.TextDim, () => Show(Tab.Mechanics));
            Ui.FixedSize(mech.gameObject, 320f, 62f);

            int totalAchievements = App.Achievements != null ? App.Achievements.All.Count : 0;
            var achi = Ui.TextButton("TabAchi", tabBar,
                "成就档案  " + App.Meta.Unlocked.Count + " / " + totalAchievements,
                Theme.Body, tab == Tab.Achievements ? Theme.AccentDeep : Theme.Panel,
                tab == Tab.Achievements ? Theme.Text : Theme.TextDim, () => Show(Tab.Achievements));
            Ui.FixedSize(achi.gameObject, 320f, 62f);

            // 提示条已删：说明页讲机制怎么结算，条目本身就在解释，不需要先自报家门。
            float contentTop = 100f;

            var content = Ui.Node("Content", body);
            Ui.Stretch(content, 0f, contentTop, 0f, 0f);

            if (tab == Tab.Cards)
            {
                BuildCards(content);
            }
            else if (tab == Tab.Mechanics)
            {
                BuildMechanics(content);
            }
            else
            {
                BuildAchievements(content);
            }
        }

        // ---------------------------------------------------------------- 图鉴

        private void BuildCards(RectTransform parent)
        {
            var content = MakeScroll(parent);

            for (int s = 0; s < Series.All.Length; s++)
            {
                var series = Series.All[s];
                var list = App.Cards.OfSeries(series);
                if (list.Count == 0)
                {
                    continue;
                }

                BuildSeriesHeader(content, series, list);
                BuildSeriesGrid(content, list);
            }

            Ui.Node("Filler", content);
        }

        private void BuildSeriesHeader(RectTransform parent, CardSeries series, List<CardDef> list)
        {
            Color color = Theme.SeriesColor(series);

            int shown = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (App.Meta.DiscoveredCards.Contains(list[i].Id))
                {
                    shown++;
                }
            }

            var header = Ui.Rounded("SeriesHeader", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(color, 0.72f), 0.95f), Theme.WithAlpha(color, 0.6f), 1);
            Ui.FixedSize(header.gameObject, 1440f, 66f);

            var glyph = Ui.Panel("Glyph", header.transform, SpriteForge.SeriesGlyph(96, Theme.Lighten(color, 0.2f), series), Color.white);
            Ui.Fixed(glyph.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(38f, 38f));

            var name = Ui.Label("Name", header.transform, Series.DisplayName(series), Theme.H3,
                Theme.Lighten(color, 0.4f), TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(200f, 40f));

            var blurb = Ui.Label("Blurb", header.transform, Series.Blurb(series), Theme.Caption,
                Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(blurb.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(266f, 0f), new Vector2(700f, 30f));

            var count = Ui.Label("Count", header.transform, "已发现 " + shown + " / " + list.Count, Theme.Body,
                shown == list.Count ? Theme.Lighten(color, 0.25f) : Theme.TextDim, TextAlignmentOptions.Right);
            Ui.Fixed(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(260f, 32f));
        }

        // 图鉴的格子按「战斗里那张牌的实际尺寸」放大到整格：卡片本体 216×312，
        // 放大 1.6 倍之后卡名 45 号、效果 34 号，在 1080p 手机上是两倍于正文的清晰度。
        // 早先每张牌压成 282×116 的一条，效果文字只剩 14 号 —— 一屏能塞 5 张，
        // 但 14 号字落到手机上不到 8 个物理像素，等于印了一行花纹。
        private const int Columns = 5;
        private const float CellW = 345f;
        private const float CellH = 498f;
        private const float CellGap = 20f;

        /// <summary>卡面从常量尺寸放大到格宽所需的倍数。</summary>
        private const float CardScale = CellW / CardView.W;

        private void BuildSeriesGrid(RectTransform parent, List<CardDef> list)
        {
            int rows = (list.Count + Columns - 1) / Columns;

            var gridNode = Ui.Node("Grid", parent);
            var grid = Ui.Grid(gridNode, new Vector2(CellW, CellH), new Vector2(CellGap, CellGap), Columns);
            grid.padding = new RectOffset(0, 0, 0, 0);

            // 滚动区的竖向布局会把子节点拉满可用宽度，比 5 列的总宽更宽；
            // 不居中的话整排卡面会贴着左边缘，右边空出一大块。
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < list.Count; i++)
            {
                BuildCardTile(gridNode, list[i]);
            }

            // 补齐空位，保证下一段从新的一行开始
            int pad = rows * Columns - list.Count;
            for (int i = 0; i < pad; i++)
            {
                Ui.Node("Pad", gridNode);
            }

            Ui.FixedSize(gridNode.gameObject, Columns * CellW + (Columns - 1) * CellGap,
                rows * CellH + (rows - 1) * CellGap);
        }

        private void BuildCardTile(RectTransform parent, CardDef def)
        {
            // 格子是空壳：GridLayoutGroup 会把直接子节点的尺寸改写成格宽格高，
            // 卡面必须挂在这层壳里，否则 216×312 会被撑成 345×498 再放大 1.6 倍，
            // 血淋淋地叠到隔壁格子上去。
            var cell = Ui.Node("Cell" + def.Id, parent);

            if (!App.Meta.DiscoveredCards.Contains(def.Id))
            {
                BuildUnknownTile(cell, def);
                return;
            }

            // 已发现的牌直接画成真卡面：图鉴要看的就是这张牌长什么样，
            // 而不是把卡面文字抄进一个小方块里再缩到看不清。
            var view = CardView.Create(cell, def, v =>
            {
                App.Audio.PlaySfx("ui_click");
                ShowCardDetail(def);
            });

            if (view == null || view.Root == null)
            {
                return;
            }

            // 只放大不动 RectTransform：卡面子元素全按 216×312 的坐标系摆放，
            // 拉伸父节点不会带动字号，卡名会缩成蚂蚁。
            view.Root.localScale = Vector3.one * CardScale;
        }

        /// <summary>未发现的牌只画一张卡背：给系列色和体积，内容一概不给。</summary>
        private void BuildUnknownTile(RectTransform cell, CardDef def)
        {
            Color color = Theme.SeriesColor(def.Series);

            var back = Ui.Rounded("Back", cell, Theme.RadiusL,
                Theme.WithAlpha(Theme.PanelSunken, 1f), Theme.WithAlpha(color, 0.22f), 2);
            Ui.Stretch(back.rectTransform, 6f, 6f, 6f, 6f);

            // 中央用系列纹章而不是问号：纹章是程序化图形，不依赖字体里有没有那个字。
            var glyph = Ui.Panel("Glyph", back.transform,
                SpriteForge.SeriesGlyph(256, Theme.WithAlpha(color, 0.34f), def.Series), Color.white);
            Ui.Fixed(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 18f), new Vector2(176f, 176f));

            var tip = Ui.Label("Tip", back.transform, "未解锁", Theme.Body, Theme.TextFaint,
                TextAlignmentOptions.Center);
            Ui.Fixed(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 82f), new Vector2(240f, 34f));

            var sub = Ui.Label("Sub", back.transform, Series.DisplayName(def.Series), Theme.Caption,
                Theme.WithAlpha(color, 0.55f), TextAlignmentOptions.Center);
            Ui.Fixed(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 46f), new Vector2(240f, 28f));
        }

        /// <summary>
        /// 单张牌的详情面板。
        ///
        /// 图鉴原本只复述卡面文字，玩家读完还是不知道「引爆」是什么、
        /// 为什么该先攒冻结。这里把该卡涉及的机制一并列出，
        /// 让「这张牌属于哪条链」在翻图鉴时就能看出来。
        /// </summary>
        /// <summary>仅供开发面板调用：直接弹出某张牌的详情，用于核对说明排版。</summary>
        public void ShowDetail(CardDef def)
        {
            ShowCardDetail(def);
        }

        private void ShowCardDetail(CardDef def)
        {
            var overlay = Ui.Solid("CardOverlay", Root, new Color(0f, 0f, 0f, 0.78f), true);
            overlay.rectTransform.SetAsLastSibling();
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => Object.Destroy(overlay.gameObject));

            Color color = Theme.SeriesColor(def.Series);

            var panel = Ui.Rounded("CardPanel", overlay.transform, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.99f), Theme.WithAlpha(color, 0.85f), 2, true);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1080f, 760f));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            // 点面板本身不该关闭，否则看说明时误触就没了
            panel.gameObject.AddComponent<Button>().onClick.AddListener(() => { });

            var stripe = Ui.Panel("Stripe", panel.transform, SpriteForge.Solid(), color);
            Ui.Fixed(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 14f), new Vector2(6f, -14f));

            var title = Ui.Label("Title", panel.transform, def.Name, Theme.H2, Theme.Text, TextAlignmentOptions.Left);
            title.characterSpacing = 2f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -34f), new Vector2(600f, 46f));

            string meta = Series.DisplayName(def.Series) + " · " + KindName(def.Kind) + " · " + def.Cost + " 费";
            var metaLabel = Ui.Label("Meta", panel.transform, meta, Theme.Caption,
                Theme.Lighten(color, 0.3f), TextAlignmentOptions.Right);
            Ui.Fixed(metaLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -40f), new Vector2(420f, 30f));

            // 当前效果
            var bodyBox = Ui.Rounded("Body", panel.transform, Theme.RadiusM,
                Theme.PanelSunken, Theme.BorderSoft, 1);
            Ui.Fixed(bodyBox.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -104f), new Vector2(1000f, 130f));

            var bodyText = Ui.Label("BodyText", bodyBox.transform, def.Text(0), Theme.Body,
                Theme.Text, TextAlignmentOptions.TopLeft);
            bodyText.margin = new Vector4(22f, 16f, 22f, 12f);
            Ui.Stretch(bodyText.rectTransform, 0f, 0f, 0f, 0f);

            // 升级预览：高度按实际行数算（长说明会折两行，写死 52 会裁字）
            string upText = def.Text(1);
            string upCaption = upText == def.Text(0)
                ? "升级：费用 " + def.Cost + " → " + Mathf.Max(0, def.Cost - 1)
                : "升级后：" + upText;

            var up = Ui.Label("Upgrade", panel.transform, upCaption, Theme.Caption,
                Theme.Gold, TextAlignmentOptions.TopLeft);
            up.margin = new Vector4(4f, 0f, 4f, 0f);
            float upH = Mathf.Max(Ui.MeasureTextHeight(upCaption, 992f, Theme.Caption) + 6f, 34f);
            float upTop = -246f;
            Ui.Fixed(up.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, upTop), new Vector2(1000f, upH));

            // 相关机制标题：贴在升级预览之下（间距 18），不再和滚动区之间留大空洞
            float mechTop = upTop - upH - 18f;
            var mechTitle = Ui.Label("MechTitle", panel.transform, "相关机制", Theme.H4,
                Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(mechTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, mechTop), new Vector2(1000f, 30f));

            var entries = Glossary.Match(def.Raw);
            if (entries.Count == 0)
            {
                var none = Ui.Label("NoMech", panel.transform, "这张牌不涉及特殊机制。", Theme.Caption,
                    Theme.TextFaint, TextAlignmentOptions.Left);
                Ui.Fixed(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, mechTop - 36f), new Vector2(1000f, 30f));
            }
            else
            {
                // 滚动区高度 = 从标题下沿到「关闭」按钮上方（底距 96）之间的全部空间
                float scrollH = 760f + mechTop - 30f - 18f - 96f;
                RectTransform listContent;
                var scroll = Ui.VerticalScroll("MechList", panel.transform, out listContent);
                Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 96f), new Vector2(1000f, scrollH));

                for (int i = 0; i < entries.Count; i++)
                {
                    BuildMechanicRow(listContent, entries[i], false);
                }
            }

            var close = Ui.TextButton("Close", panel.transform, "关闭", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, () => Object.Destroy(overlay.gameObject));
            Ui.Fixed(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(240f, 56f));
        }

        private static string KindName(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Attack: return "攻击";
                case CardKind.Skill: return "技能";
                case CardKind.Spell: return "咒文";
                case CardKind.Power: return "能力";
                default: return string.Empty;
            }
        }

        // ---------------------------------------------------------------- 机制说明

        private void BuildMechanics(RectTransform parent)
        {
            var content = MakeScroll(parent);

            var groups = new[]
            {
                GlossaryGroup.Rule, GlossaryGroup.Buff, GlossaryGroup.Debuff, GlossaryGroup.Payoff
            };

            for (int g = 0; g < groups.Length; g++)
            {
                var entries = Glossary.OfGroup(groups[g]);
                if (entries.Count == 0)
                {
                    continue;
                }

                BuildMechanicHeader(content, groups[g]);
                for (int i = 0; i < entries.Count; i++)
                {
                    BuildMechanicRow(content, entries[i], true);
                }
            }

            Ui.Node("Filler", content);
        }

        private void BuildMechanicHeader(RectTransform parent, GlossaryGroup group)
        {
            Color accent = group == GlossaryGroup.Debuff ? Theme.Danger
                : group == GlossaryGroup.Payoff ? Theme.Gold
                : group == GlossaryGroup.Buff ? Theme.Accent
                : Theme.Mana;

            var header = Ui.Rounded("MechGroup", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(accent, 0.72f), 0.95f), Theme.WithAlpha(accent, 0.6f), 1);
            Ui.FixedSize(header.gameObject, 1440f, 58f);

            var name = Ui.Label("Name", header.transform, Glossary.GroupName(group), Theme.H4,
                Theme.Lighten(accent, 0.35f), TextAlignmentOptions.Left);
            name.margin = new Vector4(26f, 0f, 0f, 0f);
            Ui.Stretch(name.rectTransform, 0f, 0f, 0f, 0f);
        }

        /// <summary>一条机制词条。图鉴里是可读的整行；卡牌详情里是紧凑行。</summary>
        private void BuildMechanicRow(RectTransform parent, GlossaryEntry entry, bool full)
        {
            Color accent = entry.Group == GlossaryGroup.Debuff ? Theme.Danger
                : entry.Group == GlossaryGroup.Payoff ? Theme.Gold
                : entry.Group == GlossaryGroup.Buff ? Theme.Accent
                : Theme.Mana;

            float width = full ? 1440f : 976f;

            // 说明长短差别很大（一句到三句），行高与文本区都跟着内容走，
            // 否则短词条下方留一片空白、长词条被裁掉尾巴。
            float textWidth = width - 44f;
            float height = Ui.MeasureTextHeight(entry.Text, textWidth, Theme.Caption) + 56f;

            var row = Ui.Rounded("Mech" + entry.Key, parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.PanelSunken, 1f), Theme.WithAlpha(accent, 0.35f), 1);
            Ui.FixedSize(row.gameObject, width, height);

            var stripe = Ui.Panel("Stripe", row.transform, SpriteForge.Solid(), Theme.WithAlpha(accent, 0.85f));
            Ui.Fixed(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(2f, 4f), new Vector2(5f, height - 8f));

            var name = Ui.Label("Name", row.transform, entry.Name, Theme.H4,
                Theme.Lighten(accent, 0.4f), TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -12f), new Vector2(240f, 30f));

            // 玩家卡池里没有这几个状态，混在增益列表里会让人白找一轮。
            // 靠右对齐：词条名长短不一，跟着名字排会忽左忽右。
            if (entry.EnemyOnly)
            {
                var badge = Ui.Rounded("EnemyOnly", row.transform, Theme.RadiusS,
                    Theme.WithAlpha(Theme.PanelRaised, 0.9f), Theme.WithAlpha(Theme.Danger, 0.55f), 1);
                Ui.Fixed(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-22f, -14f), new Vector2(112f, 26f));

                var badgeText = Ui.Label("T", badge.transform, "敌方专用", Theme.Caption,
                    Theme.Danger, TextAlignmentOptions.Center);
                Ui.Stretch(badgeText.rectTransform, 4f, 1f, 4f, 1f);
            }

            // 宽度必须显式算出来：行是固定宽度的，用负 sizeDelta 配错 pivot 会让
            // 文本区宽度变成负数，TMP 会把每个字单独折成一行。
            var text = Ui.Label("Text", row.transform, entry.Text, Theme.Caption,
                Theme.TextDim, TextAlignmentOptions.TopLeft);
            text.enableWordWrapping = true;
            Ui.Fixed(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -44f), new Vector2(textWidth, height - 50f));

            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        // ---------------------------------------------------------------- 成就

        private void BuildAchievements(RectTransform parent)
        {
            var content = MakeScroll(parent);

            var head = Ui.Label("TrophyLine", content,
                "当前奖杯 " + App.Meta.Trophy + "  ·  数字类成就需要先达成进度，再花奖杯兑换",
                Theme.Caption, Theme.Gold, TextAlignmentOptions.Center);
            Ui.FixedSize(head.gameObject, 1440f, 34f);

            var list = App.Achievements != null ? App.Achievements.All : null;
            if (list == null || list.Count == 0)
            {
                var empty = Ui.Label("Empty", content, "成就表未加载", Theme.Body, Theme.TextFaint, TextAlignmentOptions.Center);
                Ui.FixedSize(empty.gameObject, 1440f, 60f);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                BuildAchievementRow(content, list[i]);
            }

            Ui.Node("Filler", content);
        }

        private void BuildAchievementRow(RectTransform parent, AchievementDef def)
        {
            bool unlocked = App.Meta.IsUnlocked(def.Id);
            bool ready = App.Director.MeetsRequirement(def);
            long progress = App.Director.Progress(def);
            bool canBuy = !unlocked && ready && App.Meta.Trophy >= def.TrophyCost;

            // 隐藏成就达成之前只占一行：名字和条件都不给，
            // 否则「隐藏」这一点在界面上完全体现不出来。
            bool secret = def.Hidden && !ready && !unlocked;

            Color accent = unlocked ? Theme.Accent : (ready ? Theme.Gold : Theme.Border);

            var row = Ui.Rounded("Row", parent, Theme.RadiusM,
                unlocked ? Theme.WithAlpha(Theme.PanelRaised, 1f) : Theme.WithAlpha(Theme.PanelSunken, 1f),
                Theme.WithAlpha(accent, unlocked ? 0.75f : 0.45f), 1);
            Ui.FixedSize(row.gameObject, 1440f, 92f);

            var stripe = Ui.Panel("Stripe", row.transform, SpriteForge.Solid(), accent);
            Ui.Fixed(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));

            var badge = Ui.Panel("Badge", row.transform,
                SpriteForge.Circle(72, unlocked ? Theme.AccentDeep : Theme.PanelRaised,
                    unlocked ? Theme.Accent : Theme.Border, 2), Color.white);
            Ui.Fixed(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(52f, 52f));

            // 用 √ 而不是 ✓：Alibaba PuHuiTi 本身没有 U+2713 字形（✓ 会渲染成方框），√ 是它自带的对勾符号。
            var mark = Ui.Label("Mark", badge.transform, unlocked ? "√" : (secret ? "？" : def.Order.ToString()), Theme.H4,
                unlocked ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);

            var name = Ui.Label("Name", row.transform, secret ? "？？？" : def.Name, Theme.H4,
                unlocked ? Theme.Text : (ready ? Theme.Gold : Theme.TextDim), TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, 20f), new Vector2(560f, 32f));

            // 一行只讲一件事：左边讲条件，右边给状态。
            // 条件句里的门槛数字来自成就表（「累计造成 1,000 点伤害」），
            // 后面只补当前读数，不再写一遍「/ 1,000」—— 同一个数字在一行里
            // 出现两遍，读起来像没填完的表格。
            string detail = secret
                ? "隐藏成就 · 达成后揭晓"
                : def.IsCounter
                    ? def.Text + "  ·  已达 " + Format(progress)
                    : def.Text;

            var text = Ui.Label("Text", row.transform, detail, Theme.Caption,
                unlocked ? Theme.TextDim : Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, -20f), new Vector2(880f, 28f));

            if (unlocked)
            {
                var tag = Ui.Rounded("Tag", row.transform, Theme.RadiusS,
                    Theme.WithAlpha(Theme.AccentDeep, 0.55f), Theme.WithAlpha(Theme.Accent, 0.7f), 1);
                Ui.Fixed(tag.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(190f, 46f));

                var tagText = Ui.Label("TagText", tag.transform, "已达成", Theme.Caption, Theme.Accent, TextAlignmentOptions.Center);
                Ui.Stretch(tagText.rectTransform, 0f, 0f, 0f, 0f);
                return;
            }

            if (!ready)
            {
                // 未达成不给标签。三十多行都挂一块「未达成」只是噪音：
                // 名字与读数都已经是暗色，右边空着本身就说明还没到。
                return;
            }

            var btn = Ui.TextButton("Buy", row.transform, "兑换 · " + def.TrophyCost + " 奖杯", Theme.Caption,
                canBuy ? Theme.Gold : Theme.Panel, canBuy ? Theme.BgDeep : Theme.TextFaint,
                () => Purchase(def));
            btn.interactable = canBuy;
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(230f, 54f));
        }

        /// <summary>千分位整数：成就表里的门槛、行内的读数用同一套写法。</summary>
        private static string Format(long value)
        {
            return value.ToString("N0");
        }

        private void Purchase(AchievementDef def)
        {
            string message;
            if (!App.Director.TryPurchase(def, out message))
            {
                App.Toast(message);
                App.Audio.PlaySfx("ui_click");
                return;
            }

            App.Audio.PlaySfx("reward");
            App.ReportAchievement(def);
            Show(Tab.Achievements);
        }

        // ---------------------------------------------------------------- 通用

        private static RectTransform MakeScroll(RectTransform parent)
        {
            var viewport = Ui.Node("Viewport", parent);
            var img = viewport.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.004f);
            img.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Ui.Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            // 必须让布局组接管宽高，否则 LayoutElement 的尺寸不会被套用，
            // 所有区块的高度都会是 0 而叠在同一位置。
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 46f;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            return content;
        }
    }
}
