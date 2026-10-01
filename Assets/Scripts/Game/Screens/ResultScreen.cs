using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>结算屏：冒险终止 / 通关时显示本局战报与累计数据。</summary>
    public sealed class ResultScreen : Screen
    {
        private readonly bool victory;
        private readonly bool cleared;
        private readonly BattleSession session;
        private readonly int reachedBattle;

        public ResultScreen(bool victory, BattleSession battle, int battleIndex, bool runCleared = false)
        {
            this.victory = victory;
            cleared = runCleared;
            session = battle;
            reachedBattle = battleIndex;
        }

        protected override void Build()
        {
            App.Audio.PlayMusic(victory ? "title2" : "title1");

            Color accent = victory ? Theme.Gold : Theme.Danger;

            var glow = Ui.Panel("Glow", Root, SpriteForge.Glow(256, accent),
                Theme.WithAlpha(Color.white, victory ? 0.32f : 0.20f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 0.71f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 860f));

            Sprite art = victory ? ArtLib.Hero : ArtLib.Enemy;
            var slime = Ui.Panel("Slime", Root, art, Color.white);
            slime.preserveAspect = true;
            if (art == null)
            {
                slime.sprite = SpriteForge.Slime(256, victory ? Theme.Accent : Theme.SeriesShadow, Theme.BgDeep,
                    victory ? 9001 : 313, victory, victory ? SlimeFace.Happy : SlimeFace.Worried);
            }

            Ui.Fixed(slime.rectTransform, new Vector2(0.5f, 0.71f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            Tween.PopIn(slime.rectTransform, 0f, 0.7f);

            var title = Ui.Label("Title", Root, cleared ? "通关！" : (victory ? "本关告捷" : "牌局终止"), Theme.H1, accent, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 27f), new Vector2(1400f, 72f));
            title.rectTransform.localScale = Vector3.one * 0.86f;
            Tween.Scale(title.rectTransform, 0.86f, 1f, 0.34f, Easing.OutBack, null, 0.08f);

            var sub = Ui.Label("Sub", Root,
                cleared
                    ? "廿七战全部打赢——你打穿了整个赛程。"
                    : (victory
                        ? "击败了这位守关者，牌组已经补强。"
                        : "赛程到此为止。牌组与进度都留着，可以再挑战这一战。"),
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -37f), new Vector2(1500f, 36f));
            Tween.FadeIn(sub, 0.3f, 0.16f, 0f, 1f);

            var stats = Ui.Node("Stats", Root);
            Ui.Fixed(stats, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -151f), new Vector2(1400f, 148f));
            Ui.Row(stats, 18, new RectOffset(0, 0, 0, 0));

            int dealt = session != null ? session.DamageDealtByPlayer : 0;
            int best = session != null ? session.BestTurnDamage : 0;
            int turns = session != null ? session.TurnNumber : 0;

            BuildStat(stats, "止步战序", reachedBattle + " / " + EnemyRoster.FinalBattle, Theme.Gold, 0.02f);
            BuildStat(stats, "总伤害", dealt.ToString(), Theme.Danger, 0.08f);
            BuildStat(stats, "单回合最高", best.ToString(), Theme.SeriesChrono, 0.14f);
            BuildStat(stats, "回合数", turns.ToString(), Theme.Mana, 0.20f);
            BuildStat(stats, "累计奖杯", App.Meta.Trophy.ToString(), Theme.Accent, 0.26f);
            BuildStat(stats, "是否通关",
                App.Meta.Flags.Contains("run_cleared") ? "已通关" : "未通关", Theme.Text, 0.32f);

            var again = Ui.TextButton("Again", Root, "再 来 一 局", Theme.H3, Theme.AccentDeep, Theme.Text, Restart);
            Ui.Fixed(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 163f), new Vector2(360f, 96f));

            var home = Ui.TextButton("Home", Root, "返回主页", Theme.H3, Theme.PanelRaised, Theme.Text, GoHome);
            Ui.Fixed(home.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 163f), new Vector2(360f, 96f));
        }

        private void BuildStat(RectTransform parent, string caption, string value, Color valueColor, float delay)
        {
            var box = Ui.Rounded("Stat", parent, Theme.RadiusM, Theme.WithAlpha(Theme.Panel, 0.94f), Theme.Border, 1);
            Ui.FixedSize(box.gameObject, 218f, 138f);

            var valueLabel = Ui.Label("Value", box.transform, value, Theme.H2, valueColor, TextAlignmentOptions.Center);
            Ui.Fixed(valueLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(206f, 48f));

            var cap = Ui.Label("Cap", box.transform, caption, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(206f, 28f));

            // 六块数据从左到右依次弹出来，比整排同时出现有节奏
            box.rectTransform.localScale = Vector3.one * 0.8f;
            Tween.Scale(box.rectTransform, 0.8f, 1f, 0.3f, Easing.OutBack, null, delay + 0.2f);
            Tween.FadeIn(box, 0.24f, delay + 0.2f, 0f, 1f);
        }

        private void Restart()
        {
            App.Director.StartNewRun();
            App.RunSelfDamage = 0;
            App.PersistMeta();
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new BattleScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        private void GoHome()
        {
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new HomeScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }
    }
}
