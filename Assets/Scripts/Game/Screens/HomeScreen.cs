using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 主界面。所有区块落在同一套三栏栅格里：
    ///   左栏 / 中栏 / 右栏 = 520 / 720 / 520，栏距 40；
    ///   顶栏一条水平线，左右两栏的上下分界与底线完全对齐。
    /// 面板一律用「上边界 + 下边界」声明（见 Panel），避免中心点与高度各写各的导致错位。
    /// </summary>
    public sealed class HomeScreen : Screen
    {
        // ---------------------------------------------------------------- 栅格
        private const float ColW = 520f;        // 左右栏宽
        private const float LeftX = -660f;      // 左栏中心
        private const float RightX = 660f;      // 右栏中心
        private const float Pad = 24f;          // 面板内边距

        private const float BarY = 452f;        // 顶栏中线
        private const float BarH = 96f;         // 顶栏高度（500..404）

        private const float Top = 380f;         // 内容区上边界
        private const float Split = 52f;        // 上下两块的视觉分界线
        private const float Bottom = -420f;     // 内容区下边界（左中右三栏共线）
        private const float Gap = 24f;          // 上下两块之间的空隙

        private const float UpperBottom = Split + Gap * 0.5f;   // 64
        private const float LowerTop = Split - Gap * 0.5f;      // 40
        private const float HeroBottom = Bottom + 240f;         // -180：出征卡下沿，卡下留出预览带

        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            BuildTopBar();
            BuildRules();
            BuildSeries();
            BuildHero();
            BuildDeckPanel();
            BuildRail();
            BuildPreview();

            var footer = Ui.Label("Footer", Root, "第九张史莱姆牌 · 卡牌对决 · 本地运行",
                Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 26f), new Vector2(900f, 22f));
        }

        // ---------------------------------------------------------------- 助手

        /// <summary>按上/下边界摆放一块面板——上下界的对齐关系直接可读。</summary>
        private static RectTransform Panel(string name, RectTransform parent, float centerX, float top, float bottom, float width, Color border)
        {
            float height = top - bottom;
            var box = Ui.Rounded(name, parent, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.92f), border, 1);
            Ui.Fixed(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(centerX, (top + bottom) * 0.5f), new Vector2(width, height));
            return box.rectTransform;
        }

        /// <summary>面板内标题行：左标题 + 右副标，两栏共用同一套内边距。</summary>
        private static void Header(RectTransform panel, string title, string caption, Color captionColor)
        {
            var label = Ui.Label("Title", panel, title, Theme.H3, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Pad, -18f), new Vector2(320f, 38f));

            if (string.IsNullOrEmpty(caption))
            {
                return;
            }

            var sub = Ui.Label("Caption", panel, caption, Theme.Caption, captionColor, TextAlignmentOptions.Right);
            Ui.Fixed(sub.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Pad, -20f), new Vector2(240f, 30f));
        }

        // ---------------------------------------------------------------- 顶栏

        private void BuildTopBar()
        {
            // 左：玩家牌（与左栏同宽、同左缘）
            var plate = Ui.Rounded("PlayerPlate", Root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.94f), Theme.Border, 1);
            Ui.Fixed(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(LeftX - ColW * 0.5f, BarY), new Vector2(ColW, BarH));

            var avatar = Ui.Panel("Avatar", plate.transform,
                SpriteForge.Slime(128, Theme.Accent, Theme.BgDeep, 4242, false, SlimeFace.Happy), Color.white);
            Ui.Fixed(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(68f, 68f));

            var name = Ui.Label("Name", plate.transform, App.Tap.DisplayName, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, 15f), new Vector2(400f, 32f));

            var tag = App.Tap.IsLoggedIn ? "TapTap 已连接 · 成就同步可用" : "编辑器调试身份 · 成就仅本地";
            var mode = Ui.Label("Mode", plate.transform, tag, Theme.Caption,
                App.Tap.IsLoggedIn ? Theme.Accent : Theme.Gold, TextAlignmentOptions.Left);
            Ui.Fixed(mode.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(100f, -17f), new Vector2(400f, 26f));

            // 中：字牌（居中于整屏）
            var mark = Ui.Label("Wordmark", Root, "第 九 张 史 莱 姆 牌", Theme.H3, Theme.TextDim, TextAlignmentOptions.Center);
            mark.characterSpacing = 4f;
            Ui.Fixed(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, BarY), new Vector2(600f, 40f));

            // 右：两块等宽数据（与右栏同宽、同右缘）
            Stat("Trophy", RightX + 135f, App.Meta.Trophy.ToString(), "奖杯", Theme.Gold, Theme.WithAlpha(Theme.Gold, 0.45f));

            int best = App.Director.RunCleared ? EnemyRoster.FinalBattle : App.Meta.BestBattle;
            Stat("Best", RightX - 135f,
                App.Director.RunCleared ? "已通关" : best + " / " + EnemyRoster.FinalBattle,
                "赛程进度", Theme.Text, Theme.Border);
        }

        private void Stat(string name, float centerX, string value, string caption, Color valueColor, Color border)
        {
            var box = Ui.Rounded(name, Root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.94f), border, 1);
            Ui.Fixed(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(centerX, BarY), new Vector2(250f, BarH));

            var valueLabel = Ui.Label("Value", box.transform, value, Theme.H2, valueColor, TextAlignmentOptions.Center);
            Ui.Fixed(valueLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(230f, 44f));

            var cap = Ui.Label("Cap", box.transform, caption, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(230f, 26f));
        }

        // ---------------------------------------------------------------- 左栏

        private void BuildRules()
        {
            var panel = Panel("Rules", Root, LeftX, Top, UpperBottom, ColW, Theme.Border);
            Header(panel, "如 何 对 局", null, Theme.TextFaint);

            string[] lines =
            {
                "三幕九关廿七战：每关先锋、中坚、宗师三场，打穿第三场才算过关。",
                "按住卡牌拖到屏幕中段的出牌区，松手才会打出。",
                "每场胜利三选一补强；关底宗师战连选两轮。",
                "牌组上限 30 张，满了改为「替换」——换掉弱牌，越打越精。"
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var dot = Ui.Panel("Dot" + i, panel, SpriteForge.Circle(14, Theme.Accent, Theme.AccentDeep, 0), Color.white);
                Ui.Fixed(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(Pad + 5f, -86f - i * 58f), new Vector2(9f, 9f));

                var text = Ui.Label("Line" + i, panel, lines[i], Theme.Caption, Theme.TextDim, TextAlignmentOptions.TopLeft);
                Ui.Fixed(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(Pad + 22f, -74f - i * 58f), new Vector2(ColW - Pad * 2f - 22f, 54f));
            }
        }

        private void BuildSeries()
        {
            var panel = Panel("Series", Root, LeftX, LowerTop, Bottom, ColW, Theme.Border);
            Header(panel, "八 系 列 牌 池", App.Cards.Count + " 张卡牌", Theme.TextFaint);

            const float chipW = 112f;
            const float chipH = 160f;
            const float gapX = 10f;
            const float gapY = 16f;
            const float padX = (ColW - (4f * chipW + 3f * gapX)) * 0.5f;   // 左右等距

            for (int i = 0; i < Series.All.Length; i++)
            {
                var series = Series.All[i];
                Color color = Theme.SeriesColor(series);

                int col = i % 4;
                int row = i / 4;

                float x = padX + col * (chipW + gapX) + chipW * 0.5f;
                float y = -88f - row * (chipH + gapY) - chipH * 0.5f;

                var chip = Ui.Rounded("Chip" + i, panel, Theme.RadiusS,
                    Theme.WithAlpha(Theme.Darken(color, 0.74f), 0.96f), Theme.WithAlpha(color, 0.6f), 1);
                Ui.Fixed(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(chipW, chipH));

                var glyph = Ui.Panel("Glyph", chip.transform,
                    SpriteForge.SeriesGlyph(112, Theme.Lighten(color, 0.2f), series), Color.white);
                Ui.Fixed(glyph.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(52f, 52f));

                var label = Ui.Label("Name", chip.transform, Series.DisplayName(series), Theme.H4,
                    Theme.Lighten(color, 0.42f), TextAlignmentOptions.Center);
                Ui.Fixed(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(chipW - 8f, 30f));

                var num = Ui.Label("Count", chip.transform, App.Cards.OfSeries(series).Count + " 张", Theme.Caption,
                    Theme.TextDim, TextAlignmentOptions.Center);
                Ui.Fixed(num.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(chipW - 8f, 22f));
            }
        }

        // ---------------------------------------------------------------- 中栏

        private void BuildHero()
        {
            var director = App.Director;
            var info = director.CurrentEnemy;
            Color accent = Theme.SeriesColor(info.Accent);
            int battle = director.Battle;
            int stage = director.Stage;

            var card = Ui.Rounded("Hero", Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.96f), Theme.WithAlpha(accent, 0.45f), 2);
            Ui.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, (Top + HeroBottom) * 0.5f), new Vector2(700f, Top - HeroBottom));
            Tween.SlideIn(card.rectTransform, new Vector2(0f, -40f), 0.36f, 0.05f);

            var glow = Ui.Panel("Glow", card.transform, SpriteForge.Glow(256, accent), Theme.WithAlpha(Color.white, 0.32f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -158f), new Vector2(360f, 360f));

            var portrait = Ui.Panel("Enemy", card.transform, null, Color.white);
            Sprite foeArt = ArtLib.Enemy;
            if (foeArt != null)
            {
                portrait.sprite = foeArt;
                portrait.preserveAspect = true;
                portrait.color = Color.Lerp(Color.white, accent, 0.35f);
            }
            else
            {
                portrait.sprite = SpriteForge.Slime(256, accent, Theme.BgDeep, info.Seed, true, SlimeFace.Fierce);
            }

            Ui.Fixed(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -158f), new Vector2(200f, 200f));
            Tween.PopIn(portrait.rectTransform, 0.12f, 0.85f);

            // 第一行：这一刻该打哪一场
            var levelChip = Ui.Rounded("Level", card.transform, 10,
                Theme.WithAlpha(Theme.Darken(accent, 0.6f), 0.96f),
                EnemyRoster.IsMasterStage(battle) ? Theme.Danger : Theme.WithAlpha(accent, 0.85f), 1);
            Ui.Fixed(levelChip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(420f, 40f));

            var levelLabel = Ui.Label("LevelText", levelChip.transform,
                EnemyRoster.ActNames[EnemyRoster.ActOf(battle) - 1] + "　·　" + EnemyRoster.Describe(battle),
                Theme.Body, Theme.Lighten(accent, 0.45f), TextAlignmentOptions.Center);
            Ui.Stretch(levelLabel.rectTransform, 0f, 1f, 0f, 1f);

            var title = Ui.Label("Title", card.transform, info.Name, Theme.H1, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(620f, 56f));

            var subtitle = Ui.Label("Sub", card.transform, info.Title, Theme.Body, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -332f), new Vector2(620f, 30f));

            var stats = Ui.Label("Stats", card.transform,
                "守关者生命 " + EnemyRoster.EnemyHpFor(battle)
                + "   ·   回魔 " + EnemyRoster.EnemyManaRegenFor(battle)
                + "   ·   牌库 " + EnemyRoster.EnemyDeckSizeFor(battle) + " 张"
                + "   ·   已解锁 " + EnemyRoster.UnlockedSeries(director.Level).Count + " / 8 系",
                Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -366f), new Vector2(660f, 26f));
            Tween.FadeIn(stats, 0.3f, 0.18f, 0f, 1f);

            // 廿七战进度点：三幕，每幕九战；打过的金色、当前战放大、未到空心
            const float pipStep = 22f;
            int total = EnemyRoster.FinalBattle;
            float rowWidth = (total - 1) * pipStep;
            for (int i = 0; i < total; i++)
            {
                int battleIndex = i + 1;
                bool passed = battleIndex < battle;
                bool current = battleIndex == battle;
                bool master = EnemyRoster.IsMasterStage(battleIndex);

                // 每一关的第一战稍微抬高一点，27 个点就有了「三幕九关」的节奏感
                float lift = EnemyRoster.StageOf(battleIndex) == 1 ? 6f : 0f;

                Color dotColor = passed ? Theme.Gold : (current ? accent : Theme.TextFaint);
                var dot = Ui.Panel("Battle" + i, card.transform,
                    SpriteForge.Circle(20,
                        passed || current ? Theme.WithAlpha(dotColor, 0.92f) : Theme.WithAlpha(Theme.PanelSunken, 0.85f),
                        Theme.WithAlpha(master ? Theme.Lighten(dotColor, 0.2f) : Theme.Lighten(dotColor, 0.3f), 0.85f),
                        master ? 2 : 1),
                    passed || current ? Color.white : new Color(1f, 1f, 1f, 0.30f));

                float size = current ? (master ? 16f : 14f) : (master ? 11f : 9f);
                Ui.Fixed(dot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(i * pipStep - rowWidth * 0.5f, 140f + lift), new Vector2(size, size));
            }

            var progress = Ui.Label("Progress", card.transform,
                director.RunCleared
                    ? "已打穿全部廿七战"
                    : "进度 " + battle + " / " + total + "　·　第 " + director.Level + " 关 " + EnemyRoster.StageNames[stage - 1],
                Theme.Caption, Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Fixed(progress.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 106f), new Vector2(620f, 26f));

            var start = Ui.TextButton("Start", card.transform, "开 始 对 局", Theme.H3, Theme.AccentDeep, Theme.Text, StartRun);
            Ui.Fixed(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(440f, 80f));

            var restart = Ui.TextButton("Restart", card.transform, "重 置 牌 组", Theme.Caption, Theme.PanelRaised, Theme.TextDim, RestartRun);
            Ui.Fixed(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(440f, 38f));
        }

        /// <summary>底部中间摆一排真卡面——一屏之内就能看出这是个打牌的游戏。</summary>
        private void BuildPreview()
        {
            var strip = Ui.Node("DeckPreview", Root);

            var caption = Ui.Label("Caption", strip, "当前牌组 · 最近加入的五张", Theme.Caption,
                Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -206f), new Vector2(460f, 24f));

            var ids = new List<int>();
            for (int i = App.Director.DeckIds.Count - 1; i >= 0 && ids.Count < 5; i--)
            {
                int id = App.Director.DeckIds[i];
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            const float scale = 0.60f;
            const float gap = 16f;
            float step = CardView.W * scale + gap;      // 缩放后的实际卡宽 + 视觉间距
            float left = -step * (ids.Count - 1) * 0.5f;

            // SetPosition 的 y 是「卡底距画布底边」，-420 即与左右两栏共底线。
            for (int i = 0; i < ids.Count; i++)
            {
                var def = App.Cards.Get(ids[i]);
                if (def == null)
                {
                    continue;
                }

                var view = CardView.Create(strip, def, null);
                if (view == null)
                {
                    continue;
                }

                view.SetPosition(new Vector2(left + step * i, 120f), 0f, scale);
                view.SetInteractive(false);

                // 从右下方向上一张张发进来，像把牌摊到桌面上
                var rt = view.Root;
                Tween.LocalMove(rt, rt.anchoredPosition + new Vector2(step * 0.9f, -70f), rt.anchoredPosition,
                    0.34f, Easing.OutCubic, null, 0.04f * i);
                Tween.Scale(rt, scale * 0.7f, scale, 0.34f, Easing.OutBack, null, 0.04f * i);
            }
        }

        // ---------------------------------------------------------------- 右栏

        private void BuildRail()
        {
            float height = Top - UpperBottom;       // 316
            const float buttonH = 70f;
            float step = buttonH + (height - 4f * buttonH) / 3f;   // 4 枚等分，首尾贴齐面板上下沿

            var rail = Ui.Node("Rail", Root);
            Ui.Fixed(rail, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(RightX, (Top + UpperBottom) * 0.5f), new Vector2(ColW, height));

            string[] captions = { "卡牌图鉴", "成就档案", "设置", "清除存档" };
            System.Action[] actions =
            {
                () => App.Push(new CodexScreen(CodexScreen.Tab.Cards)),
                () => App.Push(new CodexScreen(CodexScreen.Tab.Achievements)),
                () => App.Push(new SettingsScreen()),
                ConfirmReset
            };

            Color[] fills = { Theme.PanelRaised, Theme.PanelRaised, Theme.PanelRaised, Theme.WithAlpha(Theme.Danger, 0.24f) };
            Color[] textColors = { Theme.Text, Theme.Text, Theme.Text, Theme.Danger };

            for (int i = 0; i < captions.Length; i++)
            {
                int index = i;
                var btn = Ui.TextButton("Rail" + i, rail, captions[i], Theme.H4, fills[i], textColors[i],
                    () => actions[index]());
                Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -i * step), new Vector2(ColW, buttonH));
            }
        }

        private void BuildDeckPanel()
        {
            var panel = Panel("Deck", Root, RightX, LowerTop, Bottom, ColW, Theme.Border);
            Header(panel, "牌 组", App.Director.DeckCount + " / " + App.Director.DeckCap + " 张",
                App.Director.AtDeckCap ? Theme.Gold : Theme.Accent);

            var composition = App.Director.DeckComposition();
            var perSeries = new Dictionary<CardSeries, int>();
            int max = 1;

            foreach (var pair in composition)
            {
                var def = App.Cards.Get(pair.Key);
                if (def == null)
                {
                    continue;
                }

                int n;
                perSeries.TryGetValue(def.Series, out n);
                perSeries[def.Series] = n + pair.Value;
                if (perSeries[def.Series] > max)
                {
                    max = perSeries[def.Series];
                }
            }

            // 八行等距铺满：首行中心 -73，行距 50，末行下沿正好落在内边距上。
            float y = -73f;
            foreach (var series in Series.All)
            {
                int n;
                if (!perSeries.TryGetValue(series, out n) || n <= 0)
                {
                    continue;
                }

                Color color = Theme.SeriesColor(series);

                var label = Ui.Label("S" + (int)series, panel, Series.DisplayName(series), Theme.Caption,
                    Theme.TextDim, TextAlignmentOptions.Left);
                Ui.Fixed(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(Pad, y), new Vector2(80f, 26f));

                var track = Ui.Panel("Track", panel, SpriteForge.Panel(6, Theme.PanelSunken, Theme.BorderSoft, 1), Color.white);
                track.type = Image.Type.Sliced;
                Ui.Fixed(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(120f, y), new Vector2(ColW - 220f, 18f));

                var fill = Ui.Panel("Fill", track.transform, SpriteForge.Panel(6, color, Theme.Lighten(color, 0.25f), 1), Color.white);
                fill.type = Image.Type.Sliced;
                Ui.Fixed(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f),
                    new Vector2(Mathf.Max(6f, (ColW - 228f) * n / max), 14f));

                var num = Ui.Label("N" + (int)series, panel, n.ToString(), Theme.Caption,
                    Theme.Lighten(color, 0.35f), TextAlignmentOptions.Right);
                Ui.Fixed(num.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-Pad, y), new Vector2(60f, 26f));

                y -= 50f;
            }

            if (perSeries.Count == 0)
            {
                var empty = Ui.Label("Empty", panel, "牌组为空\n点「重置牌组」重建初始牌组", Theme.Caption,
                    Theme.TextFaint, TextAlignmentOptions.Center);
                Ui.Fixed(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(280f, 80f));
            }
        }

        // ---------------------------------------------------------------- 行为

        private void StartRun()
        {
            App.Audio.PlaySfx("ui");
            App.RunSelfDamage = 0;
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new BattleScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        private void RestartRun()
        {
            App.Audio.PlaySfx("mana");
            App.Director.StartNewRun();
            App.RunSelfDamage = 0;
            App.PersistMeta();
            App.Toast("牌组已重置为新的一局");
            App.Replace(new HomeScreen());
        }

        private void ConfirmReset()
        {
            var dialog = Ui.Rounded("Dialog", Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.PanelRaised, 0.99f), Theme.Border, 1);
            Ui.Fixed(dialog.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 320f));

            var text = Ui.Label("Text", dialog.transform,
                "确定清除全部本地存档？\n奖杯、图鉴、成就与统计数据都会丢失。",
                Theme.Body, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(620f, 120f));

            var yes = Ui.TextButton("Yes", dialog.transform, "确认清除", Theme.Body, Theme.Danger, Theme.Text, () =>
            {
                SaveService.DeleteFile();
                App.Meta.Trophy = 0;
                App.Meta.BestLevel = 1;
                App.Meta.BestBattle = 1;
                App.Meta.RunsPlayed = 0;
                App.Meta.Wins = 0;
                App.Meta.Losses = 0;
                App.Meta.DiscoveredCards.Clear();
                App.Meta.Unlocked.Clear();
                App.Meta.Counters.Clear();
                App.Meta.Flags.Clear();
                App.Meta.RunDeck.Clear();
                App.Meta.RunBattle = 1;
                App.Director.StartNewRun();
                App.PersistMeta();
                Object.Destroy(dialog.gameObject);
                App.Toast("存档已清除");
                App.Replace(new HomeScreen());
            });
            Ui.Fixed(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-140f, 30f), new Vector2(240f, 68f));

            var no = Ui.TextButton("No", dialog.transform, "取消", Theme.Body, Theme.Panel, Theme.TextDim,
                () => Object.Destroy(dialog.gameObject));
            Ui.Fixed(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(140f, 30f), new Vector2(240f, 68f));
        }
    }
}
