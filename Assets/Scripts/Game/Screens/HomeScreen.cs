using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 主界面。设计原则：**一屏只回答一个问题——「我现在要做什么」**。
    ///
    /// 结构（自上而下三层，层与层之间只靠留白分隔，不画多余边框）：
    ///   ① 顶栏：我是谁（左）+ 三个核心数字（右）
    ///   ② 主卡：这一局打到哪、打谁、还有多少血、点这个继续 —— 唯一视觉重心
    ///   ③ 底栏：牌库 / 设置 / 重开一局 —— 全部降级为哑光小按钮
    ///
    /// 旧版把「如何对局」「八系列牌池」「牌组构成」「牌组预览」四块面板平铺在一屏，
    /// 60+ 行文字，玩家第一眼没有任何可落脚的地方。那些内容要么搬进档案室，
    /// 要么只在玩家主动查看时出现。
    /// </summary>
    public sealed class HomeScreen : Screen
    {
        // ---------------------------------------------------------------- 栅格
        private const float ColW = 1080f;
        private const float Pad = 36f;

        private const float TopBarY = 466f;
        private const float TopBarH = 96f;

        // 主卡：上下界直接写死，内部元素全部相对卡片自身排布（详见 BuildHero 的纵轴表）
        private const float CardTop = 372f;
        private const float CardBottom = -300f;
        private const float CardH = CardTop - CardBottom;   // 672

        private const float FooterY = -430f;

        protected override void Build()
        {
            App.Audio.PlayMusic("title1");

            BuildTopBar();
            BuildHero();
            BuildFooter();

            // 新春装饰最后挂到 Root（渲染在最上层），但只占屏幕四角的空地——
            // 中轴的顶栏与主卡一个像素都不碰；窗口外或玩家在设置里关掉就整层不建。
            if (Festival.IsActive)
            {
                BuildFestivalDecor();
            }
        }

        /// <summary>
        /// 新春装饰：左右春联、檐口双灯笼、顶部福字。全部 SpriteForge 程序化，无新素材。
        /// 只占屏幕四角的空地，中轴（顶栏与主卡）一个像素都不碰。
        /// </summary>
        private void BuildFestivalDecor()
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

            // ---- 春联：左右两条红底金字的竖批 ----
            BuildCouplet(-878f, "辞\n旧\n迎\n新", 0.05f);
            BuildCouplet(878f, "万\n事\n胜\n意", 0.14f);

            // ---- 双灯笼：挂在檐口（屏幕顶缘），带垂绳 ----
            BuildLantern(-640f, 0f);
            BuildLantern(640f, 0.12f);

            // ---- 福字：顶栏中段的空地，一枚红底金字的菱印 ----
            var fu = Ui.Rounded("Fu", Root, Theme.RadiusS,
                Theme.WithAlpha(Theme.Darken(red, 0.2f), 0.94f), Theme.WithAlpha(gold, 0.85f), 2);
            Ui.Fixed(fu.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 474f), new Vector2(92f, 92f));
            fu.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var fuText = Ui.Label("FuText", fu.transform, "福", Theme.H3, gold, TextAlignmentOptions.Center);
            Ui.Fixed(fuText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(130f, 130f));
            fuText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Tween.PopIn(fu.rectTransform, 0.32f, 0.5f, 0.4f);
        }

        private void BuildCouplet(float x, string verticalText, float delay)
        {
            Color red = Theme.SeriesSpring;

            var strip = Ui.Rounded("Couplet", Root, Theme.RadiusS,
                Theme.WithAlpha(Theme.Darken(red, 0.15f), 0.94f), Theme.WithAlpha(Theme.Gold, 0.6f), 2);
            Ui.Fixed(strip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 60f), new Vector2(64f, 560f));
            Tween.SlideIn(strip.rectTransform, new Vector2(x > 0 ? 120f : -120f, 0f), 0.42f, delay);

            var text = Ui.Label("Text", strip.transform, verticalText, Theme.Body,
                Theme.Gold, TextAlignmentOptions.Center);
            Ui.Stretch(text.rectTransform, 4f, 8f, 4f, 8f);
            Tween.FadeIn(text, 0.4f, delay + 0.16f, 0f, 1f);
        }

        private void BuildLantern(float x, float delay)
        {
            Color red = Theme.SeriesSpring;
            Color gold = Theme.Gold;

            // 垂绳：从屏幕顶缘垂到灯笼
            var rope = Ui.Solid("Rope", Root, Theme.WithAlpha(gold, 0.55f));
            rope.raycastTarget = false;
            Ui.Fixed(rope.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -34f), new Vector2(2.5f, 68f));

            var lantern = Ui.Panel("Lantern", Root, SpriteForge.Lantern(160, red, gold), Color.white);
            lantern.raycastTarget = false;
            Ui.Fixed(lantern.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -122f), new Vector2(124f, 148f));
            lantern.preserveAspect = true;
            Tween.SlideIn(lantern.rectTransform, new Vector2(0f, 110f), 0.5f, delay);
        }

        // ---------------------------------------------------------------- 顶栏

        private void BuildTopBar()
        {
            var bar = Ui.Node("TopBar", Root);
            Ui.Fixed(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, TopBarY), new Vector2(ColW, TopBarH));

            // --- 左：身份（头像 + 名字）。技术上「是否登录」不再是主界面信息，挪到设置里去了 ---
            // 头像用真立绘，和战斗里的主角保持同一张脸；缺图才回退到程序化史莱姆。
            Sprite heroArt = ArtLib.Hero;
            var avatar = Ui.Panel("Avatar", bar,
                heroArt != null
                    ? heroArt
                    : SpriteForge.Slime(160, Theme.Accent, Theme.BgDeep, 4242, false, SlimeFace.Happy),
                Color.white);
            avatar.preserveAspect = true;
            Ui.Fixed(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(80f, 80f));

            var name = Ui.Label("Name", bar, App.Tap.DisplayName, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, 0f), new Vector2(360f, 40f));

            // --- 右：三个数字，从右缘往左依次排，每个是「数值在上、标签在下」 ---
            int best = App.Meta.BestChapter > 0 ? App.Meta.BestChapter : App.Director.Chapter;

            // 从右往左：奖杯（最重要，放最右） → 金币 → 最远章节
            Stat(bar, "Trophy", App.Meta.Trophy.ToString(), "奖杯", 0f);
            Stat(bar, "Gold", App.Director.Gold.ToString(), "金币", -200f);
            Stat(bar, "Chapter", best + " / " + EnemyRoster.TotalChapters, "最远章节", -400f);
        }

        /// <summary>一枚右对齐的数字块。<paramref name="rightOffset"/> 是距顶栏右缘的距离。</summary>
        private void Stat(RectTransform bar, string name, string value, string caption, float rightOffset)
        {
            const float w = 180f;

            var box = Ui.Node(name, bar);
            Ui.Fixed(box, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(rightOffset, 0f), new Vector2(w, 92f));

            var v = Ui.Label("Value", box, value, Theme.H2, Theme.Text, TextAlignmentOptions.Right);
            Ui.Fixed(v.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, 14f), new Vector2(w, 48f));

            var c = Ui.Label("Cap", box, caption, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Right);
            Ui.Fixed(c.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -26f), new Vector2(w, 30f));
        }

        // ---------------------------------------------------------------- 主卡

        /// <summary>
        /// 唯一的重心：一块大地台，放章节 / 对手立绘 / 血条 / 主按钮。
        /// 纵轴（相对卡片上缘，负值向下，卡片高 672）：
        ///   -48  章节·层 胶囊（高 50）
        ///   -240 对手立绘（236 见方，占 -122..-358）
        ///   -388 章节副标题（占 -362..-414）
        ///   -440 下一个对手（占 -422..-458）
        ///   底 172 血条（高 34，数字写在条内）/ 底 32 主按钮（高 90）
        /// </summary>
        private void BuildHero()
        {
            var director = App.Director;
            var info = director.NextEnemyPreview;
            Color accent = Theme.SeriesColor(info.Accent);

            var card = Ui.Rounded("Hero", Root, Theme.RadiusL,
                Theme.WithAlpha(Theme.Panel, 0.96f), Theme.WithAlpha(accent, 0.5f), 2);
            Ui.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, (CardTop + CardBottom) * 0.5f), new Vector2(ColW, CardH));
            Tween.SlideIn(card.rectTransform, new Vector2(0f, -40f), 0.36f, 0.05f);

            // 光晕压在立绘后面，给立绘一个"从暗场里亮出来"的落点
            var glow = Ui.Panel("Glow", card.transform, SpriteForge.Glow(256, accent),
                Theme.WithAlpha(Color.white, 0.30f));
            Ui.Fixed(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -238f), new Vector2(410f, 410f));

            var portrait = Ui.Panel("Enemy", card.transform, null, Color.white);
            Sprite foeArt = ArtLib.Monster(info.Sprite);
            portrait.sprite = foeArt != null
                ? foeArt
                : SpriteForge.Slime(256, accent, Theme.BgDeep, info.Seed, true, SlimeFace.Fierce);
            portrait.preserveAspect = true;
            Ui.Fixed(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -240f), new Vector2(236f, 236f));
            Tween.PopIn(portrait.rectTransform, 0.12f, 0.85f);

            // 章节 · 层
            var chip = Ui.Rounded("Level", card.transform, 10,
                Theme.WithAlpha(Theme.Darken(accent, 0.62f), 0.96f), Theme.WithAlpha(accent, 0.85f), 1);
            // 胶囊再往下 14：原先半贴卡片上沿，像被人推到边上的标签。
            Ui.Fixed(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -62f), new Vector2(600f, 50f));

            var chipText = Ui.Label("LevelText", chip.transform,
                director.ChapterName + "　·　第 " + (director.Floor + 1) + " 层",
                Theme.Body, Theme.Lighten(accent, 0.45f), TextAlignmentOptions.Center);
            Ui.Stretch(chipText.rectTransform, 0f, 0f, 0f, 0f);

            var title = Ui.Label("Title", card.transform, director.ChapterSubtitle,
                Theme.H2, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -396f), new Vector2(ColW - Pad * 2f, 52f));

            var foe = Ui.Label("Foe", card.transform, "下一个对手 · " + info.Name,
                Theme.Body, Theme.WithAlpha(accent, 1f), TextAlignmentOptions.Center);
            Ui.Fixed(foe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -448f), new Vector2(ColW - Pad * 2f, 36f));

            // 血条：跨节点保留的唯一稀缺资源。数字直接写在条里，省掉一行「生命 X/Y」
            var hpTrack = Ui.Panel("HpTrack", card.transform,
                SpriteForge.Panel(12, Theme.PanelSunken, Theme.BorderSoft, 1), Color.white);
            hpTrack.type = Image.Type.Sliced;
            // 血条和主按钮同宽：铺满整张卡片的话，34 高的红条会读成一条警示横幅，
            // 而不是「我现在还剩多少血」。
            const float hpW = 600f;
            Ui.Fixed(hpTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 172f), new Vector2(hpW, 34f));

            float hpPct = director.HpMax <= 0 ? 0f : (float)director.Hp / director.HpMax;
            var hpFill = Ui.Panel("HpFill", hpTrack.transform,
                SpriteForge.Panel(12, hpPct > 0.5f ? Theme.Hp : Theme.Danger,
                    Theme.Lighten(Theme.Hp, 0.3f), 1), Color.white);
            hpFill.type = Image.Type.Sliced;
            Ui.Fixed(hpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(3f, 0f), new Vector2(Mathf.Max(8f, (hpW - 6f) * hpPct), 28f));

            var hpText = Ui.Label("HpText", hpTrack.transform,
                "生命 " + director.Hp + " / " + director.HpMax,
                Theme.Caption, Theme.Text, TextAlignmentOptions.Center);
            Ui.Stretch(hpText.rectTransform, 0f, 0f, 0f, 0f);

            // 主按钮：整屏唯一的实心高饱和色块
            var start = Ui.TextButton("Start", card.transform, "继 续 冒 险", Theme.H3,
                Theme.AccentDeep, Theme.Text, StartRun);
            Ui.Fixed(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(600f, 90f));
        }

        // ---------------------------------------------------------------- 底栏

        /// <summary>
        /// 底栏只放三个「玩家会主动想点」的入口。
        /// 「清除存档」本来设置里就有，从主界面移除以减少一个破坏性按钮的暴露面。
        /// </summary>
        private void BuildFooter()
        {
            var bar = Ui.Node("Footer", Root);
            Ui.Fixed(bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, FooterY), new Vector2(ColW, 76f));

            const float gap = 24f;
            const float w = (ColW - gap * 3f) / 4f;

            Entry(bar, "Codex", "牌 库", -ColW * 0.5f + w * 0.5f, w,
                () => App.Push(new CodexScreen(CodexScreen.Tab.Cards)));
            Entry(bar, "Endless", "无限年", -ColW * 0.5f + w * 0.5f + w + gap, w, ConfirmEndless);
            Entry(bar, "Settings", "设 置", -ColW * 0.5f + w * 0.5f + (w + gap) * 2f, w,
                () => App.Push(new SettingsScreen()));
            Entry(bar, "Restart", "重开一局", -ColW * 0.5f + w * 0.5f + (w + gap) * 3f, w, RestartRun);
        }

        private void Entry(RectTransform bar, string name, string caption, float x, float w, System.Action onClick)
        {
            var btn = Ui.TextButton(name, bar, caption, Theme.Body, Theme.PanelRaised, Theme.TextDim, onClick);
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(w, 76f));
        }

        // ---------------------------------------------------------------- 行为

        private void StartRun()
        {
            App.Audio.PlaySfx("ui");
            App.Fade(0f, 1f, Theme.Fast, () =>
            {
                App.Replace(new MapScreen());
                App.Fade(1f, 0f, Theme.Normal, null);
            });
        }

        private void RestartRun()
        {
            // 重开会清掉当前局，属于不可撤回操作，必须二次确认
            App.ShowConfirm("重开一局会放弃当前进度\n（奖杯与图鉴记录保留）", "确认重开", "取消", () =>
            {
                App.Audio.PlaySfx("mana");
                App.Director.StartNewRun();
                App.PersistMeta();
                App.Toast("已重开一局");
                App.Replace(new HomeScreen());
            });
        }

        private void ConfirmEndless()
        {
            // 手上有一局没打完才需要警告；空档期直接进
            if (App.Director.DeckCount > 0)
            {
                App.ShowConfirm("进入无限年会放弃当前进度\n（奖杯与图鉴记录保留）", "进入", "取消", StartEndless);
                return;
            }

            StartEndless();
        }

        private void StartEndless()
        {
            App.Audio.PlaySfx("mana");
            App.Director.StartNewRun(true);
            App.PersistMeta();
            App.Toast("无限年 · 第 1 层");
            App.Replace(new HomeScreen());
        }
    }
}
