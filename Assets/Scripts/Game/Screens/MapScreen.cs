using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 章内节点地图。纵向自下而上推进：眼下可达的节点亮起，走过的留下痕迹，未连通的不可点。
    /// 布局完全由 MapNode 的归一化坐标驱动，所以换生成算法不用改界面。
    /// </summary>
    public sealed class MapScreen : Screen
    {
        // 地图是「窗口式」的：只画当前层上下各 VisibleSpan 层。
        //
        // 一章 12 层、每层 2~5 个节点，全摊在一屏里就是三十多个直径 40 的圆点。
        // 手机上每个圆点不到 8 毫米，字认不出来，连线也糊成一张网。
        // 只留五层、节点放大到 88、每个带名字：一屏最多十五个节点，看得清也点得准。
        // 全局进度交给标题栏的「第 N / 12 层」，不靠一张缩略图去猜。
        private const int VisibleSpan = 2;

        /// <summary>相邻两层的纵向间距。必须同时容下「节点直径 + 节点名」，否则名字会蹭到下一层。</summary>
        private const float LayerGap = 158f;

        private const float MapLeft = -750f;
        private const float MapRight = 750f;
        private const float MapBottom = -350f;
        private const float MapTop = 340f;

        private const float NodeSize = 88f;

        private readonly Dictionary<int, Image> nodeViews = new Dictionary<int, Image>();
        private readonly Dictionary<int, TextMeshProUGUI> nodeGlyphs = new Dictionary<int, TextMeshProUGUI>();
        private readonly Dictionary<int, TextMeshProUGUI> nodeNames = new Dictionary<int, TextMeshProUGUI>();

        /// <summary>窗口的地图中心层。BuildMap 定下来，RefreshNodes 靠它算景深。</summary>
        private int centerFloor;

        private TextMeshProUGUI hintLabel;
        private TextMeshProUGUI hpLabel;
        private TextMeshProUGUI goldLabel;
        private TextMeshProUGUI deckLabel;

        private readonly BattleSession rewardFrom;

        /// <param name="rewardSession">
        /// 战斗胜利后带奖进来：地图直接把奖励弹层盖在自己身上，
        /// 不再单独切一块战利品屏——少一次过场，战后节奏更顺。
        /// </param>
        public MapScreen(BattleSession rewardSession = null)
        {
            rewardFrom = rewardSession;
        }

        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            var director = App.Director;
            director.EnsureRun();

            // 结局：通关或阵亡都不该停在地图上
            if (director.Phase == RunPhase.Victory || director.Phase == RunPhase.Defeat)
            {
                App.Replace(new ResultScreen(director.Phase == RunPhase.Victory, null));
                return;
            }

            // 层号必须写在标题栏：窗口式地图只显示当前附近五层，
            // 玩家看不到全貌，得有个地方告诉他「这章还剩多少」。
            // 无限年没有总层数，只报当前层。
            string sub = director.Endless
                ? "第 " + (director.Floor + 1) + " 层 · " + director.ChapterSubtitle
                : "第 " + (director.Floor + 1) + " / " + EnemyRoster.FloorsPerChapter + " 层 · "
                    + director.ChapterSubtitle;
            var header = BuildHeader(director.ChapterName, sub, null);
            BuildHeaderStats(header);
            BuildHint();
            BuildMap();
            RefreshNodes();
            RefreshStats();

            // 战斗胜利带奖进来：奖励弹层盖在地图上，选完由 OnRewardComplete 接手
            if (rewardFrom != null && director.Phase == RunPhase.Reward)
            {
                new RewardOverlay(rewardFrom, OnRewardComplete).Build(Root);
            }
        }

        /// <summary>奖励弹层收尾：推进层并原地换一份新地图（无过场，层号与可达节点即时更新）。</summary>
        private void OnRewardComplete()
        {
            var director = App.Director;
            director.LeaveNode();
            App.PersistMeta();
            App.Replace(new MapScreen());
        }

        /// <summary>
        /// 状态与放弃入口都挂在标题栏里，不再单独占一列侧栏。
        ///
        /// 原来那根 460 宽的右栏只装了三个数字加一张图例，却把地图横向挤掉四分之一、
        /// 纵向也只能缩到 870 高 —— 12 层地图因此挤成一团。状态是"随时扫一眼"的辅助信息，
        /// 它配不上四分之一的屏幕。
        /// </summary>
        private void BuildHeaderStats(RectTransform header)
        {
            var abandon = Ui.TextButton("Abandon", header, "放弃本局", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, ConfirmAbandon);
            Ui.Fixed(abandon.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, 0f), new Vector2(176f, 64f));

            // 从右往左排，紧贴放弃按钮。宽度按最长文案给足：中文框一窄就被折成两行。
            deckLabel = HeaderStat(header, -220f, Theme.Accent);
            goldLabel = HeaderStat(header, -448f, Theme.Gold);
            hpLabel = HeaderStat(header, -672f, Theme.Hp);
        }

        private TextMeshProUGUI HeaderStat(RectTransform header, float rightOffset, Color color)
        {
            var label = Ui.Label("Stat", header, string.Empty, Theme.H4, color, TextAlignmentOptions.Right);
            Ui.Fixed(label.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(rightOffset, 0f), new Vector2(216f, 40f));
            return label;
        }

        /// <summary>
        /// 放弃本局。刻意不用 BuildHeader 的返回按钮：那个走 App.Pop()，
        /// 而地图是 Replace 进来的，栈里只有它自己——Pop 会把栈清空直接黑屏。
        /// </summary>
        private void ConfirmAbandon()
        {
            App.Audio.PlaySfx("ui");
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

        // ---------------------------------------------------------------- 地图

        /// <summary>
        /// 底部只留一行提示语。
        ///
        /// 七项图例已删：每个节点下面本来就写着它的名字（战斗/精英/篝火…），
        /// 图例是同一信息说第二遍，纯占版面。提示语留在原来图例的最左格。
        /// </summary>
        private void BuildHint()
        {
            hintLabel = Ui.Label("Hint", Root, string.Empty, Theme.Caption, Theme.TextFaint,
                TextAlignmentOptions.Left);
            Ui.Fixed(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-780f, EdgeMargin + 22f), new Vector2(340f, 32f));
        }

        private void RefreshStats()
        {
            var director = App.Director;
            hpLabel.text = "生命 " + director.Hp + " / " + director.HpMax;
            goldLabel.text = "金币 " + director.Gold;
            deckLabel.text = "牌组 " + director.DeckCount + " / " + DeckRules.DeckCap;
        }

        // ---------------------------------------------------------------- 地图

        private void BuildMap()
        {
            var director = App.Director;
            var map = director.Map;
            if (map == null)
            {
                return;
            }

            float width = MapRight - MapLeft;
            float height = MapTop - MapBottom;
            Vector2 center = new Vector2(0f, (MapBottom + MapTop) * 0.5f);

            // 窗口贴边：当前层在首末两层时不再强行居中，否则一半屏幕是空的。
            // 贴住上下边缘后，任何时刻都能看到满 VisibleSpan*2+1 层。
            // 上限取地图自身而不是章节常量——无限年的线段层数与普通图不同。
            int maxFloor = map.MaxFloor;
            centerFloor = Mathf.Clamp(director.Floor, VisibleSpan,
                Mathf.Max(VisibleSpan, maxFloor - VisibleSpan));

            // 底图：一条向上收窄的光带，暗示推进方向。
            // 用横向拉长的椭圆而不是正圆——正圆在 16:9 屏上会露出清晰的圆形边界。
            var backdrop = Ui.Panel("Backdrop", Root, SpriteForge.Glow(512, Theme.WithAlpha(Theme.Accent, 0.16f)), Color.white);
            Ui.Fixed(backdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                center, new Vector2(width * 1.7f, height * 1.3f));

            var mapRoot = Ui.Node("Map", Root);
            Ui.Fixed(mapRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(width, height));

            // 先画连线，再画节点——线必须在圆点下面。
            //
            // 连线分三档而不是两档：一层四五个节点、每个连下一层两三个，
            // 全画成同一浓度就是一张蜘蛛网，读不出自己在哪、能去哪。
            // 只有「走过的路」和「下一步能走的路」值得显形，其余退成底纹。
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var from = map.Nodes[i];
                if (!InWindow(from))
                {
                    continue;
                }

                for (int k = 0; k < from.Next.Count; k++)
                {
                    var to = map.Get(from.Next[k]);
                    if (to == null || !InWindow(to))
                    {
                        continue;
                    }

                    bool walked = from.Visited && to.Visited;
                    bool step = director.Reachable.Contains(to.Id);

                    Color color;
                    float thickness;
                    if (walked)
                    {
                        color = Theme.WithAlpha(Theme.Gold, 0.8f);
                        thickness = 6f;
                    }
                    else if (step)
                    {
                        color = Theme.WithAlpha(Theme.Accent, 0.55f);
                        thickness = 5f;
                    }
                    else
                    {
                        // 底纹要淡到「能看出有路」但「不参与抢注意力」。
                        // 0.15 太淡了：底纹彻底消失后，玩家看不出这些节点之间有没有路，
                        // 反而以为每个圆点是孤立的。0.26 能看出连线，又不抢焦点。
                        color = Theme.WithAlpha(Theme.Border, 0.26f);
                        thickness = 4f;
                    }

                    var img = Ui.Solid("Link", mapRoot, color);
                    img.raycastTarget = false;
                    RectTransform line = img.rectTransform;

                    Vector2 a = NodePos(from);
                    Vector2 b = NodePos(to);
                    Vector2 mid = (a + b) * 0.5f;
                    float len = Vector2.Distance(a, b);
                    float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

                    Ui.Fixed(line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), mid, new Vector2(len, thickness));
                    line.localRotation = Quaternion.Euler(0f, 0f, ang);
                }
            }

            // 再画节点
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                if (!InWindow(node))
                {
                    continue;
                }

                Vector2 pos = NodePos(node);
                Color color = KindColor(node.Kind);

                // Boss 比普通节点大一档：它是整章的终点，视觉上必须能一眼认出。
                float size = node.Kind == NodeKind.Boss ? NodeSize * 1.18f : NodeSize;

                var img = Ui.Panel("Node" + node.Id, mapRoot,
                    SpriteForge.Circle(128, Theme.Darken(color, 0.55f), Theme.Lighten(color, 0.25f), 4), Color.white);
                img.raycastTarget = true;
                Ui.Fixed(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));

                // 字形要显式给成节点尺寸：Ui.Label 新建的 RectTransform 是零尺寸，
                // 只靠 Stretch 撑不开，文字会溢出到圆外。
                var glyph = Ui.Label("Glyph", img.transform, NodeKinds.Glyph(node.Kind),
                    Theme.H3, Theme.Text, TextAlignmentOptions.Center);
                Ui.Fixed(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size, size));

                // 圆点里只有一个字形，玩家得靠图例反查才知道「剑」是普通战斗。
                // 名字直接写在下面，辨识负担归零——这是放大节点换来的空间该花的地方。
                var name = Ui.Label("Name", mapRoot, node.Label, Theme.Body, Theme.TextDim,
                    TextAlignmentOptions.Center);
                Ui.Fixed(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(pos.x, pos.y - size * 0.5f - 24f), new Vector2(190f, 30f));

                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int id = node.Id;
                btn.onClick.AddListener(() => OnNodeClicked(id));

                nodeViews[node.Id] = img;
                nodeGlyphs[node.Id] = glyph;
                nodeNames[node.Id] = name;
            }

            hintLabel.text = director.CurrentNodeId == 0 ? "选择起点出发" : "点亮才可前往";
        }

        /// <summary>节点是否落在当前窗口（当前层上下 VisibleSpan 层）内。</summary>
        private bool InWindow(MapNode node)
        {
            return Mathf.Abs(node.Floor - centerFloor) <= VisibleSpan;
        }

        /// <summary>
        /// 节点本地坐标。
        /// 纵向按「距当前层的层数 × LayerGap」，而不是归一化 Y —— 归一化 Y 会把整章 12 层
        /// 压进地图高度里，层距被摊薄到节点互相咬合；窗口式地图只需要管好这五层。
        /// </summary>
        private Vector2 NodePos(MapNode node)
        {
            float width = MapRight - MapLeft;
            float x = (node.X - 0.5f) * width;
            float y = (node.Floor - centerFloor) * LayerGap;
            return new Vector2(x, y);
        }

        private void RefreshNodes()
        {
            var director = App.Director;
            var map = director.Map;
            if (map == null)
            {
                return;
            }

            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                Image img;
                if (!nodeViews.TryGetValue(node.Id, out img))
                {
                    continue;
                }

                bool reachable = director.Reachable.Contains(node.Id);
                Color color = KindColor(node.Kind);

                // 景深：离当前层越远越淡。五层画成同一个浓度，
                // 玩家看不出哪一层是「现在」，也就读不出自己站在哪一阶。
                // 可达节点不参与淡出——它们是这一屏唯一要你点的东西。
                float depth = VisibleSpan <= 0 ? 0f : Mathf.Abs(node.Floor - centerFloor) / (float)VisibleSpan;
                float alpha = reachable ? 1f : Mathf.Lerp(1f, 0.42f, Mathf.Clamp01(depth));

                if (reachable)
                {
                    img.sprite = SpriteForge.Circle(128, color, Theme.Lighten(color, 0.55f), 5);
                    img.color = Color.white;
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                    }

                    // 可达节点轻轻放大一下，把注意力引过去。第二个参数是缩放倍率不是尺寸——
                    // 传 NodeSize 会让节点被放大几十倍，铺满整屏。
                    Tween.PopInScaled(img.rectTransform, 1.06f, 0f, 0.94f);
                }
                else if (node.Visited)
                {
                    img.sprite = SpriteForge.Circle(128, Theme.WithAlpha(Theme.Darken(color, 0.72f), 0.9f), Theme.Gold, 2);
                    img.color = new Color(1f, 1f, 1f, 0.92f * alpha);
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = false;
                    }
                }
                else
                {
                    // 未到达节点必须看得见：整张地图要一眼看出「往后能走到哪」。
                    // 底色按节点类型着色并压暗，但保留足够实度，不能透明到只剩字形。
                    // 0.9 的不透明度是硬下限——再低就会和背景里的城堡剪影糊在一起。
                    img.sprite = SpriteForge.Circle(128, Theme.WithAlpha(Theme.Darken(color, 0.45f), 1f),
                        Theme.WithAlpha(Theme.Lighten(color, 0.15f), 0.9f), 3);
                    img.color = new Color(1f, 1f, 1f, 0.9f * alpha);
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = false;
                    }
                }

                TextMeshProUGUI glyph;
                if (nodeGlyphs.TryGetValue(node.Id, out glyph))
                {
                    float ga = reachable ? 1f : (node.Visited ? 0.85f : 0.72f);
                    glyph.color = Theme.WithAlpha(Theme.Text, ga * alpha);
                }

                TextMeshProUGUI nameLabel;
                if (nodeNames.TryGetValue(node.Id, out nameLabel))
                {
                    nameLabel.color = Theme.WithAlpha(reachable ? Theme.Text : Theme.TextDim, alpha);
                }
            }
        }

        private void OnNodeClicked(int nodeId)
        {
            var director = App.Director;
            if (!director.Reachable.Contains(nodeId))
            {
                return;
            }

            App.Audio.PlaySfx("ui");
            if (!director.EnterNode(nodeId))
            {
                return;
            }

            App.PersistMeta();
            Route();
        }

        /// <summary>进入节点后按阶段决定去哪一屏。</summary>
        private void Route()
        {
            var director = App.Director;

            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                switch (director.Phase)
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
                        App.Replace(new MapScreen());
                        break;
                }

                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        /// <summary>节点配色：按危险度排序，红=危险、金=收益、绿=恢复。</summary>
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
