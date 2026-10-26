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

        protected override string Subtitle { get { return "火光够撑到天亮。休息，或者磨一磨手里的牌。"; } }

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

            var hpInfo = Ui.Label("HpInfo", body,
                "当前生命 " + director.Hp + " / " + director.HpMax +
                (resolved ? "" : full ? "　·　生命已满" : "　·　休息可回复 " + actual),
                Theme.H3, Theme.Hp, TextAlignmentOptions.Center);
            Ui.Fixed(hpInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(900f, 40f));

            if (resolved)
            {
                var leave = Ui.TextButton("Leave", body, "继 续 前 行", Theme.H3, Theme.AccentDeep, Theme.Text, Leave);
                Ui.Fixed(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 40f), new Vector2(440f, 78f));
                return;
            }

            var restBtn = Ui.TextButton("Rest", body, full ? "休 息（生命已满）" : "休 息（+" + actual + " 生命）",
                Theme.H3, Theme.AccentDeep, Theme.Text, OnRest);
            restBtn.interactable = !full;
            Ui.Fixed(restBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260f, -10f), new Vector2(440f, 90f));

            var forgeBtn = Ui.TextButton("Forge", body, "锻 造（升级一张牌）", Theme.H3,
                Theme.PanelRaised, Theme.Gold, OnForge);
            Ui.Fixed(forgeBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(260f, -10f), new Vector2(440f, 90f));

            var tip = Ui.Label("Tip", body, "升级后的牌数值提高，费用不变——长局里比一次回血更值钱。",
                Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(900f, 26f));
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
            App.Audio.PlaySfx("ui");
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
                            App.Audio.PlaySfx("mana");
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
