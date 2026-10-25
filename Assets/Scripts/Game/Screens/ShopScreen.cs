using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 商人。四张明码标价的牌，外加一次付费删牌。
    /// 删牌服务的定价刻意偏高：牌组变薄是长局里最强的成长，如果便宜，
    /// 商店就会退化成「每次进来只删牌」的固定动作。
    /// </summary>
    public sealed class ShopScreen : NodeScreen
    {
        private const float CardScale = 0.86f;
        private const float CardGap = 28f;

        private sealed class Offer
        {
            public CardDef Def;
            public int Price;
            public bool Sold;
        }

        private readonly List<Offer> offers = new List<Offer>();
        private readonly List<CardView> views = new List<CardView>();

        private RectTransform body;
        private TextMeshProUGUI goldLabel;
        private Button removeButton;
        private TextMeshProUGUI removeCaption;
        private bool removeUsed;
        private int removePrice;

        protected override string Title { get { return "商 人"; } }

        protected override string Subtitle { get { return "他把货摊在布上，不问你从哪来，只问你带了多少。"; } }

        protected override Color Accent { get { return Theme.SeriesThunder; } }

        protected override void BuildContent(RectTransform panel)
        {
            body = panel;
            rollOffers();
            render();
        }

        /// <summary>库存只在进入时决定一次，之后重绘不再改动价格。</summary>
        private void rollOffers()
        {
            var director = App.Director;
            var pool = DeckRules.RewardPool(App.Cards, director.Chapter, director.Chapter >= 2);

            // 商店用自己的随机源，避免和地图/战斗的序列互相干扰
            var rng = new DeterministicRng(director.Chapter * 7331 + director.Floor * 97 + director.DeckCount * 13);

            var bag = new List<CardDef>(pool);
            rng.Shuffle(bag);

            for (int i = 0; i < bag.Count && offers.Count < 4; i++)
            {
                var def = bag[i];

                // 同一系列最多出现一张，避免整摊都是同色
                bool dup = false;
                for (int k = 0; k < offers.Count; k++)
                {
                    if (offers[k].Def.Series == def.Series)
                    {
                        dup = true;
                        break;
                    }
                }

                if (dup)
                {
                    continue;
                }

                offers.Add(new Offer { Def = def, Price = PriceOf(def) });
            }

            // 系列分散后凑不满就补，宁可同色也不要空位
            for (int i = 0; i < bag.Count && offers.Count < 4; i++)
            {
                if (contains(bag[i]))
                {
                    continue;
                }

                offers.Add(new Offer { Def = bag[i], Price = PriceOf(bag[i]) });
            }

            removePrice = 70 + director.Chapter * 15;
        }

        private bool contains(CardDef def)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Def.Id == def.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private static int PriceOf(CardDef def)
        {
            int chapter = GameApp.I.Director.Chapter;
            int basePrice;
            switch (def.Rarity)
            {
                case 2: basePrice = 130; break;
                case 1: basePrice = 88; break;
                default: basePrice = 52; break;
            }

            return basePrice + (chapter - 1) * 18;
        }

        // ---------------------------------------------------------------- 渲染

        private void render()
        {
            Ui.ClearChildren(body);

            views.Clear();

            var director = App.Director;

            goldLabel = Ui.Label("Gold", body, "金币 " + director.Gold, Theme.H2,
                Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(goldLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -4f), new Vector2(600f, 42f));

            buildOffers();

            // 底部：删牌服务 + 离开
            removeButton = Ui.TextButton("Remove", body,
                "移除一张牌　" + removePrice + " 金币", Theme.H3,
                removeUsed ? Theme.PanelRaised : Theme.PanelRaised,
                removeUsed ? Theme.TextFaint : Theme.Gold, onRemoveClicked);
            removeCaption = removeButton.GetComponentInChildren<TextMeshProUGUI>();
            Ui.Fixed(removeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-240f, 20f), new Vector2(440f, 74f));

            var leave = Ui.TextButton("Leave", body, "离 开 商 店", Theme.H3,
                Theme.AccentDeep, Theme.Text, Leave);
            Ui.Fixed(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(240f, 20f), new Vector2(440f, 74f));

            refreshAffordability();
        }

        private void buildOffers()
        {
            if (offers.Count == 0)
            {
                var none = Ui.Label("None", body, "今天没什么好货。", Theme.Body,
                    Theme.TextDim, TextAlignmentOptions.Center);
                Ui.Fixed(none.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 40f), new Vector2(800f, 32f));
                return;
            }

            float step = CardView.W * CardScale + CardGap;
            float left = -(offers.Count - 1) * 0.5f * step;

            // body 高 520。四张卡按 0.86 缩放是 268 高：
            // 底边留 150 给价格牌与按钮，顶边不压住金币行。
            const float CardY = 150f;

            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                var offer = offers[i];

                var view = CardView.Create(body, offer.Def, v => onBuy(index));
                view.SetPosition(new Vector2(left + step * i, CardY), 0f, CardScale);
                views.Add(view);

                var rt = view.Root;
                rt.localScale = Vector3.one * (CardScale * 0.82f);
                Tween.Scale(rt, CardScale * 0.82f, CardScale, 0.3f, Easing.OutBack, null, 0.05f * i);

                // 价格牌挂在卡下沿。卡片是底锚定的，价格牌同样用底锚点，
                // 否则两套坐标系会在卡面上撞到一起。
                float priceY = CardY - 44f;
                var tag = Ui.Rounded("Price", body, Theme.RadiusS,
                    Theme.WithAlpha(Theme.PanelSunken, 0.95f),
                    Theme.WithAlpha(offer.Sold ? Theme.TextFaint : Theme.Gold, 0.7f), 1);
                Ui.Fixed(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(left + step * i, priceY), new Vector2(150f, 40f));

                var priceText = Ui.Label("T", tag.transform,
                    offer.Sold ? "已 售 出" : offer.Price + " 金币",
                    Theme.Body, offer.Sold ? Theme.TextFaint : Theme.Gold, TextAlignmentOptions.Center);
                Ui.Stretch(priceText.rectTransform, 4f, 2f, 4f, 2f);

                // 售出的牌要在重建时就压暗并关掉交互。
                // 只靠 refreshAffordability 不够——它在这之后才跑，
                // 而 SetAffordable 会把一张已售出的牌重新点亮成可点。
                if (offer.Sold)
                {
                    view.SetDimmed(true);
                    view.SetInteractive(false);
                }
            }
        }

        private void refreshAffordability()
        {
            var director = App.Director;
            goldLabel.text = "金币 " + director.Gold;

            for (int i = 0; i < views.Count && i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer.Sold)
                {
                    continue;
                }

                bool affordable = director.Gold >= offer.Price;
                // 买不起的牌压暗但不隐藏：让玩家看得见买不起什么，才有「攒钱」的动机
                views[i].SetAffordable(affordable);
            }

            if (removeButton != null)
            {
                bool canRemove = !removeUsed && director.Gold >= removePrice && director.DeckCount > 1;
                removeButton.interactable = canRemove;
                removeCaption.color = removeUsed ? Theme.TextFaint
                    : (canRemove ? Theme.Gold : Theme.TextFaint);
            }
        }

        // ---------------------------------------------------------------- 交互

        private void onBuy(int index)
        {
            if (index < 0 || index >= offers.Count)
            {
                return;
            }

            var offer = offers[index];
            if (offer.Sold)
            {
                return;
            }

            var director = App.Director;
            if (director.Gold < offer.Price)
            {
                App.Audio.PlaySfx("ui");
                App.Toast("金币不足（需要 " + offer.Price + "）");
                return;
            }

            if (director.AtDeckCap)
            {
                App.Audio.PlaySfx("ui");
                App.Toast("牌组已满 " + director.DeckCount + " / " + DeckRules.DeckCap);
                return;
            }

            director.Gold -= offer.Price;
            director.TakeReward(offer.Def);
            offer.Sold = true;

            App.Audio.PlaySfx("mana");
            App.Toast("买下了《" + offer.Def.Name + "》");
            render();
        }

        private void onRemoveClicked()
        {
            var director = App.Director;

            if (removeUsed)
            {
                return;
            }

            if (director.Gold < removePrice)
            {
                App.Audio.PlaySfx("ui");
                App.Toast("金币不足（需要 " + removePrice + "）");
                return;
            }

            App.Audio.PlaySfx("ui");
            showRemovePicker();
        }

        /// <summary>列出整个牌组供挑选，删掉的那张真的从本局消失。</summary>
        private void showRemovePicker()
        {
            var overlay = Ui.Solid("RemoveOverlay", Root, new Color(0f, 0f, 0f, 0.74f), true);
            overlay.rectTransform.SetAsLastSibling();

            var panel = Ui.Rounded("RemovePanel", overlay.transform, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.99f), Theme.Gold, 2);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1180f, 820f));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            var title = Ui.Label("Title", panel.transform,
                "移除一张牌　" + removePrice + " 金币", Theme.H2, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(1000f, 44f));

            var hint = Ui.Label("Hint", panel.transform,
                "移除后本局不再出现。初始的斩击与格挡是最该换掉的东西。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -68f), new Vector2(1000f, 30f));

            RectTransform listContent;
            var scroll = Ui.VerticalScroll("List", panel.transform, out listContent);
            Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -16f), new Vector2(1040f, 560f));

            var composition = App.Director.DeckComposition();
            var ids = new List<int>(composition.Keys);
            ids.Sort((a, b) =>
            {
                var da = App.Cards.Get(a);
                var db = App.Cards.Get(b);
                if (da == null || db == null)
                {
                    return 0;
                }

                return da.Cost != db.Cost ? da.Cost - db.Cost : da.Id - db.Id;
            });

            for (int i = 0; i < ids.Count; i++)
            {
                var def = App.Cards.Get(ids[i]);
                if (def == null)
                {
                    continue;
                }

                int cardId = ids[i];
                int owned = composition[cardId];
                var row = buildRemoveRow(listContent, def, owned, () =>
                {
                    if (!App.Director.RemoveFromDeck(cardId))
                    {
                        return;
                    }

                    App.Director.Gold -= removePrice;
                    removeUsed = true;

                    App.Audio.PlaySfx("play");
                    App.Toast("移除了《" + def.Name + "》");
                    Object.Destroy(overlay.gameObject);
                    render();
                });
            }

            var cancel = Ui.TextButton("Cancel", panel.transform, "算了，什么都不买", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, () =>
                {
                    App.Audio.PlaySfx("ui");
                    Object.Destroy(overlay.gameObject);
                });
            Ui.Fixed(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(420f, 60f));
        }

        private GameObject buildRemoveRow(RectTransform parent, CardDef def, int owned, System.Action onPick)
        {
            Color series = Theme.SeriesColor(def.Series);

            var img = Ui.Rounded("Row", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(series, 0.7f), 0.94f), Theme.WithAlpha(series, 0.5f), 1, true);
            Ui.FixedSize(img.gameObject, 1000f, 70f);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                App.Audio.PlaySfx("ui");
                Tween.Punch(img.rectTransform, 0.03f, 0.2f);
                onPick();
            });

            var cost = Ui.Label("Cost", img.transform, def.Cost.ToString(), Theme.H3,
                Theme.Lighten(series, 0.4f), TextAlignmentOptions.Center);
            Ui.Fixed(cost.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(38f, 0f), new Vector2(48f, 40f));

            var name = Ui.Label("Name", img.transform, def.Name, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(74f, 10f), new Vector2(380f, 30f));

            var desc = Ui.Label("Desc", img.transform, def.Text(0), Theme.Caption, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(74f, -17f), new Vector2(680f, 24f));

            if (owned > 1)
            {
                var count = Ui.Label("Count", img.transform, "×" + owned, Theme.Caption,
                    Theme.TextFaint, TextAlignmentOptions.Center);
                Ui.Fixed(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-36f, 0f), new Vector2(70f, 26f));
            }

            return img.gameObject;
        }
    }
}
