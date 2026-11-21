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

            // 血量读数贴顶左对齐：满血时换绿色，血红色在这一行读起来像
            // 「你受伤了」，而「生命已满」恰恰是这一屏最好的消息。
            var hpInfo = Ui.Label("HpInfo", body,
                "当前生命 " + director.Hp + " / " + director.HpMax +
                (resolved ? "" : full ? "　·　生命已满" : "　·　休息可回复 " + actual),
                Theme.H3, full && !resolved ? Theme.Accent : Theme.Hp, TextAlignmentOptions.Left);
            Ui.Fixed(hpInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(4f, -16f), new Vector2(1100f, 44f));

            if (resolved)
            {
                var leave = BottomButton(body, "继 续 前 行", 0f, new Vector2(560f, 88f), Theme.AccentDeep);
                leave.onClick.AddListener(Leave);
                return;
            }

            // 两个选项做成左右分立的大卡：占画面主体，直接落场景——
            // 篝火屏唯一的决策就是它们，不再挤在中央面板里。
            BuildOption("Rest", full, "休 息", "回复 " + actual + " 点生命",
                full ? "生命已满，休息不会有收益" : "火光能撑到天亮", Theme.Accent, OnRest);
            BuildOption("Forge", true, "锻 造", "升级一张牌",
                "数值提高，费用不变", Theme.Gold, OnForge);
        }

        /// <summary>一枚 460x330 的竖排选项卡，左右分立。</summary>
        private void BuildOption(string name, bool enabled,
            string title, string highlight, string note, Color accent, System.Action onClick)
        {
            const float w = 460f;
            const float h = 330f;

            var card = Ui.Rounded(name, body, Theme.RadiusL,
                Theme.WithAlpha(Theme.PanelRaised, 0.94f), Theme.WithAlpha(accent, enabled ? 0.7f : 0.25f), 2, true);
            Ui.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(name == "Rest" ? -300f : 300f, -30f), new Vector2(w, h));

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
            Ui.Fixed(mark.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -64f), new Vector2(76f, 76f));

            var t = Ui.Label("Title", card.transform, title, Theme.H2, enabled ? accent : Theme.TextFaint,
                TextAlignmentOptions.Center);
            Ui.Fixed(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -142f), new Vector2(w, 50f));

            var hl = Ui.Label("Highlight", card.transform, highlight, Theme.H4,
                enabled ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(hl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -208f), new Vector2(w - 48f, 34f));

            var n = Ui.Label("Note", card.transform, note, Theme.Body,
                Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(n.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -254f), new Vector2(w - 48f, 30f));
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

            var title = Ui.Label("Pick", body, "选择一张牌升级", Theme.H3, Theme.Gold, TextAlignmentOptions.Left);
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(4f, -14f), new Vector2(900f, 44f));

            RectTransform content;
            var scroll = Ui.VerticalScroll("List", body, out content);
            // 滚动区底部必须让开「跳过」按钮（沉底一行），否则最后两行被按钮压住点不到
            Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -60f), new Vector2(1400f, 560f));

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
                new Vector2(0f, 56f), new Vector2(480f, 70f));
        }

        private RectTransform BuildRow(RectTransform parent, CardInstance card, bool maxed, int index)
        {
            Color series = Theme.SeriesColor(card.Def.Series);

            var img = Ui.Rounded("Row", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(series, 0.7f), 0.94f), Theme.WithAlpha(series, 0.5f), 1, true);
            Ui.FixedSize(img.gameObject, 1400f, 64f);
            img.color = maxed ? new Color(1f, 1f, 1f, 0.4f) : Color.white;

            var name = Ui.Label("Name", img.transform, card.Name + (maxed ? "（已升级）" : ""),
                Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 8f), new Vector2(420f, 28f));

            var desc = Ui.Label("Desc", img.transform, card.DisplayText, Theme.Caption,
                Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, -16f), new Vector2(1200f, 22f));

            var cost = Ui.Label("Cost", img.transform, card.Cost.ToString(), Theme.H4,
                Theme.Lighten(series, 0.4f), TextAlignmentOptions.Center);
            Ui.Fixed(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-50f, 0f), new Vector2(52f, 34f));

            return img.rectTransform;
        }
    }
}
