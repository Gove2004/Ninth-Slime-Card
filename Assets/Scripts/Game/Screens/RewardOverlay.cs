using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 战利品弹层：盖在章节卡牌屏上选奖励，不再单独切一块屏。
    /// 每场胜利 3 选 1；精英战给两轮（第二轮锁定该系列）。
    /// 牌组到达上限后奖励转为「替换」——选一张移出，再把新牌放进去，
    /// 这样一路打下来牌组不会膨胀到 40 张，构筑反而越打越精。
    /// </summary>
    public sealed class RewardOverlay
    {
        private const float CardScale = 1.15f;
        private const float CardGap = 48f;

        private readonly BattleSession session;
        private readonly System.Action onDone;

        private RectTransform root;
        private RectTransform content;
        private TextMeshProUGUI headline;

        private readonly List<CardView> choices = new List<CardView>();
        private readonly List<GameObject> hintNodes = new List<GameObject>();
        private GameObject skipButton;

        /// <summary>
        /// 调试用：固定候选，好验收特定卡面的提示文案。
        /// 奖励候选是随机的，靠反复跳转碰运气验证不了任何一种提示。真机包里恒为 null。
        /// </summary>
        public static List<CardDef> ForcedChoices;

        private int round;

        public RewardOverlay(BattleSession battle, System.Action done)
        {
            session = battle;
            onDone = done;
        }

        /// <summary>精英额外多给一轮奖励，普通战一轮（Boss 战走章节推进，不进弹层）。</summary>
        private int TotalRounds { get { return session != null && session.IsElite ? 2 : 1; } }

        public void Build(RectTransform host)
        {
            root = Ui.Node("RewardOverlay", host);

            // 压暗章节屏并挡住它的点击：弹层期间底层不参与交互。
            // 0.88 起步——0.82 时遭遇卡的高亮色还是能透出来抢视线。
            Ui.Solid("Dim", root, new Color(0f, 0f, 0f, 0.88f), true);

            // 「选一张牌」原来直接裸摆在背景上，正好落在月亮和城堡的亮部，
            // 灰字读不出来。给它们一条半透明底板，文字才有地方站。
            var banner = Ui.Rounded("Banner", root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.86f), Theme.WithAlpha(Theme.Border, 0.45f), 1);
            Ui.Fixed(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -196f), new Vector2(920f, 100f));

            headline = Ui.Label("Headline", banner.transform, string.Empty, Theme.H2, Theme.Gold,
                TextAlignmentOptions.Center);
            Ui.Fixed(headline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(880f, 44f));

            content = Ui.Node("Content", root);
            Ui.Stretch(content, 0f, 272f, 0f, 120f);

            Render();
        }

        // ------------------------------------------------------------ 候选渲染

        private void Render()
        {
            ClearChoices();

            if (round >= TotalRounds)
            {
                Complete();
                return;
            }

            var list = ForcedChoices != null && round == 0
                ? new List<CardDef>(ForcedChoices)
                : GameApp.I.Director.BuildRewardChoices(3);
            if (list.Count == 0)
            {
                Complete();
                return;
            }

            // 候选一出现就记入图鉴：单局没选的牌，跨局仍然会慢慢集齐
            GameApp.I.Director.MarkSeen(list);

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
                // 260 是卡片的**底边**：Ui.Fixed 的坐标是 pivot 位置，而 pivot 跟着 anchor
                // 走（这里锚在内容区底边）。卡高 312×1.15≈359，所以卡面落在 260~619，
                // 下面 260px 留给「角色标签 + 适配提示 + 跳过」。
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

                BuildRoleTag(content, def, left + step * i, 0.06f * i);
            }

            var skip = Ui.TextButton("Skip", content, TotalRounds > 1 ? "放弃剩余奖励" : "跳 过", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, Skip);
            // 让开卡下三行提示的地盘（提示块最矮也会落到 y=88），同时把底部那段空地收一收
            Ui.Fixed(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, -14f), new Vector2(300f, 62f));
            skipButton = skip.gameObject;
        }

        /// <summary>卡片下方的角色标签：这张牌在链里是铺垫还是兑现。</summary>
        private void BuildRoleTag(RectTransform parent, CardDef def, float x, float delay)
        {
            var role = CardRoles.Role(def);
            Color roleColor = role == CardRole.Payoff ? Theme.Gold
                : role == CardRole.Setup ? Theme.Mana
                : role == CardRole.Both ? Theme.Accent
                : role == CardRole.Conditional ? Theme.SeriesThunder
                : Theme.TextFaint;

            // 卡片底边在 y=260（见上面 SetPosition 的注释）。标签占 [200, 238]，
            // 与卡面留 22px —— 再靠上去就会被卡面下缘的深色描边吃掉上半截。
            const float TagBottom = 200f;
            const float TagHeight = 38f;
            var tag = Ui.Rounded("Role" + def.Id, parent, Theme.RadiusS,
                Theme.WithAlpha(Theme.PanelSunken, 0.92f), Theme.WithAlpha(roleColor, 0.55f), 1);
            Ui.Fixed(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x, TagBottom), new Vector2(112f, TagHeight));
            hintNodes.Add(tag.gameObject);

            var label = Ui.Label("T", tag.transform, CardRoles.RoleName(role), Theme.Caption,
                role == CardRole.Plain ? Theme.TextFaint : Theme.Lighten(roleColor, 0.3f),
                TextAlignmentOptions.Center);
            Ui.Stretch(label.rectTransform, 6f, 4f, 6f, 4f);
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

            GameApp.I.Audio.PlaySfx("reward");

            // 牌组满员：先请玩家弃一张，再入库
            if (GameApp.I.Director.AtDeckCap)
            {
                ShowReplacePrompt(def);
                return;
            }

            GameApp.I.Director.TakeReward(def);
            GameApp.I.Toast("加入牌组 · " + def.Name);
            round++;
            Render();
        }

        private void Skip()
        {
            GameApp.I.Audio.PlaySfx("ui_click");
            round = TotalRounds;
            Render();
        }

        /// <summary>
        /// 牌组已满：列出当前牌组，让玩家亲手挑一张换出去。
        /// 这一步是后期牌组唯一的成长方式——换掉初始的弱牌，而不是无限堆牌。
        /// </summary>
        private void ShowReplacePrompt(CardDef incoming)
        {
            var overlay = Ui.Solid("ReplaceOverlay", root, new Color(0f, 0f, 0f, 0.72f), true);
            overlay.rectTransform.SetAsLastSibling();

            var panel = Ui.Rounded("ReplacePanel", overlay.transform, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.99f), Theme.Gold, 2);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1320f, 780f));
            Tween.PopIn(panel.rectTransform, 0f, 0.9f);

            var title = Ui.Label("Title", panel.transform,
                "牌组已满 " + GameApp.I.Director.DeckCount + " / " + DeckRules.DeckCap,
                Theme.H2, Theme.Gold, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(1200f, 44f));

            RectTransform listContent;
            var scroll = Ui.VerticalScroll("List", panel.transform, out listContent);
            Ui.Fixed(scroll.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -14f), new Vector2(1180f, 580f));

            var composition = GameApp.I.Director.DeckComposition();
            var ids = new List<int>(composition.Keys);
            ids.Sort((a, b) =>
            {
                var da = GameApp.I.Cards.Get(a);
                var db = GameApp.I.Cards.Get(b);
                if (da == null || db == null)
                {
                    return 0;
                }

                return da.Cost != db.Cost ? da.Cost - db.Cost : da.Id - db.Id;
            });

            for (int i = 0; i < ids.Count; i++)
            {
                var def = GameApp.I.Cards.Get(ids[i]);
                if (def == null)
                {
                    continue;
                }

                int index = i;
                int owned = composition[ids[i]];
                var row = BuildDeckRow(listContent, def, owned, () =>
                {
                    GameApp.I.Director.RemoveFromDeck(def.Id);
                    GameApp.I.Director.TakeReward(incoming);
                    GameApp.I.Audio.PlaySfx("reward");
                    GameApp.I.Toast("换入《" + incoming.Name + "》，移出《" + def.Name + "》");

                    Object.Destroy(overlay.gameObject);
                    round++;
                    Render();
                });

                var rt = row.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * 0.96f;
                Tween.Scale(rt, 0.96f, 1f, 0.24f, Easing.OutBack, null, 0.02f * index);
            }

            var cancel = Ui.TextButton("Cancel", panel.transform, "保留原牌组（放弃本次奖励）", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, () =>
                {
                    GameApp.I.Audio.PlaySfx("ui_back");
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
                GameApp.I.Audio.PlaySfx("ui_click");
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

        /// <summary>奖励流程收尾：先撤弹层，再让宿主章节屏接手（推进层并刷新）。</summary>
        private void Complete()
        {
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                root = null;
            }

            onDone?.Invoke();
        }
    }
}
