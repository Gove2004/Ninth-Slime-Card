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

        protected override string Subtitle { get { return "锁扣一掰就开了，里面躺着一件还没被取走的东西。"; } }

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
            // 那一枚字符既不像箱子，也让整块 720 高的面板空得像没做完。
            var chest = Ui.Node("Chest", body);
            Ui.Fixed(chest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 96f), new Vector2(360f, 300f));

            var glow = Ui.Panel("Glow", chest, SpriteForge.Glow(256, Theme.Gold),
                Theme.WithAlpha(Color.white, 0.20f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(560f, 560f));

            var baseBox = Ui.Rounded("Base", chest, 18,
                Theme.WithAlpha(Theme.Darken(Theme.Gold, 0.72f), 0.96f), Theme.WithAlpha(Theme.Gold, 0.85f), 4);
            Ui.Fixed(baseBox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(340f, 182f));

            var lid = Ui.Rounded("Lid", chest, 92,
                Theme.WithAlpha(Theme.Darken(Theme.Gold, 0.64f), 0.98f), Theme.WithAlpha(Theme.Gold, 0.9f), 4);
            Ui.Fixed(lid.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 176f), new Vector2(340f, 124f));

            // 盖顶一道高光：只有描边的话，箱盖会和箱体糊成一整块金色。
            var shine = Ui.Rounded("Shine", lid.transform, 40, Theme.WithAlpha(Theme.Gold, 0.30f),
                Color.clear, 0);
            Ui.Fixed(shine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(250f, 22f));

            for (int i = 0; i < 2; i++)
            {
                var strap = Ui.Solid("Strap", chest, Theme.WithAlpha(Theme.Gold, 0.55f));
                Ui.Fixed(strap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(i == 0 ? -104f : 104f, 18f), new Vector2(16f, 282f));
            }

            var clasp = Ui.Rounded("Clasp", chest, 8, Theme.Gold, Theme.Lighten(Theme.Gold, 0.45f), 2);
            Ui.Fixed(clasp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 152f), new Vector2(58f, 74f));

            var hole = Ui.Rounded("Hole", clasp.transform, 6, Theme.BgDeep, Color.clear, 0);
            Ui.Fixed(hole.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f), new Vector2(14f, 26f));

            Tween.PopInScaled(chest, 1f, 0f, 0.9f);

            var hint = Ui.Label("Hint", body, "打开它会直接得到一张卡牌，不占奖励次数。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -110f), new Vector2(900f, 32f));

            var open = ActionButton(body, "打 开 宝 箱", new Vector2(0f, 20f), new Vector2(440f, 88f), Theme.Gold);
            open.GetComponentInChildren<TextMeshProUGUI>().color = Theme.BgDeep;
            open.onClick.AddListener(onOpen);
        }

        private void onOpen()
        {
            App.Audio.PlaySfx("mana");
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
            var gotten = Ui.Label("Gotten", body,
                "获得 " + goldGained + " 金币和一张卡牌",
                Theme.Body, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(gotten.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(900f, 32f));

            if (card == null)
            {
                // 卡池为空这种事不该发生，但真发生了也别把玩家卡在这个屏里
                var empty = Ui.Label("Empty", body, "宝箱是空的。", Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
                Ui.Fixed(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(600f, 32f));
                var leave = ActionButton(body, "继 续 前 行", new Vector2(0f, 30f), new Vector2(420f, 76f), Theme.AccentDeep);
                leave.onClick.AddListener(Leave);
                return;
            }

            // body 高 520，卡片按 1.0 缩放是 312 高：底边留 120 给按钮，顶边不压住顶部文案
            const float CardY = 112f;
            const float ButtonY = 20f;

            var view = CardView.Create(body, card, v => onTake());
            view.SetPosition(new Vector2(0f, CardY), 0f, CardScale);
            view.Root.localScale = Vector3.one * (CardScale * 0.8f);
            Tween.Scale(view.Root, CardScale * 0.8f, CardScale, 0.34f, Easing.OutBack);

            if (taken)
            {
                view.SetDimmed(true);
                var done = ActionButton(body, "继 续 前 行", new Vector2(0f, ButtonY), new Vector2(420f, 76f), Theme.AccentDeep);
                done.onClick.AddListener(Leave);
                return;
            }

            var take = ActionButton(body, "收 入 牌 组", new Vector2(-230f, ButtonY), new Vector2(400f, 74f), Theme.AccentDeep);
            take.onClick.AddListener(onTake);

            var skip = ActionButton(body, "不 要", new Vector2(230f, ButtonY), new Vector2(400f, 74f), Theme.PanelRaised);
            skip.GetComponentInChildren<TextMeshProUGUI>().color = Theme.TextDim;
            skip.onClick.AddListener(() =>
            {
                App.Audio.PlaySfx("ui");
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

            App.Audio.PlaySfx("mana");

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
