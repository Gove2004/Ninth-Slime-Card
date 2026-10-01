using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>档案屏：卡牌图鉴（按八系列分组）+ 成就档案（用奖杯兑换）。</summary>
    public sealed class CodexScreen : Screen
    {
        public enum Tab
        {
            Cards = 0,
            Achievements = 1
        }

        private readonly Tab initialTab;
        private RectTransform body;

        public CodexScreen(Tab tab)
        {
            initialTab = tab;
        }

        protected override void Build()
        {
            BuildHeader("档案室", "八系列牌池与成就记录", "返回");
            body = BuildBody(142f);
            Show(initialTab);
        }

        private void Show(Tab tab)
        {
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(body.GetChild(i).gameObject);
            }

            var tabBar = Ui.Node("TabBar", body);
            Ui.Fixed(tabBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(700f, 66f));

            var row = Ui.Row(tabBar, 12, new RectOffset(0, 0, 0, 0));
            row.childForceExpandWidth = true;

            var cards = Ui.TextButton("TabCards", tabBar,
                "卡牌图鉴  " + App.Meta.DiscoveredCards.Count + " / " + App.Cards.Count,
                Theme.Body, tab == Tab.Cards ? Theme.AccentDeep : Theme.Panel,
                tab == Tab.Cards ? Theme.Text : Theme.TextDim, () => Show(Tab.Cards));
            Ui.FixedSize(cards.gameObject, 340f, 62f);

            int totalAchievements = App.Achievements != null ? App.Achievements.All.Count : 0;
            var achi = Ui.TextButton("TabAchi", tabBar,
                "成就档案  " + App.Meta.Unlocked.Count + " / " + totalAchievements,
                Theme.Body, tab == Tab.Achievements ? Theme.AccentDeep : Theme.Panel,
                tab == Tab.Achievements ? Theme.Text : Theme.TextDim, () => Show(Tab.Achievements));
            Ui.FixedSize(achi.gameObject, 340f, 62f);

            var content = Ui.Node("Content", body);
            Ui.Stretch(content, 0f, 78f, 0f, 0f);

            if (tab == Tab.Cards)
            {
                BuildCards(content);
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

            var count = Ui.Label("Count", header.transform, "已发现 " + shown + " / " + list.Count, Theme.Caption,
                Theme.TextFaint, TextAlignmentOptions.Right);
            Ui.Fixed(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(240f, 30f));
        }

        private void BuildSeriesGrid(RectTransform parent, List<CardDef> list)
        {
            const int columns = 5;
            const float cellW = 282f;
            const float cellH = 116f;
            const float gap = 12f;

            int rows = (list.Count + columns - 1) / columns;

            var gridNode = Ui.Node("Grid", parent);
            var grid = Ui.Grid(gridNode, new Vector2(cellW, cellH), new Vector2(gap, gap), columns);
            grid.padding = new RectOffset(0, 0, 0, 0);

            for (int i = 0; i < list.Count; i++)
            {
                BuildCardTile(gridNode, list[i]);
            }

            // 补齐空位，保证下一段从新的一行开始
            int pad = rows * columns - list.Count;
            for (int i = 0; i < pad; i++)
            {
                Ui.Node("Pad", gridNode);
            }

            Ui.FixedSize(gridNode.gameObject, columns * cellW + (columns - 1) * gap, rows * cellH + (rows - 1) * gap);
        }

        private void BuildCardTile(RectTransform parent, CardDef def)
        {
            bool known = App.Meta.DiscoveredCards.Contains(def.Id);
            Color color = Theme.SeriesColor(def.Series);

            var tile = Ui.Rounded("Tile" + def.Id, parent, Theme.RadiusS,
                known ? Theme.WithAlpha(Theme.Darken(color, 0.70f), 0.95f) : Theme.PanelSunken,
                known ? Theme.WithAlpha(color, 0.65f) : Theme.BorderSoft, 1);
            Ui.FixedSize(tile.gameObject, 282f, 116f);

            var bar = Ui.Panel("Bar", tile.transform,
                SpriteForge.Panel(6, known ? color : Theme.TextFaint, Theme.Lighten(color, 0.2f), 1), Color.white);
            bar.type = Image.Type.Sliced;
            Ui.Fixed(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(7f, 0f));

            var cost = Ui.Label("Cost", tile.transform, def.Cost + " 费", Theme.Caption,
                known ? Theme.Lighten(color, 0.35f) : Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(cost.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(120f, 24f));

            var series = Ui.Label("Series", tile.transform, Series.DisplayName(def.Series), 13,
                Theme.TextFaint, TextAlignmentOptions.Right);
            Ui.Fixed(series.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(110f, 24f));

            var name = Ui.Label("Name", tile.transform, known ? def.Name : "？？？", Theme.H4,
                known ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -34f), new Vector2(250f, 28f));

            string body = known ? def.Text() : "在冒险中获取后解锁";
            var text = Ui.Label("Text", tile.transform, body, 14,
                known ? Theme.TextDim : Theme.TextFaint, TextAlignmentOptions.TopLeft);
            Ui.Fixed(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -58f), new Vector2(250f, 50f));
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
            var mark = Ui.Label("Mark", badge.transform, unlocked ? "√" : def.Order.ToString(), Theme.H4,
                unlocked ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);

            var name = Ui.Label("Name", row.transform, def.Name, Theme.H4,
                unlocked ? Theme.Text : (ready ? Theme.Gold : Theme.TextDim), TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, 20f), new Vector2(560f, 32f));

            string detail;
            if (def.IsCounter)
            {
                detail = def.Text + "   （进度 " + Format(progress) + " / " + Format(def.Threshold) + "）";
            }
            else
            {
                detail = def.Text + "   （特殊条件）";
            }

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

            string caption = ready ? ("兑换 · " + def.TrophyCost + " 奖杯") : ("需 " + Format(def.Threshold));
            var btn = Ui.TextButton("Buy", row.transform, caption, Theme.Caption,
                canBuy ? Theme.Gold : Theme.Panel, canBuy ? Theme.BgDeep : Theme.TextFaint,
                () => Purchase(def));
            btn.interactable = canBuy;
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-26f, 0f), new Vector2(230f, 54f));
        }

        /// <summary>千分位整数：进度和门槛用同一套写法，避免"12.9万 / 100M"这种混用。</summary>
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
                App.Audio.PlaySfx("ui");
                return;
            }

            App.Audio.PlaySfx("mana");
            App.ReportAchievement(def.Id, def.Name);
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
