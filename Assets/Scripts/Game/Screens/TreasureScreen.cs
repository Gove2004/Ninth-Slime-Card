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
            // 图标用中心锚点放在上方，按钮用底锚点压在最下，两者不再撞在一起
            var icon = Ui.Panel("Chest", body,
                SpriteForge.Circle(200, Theme.WithAlpha(Theme.Gold, 0.16f), Theme.WithAlpha(Theme.Gold, 0.7f), 3), Color.white);
            Ui.Fixed(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(200f, 200f));
            // 第二参是缩放倍率，不是尺寸：传 200 会把图标放大两百倍糊满整屏
            Tween.PopInScaled(icon.rectTransform, 1f, 0f, 0.86f);

            var glyph = Ui.Label("Glyph", icon.transform, "箱", Theme.H1, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Stretch(glyph.rectTransform, 0f, 0f, 0f, 0f);

            var hint = Ui.Label("Hint", body, "打开它会直接得到一张卡牌，不占奖励次数。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f), new Vector2(900f, 30f));

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
