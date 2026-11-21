using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 宝箱。翻开就有收益，但只给一张牌——不做三选一。
    /// 三选一属于战斗奖励，宝箱的价值在于「金币 + 一张确定更好的牌」，走的是白给路线。
    /// </summary>
    public sealed class TreasureScreen : NodeScreen
    {
        private const float CardScale = 1.12f;

        private RectTransform body;
        private CardDef card;
        private bool opened;
        private bool taken;
        private int goldGained;

        protected override string Title { get { return "宝 箱"; } }

        public override string BgKey { get { return "bg_treasure"; } }

        protected override Color Accent { get { return Theme.Gold; } }

        protected override void BuildContent(RectTransform panel)
        {
            body = panel;
            render();
        }

        private void render()
        {
            Ui.ClearChildren(body);

            if (!opened)
            {
                buildChest();
                return;
            }

            buildContent();
        }

        // ---------------------------------------------------------------- 未开启

        private void buildChest()
        {
            // 宝箱用几块圆角底板拼出来，不再用一个圈里写个「箱」字顶事：
            // 那一枚字符既不像箱子，也让整屏空得像没做完。
            var chest = Ui.Node("Chest", body);
            Ui.Fixed(chest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 130f), new Vector2(450f, 375f));

            var glow = Ui.Panel("Glow", chest, SpriteForge.Glow(256, Theme.Gold),
                Theme.WithAlpha(Color.white, 0.22f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(760f, 760f));

            var baseBox = Ui.Rounded("Base", chest, 22,
                Theme.WithAlpha(Theme.Darken(Theme.Gold, 0.72f), 0.96f), Theme.WithAlpha(Theme.Gold, 0.85f), 4);
            Ui.Fixed(baseBox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(425f, 228f));

            var lid = Ui.Rounded("Lid", chest, 110,
                Theme.WithAlpha(Theme.Darken(Theme.Gold, 0.64f), 0.98f), Theme.WithAlpha(Theme.Gold, 0.9f), 4);
            Ui.Fixed(lid.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 220f), new Vector2(425f, 155f));

            // 盖顶一道高光：只有描边的话，箱盖会和箱体糊成一整块金色。
            var shine = Ui.Rounded("Shine", lid.transform, 48, Theme.WithAlpha(Theme.Gold, 0.30f),
                Color.clear, 0);
            Ui.Fixed(shine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(310f, 26f));

            for (int i = 0; i < 2; i++)
            {
                var strap = Ui.Solid("Strap", chest, Theme.WithAlpha(Theme.Gold, 0.55f));
                Ui.Fixed(strap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(i == 0 ? -130f : 130f, 22f), new Vector2(20f, 352f));
            }

            var clasp = Ui.Rounded("Clasp", chest, 10, Theme.Gold, Theme.Lighten(Theme.Gold, 0.45f), 2);
            Ui.Fixed(clasp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 190f), new Vector2(72f, 92f));

            var hole = Ui.Rounded("Hole", clasp.transform, 6, Theme.BgDeep, Color.clear, 0);
            Ui.Fixed(hole.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f), new Vector2(14f, 26f));

            Tween.PopInScaled(chest, 1f, 0f, 0.9f);

            var open = BottomButton(body, "打 开 宝 箱", 0f, new Vector2(640f, 92f), Theme.Gold);
            open.GetComponentInChildren<TextMeshProUGUI>().color = Theme.BgDeep;
            open.onClick.AddListener(onOpen);
        }

        private void onOpen()
        {
            App.Audio.PlaySfx("treasure");
            card = App.Director.RollTreasureCard();
            opened = true;

            // 宝箱里顺手放一笔金币：金币只有商店和事件能花掉，
            // 如果宝箱不给，商店就会变成「只有精英战之后才能用」的地方。
            goldGained = GoldFor(App.Director.Chapter, App.Director.Floor);
            App.Director.Gold += goldGained;
            App.Director.GoldEarned += goldGained;
            App.Toast("获得 " + goldGained + " 金币");

            render();
        }

        // ---------------------------------------------------------------- 已开启

        private void buildContent()
        {
            if (card == null)
            {
                // 卡池为空这种事不该发生，但真发生了也别把玩家卡在这个屏里
                var empty = Ui.Label("Empty", body, "宝箱是空的。", Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
                Ui.Fixed(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(600f, 32f));
                var leave = BottomButton(body, "继 续 前 行", 0f, new Vector2(560f, 88f), Theme.AccentDeep);
                leave.onClick.AddListener(Leave);
                return;
            }

            // 金币收益走 toast，不再占一行版面；画面主体就是那张飞出来的卡。
            const float CardY = 180f;

            var view = CardView.Create(body, card, v => onTake());
            view.SetPosition(new Vector2(0f, CardY), 0f, CardScale);
            view.Root.localScale = Vector3.one * (CardScale * 0.8f);
            Tween.Scale(view.Root, CardScale * 0.8f, CardScale, 0.34f, Easing.OutBack);

            if (taken)
            {
                view.SetDimmed(true);
                var done = BottomButton(body, "继 续 前 行", 0f, new Vector2(560f, 88f), Theme.AccentDeep);
                done.onClick.AddListener(Leave);
                return;
            }

            var take = BottomButton(body, "收 入 牌 组", -330f, new Vector2(600f, 88f), Theme.AccentDeep);
            take.onClick.AddListener(onTake);

            var skip = BottomButton(body, "不 要", 330f, new Vector2(600f, 88f), Theme.PanelRaised);
            skip.GetComponentInChildren<TextMeshProUGUI>().color = Theme.TextDim;
            skip.onClick.AddListener(() =>
            {
                App.Audio.PlaySfx("ui_back");
                App.Toast("放弃了这张牌");
                Leave();
            });
        }

        /// <summary>宝箱金币：随章节与层数走高，后期才够买得起商店里的稀有牌。</summary>
        private static int GoldFor(int chapter, int floor)
        {
            return 25 + chapter * 10 + floor * 2;
        }

        private void onTake()
        {
            if (taken || card == null)
            {
                return;
            }

            App.Audio.PlaySfx("reward");

            // 牌组满员时不再弹替换面板：宝箱是顺手捡的东西，
            // 为一个可能不需要的牌打断流程去挑弃牌，代价比收益高。
            if (App.Director.AtDeckCap)
            {
                App.Toast("牌组已满，无法收入");
                Leave();
                return;
            }

            App.Director.TakeReward(card);
            App.Toast("收入牌组 · " + card.Name);
            taken = true;
            render();
        }
    }
}
