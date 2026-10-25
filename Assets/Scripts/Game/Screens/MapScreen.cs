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
        private const float MapLeft = -700f;
        private const float MapRight = 300f;
        private const float MapBottom = -420f;
        private const float MapTop = 340f;

        private const float PanelX = 660f;
        private const float NodeSize = 82f;

        private readonly Dictionary<int, Image> nodeViews = new Dictionary<int, Image>();
        private readonly Dictionary<int, TextMeshProUGUI> nodeGlyphs = new Dictionary<int, TextMeshProUGUI>();

        private TextMeshProUGUI hintLabel;
        private TextMeshProUGUI hpLabel;
        private TextMeshProUGUI goldLabel;
        private TextMeshProUGUI deckLabel;
        private RectTransform legend;

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

            BuildHeader(director.ChapterName, director.ChapterSubtitle, null);
            BuildAbandonButton();
            BuildStatusRail();
            BuildMap();
            RefreshNodes();
        }

        /// <summary>
        /// 放弃本局。刻意不用 BuildHeader 的返回按钮：那个走 App.Pop()，
        /// 而地图是 Replace 进来的，栈里只有它自己——Pop 会把栈清空直接黑屏。
        /// </summary>
        private void BuildAbandonButton()
        {
            var back = Ui.TextButton("Abandon", Root, "放弃本局", Theme.Body,
                Theme.PanelRaised, Theme.TextDim, ConfirmAbandon);
            Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-SideMargin - 24f, -TopMargin + 48f), new Vector2(180f, 60f));
        }

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

        // ---------------------------------------------------------------- 右侧状态

        private void BuildStatusRail()
        {
            var director = App.Director;

            var panel = Ui.Rounded("Rail", Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.94f), Theme.Border, 1);
            Ui.Fixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PanelX, 40f), new Vector2(460f, 620f));

            var title = Ui.Label("Title", panel.transform, "勇 者 状 态", Theme.H3, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -28f), new Vector2(300f, 34f));

            hpLabel = Ui.Label("Hp", panel.transform, string.Empty, Theme.H2, Theme.Hp, TextAlignmentOptions.Left);
            Ui.Fixed(hpLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -86f), new Vector2(400f, 44f));

            var hpTrack = Ui.Panel("HpTrack", panel.transform,
                SpriteForge.Panel(8, Theme.PanelSunken, Theme.BorderSoft, 1), Color.white);
            hpTrack.type = Image.Type.Sliced;
            Ui.Fixed(hpTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -140f), new Vector2(404f, 16f));

            float pct = director.HpMax <= 0 ? 0f : (float)director.Hp / director.HpMax;
            var hpFill = Ui.Panel("HpFill", hpTrack.transform,
                SpriteForge.Panel(8, pct > 0.5f ? Theme.Hp : Theme.Danger, Theme.Lighten(Theme.Hp, 0.3f), 1), Color.white);
            hpFill.type = Image.Type.Sliced;
            Ui.Fixed(hpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f),
                new Vector2(Mathf.Max(6f, 400f * pct), 12f));

            goldLabel = Ui.Label("Gold", panel.transform, string.Empty, Theme.H4, Theme.Gold, TextAlignmentOptions.Left);
            Ui.Fixed(goldLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -186f), new Vector2(400f, 34f));

            deckLabel = Ui.Label("Deck", panel.transform, string.Empty, Theme.H4, Theme.Accent, TextAlignmentOptions.Left);
            Ui.Fixed(deckLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -228f), new Vector2(400f, 34f));

            // 图例：把六种节点讲清楚，否则第一次进来不知道「焰」是什么
            legend = Ui.Node("Legend", panel.transform);
            Ui.Fixed(legend, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -290f), new Vector2(404f, 250f));

            var kinds = new[]
            {
                NodeKind.Battle, NodeKind.Elite, NodeKind.Rest,
                NodeKind.Treasure, NodeKind.Shop, NodeKind.Event, NodeKind.Boss
            };

            for (int i = 0; i < kinds.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                float x = col * 200f;
                float y = -row * 46f;

                var dot = Ui.Panel("Dot", legend, SpriteForge.Circle(28, KindColor(kinds[i]), Theme.Lighten(KindColor(kinds[i]), 0.3f), 1), Color.white);
                Ui.Fixed(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(x, y), new Vector2(18f, 18f));

                var label = Ui.Label("L", legend, NodeKinds.DisplayName(kinds[i]), Theme.Caption,
                    Theme.TextDim, TextAlignmentOptions.Left);
                Ui.Fixed(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(x + 26f, y), new Vector2(150f, 26f));
            }

            hintLabel = Ui.Label("Hint", panel.transform, string.Empty, Theme.Caption,
                Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(hintLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(420f, 26f));

            RefreshRail();
        }

        private void RefreshRail()
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
            Vector2 center = new Vector2((MapLeft + MapRight) * 0.5f, (MapBottom + MapTop) * 0.5f);

            // 底图：一条向上收窄的光带，暗示推进方向
            var backdrop = Ui.Panel("Backdrop", Root, SpriteForge.Glow(512, Theme.WithAlpha(Theme.Accent, 0.16f)), Color.white);
            Ui.Fixed(backdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                center, new Vector2(width * 1.2f, height * 1.1f));

            var mapRoot = Ui.Node("Map", Root);
            Ui.Fixed(mapRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(width, height));

            // 先画连线，再画节点——线必须在圆点下面
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var from = map.Nodes[i];
                for (int k = 0; k < from.Next.Count; k++)
                {
                    var to = map.Get(from.Next[k]);
                    if (to == null)
                    {
                        continue;
                    }

                    RectTransform line;
                    Color color = from.Visited && to.Visited
                        ? Theme.WithAlpha(Theme.Gold, 0.85f)
                        : Theme.WithAlpha(Theme.Border, 0.55f);
                    var img = Ui.Solid("Link", mapRoot, color);
                    img.raycastTarget = false;
                    line = img.rectTransform;

                    Vector2 a = NodePos(from, width, height);
                    Vector2 b = NodePos(to, width, height);
                    Vector2 mid = (a + b) * 0.5f;
                    float len = Vector2.Distance(a, b);
                    float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

                    Ui.Fixed(line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), mid, new Vector2(len, 4f));
                    line.localRotation = Quaternion.Euler(0f, 0f, ang);
                }
            }

            // 再画节点
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                Vector2 pos = NodePos(node, width, height);
                Color color = KindColor(node.Kind);

                var img = Ui.Panel("Node" + node.Id, mapRoot,
                    SpriteForge.Circle(96, Theme.Darken(color, 0.55f), Theme.Lighten(color, 0.25f), 3), Color.white);
                img.raycastTarget = true;
                Ui.Fixed(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(NodeSize, NodeSize));

                var glyph = Ui.Label("Glyph", img.transform, NodeKinds.Glyph(node.Kind),
                    Theme.H3, Theme.Text, TextAlignmentOptions.Center);
                Ui.Stretch(glyph.rectTransform, 0f, 0f, 0f, 0f);

                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int id = node.Id;
                btn.onClick.AddListener(() => OnNodeClicked(id));

                nodeViews[node.Id] = img;
                nodeGlyphs[node.Id] = glyph;
            }

            hintLabel.text = director.CurrentNodeId == 0 ? "选择起始节点开始本章" : "选择下一个节点继续前进";
        }

        /// <summary>归一化坐标 → 本地坐标。Y 轴翻过来，让 Boss 在屏幕上方。</summary>
        private static Vector2 NodePos(MapNode node, float width, float height)
        {
            return new Vector2((node.X - 0.5f) * width, (node.Y - 0.5f) * height);
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

                if (reachable)
                {
                    img.sprite = SpriteForge.Circle(96, color, Theme.Lighten(color, 0.55f), 4);
                    img.color = Color.white;
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = true;
                    }

                    // 可达节点轻轻放大一下，把注意力引过去
                    Tween.PopInScaled(img.rectTransform, NodeSize, 0f, 0.94f);
                }
                else if (node.Visited)
                {
                    img.sprite = SpriteForge.Circle(96, Theme.WithAlpha(Theme.Darken(color, 0.72f), 0.9f), Theme.Gold, 2);
                    img.color = new Color(1f, 1f, 1f, 0.9f);
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = false;
                    }
                }
                else
                {
                    img.sprite = SpriteForge.Circle(96, Theme.WithAlpha(Theme.Darken(color, 0.82f), 0.75f),
                        Theme.WithAlpha(Theme.Border, 0.5f), 2);
                    img.color = new Color(1f, 1f, 1f, 0.45f);
                    var btn = img.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.interactable = false;
                    }
                }

                TextMeshProUGUI glyph;
                if (nodeGlyphs.TryGetValue(node.Id, out glyph))
                {
                    glyph.color = reachable ? Theme.Text : Theme.WithAlpha(Theme.Text, 0.35f);
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
