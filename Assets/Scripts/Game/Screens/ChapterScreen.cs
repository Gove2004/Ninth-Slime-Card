using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 章节卡牌屏。每层发一组遭遇卡横排在面前，选一张进入，结算完推进到下一层。
    /// 没有连线与可达性：一屏只回答「这一层做哪件事」。
    /// 卡上只有巨大字形和类型名，没有任何说明小字。
    /// </summary>
    public sealed class ChapterScreen : Screen
    {
        private const float CardW = 440f;
        private const float CardH = 560f;
        private const float CardStep = 500f;

        private readonly BattleSession rewardFrom;

        /// <param name="rewardSession">战斗胜利后带奖进来，奖励弹层直接盖在本屏上。</param>
        public ChapterScreen(BattleSession rewardSession = null)
        {
            rewardFrom = rewardSession;
        }

        public override string BgKey
        {
            get { return App.Director != null && App.Director.Endless ? "bg_endless" : "bg_map"; }
        }

        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            var director = App.Director;
            director.EnsureRun();

            if (director.Phase == RunPhase.Victory || director.Phase == RunPhase.Defeat)
            {
                App.Replace(new ResultScreen(director.Phase == RunPhase.Victory, null));
                return;
            }

            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.38f));
            veil.raycastTarget = false;

            BuildHeader(director);
            BuildCards(director);

            if (rewardFrom != null && director.Phase == RunPhase.Reward)
            {
                new RewardOverlay(rewardFrom, OnRewardComplete).Build(Root);
            }
        }

        // ---------------------------------------------------------------- 头部

        private void BuildHeader(RunDirector director)
        {
            string floor = director.Endless
                ? "第 " + (director.Floor + 1) + " 层"
                : "第 " + (director.Floor + 1) + " / " + EnemyRoster.FloorsPerChapter + " 层";

            var title = Ui.Label("Title", Root, director.ChapterName, Theme.H1, Theme.Text,
                TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -52f), new Vector2(760f, 74f));

            var floorLabel = Ui.Label("Floor", Root, floor, Theme.H3, Theme.Accent,
                TextAlignmentOptions.Left);
            floorLabel.characterSpacing = 4f;
            Ui.Fixed(floorLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, -140f), new Vector2(520f, 48f));

            var rule = Ui.Solid("Underline", Root, Theme.WithAlpha(Theme.Accent, 0.85f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -198f), new Vector2(132f, 5f));

            // 三个读数从右往左排，右侧给放弃入口留位置。全部是 H4，没有说明小字。
            Stat("Deck", "牌组 " + director.DeckCount, -250f, Theme.Accent);
            Stat("Gold", "金币 " + director.Gold, -500f, Theme.Gold);
            Stat("Hp", "生命 " + director.Hp + " / " + director.HpMax, -750f, Theme.Hp);

            var abandon = Ui.LinkButton("Abandon", Root, "放 弃 本 局", Theme.Body, ConfirmAbandon);
            Ui.Fixed(abandon.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-72f, -146f), new Vector2(220f, 56f));
        }

        private void Stat(string name, string value, float rightOffset, Color color)
        {
            var label = Ui.Label(name, Root, value, Theme.H4, color, TextAlignmentOptions.Right);
            Ui.Fixed(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(rightOffset, -68f), new Vector2(220f, 44f));
        }

        private void ConfirmAbandon()
        {
            App.Audio.PlaySfx("ui_click");
            App.ShowConfirm("放弃本局？本局的牌组与进度都会清空。", "放 弃", "继 续 冒 险", () =>
            {
                App.Director.AbandonRun();
                App.PersistMeta();
                App.Fade(0f, 1f, Theme.Fast, () =>
                {
                    App.Replace(new HomeScreen());
                    App.Fade(1f, 0f, Theme.Normal, null);
                });
            });
        }

        // ---------------------------------------------------------------- 遭遇卡

        private void BuildCards(RunDirector director)
        {
            var hand = director.Hand;
            if (hand.Count == 0)
            {
                return;
            }

            // 首领层只有一张：居中放大，独占整屏的注意力。
            bool single = hand.Count == 1;
            float width = single ? 560f : CardW;
            float height = single ? 620f : CardH;

            for (int i = 0; i < hand.Count; i++)
            {
                float x = single ? 0f : (i - (hand.Count - 1) * 0.5f) * CardStep;
                Card(hand[i], x, width, height, i);
            }
        }

        private void Card(EncounterCard card, float x, float width, float height, int index)
        {
            Color accent = KindColor(card.Kind);
            var face = Ui.Rounded("Card" + card.Slot, Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.94f), Theme.WithAlpha(accent, 0.85f), 3, true);
            Ui.Fixed(face.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -70f), new Vector2(width, height));

            // 卡面上半部一条实色区，让远看也能靠颜色区分遭遇类型
            var band = Ui.Solid("Band", face.transform, Theme.WithAlpha(accent, 0.20f));
            band.raycastTarget = false;
            Ui.Fixed(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(width - 8f, height * 0.62f));

            var glyph = Ui.Label("Glyph", face.transform, NodeKinds.Glyph(card.Kind),
                168, Theme.Lighten(accent, 0.35f), TextAlignmentOptions.Center);
            Ui.Fixed(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 74f), new Vector2(width, 220f));

            var name = Ui.Label("Name", face.transform, card.Title,
                Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            name.characterSpacing = 6f;
            Ui.Fixed(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -104f), new Vector2(width - 40f, 80f));

            var rule = Ui.Solid("Rule", face.transform, Theme.WithAlpha(accent, 0.9f));
            rule.raycastTarget = false;
            Ui.Fixed(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -172f), new Vector2(96f, 5f));

            var btn = face.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            int slot = card.Slot;
            btn.onClick.AddListener(() => OnCardClicked(slot));

            Tween.PopIn(face.rectTransform, 0.08f * index, 0.92f, 0.30f);
        }

        private void OnCardClicked(int slot)
        {
            if (!App.Director.ChooseCard(slot))
            {
                return;
            }

            App.Audio.PlaySfx("ui_click");
            App.PersistMeta();
            Route();
        }

        /// <summary>选卡后按阶段决定去哪一屏，和非战斗屏共用同一套过场。</summary>
        private void Route()
        {
            var phase = App.Director.Phase;
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                switch (phase)
                {
                    case RunPhase.Battle:
                        App.Replace(new BattleScreen());
                        break;
                    case RunPhase.Treasure:
                        App.Replace(new TreasureScreen());
                        break;
                    case RunPhase.Rest:
                        App.Replace(new RestScreen());
                        break;
                    case RunPhase.Shop:
                        App.Replace(new ShopScreen());
                        break;
                    case RunPhase.Event:
                        App.Replace(new EventScreen());
                        break;
                    default:
                        App.Replace(new ChapterScreen());
                        break;
                }

                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        private void OnRewardComplete()
        {
            App.Director.Advance();
            App.PersistMeta();
            App.Replace(new ChapterScreen());
        }

        /// <summary>遭遇配色：红=危险、金=收益、绿=恢复。</summary>
        private static Color KindColor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Battle: return Theme.SeriesStrike;
                case NodeKind.Elite: return Theme.SeriesBlade;
                case NodeKind.Treasure: return Theme.Gold;
                case NodeKind.Rest: return Theme.Accent;
                case NodeKind.Shop: return Theme.SeriesThunder;
                case NodeKind.Event: return Theme.SeriesDark;
                case NodeKind.Boss: return Theme.Danger;
                default: return Theme.TextFaint;
            }
        }
    }
}
