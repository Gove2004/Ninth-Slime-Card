using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 战利品结算。每场胜利 3 选 1；关底宗师战给两轮（第二轮锁定该宗师系列）。
    /// 牌组到达上限后奖励转为「替换」——选一张移出，再把新牌放进去，
    /// 这样廿七战走下来牌组不会膨胀到 40 张，构筑反而越打越精。
    /// </summary>
    public sealed class RewardScreen : Screen
    {
        private const float CardScale = 1.15f;
        private const float CardGap = 48f;

        private readonly BattleSession session;

        private RectTransform content;
        private TextMeshProUGUI headline;
        private TextMeshProUGUI subtitle;
        private TextMeshProUGUI deckInfo;
        private TextMeshProUGUI stageLine;

        private readonly List<CardView> choices = new List<CardView>();
        private readonly List<GameObject> hintNodes = new List<GameObject>();
        private GameObject skipButton;

        /// <summary>
        /// 调试用：固定候选，好验收特定卡面的提示文案。
        /// 奖励候选是随机的，靠反复跳转碰运气验证不了任何一种提示。真机包里恒为 null。
        /// </summary>
        public static List<CardDef> ForcedChoices;

        /// <summary>
        /// 调试用：临时替换「当前牌组」来算配套提示。
        /// 想看「兑现牌缺铺垫」就得让牌组为空，但真清空会毁掉正在进行的存档局，
        /// 所以只覆盖分析用的那份视图。真机包里恒为 null。
        /// </summary>
        public static List<CardDef> ForcedDeck;

        private int round;

        public RewardScreen(BattleSession battle)
        {
            session = battle;
        }

        /// <summary>精英与首领额外多给一轮奖励，普通战一轮。</summary>
        private int TotalRounds { get { return session != null && (session.IsElite || session.IsBoss) ? 2 : 1; } }

        protected override void Build()
        {
            App.Audio.PlayMusic("title2");

            var director = App.Director;
            var defeated = director.LastEnemy;

            BuildHeader("战 利 品",
                (defeated != null ? defeated.Name : "对手") + " 已被击败",
                string.Empty);

            // 页头占 40..136（顶部边距 + 96 高），下面这几行必须排在 136 之后，
            // 否则会直接压在页头标题上——之前 -96 就落在页头内部，看起来像两行字叠在一起。
            stageLine = Ui.Label("StageLine", Root,
                director.ChapterName
                + "  ·  第 " + (director.Floor + 1) + " 层"
                + "  ·  生命 " + director.Hp + " / " + director.HpMax,
                Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(stageLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(1400f, 26f));
            Tween.FadeIn(stageLine, 0.3f, 0.05f, 0f, 1f);

            headline = Ui.Label("Headline", Root, string.Empty, Theme.H2, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -188f), new Vector2(1600f, 46f));

            subtitle = Ui.Label("Subtitle", Root, BuildSummary(), Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -246f), new Vector2(1600f, 32f));
            Tween.FadeIn(subtitle, 0.3f, 0.12f, 0f, 1f);

            content = Ui.Node("Content", Root);
            Ui.Stretch(content, 0f, 264f, 0f, 120f);

            deckInfo = Ui.Label("DeckInfo", Root, string.Empty, Theme.Caption,
                Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(deckInfo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 16f), new Vector2(1200f, 28f));

            RefreshDeckInfo();
            Render();
        }

        private string BuildSummary()
        {
            if (session == null)
            {
                return "牌组已补强";
            }

            return "造成伤害 " + session.DamageDealtByPlayer
                 + "  ·  单回合最高 " + session.BestTurnDamage
                 + "  ·  出牌 " + session.CardsPlayedByPlayer
                 + "  ·  用了 " + session.TurnNumber + " 回合";
        }

        private void RefreshDeckInfo()
        {
            var director = App.Director;
            int count = director.DeckCount;
            int cap = DeckRules.DeckCap;

            deckInfo.text = "当前牌组 " + count + " / " + cap
                            + (count >= cap ? "　·　已满，奖励将以「替换」形式加入" : "　·　还可加入 " + (cap - count) + " 张");
            deckInfo.color = count >= cap ? Theme.Gold : Theme.TextDim;
        }

        // ------------------------------------------------------------ 候选渲染

        private void Render()
        {
            ClearChoices();

            if (round >= TotalRounds)
            {
                Advance();
                return;
            }

            var list = ForcedChoices != null && round == 0
                ? new List<CardDef>(ForcedChoices)
                : App.Director.BuildRewardChoices(3);
            if (list.Count == 0)
            {
                Advance();
                return;
            }

            // 候选一出现就记入图鉴：单局没选的牌，跨局仍然会慢慢集齐
            App.Director.MarkSeen(list);

            bool bonus = round > 0;
            headline.text = bonus
                ? "精英额外奖励 · 再选一张"
                : "选择一张卡牌加入牌组";
            headline.color = bonus ? Theme.SeriesBlade : Theme.Gold;
            headline.rectTransform.localScale = Vector3.one * 0.9f;
            Tween.Scale(headline.rectTransform, 0.9f, 1f, 0.28f, Easing.OutBack);

            float step = CardView.W * CardScale + CardGap;
            float left = -(list.Count - 1) * 0.5f * step;

            for (int i = 0; i < list.Count; i++)
            {
                var def = list[i];
                var view = CardView.Create(content, def, v => Take(v.Def));
                view.SetPosition(new Vector2(left + step * i, 260f), 0f, CardScale);
                choices.Add(view);

                // 逐张发牌：错峰弹入 + 把初始倾角抹平，比整排"啪"地出现像发牌
                var rt = view.Root;
                rt.localScale = Vector3.one * (CardScale * 0.7f);
                rt.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 6f);
                Tween.Scale(rt, CardScale * 0.7f, CardScale, 0.34f, Easing.OutBack, null, 0.06f * i);
                Tween.To(0.34f, k =>
                {
                    if (rt == null)
                    {
                        return;
                    }

                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp((i - 1) * 6f, 0f, k));
                }, Easing.OutCubic, null, 0.06f * i);

                BuildRoleHint(content, def, left + step * i, 0.06f * i);
            }

            var skip = Ui.TextButton("Skip", content, TotalRounds > 1 ? "放弃剩余奖励" : "跳 过", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, Skip);
            Ui.Fixed(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(300f, 62f));
            skipButton = skip.gameObject;
        }

        /// <summary>
        /// 卡片下方那两行：这张牌在链里是什么角色、和当前牌组配不配。
        ///
        /// 抽卡的这一刻是玩家真正做构筑决策的时候。卡面只写「做什么」，
        /// 而「引爆冻结」这类牌的价值完全取决于手上有没有冻结 ——
        /// 不在这里说清楚，玩家就得靠记住 144 张卡来推断，那不可能。
        /// </summary>
        private void BuildRoleHint(RectTransform parent, CardDef def, float x, float delay)
        {
            var role = CardRoles.Role(def);
            Color roleColor = role == CardRole.Payoff ? Theme.Gold
                : role == CardRole.Setup ? Theme.Mana
                : role == CardRole.Both ? Theme.Accent
                : role == CardRole.Conditional ? Theme.SeriesThunder
                : Theme.TextFaint;

            var tag = Ui.Rounded("Role" + def.Id, parent, Theme.RadiusS,
                Theme.WithAlpha(Theme.PanelSunken, 0.92f), Theme.WithAlpha(roleColor, 0.55f), 1);
            Ui.Fixed(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x, 200f), new Vector2(104f, 30f));
            hintNodes.Add(tag.gameObject);

            var label = Ui.Label("T", tag.transform, CardRoles.RoleName(role), Theme.Caption,
                role == CardRole.Plain ? Theme.TextFaint : Theme.Lighten(roleColor, 0.3f),
                TextAlignmentOptions.Center);
            Ui.Stretch(label.rectTransform, 4f, 2f, 4f, 2f);

            string fit = CardRoles.Fit(def, DeckDefs());
            if (string.IsNullOrEmpty(fit))
            {
                return;
            }

            // 只有「兑现牌但没有铺垫」才是真的打不出来，值得用警告色。
            // 反过来「攒了层没人兑现」并不亏 —— 中毒照样掉血、护盾照样挡伤，
            // 用红色说它不行，玩家会误以为这张牌是废的。
            bool blocked = role == CardRole.Payoff && fit.Contains("打不出来");
            var hint = Ui.Label("Fit" + def.Id, parent, fit, Theme.Caption,
                blocked ? Theme.Danger : Theme.TextFaint, TextAlignmentOptions.Top);
            hint.enableWordWrapping = true;

            // 高度按实际字数算：提示可能一到两行，给死高度会把第二行裁掉
            float hintH = Ui.MeasureTextHeight(fit, 276f, Theme.Caption) + 4f;
            Ui.Fixed(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x, 156f), new Vector2(276f, hintH));

            hint.alpha = 0f;
            Tween.FadeIn(hint, 0.32f, 0.4f + delay, 0f, 1f);
            hintNodes.Add(hint.gameObject);
        }

        /// <summary>当前牌组里的卡牌定义（角色分析要按 def 比对，不是按实例）。</summary>
        private List<CardDef> DeckDefs()
        {
            if (ForcedDeck != null)
            {
                return ForcedDeck;
            }

            var list = new List<CardDef>();
            var deck = App.Director.Deck;
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] != null && deck[i].Def != null)
                {
                    list.Add(deck[i].Def);
                }
            }

            return list;
        }

        private void ClearChoices()
        {
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].Destroy();
            }

            choices.Clear();

            // 角色提示是独立节点，不挂在 CardView 下，得自己收。
            // 漏掉这一步，第二轮奖励会把第一轮的三行提示留在原地。
            for (int i = hintNodes.Count - 1; i >= 0; i--)
            {
                if (hintNodes[i] != null)
                {
                    Object.Destroy(hintNodes[i]);
                }
            }

            hintNodes.Clear();

            if (skipButton != null)
            {
                Object.Destroy(skipButton);
                skipButton = null;
            }
        }

        // ------------------------------------------------------------ 交互

        private void Take(CardDef def)
        {
            if (def == null)
            {
                return;
            }

            App.Audio.PlaySfx("mana");

            // 牌组满员：先请玩家弃一张，再入库
            if (App.Director.AtDeckCap)
            {
                ShowReplacePrompt(def);
                return;
            }

            App.Director.TakeReward(def);
            App.Toast("加入牌组 · " + def.Name);
            round++;
            RefreshDeckInfo();
            Render();
        }

        private void Skip()
        {
            App.Audio.PlaySfx("ui");
            round = TotalRounds;
            RefreshDeckInfo();
            Render();
        }

        /// <summary>
        /// 牌组已满：列出当前牌组，让玩家亲手挑一张换出去。
        /// 这一步是后期牌组唯一的成长方式——换掉初始的弱牌，而不是无限堆牌。
        /// </summary>
        private void ShowReplacePrompt(CardDef incoming)
        {
            var overlay = Ui.Solid("ReplaceOverlay", Root, new Color(0f, 0f, 0f, 0.72f), true);
            overlay.rectTransform.SetAsLastSibling();

            var panel = Ui.Rounded("ReplacePanel", overlay.transform, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.99f), Theme.Gold, 2);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1320f, 780f));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            var title = Ui.Label("Title", panel.transform,
                "牌组已满 " + App.Director.DeckCount + " / " + DeckRules.DeckCap,
                Theme.H2, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(1200f, 44f));

            var hint = Ui.Label("Hint", panel.transform,
                "选一张移出牌组，为《" + incoming.Name + "》腾出位置",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(1200f, 30f));

            RectTransform listContent;
            var scroll = Ui.VerticalScroll("List", panel.transform, out listContent);
            Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -22f), new Vector2(1180f, 520f));

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

                int index = i;
                int owned = composition[ids[i]];
                var row = BuildDeckRow(listContent, def, owned, () =>
                {
                    App.Director.RemoveFromDeck(def.Id);
                    App.Director.TakeReward(incoming);
                    App.Audio.PlaySfx("mana");
                    App.Toast("换入《" + incoming.Name + "》，移出《" + def.Name + "》");

                    Object.Destroy(overlay.gameObject);
                    round++;
                    RefreshDeckInfo();
                    Render();
                });

                var rt = row.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * 0.96f;
                Tween.Scale(rt, 0.96f, 1f, 0.24f, Easing.OutBack, null, 0.02f * index);
            }

            var cancel = Ui.TextButton("Cancel", panel.transform, "保留原牌组（放弃本次奖励）", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, () =>
                {
                    App.Audio.PlaySfx("ui");
                    Object.Destroy(overlay.gameObject);
                    round = TotalRounds;
                    Render();
                });
            Ui.Fixed(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(520f, 62f));
        }

        private GameObject BuildDeckRow(RectTransform parent, CardDef def, int owned, System.Action onPick)
        {
            Color series = Theme.SeriesColor(def.Series);

            var img = Ui.Rounded("Row", parent, Theme.RadiusM,
                Theme.WithAlpha(Theme.Darken(series, 0.7f), 0.94f), Theme.WithAlpha(series, 0.55f), 1, true);
            Ui.FixedSize(img.gameObject, 1140f, 74f);

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
                new Vector2(40f, 0f), new Vector2(48f, 40f));

            var name = Ui.Label("Name", img.transform, def.Name, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(76f, 10f), new Vector2(420f, 30f));

            var desc = Ui.Label("Desc", img.transform, def.Text(0), Theme.Caption, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(76f, -18f), new Vector2(700f, 24f));

            var tag = Ui.Label("Tag", img.transform, Series.DisplayName(def.Series), Theme.Caption,
                Theme.Lighten(series, 0.35f), TextAlignmentOptions.Center);
            Ui.Fixed(tag.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(120f, 26f));

            if (owned > 1)
            {
                var count = Ui.Label("Count", img.transform, "×" + owned, Theme.Caption,
                    Theme.TextFaint, TextAlignmentOptions.Center);
                Ui.Fixed(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-152f, 0f), new Vector2(60f, 26f));
            }

            return img.gameObject;
        }

        private void Advance()
        {
            App.Director.LeaveNode();
            App.PersistMeta();
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new MapScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }
    }
}
