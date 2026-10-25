using System;
using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 随机事件。每个事件给出 2~3 个选项，代价与收益都写清楚——
    /// 事件的分量在于「愿不愿意用确定的东西换不确定的东西」，藏起结果就只剩读运气。
    /// </summary>
    public sealed class EventScreen : NodeScreen
    {
        /// <summary>一个选项：标题、说明、执行体。</summary>
        private sealed class Choice
        {
            public string Caption;
            public string Detail;
            /// <summary>返回结果文案，显示在执行后的回执里。</summary>
            public Func<string> Apply;
            /// <summary>条件不满足时给出原因；返回 null 表示可选。</summary>
            public Func<string> Blocked;
        }

        /// <summary>一次遭遇。</summary>
        private sealed class Encounter
        {
            public string Id;
            public string Body;
            public readonly List<Choice> Choices = new List<Choice>();
        }

        private RectTransform body;
        private Encounter encounter;
        private string receipt;

        protected override string Title { get { return "遭 遇"; } }

        protected override string Subtitle { get { return "路上有些东西在等你，它不一定是敌人。"; } }

        protected override Color Accent { get { return Theme.SeriesDark; } }

        protected override void BuildContent(RectTransform panel)
        {
            body = panel;

            var director = App.Director;
            // 事件按节点+章节确定性地挑：同一节点反复重绘不会换题目
            int seed = director.CurrentNodeId * 2749 + director.Chapter * 613 + 11;
            var rng = new DeterministicRng(seed);
            encounter = buildEncounters(rng);
            render();
        }

        // ---------------------------------------------------------------- 事件库

        private Encounter buildEncounters(DeterministicRng rng)
        {
            var all = new List<Func<Encounter>> { fountain, woundedKnight, gambler, mimic, shrine, peddler };
            return all[rng.Range(0, all.Count)]();
        }

        /// <summary>治愈之泉：付钱回血，或者直接喝（有代价）。</summary>
        private Encounter fountain()
        {
            var e = new Encounter
            {
                Id = "fountain",
                Body = "石缝里渗出一股清水，水面浮着极淡的绿光。你渴了，但它看起来不止是水。"
            };

            int cost = 40 + App.Director.Chapter * 10;
            e.Choices.Add(new Choice
            {
                Caption = "投币后取水",
                Detail = "花费 " + cost + " 金币，回复一半生命",
                Blocked = () => App.Director.Gold < cost ? "金币不足" : null,
                Apply = () =>
                {
                    App.Director.Gold -= cost;
                    int heal = Mathf.Max(1, App.Director.HpMax / 2);
                    int before = App.Director.Hp;
                    App.Director.Hp = Math.Min(App.Director.HpMax, App.Director.Hp + heal);
                    return "水很凉。回复了 " + (App.Director.Hp - before) + " 点生命。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "直接喝",
                Detail = "不花钱，但水里的东西会跟着你走一段",
                Apply = () =>
                {
                    int heal = Mathf.Max(1, App.Director.HpMax / 3);
                    int before = App.Director.Hp;
                    App.Director.Hp = Math.Min(App.Director.HpMax, App.Director.Hp + heal);
                    return "喉咙舒服了，但胃里一直有东西在动。你回复了 "
                         + (App.Director.Hp - before) + " 点生命。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "捧一捧就走",
                Detail = "什么都不做，继续赶路",
                Apply = () => "你把水留在原地。它也没有跟上来。"
            });

            return e;
        }

        /// <summary>受伤的骑士：给他金币或牌，换一张更好的牌。</summary>
        private Encounter woundedKnight()
        {
            var e = new Encounter
            {
                Id = "knight",
                Body = "一个骑士靠在石头上，甲片碎了半边。他抬手拦住你，"
                     + "说自己不缺力气，缺的是一张能换命的东西。"
            };

            int cost = 60 + App.Director.Chapter * 12;
            e.Choices.Add(new Choice
            {
                Caption = "给他金币",
                Detail = "花费 " + cost + " 金币，他回赠一张牌",
                Blocked = () => App.Director.Gold < cost ? "金币不足" : null,
                Apply = () =>
                {
                    App.Director.Gold -= cost;
                    var card = App.Director.RollTreasureCard();
                    if (card == null || App.Director.AtDeckCap)
                    {
                        App.Director.Gold += cost;
                        return "他翻了半天，最后什么也没能给出来。钱退回去了。";
                    }

                    App.Director.TakeReward(card);
                    return "他塞给你《" + card.Name + "》，然后闭上了眼。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "替他包扎",
                Detail = "不要报酬，回复少量生命",
                Apply = () =>
                {
                    int heal = Mathf.Max(8, App.Director.HpMax / 5);
                    int before = App.Director.Hp;
                    App.Director.Hp = Math.Min(App.Director.HpMax, App.Director.Hp + heal);
                    return "他走的时候把水囊留给了你。回复了 " + (App.Director.Hp - before) + " 点生命。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "绕开他",
                Detail = "受伤的人未必是弱者",
                Apply = () => "你从他背后绕了过去。他没有回头。"
            });

            return e;
        }

        /// <summary>赌徒：赌一次金币翻倍或归零。</summary>
        private Encounter gambler()
        {
            var e = new Encounter
            {
                Id = "gambler",
                Body = "一个裹着毯子的人蹲在火堆旁，面前摊着三张牌。"
                     + "「押多少都行，」他说，「只要你不问牌是怎么洗的。」"
            };

            int cost = 50 + App.Director.Chapter * 10;
            e.Choices.Add(new Choice
            {
                Caption = "押上 " + cost + " 金币",
                Detail = "一半机会翻倍，一半机会归零",
                Blocked = () => App.Director.Gold < cost ? "金币不足" : null,
                Apply = () =>
                {
                    var director = App.Director;
                    director.Gold -= cost;

                    // 用节点与当前金币做种子：同一笔赌注反复点不会换结果
                    var rng = new DeterministicRng(director.CurrentNodeId * 31 + director.Gold * 7 + 5);
                    if (rng.Range(0, 100) < 45)
                    {
                        int win = cost * 2;
                        director.Gold += win;
                        director.GoldEarned += win;
                        App.Audio.PlaySfx("mana");
                        return "他翻开一张空牌。「你赢了。」收回 " + win + " 金币。";
                    }

                    App.Audio.PlaySfx("venom");
                    return "他把金币扫进袖子里，没再看你。那 " + cost + " 金币没了。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "不看，走人",
                Detail = "赌局只在坐下的时候开始",
                Apply = () => "你从他身边走过。他也没抬头。"
            });

            return e;
        }

        /// <summary>伪装的宝箱：开它就得挨一下，但东西是真的。</summary>
        private Encounter mimic()
        {
            var e = new Encounter
            {
                Id = "mimic",
                Body = "路中央摆着一个箱子，锁是开的，箱子底下有一圈被压平的草——"
                     + "像是有人经常把它挪来挪去。"
            };

            e.Choices.Add(new Choice
            {
                Caption = "伸手进去",
                Detail = "必定受伤，但拿到一张稀有卡和金币",
                Apply = () =>
                {
                    var director = App.Director;
                    int dmg = Mathf.Max(6, director.HpMax / 8);
                    director.Hp = Math.Max(1, director.Hp - dmg);

                    int gold = 60 + director.Chapter * 20;
                    director.Gold += gold;
                    director.GoldEarned += gold;

                    var card = director.RollTreasureCard();
                    string tail = "，外加 " + gold + " 金币";
                    if (card != null && !director.AtDeckCap)
                    {
                        director.TakeReward(card);
                        tail += " 和《" + card.Name + "》";
                    }

                    App.Audio.PlaySfx("slash");
                    return "箱子咬住了你的手。你甩开它，损失 " + dmg + " 点生命" + tail + "。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "用石头砸开",
                Detail = "不受伤，只拿到金币",
                Apply = () =>
                {
                    int gold = 30 + App.Director.Chapter * 10;
                    App.Director.Gold += gold;
                    App.Director.GoldEarned += gold;
                    var rng = new DeterministicRng(App.Director.CurrentNodeId * 17 + 3);
                    return rng.Range(0, 100) < 50
                        ? "石头砸下去，箱子怪叫一声跑了。" + gold + " 金币散在地上。"
                        : "箱子里确实只有钱。" + gold + " 金币。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "踢一脚就走",
                Detail = "不碰它",
                Apply = () => "箱子被你踢翻，滚出几颗石子。你决定不深究。"
            });

            return e;
        }

        /// <summary>神龛：牺牲一张牌换永久强化，或换一次升级。</summary>
        private Encounter shrine()
        {
            var e = new Encounter
            {
                Id = "shrine",
                Body = "一座没有神像的神龛，凹槽里积着旧灰。它要的东西一直没变，"
                     + "问题是你愿不愿意给。"
            };

            e.Choices.Add(new Choice
            {
                Caption = "献上一张牌",
                Detail = "从牌组中随机移除一张，随后回复全部生命",
                Blocked = () => App.Director.DeckCount <= 1 ? "牌组太薄，不能再删" : null,
                Apply = () =>
                {
                    var director = App.Director;
                    var rng = new DeterministicRng(director.CurrentNodeId * 41 + director.DeckCount * 7);
                    int index = rng.Range(0, director.DeckCount);
                    string name = director.Deck[index].Name;

                    if (!director.RemoveFromDeck(director.Deck[index].Def.Id))
                    {
                        return "神龛没有接受。";
                    }

                    director.Hp = director.HpMax;
                    App.Audio.PlaySfx("heal");
                    return "《" + name + "》化成了灰。你的伤全部好了。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "献上一半金币",
                Detail = "金币减半，随机升级牌组里的一张牌",
                Blocked = () => App.Director.Gold < 20 ? "金币太少，它看不上" : null,
                Apply = () =>
                {
                    var director = App.Director;
                    int paid = director.Gold / 2;
                    director.Gold -= paid;

                    var candidates = new List<int>();
                    for (int i = 0; i < director.Deck.Count; i++)
                    {
                        if (director.Deck[i].Level == 0)
                        {
                            candidates.Add(i);
                        }
                    }

                    if (candidates.Count == 0)
                    {
                        director.Gold += paid;
                        return "神龛扫过你的牌组，一无所获。金币回到了你手里。";
                    }

                    var rng = new DeterministicRng(director.CurrentNodeId * 29 + paid);
                    int pick = candidates[rng.Range(0, candidates.Count)];
                    var card = director.Deck[pick];
                    director.UpgradeCard(pick);

                    App.Audio.PlaySfx("mana");
                    return "你付了 " + paid + " 金币，《" + card.Name + "》变得锋利了。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "空手离开",
                Detail = "有些东西不给也罢",
                Apply = () => "你往外走，身后传来一声很轻的叹息，也可能只是风。"
            });

            return e;
        }

        /// <summary>行脚商人：低价卖一张有代价的牌。</summary>
        private Encounter peddler()
        {
            var e = new Encounter
            {
                Id = "peddler",
                Body = "一个背着布袋的人拦下你，说手上有些「来路不太干净」的货，"
                     + "便宜，但用了别怪他。"
            };

            int cost = 35 + App.Director.Chapter * 8;
            e.Choices.Add(new Choice
            {
                Caption = "买下他的货",
                Detail = "花费 " + cost + " 金币，换取一张牌",
                Blocked = () => App.Director.Gold < cost ? "金币不足" : null,
                Apply = () =>
                {
                    var director = App.Director;
                    director.Gold -= cost;

                    if (director.AtDeckCap)
                    {
                        director.Gold += cost;
                        return "他看了看你鼓得快撑开的牌袋，摇摇头，把钱退给你了。";
                    }

                    var card = director.RollTreasureCard();
                    if (card == null)
                    {
                        director.Gold += cost;
                        return "他翻了半天袋子，最后摊开手。钱退给你了。";
                    }

                    director.TakeReward(card);
                    return "他从袋子里摸出《" + card.Name + "》递给你，没再多说一句。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "用钱换路",
                Detail = "花费 " + (cost / 2) + " 金币，回复少量生命",
                Blocked = () => App.Director.Gold < cost / 2 ? "金币不足" : null,
                Apply = () =>
                {
                    var director = App.Director;
                    int paid = cost / 2;
                    director.Gold -= paid;
                    int heal = Mathf.Max(10, director.HpMax / 4);
                    int before = director.Hp;
                    director.Hp = Math.Min(director.HpMax, director.Hp + heal);
                    return "他给了你一小瓶东西，说「喝了别问」。回复了 "
                         + (director.Hp - before) + " 点生命。";
                }
            });

            e.Choices.Add(new Choice
            {
                Caption = "摆手拒绝",
                Detail = "来路不干净的东西最好别碰",
                Apply = () => "他耸耸肩，继续往前走，布袋晃得很响。"
            });

            return e;
        }

        // ---------------------------------------------------------------- 渲染

        private void render()
        {
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(body.GetChild(i).gameObject);
            }

            if (encounter == null)
            {
                return;
            }

            var text = Ui.Label("Body", body, encounter.Body, Theme.Body,
                Theme.TextDim, TextAlignmentOptions.Center);
            text.enableWordWrapping = true;
            Ui.Fixed(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(920f, 96f));

            if (receipt != null)
            {
                var box = Ui.Rounded("Receipt", body, Theme.RadiusM,
                    Theme.WithAlpha(Theme.PanelSunken, 0.96f), Theme.WithAlpha(Theme.SeriesDark, 0.6f), 1);
                Ui.Fixed(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -20f), new Vector2(920f, 150f));

                var result = Ui.Label("Result", box.transform, receipt, Theme.Body,
                    Theme.Text, TextAlignmentOptions.Center);
                result.enableWordWrapping = true;
                Ui.Stretch(result.rectTransform, 28f, 20f, 28f, 20f);
                Tween.FadeIn(result, 0.3f, 0.05f, 0f, 1f);

                var leave = ActionButton(body, "继 续 前 行", new Vector2(0f, 40f), new Vector2(420f, 76f), Theme.AccentDeep);
                leave.onClick.AddListener(Leave);
                return;
            }

            buildChoices();
        }

        private void buildChoices()
        {
            float step = 132f;
            float top = -110f;

            for (int i = 0; i < encounter.Choices.Count; i++)
            {
                var choice = encounter.Choices[i];
                string blocked = choice.Blocked != null ? choice.Blocked() : null;
                bool enabled = blocked == null;

                var row = Ui.Rounded("Choice", body, Theme.RadiusM,
                    Theme.WithAlpha(enabled ? Theme.PanelRaised : Theme.PanelSunken, 0.94f),
                    Theme.WithAlpha(enabled ? Theme.SeriesDark : Theme.Border, enabled ? 0.6f : 0.3f), 1, true);
                Ui.Fixed(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, top - step * i), new Vector2(920f, 118f));

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = row;
                btn.interactable = enabled;

                int index = i;
                btn.onClick.AddListener(() => onChoose(index));

                var caption = Ui.Label("Caption", row.transform,
                    choice.Caption + (enabled ? string.Empty : "（" + blocked + "）"),
                    Theme.H3, enabled ? Theme.Text : Theme.TextFaint, TextAlignmentOptions.Left);
                Ui.Fixed(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(32f, 20f), new Vector2(800f, 34f));

                var detail = Ui.Label("Detail", row.transform, choice.Detail, Theme.Caption,
                    enabled ? Theme.TextDim : Theme.TextFaint, TextAlignmentOptions.Left);
                Ui.Fixed(detail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(32f, -22f), new Vector2(840f, 26f));

                if (enabled)
                {
                    var rt = row.rectTransform;
                    rt.localScale = Vector3.one * 0.96f;
                    Tween.Scale(rt, 0.96f, 1f, 0.26f, Easing.OutBack, null, 0.05f * i);
                }
            }
        }

        private void onChoose(int index)
        {
            if (encounter == null || index < 0 || index >= encounter.Choices.Count)
            {
                return;
            }

            var choice = encounter.Choices[index];
            if (choice.Blocked != null && choice.Blocked() != null)
            {
                return;
            }

            App.Audio.PlaySfx("ui");
            receipt = choice.Apply != null ? choice.Apply() : "什么也没发生。";

            // 事件改变的是局内状态，立刻落盘，避免中途被杀进程丢进度
            App.PersistMeta();
            render();
        }
    }
}
