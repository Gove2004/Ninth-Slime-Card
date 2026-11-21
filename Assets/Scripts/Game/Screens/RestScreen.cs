using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 篝火。二选一：回血 or 升级一张牌。
    /// 这个选择的张力在于——血量和牌组强度在长局里互相挤占，玩家得自己判断哪边更缺。
    /// </summary>
    public sealed class RestScreen : NodeScreen
    {
        private RectTransform body;
        private bool resolved;

        protected override string Title { get { return "篝 火"; } }

        public override string BgKey { get { return "bg_rest"; } }

        protected override Color Accent { get { return Theme.Accent; } }

        protected override void BuildContent(RectTransform panel)
        {
            body = panel;
            Render();
        }

        private void Render()
        {
            Ui.ClearChildren(body);

            var director = App.Director;
            // 必须问 Director 要实际数值：这里写死公式的话，调平衡时界面上的
            // 「回复 N」会和真正回的血对不上，玩家看到的就是假信息。
            int heal = director.RestHealPreview;
            // 满血时回血毫无收益，显示「+57」只会误导玩家以为还有得赚。
            int actual = Mathf.Min(heal, director.HpMax - director.Hp);
            bool full = actual <= 0;

            // 满血时换绿色：血红色在这一行读起来像「你受伤了」，
            // 而「生命已满」恰恰是这一屏最好的消息。
            var hpInfo = Ui.Label("HpInfo", body,
                "当前生命 " + director.Hp + " / " + director.HpMax +
                (resolved ? "" : full ? "　·　生命已满" : "　·　休息可回复 " + actual),
                Theme.H3, full && !resolved ? Theme.Accent : Theme.Hp, TextAlignmentOptions.Center);
            Ui.Fixed(hpInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(900f, 40f));

            if (resolved)
            {
                var leave = Ui.TextButton("Leave", body, "继 续 前 行", Theme.H3, Theme.AccentDeep, Theme.Text, Leave);
                Ui.Fixed(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 40f), new Vector2(440f, 78f));
                return;
            }

            // 两个选项做成等宽大卡：标题 + 说明 + 图形，占满正文区中段。
            // 原来是两枚悬浮文字按钮挤在中线，上下各留 200 空屏，一屏只有三行字。
            // body 高 520：血量行贴顶（-6），选项卡居中（高 250，中心 -40），提示贴底（26）。
            BuildOption("Rest", -40f, full, "休 息", "回复 " + actual + " 点生命",
                full ? "生命已满，休息不会有收益" : "火光能撑到天亮", Theme.Accent, OnRest);
            BuildOption("Forge", -40f, true, "锻 造", "升级一张牌",
                "数值提高，费用不变", Theme.Gold, OnForge);
        }

        /// <summary>一枚 400x250 的竖排选项卡，横轴居中偏移 offsetX。</summary>
        private void BuildOption(string name, float offsetY, bool enabled,
            string title, string highlight, string note, Color accent, System.Action onClick)
        {
            const float w = 400f;
            const float h = 250f;

            var card = Ui.Rounded(name, body, Theme.RadiusL,
                Theme.WithAlpha(Theme.PanelRaised, 0.97f), Theme.WithAlpha(accent, enabled ? 0.65f : 0.25f), 2, true);
            Ui.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(name == "Rest" ? -230f : 230f, offsetY), new Vector2(w, h));

            if (enabled)
            {
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card;
                btn.onClick.AddListener(() =>
                {
                    App.Audio.PlaySfx("ui_click");
                    Tween.Punch(card.rectTransform, 0.06f, 0.2f);
                    onClick();
                });
            }

            var mark = Ui.Panel("Mark", card.transform, SpriteForge.Circle(96,
                Theme.Darken(accent, 0.5f), Theme.Lighten(accent, 0.3f), 3), Color.white);
            mark.raycastTarget = false;
            Ui.Fixed(mark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -58f), new Vector2(64f, 64f));

            var t = Ui.Label("Title", card.transform, title, Theme.H3, enabled ? accent : Theme.TextFaint,
                TextAlignmentOptions.Center);
            Ui.Fixed(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -118f), new Vector2(w, 40f));

            var hl = Ui.Label("Highlight", card.transform, highlight, Theme.Body,
                enabled ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(hl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -162f), new Vector2(w - 40f, 30f));

            var n = Ui.Label("Note", card.transform, note, Theme.Caption, Theme.TextDim,
                TextAlignmentOptions.Center);
            Ui.Fixed(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -198f), new Vector2(w - 40f, 26f));
        }

        private void OnRest()
        {
            App.Audio.PlaySfx("heal");
            int healed = App.Director.RestHeal();
            App.Toast("回复 " + healed + " 点生命");
            resolved = true;
            Render();
        }

        private void OnForge()
        {
            App.Audio.PlaySfx("ui_click");
            resolved = true;
            ShowUpgradePicker();
        }

        /// <summary>列出可升级的牌，点一张就升它。</summary>
        private void ShowUpgradePicker()
        {
            Ui.ClearChildren(body);

            var director = App.Director;

            var title = Ui.Label("Pick", body, "选择一张牌升级", Theme.H3, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(900f, 40f));

            RectTransform content;
            var scroll = Ui.VerticalScroll("List", body, out content);
            // 滚动区底部必须让开「跳过」按钮（占 24..84），否则最后两行被按钮压住点不到
            Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 26f), new Vector2(940f, 356f));

            for (int i = 0; i < director.Deck.Count; i++)
            {
                var card = director.Deck[i];
                bool maxed = card.Level > 0;
                var row = BuildRow(content, card, maxed, i);
                if (row != null)
                {
                    int index = i;
                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = row.GetComponent<Image>();
                    if (!maxed)
                    {
                        btn.onClick.AddListener(() =>
                        {
                            App.Audio.PlaySfx("reward");
                            App.Director.UpgradeCard(index);
                            App.Toast("已升级《" + card.Name + "》");
                            resolved = true;
                            Render();
                        });
                    }
                }
            }

            var skip = Ui.TextButton("Skip", body, "不升级，直接前进", Theme.Body, Theme.PanelRaised, Theme.TextDim, Leave);
            Ui.Fixed(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(360f, 60f));
        }

        private RectTransform BuildRow(RectTransform parent, CardInstance card, bool maxed, int index)
        {
            Color series = Theme.SeriesColor(card.Def.Series);

            var img = Ui.Rounded("Row", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(series, 0.7f), 0.94f), Theme.WithAlpha(series, 0.5f), 1, true);
            Ui.FixedSize(img.gameObject, 900f, 64f);
            img.color = maxed ? new Color(1f, 1f, 1f, 0.4f) : Color.white;

            var name = Ui.Label("Name", img.transform, card.Name + (maxed ? "（已升级）" : ""),
                Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 8f), new Vector2(420f, 28f));

            var desc = Ui.Label("Desc", img.transform, card.DisplayText, Theme.Caption,
                Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, -16f), new Vector2(700f, 22f));

            var cost = Ui.Label("Cost", img.transform, card.Cost.ToString(), Theme.H4,
                Theme.Lighten(series, 0.4f), TextAlignmentOptions.Center);
            Ui.Fixed(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-50f, 0f), new Vector2(52f, 34f));

            return img.rectTransform;
        }
    }
}
