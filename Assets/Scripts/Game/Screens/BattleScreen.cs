using System.Collections;
using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 1v1 卡牌对决主画面。舞台式布局：
    /// 玩家居左、敌人居右，同一条水平线同构信息列；手牌沉底居中。
    /// 出牌方式是「拖到屏幕中段的出牌区再松手」，松手位置决定成败，不是点击。
    /// </summary>
    public sealed class BattleScreen : Screen, ICardDragHost
    {
        // 画布按 1920x1080 设计，实际安全范围 x∈[-920,920]、y∈[-500,500]。
        // 手牌锚在屏幕下沿（CardView.SetPosition 用 bottom-center 锚点），
        // 因此这里的 y 是"底边探出屏幕多少"——正数留白、负数代表整体露在画面内。
        private const float HandBottom = 6f;
        private const float HandMaxWidth = 820f;
        private const float HandScale = 0.9f;
        private const float HandCenterX = 0f;
        private const float HandHoverLift = 16f;   // 指针悬停时抬高多少

        // ---- 顶部基线：舞台牌居中、赛程牌贴左上、认输贴右上，同一水平线 ----
        private const float TopInset = 40f;

        // ---- 对峙舞台：玩家居左、敌人居右，同一水平线，信息列收在各自立绘正下方 ----
        private const float StageX = 450f;         // 双方中轴（玩家 -StageX / 敌人 +StageX）
        private const float PortraitY = 150f;
        private const float PortraitSize = 280f;
        private const float NameY = -24f;          // 立绘名字基线（双方同构）
        private const float HpY = -68f;
        private const float StatusY = -108f;       // 状态 chips / 增益流式区顶缘
        private const float HpBarW = 380f;
        private const float EnemySubY = 316f;      // 敌人称号+手牌数（立绘上方）

        // ---- 出牌释放区：屏幕中段一整条「牌桌」，拖到这里松手才算打出 ----
        private const float ZoneY = -160f;
        private const float ZoneW = 1180f;
        private const float ZoneH = 124f;
        private const float ZoneSlack = 26f;       // 判定放宽，别让手抖毁掉一次出牌

        // ---- 底部角位：魔力宝石 / 抽牌堆靠左，弃牌堆、结束回合靠右 ----
        private const float ManaX = -640f;
        private const float ManaY = -396f;
        private const float DrawPileX = -840f;
        private const float PileY = -392f;
        private const float DiscardPileX = 840f;
        private const float EndTurnX = 800f;
        private const float EndTurnY = -120f;

        private BattleSession session;
        private readonly List<CardView> handViews = new List<CardView>();
        private readonly List<Floating> floats = new List<Floating>();

        private Screen backTo;

        // 敌方
        private Image enemyPortrait;
        private Image enemyHpFill;
        private Image enemyHpBack;
        private TextMeshProUGUI enemyHpText;
        private TextMeshProUGUI enemyNameLabel;
        private TextMeshProUGUI enemySubLabel;
        private RectTransform enemyStatusRow;
        private CanvasGroup enemyStatusFade;
        private CanvasGroup playerBuffsFade;
        private Image enemyHitFlash;

        // 玩家
        private Image playerPortrait;
        private Image playerHpFill;
        private TextMeshProUGUI playerHpText;
        private RectTransform playerBuffs;

        // 中间
        private RectTransform manaRow;
        private TextMeshProUGUI turnLabel;
        private TextMeshProUGUI drawPileCount;
        private TextMeshProUGUI discardPileCount;
        private TextMeshProUGUI banner;
        private float bannerUntil;

        // 顶部赛程牌
        private RectTransform stageChip;
        private Image stageChipFill;
        private TextMeshProUGUI stageActLabel;
        private TextMeshProUGUI stageLabel;
        private TextMeshProUGUI stageCountLabel;
        private readonly List<Image> stagePips = new List<Image>();

        private RectTransform handRoot;
        private Button endTurnButton;
        private Image endTurnImage;
        private TextMeshProUGUI endTurnLabel;
        private TextMeshProUGUI emptyHint;

        // 拖动
        private RectTransform dragLayer;
        private RectTransform dropZone;
        private Image dropZoneFill;
        private Image dropZoneBorderImage;
        private TextMeshProUGUI dropZoneCaption;
        private TextMeshProUGUI dropZoneLabel;
        private RectTransform targetRing;
        private Image targetRingImage;
        private TextMeshProUGUI targetHint;
        private CardView dragView;
        private int dragIndex = -1;
        private Vector2 dragGrab;          // 手指与卡片锚点的偏移，保证"抓哪儿从哪儿跟手"
        private bool dropHot;
        private bool dropBlocked;
        private float zoneShownAt;

        private int hoverIndex = -1;
        private bool busy;
        private float startDelay = 0.35f;
        private float settleTimer;
        private bool settlePending;

        private struct Floating
        {
            public TextMeshProUGUI label;
            public float bornAt;
            public float drift;
        }

        public BattleScreen()
        {
            var app = GameApp.I;
            session = app.Director.CreateBattle(6001 + app.Director.Battle * 131 + app.RunSelfDamage);
        }

        protected override void Build()
        {
            BuildArena();
            BuildTable();
            BuildEnemy();
            BuildTopBar();
            BuildPlayer();
            BuildFooter();

            // 拖拽层必须最后建：它得盖在所有界面元素之上，被拖起来的牌才不会被压住
            BuildDragLayer();

            RefreshAll();

            // 每次开战先弹一次玩法提示，别让人对着手牌点半天
            GameApp.I.Toast("拖动卡牌到中间的出牌区，松手即可打出");
        }

        #region 背景与战场

        private void BuildArena()
        {
            // 双方各一团身后光晕：敌方系列红、玩家翠绿，一左一右界定舞台
            var enemyGlow = Ui.Panel("EnemyGlow", Root, SpriteForge.Glow(512, Theme.WithAlpha(Theme.SeriesBlood, 0.5f)), Color.white);
            Ui.Fixed(enemyGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, PortraitY), new Vector2(900f, 640f));

            var playerGlow = Ui.Panel("PlayerGlow", Root, SpriteForge.Glow(512, Theme.WithAlpha(Theme.Accent, 0.32f)), Color.white);
            Ui.Fixed(playerGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, PortraitY), new Vector2(760f, 560f));
        }

        #endregion

        #region 敌方（右）

        private void BuildEnemy()
        {
            var info = session.EnemyInfo;
            Color accent = Theme.SeriesColor(info.Accent);

            enemyHitFlash = Ui.Panel("EnemyFlash", Root, SpriteForge.Glow(384, Theme.WithAlpha(Theme.Danger, 0.7f)), Theme.WithAlpha(Color.white, 0f));
            Ui.Fixed(enemyHitFlash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, PortraitY), new Vector2(400f, 400f));

            var halo = Ui.Panel("EnemyHalo", Root, SpriteForge.Glow(384, Theme.WithAlpha(accent, 0.5f)), Color.white);
            Ui.Fixed(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, PortraitY - 4f), new Vector2(330f, 330f));

            enemyPortrait = Ui.Panel("EnemyPortrait", Root, null, Color.white);
            Sprite art = ArtLib.Enemy;
            // 素材怒眼朝左，摆在右侧正好怒视玩家
            enemyPortrait.sprite = art != null ? art : SpriteForge.Slime(320, accent, Theme.BgDeep, info.Seed, true, SlimeFace.Fierce);
            enemyPortrait.preserveAspect = true;
            // 每位宗师染上自己的系列色，八关的形象才拉得开
            enemyPortrait.color = art != null ? Color.Lerp(Color.white, accent, 0.35f) : Color.white;
            Ui.Fixed(enemyPortrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, PortraitY), new Vector2(PortraitSize, PortraitSize));

            enemySubLabel = Ui.Label("EnemySub", Root, info.Title, Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(enemySubLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, EnemySubY), new Vector2(460f, 24f));

            enemyNameLabel = Ui.Label("EnemyName", Root, info.Name, Theme.H2, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(enemyNameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, NameY), new Vector2(460f, 40f));

            enemyHpBack = Ui.Rounded("EnemyHpBar", Root, 12, Theme.PanelSunken, Theme.WithAlpha(Theme.Danger, 0.55f), 2);
            Ui.Fixed(enemyHpBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, HpY), new Vector2(HpBarW, 30f));

            enemyHpFill = Ui.Panel("EnemyHpFill", enemyHpBack.transform, SpriteForge.Bar(64, 28, 10, Theme.Hp), Color.white);
            enemyHpFill.type = Image.Type.Sliced;
            Ui.Fixed(enemyHpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(HpBarW - 8f, 22f));

            enemyHpText = Ui.Label("EnemyHpText", enemyHpBack.transform, string.Empty, Theme.Body, Theme.Text, TextAlignmentOptions.Center);
            Ui.Stretch(enemyHpText.rectTransform, 0f, 0f, 0f, 1f);

            enemyStatusRow = Ui.Node("EnemyStatus", Root);
            enemyStatusFade = enemyStatusRow.gameObject.AddComponent<CanvasGroup>();
            Ui.Fixed(enemyStatusRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, StatusY), new Vector2(HpBarW + 80f, 34f));
        }

        private void RefreshEnemy()
        {
            var e = session.Enemy;
            float pct = e.HealthPercent;

            var fillRt = enemyHpFill.rectTransform;
            Ui.Fixed(fillRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f),
                new Vector2(Mathf.Max(0f, (HpBarW - 8f) * pct), 22f));

            // 血量越低越红，越高越接近正常的血条色
            Color hpColor = Color.Lerp(Theme.Danger, Theme.Hp, pct);
            enemyHpFill.sprite = SpriteForge.Bar(64, 28, 10, hpColor);
            enemyHpFill.type = Image.Type.Sliced;
            enemyHpFill.color = Color.white;

            enemyHpText.text = e.Hp + " / " + e.HpMax;

            RebuildStatus(enemyStatusRow, e, true);
            // 称号 + 对手手牌/牌库并成一行，不再单独立一块角标
            enemySubLabel.text = session.EnemyInfo.Title + "  ·  手牌 " + e.Hand.Count + "  ·  牌库 " + e.Deck.Count;
        }

        #endregion

        #region 顶部：赛程牌 + 回合胶囊 + 牌堆

        private void BuildTopBar()
        {
            // 左上：赛程牌——这一场是三幕九关廿七战里的哪一战，一眼要能看见
            stageChipFill = Ui.Rounded("StageChip", Root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.92f), Theme.Border, 1);
            stageChip = stageChipFill.rectTransform;
            Ui.Fixed(stageChip, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(TopInset, -TopInset), new Vector2(430f, 54f));

            stageActLabel = Ui.Label("StageAct", stageChip, string.Empty, Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(stageActLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 11f), new Vector2(220f, 22f));

            stageLabel = Ui.Label("StageName", stageChip, string.Empty, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(stageLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, -12f), new Vector2(240f, 26f));

            // 右侧三个小点：先锋 / 中坚 / 宗师，走过的点亮
            for (int i = 0; i < EnemyRoster.StagesPerLevel; i++)
            {
                var pip = Ui.Panel("StagePip" + i, stageChip,
                    SpriteForge.Circle(26, Theme.PanelSunken, Theme.WithAlpha(Theme.Border, 0.8f), 1), Color.white);
                Ui.Fixed(pip.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-30f - (EnemyRoster.StagesPerLevel - 1 - i) * 30f, 0f), new Vector2(16f, 16f));
                stagePips.Add(pip);
            }

            stageCountLabel = Ui.Label("StageCount", stageChip, string.Empty, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Right);
            Ui.Fixed(stageCountLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -8f), new Vector2(130f, 22f));

            // 中：回合胶囊
            var capsule = Ui.Rounded("TurnCapsule", Root, Theme.RadiusM,
                Theme.WithAlpha(Theme.Panel, 0.9f), Theme.Border, 1);
            Ui.Fixed(capsule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -TopInset), new Vector2(420f, 54f));

            turnLabel = Ui.Label("Turn", capsule.transform, string.Empty, Theme.H4, Theme.Text, TextAlignmentOptions.Center);
            Ui.Stretch(turnLabel.rectTransform, 12f, 0f, 12f, 0f);

            // 战报：平时隐形，有事才在胶囊下方淡出，不留常驻文字块
            banner = Ui.Label("Banner", Root, string.Empty, Theme.Body,
                Theme.WithAlpha(Theme.TextDim, 0f), TextAlignmentOptions.Center);
            Ui.Fixed(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -106f), new Vector2(760f, 32f));

            // 右上：认输
            var quit = Ui.TextButton("Quit", Root, "认输", Theme.Caption,
                Theme.WithAlpha(Theme.PanelRaised, 0.9f), Theme.TextFaint, OnQuitClicked);
            Ui.Fixed(quit.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-TopInset, -TopInset), new Vector2(150f, 54f));

            drawPileCount = BuildPile(DrawPileX, PileY, "抽牌");
            discardPileCount = BuildPile(DiscardPileX, PileY, "弃牌");
        }

        private void RefreshStageChip()
        {
            int battle = session.Battle;
            int stage = session.Stage;
            Color accent = Theme.SeriesColor(session.EnemyInfo.Accent);

            stageActLabel.text = EnemyRoster.ActNames[EnemyRoster.ActOf(battle) - 1];
            stageLabel.text = "第 " + session.Level + " 关 · " + EnemyRoster.StageNames[stage - 1];
            stageCountLabel.text = battle + " / " + EnemyRoster.FinalBattle;

            Color border = EnemyRoster.IsMasterStage(battle) ? Theme.Danger : Theme.WithAlpha(accent, 0.55f);
            stageChipFill.sprite = SpriteForge.Panel(Theme.RadiusM, Theme.WithAlpha(Theme.Panel, 0.92f), border, 1);
            stageChipFill.type = Image.Type.Sliced;
            stageChipFill.color = Color.white;

            stageLabel.color = EnemyRoster.IsMasterStage(battle) ? Theme.Danger : Theme.Text;

            for (int i = 0; i < stagePips.Count; i++)
            {
                bool done = i + 1 < stage;
                bool current = i + 1 == stage;
                Color c = done ? Theme.Gold : (current ? accent : Theme.TextFaint);
                stagePips[i].sprite = SpriteForge.Circle(26,
                    Theme.WithAlpha(c, current ? 0.95f : 0.55f),
                    Theme.WithAlpha(Theme.Lighten(c, 0.3f), 0.85f), 1);
                stagePips[i].color = Color.white;
                stagePips[i].rectTransform.sizeDelta = new Vector2(current ? 20f : 14f, current ? 20f : 14f);
            }
        }

        /// <summary>底部牌桌台面：把魔力、手牌、牌堆、结束回合统一到一整块区域里。</summary>
        private void BuildTable()
        {
            var table = Ui.Panel("Table", Root, SpriteForge.Scrim(32, 256, Theme.BgDeep, 0f, 0.85f), Color.white);
            Ui.Fixed(table.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, -70f), new Vector2(1920f, 420f));

            // 台面上沿的一条细高光，明确"这里是桌面"
            var edge = Ui.Panel("TableEdge", Root, SpriteForge.Panel(2, Theme.WithAlpha(Theme.Border, 0.85f), Color.clear, 0), Color.white);
            Ui.Fixed(edge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 262f), new Vector2(1760f, 2f));
        }

        private TextMeshProUGUI BuildPile(float x, float y, string caption)
        {
            // 牌堆 = 一小摞牌背 + 数量 + 名称，分踞左下 / 右下角
            var pile = Ui.Rounded("Pile" + caption, Root, 12,
                Theme.WithAlpha(Theme.PanelSunken, 0.92f), Theme.Border, 2);
            Ui.Fixed(pile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, y), new Vector2(104f, 118f));

            var back = Ui.Panel("Back", pile.transform, null, Theme.WithAlpha(Theme.PanelRaised, 0.5f));
            Ui.Fixed(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(76f, 38f));

            var count = Ui.Label("Count", pile.transform, "0", Theme.H3, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(90f, 40f));

            var cap = Ui.Label("Cap", pile.transform, caption, Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Center);
            Ui.Fixed(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(90f, 24f));

            return count;
        }

        #endregion

        #region 玩家

        private void BuildPlayer()
        {
            // 玩家立于左中场，面向右上方的敌人——不再缩在角落的小面板里
            var halo = Ui.Panel("PlayerHalo", Root, SpriteForge.Glow(256, Theme.WithAlpha(Theme.Accent, 0.5f)), Color.white);
            Ui.Fixed(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, PortraitY - 4f), new Vector2(330f, 330f));

            playerPortrait = Ui.Panel("PlayerPortrait", Root, null, Color.white);
            Sprite heroArt = ArtLib.Hero;
            // 素材持剑朝左，镜像后剑尖指向右侧的敌人
            playerPortrait.sprite = heroArt != null
                ? heroArt
                : SpriteForge.Slime(220, Theme.Accent, Theme.BgDeep, 4242, false, SlimeFace.Neutral);
            playerPortrait.preserveAspect = true;
            playerPortrait.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Ui.Fixed(playerPortrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, PortraitY), new Vector2(PortraitSize, PortraitSize));

            // 脚下的影子，把立绘"放"在地上
            var shadow = Ui.Panel("PlayerShadow", Root, SpriteForge.Glow(128, Theme.WithAlpha(Color.black, 0.5f)), Color.white);
            Ui.Fixed(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, PortraitY - PortraitSize * 0.5f - 16f), new Vector2(250f, 54f));

            // 与敌人完全同构的信息列：名字 / 血条 / 护盾与增益（chips 流式）
            var nameLabel = Ui.Label("PlayerName", Root, "你", Theme.H2, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, NameY), new Vector2(460f, 40f));

            var hpBack = Ui.Rounded("HpBar", Root, 10, Theme.PanelSunken, Theme.WithAlpha(Theme.Danger, 0.5f), 2);
            Ui.Fixed(hpBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-StageX, HpY), new Vector2(HpBarW, 30f));

            playerHpFill = Ui.Panel("HpFill", hpBack.transform, SpriteForge.Bar(64, 22, 8, Theme.Hp), Color.white);
            playerHpFill.type = Image.Type.Sliced;
            Ui.Fixed(playerHpFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(HpBarW - 8f, 22f));

            playerHpText = Ui.Label("HpText", hpBack.transform, string.Empty, Theme.Caption, Theme.Text, TextAlignmentOptions.Center);
            Ui.Stretch(playerHpText.rectTransform, 0f, 0f, 0f, 1f);

            // 增益：与血条左缘对齐，流式排布，护盾作为第一枚 chip 混排。
            // 拖拽出牌时会临时淡出——这块地方正好和出牌区重叠，不淡出会糊成一片。
            playerBuffs = Ui.Node("Buffs", Root);
            playerBuffsFade = playerBuffs.gameObject.AddComponent<CanvasGroup>();
            Ui.Fixed(playerBuffs, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                new Vector2(-StageX - HpBarW * 0.5f, StatusY), new Vector2(HpBarW, 80f));

            // 魔力宝石：左下角，一颗大宝石 + 数字
            manaRow = Ui.Node("ManaRow", Root);
            Ui.Fixed(manaRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(ManaX, ManaY), new Vector2(240f, 120f));
        }

        private void RefreshPlayer()
        {
            var p = session.Player;

            var fillRt = playerHpFill.rectTransform;
            Ui.Fixed(fillRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f),
                new Vector2(Mathf.Max(0f, (HpBarW - 8f) * p.HealthPercent), 22f));

            Color hpColor = Color.Lerp(Theme.Danger, Theme.Hp, p.HealthPercent);
            playerHpFill.sprite = SpriteForge.Bar(64, 22, 8, hpColor);
            playerHpFill.type = Image.Type.Sliced;
            playerHpFill.color = Color.white;

            playerHpText.text = p.Hp + " / " + p.HpMax;

            RebuildMana();
            RebuildBuffs(playerBuffs, p);
        }

        private void RebuildMana()
        {
            for (int i = manaRow.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(manaRow.GetChild(i).gameObject);
            }

            var p = session.Player;

            // 一颗大宝石替代整排珠子：当前魔力写在宝石里，上限跟在右侧
            var gem = Ui.Panel("ManaGem", manaRow,
                SpriteForge.Circle(72, Theme.Mana, Theme.Lighten(Theme.Mana, 0.45f), 4), Color.white);
            Ui.Fixed(gem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-42f, 0f), new Vector2(84f, 84f));

            var manaText = Ui.Label("ManaText", gem.transform, p.Mana.ToString(), Theme.H3,
                Theme.BgDeep, TextAlignmentOptions.Center);
            Ui.Stretch(manaText.rectTransform, 0f, 0f, 0f, 0f);

            var value = Ui.Label("ManaValue", manaRow, "/ " + p.ManaRegen, Theme.H3, Theme.Text,
                TextAlignmentOptions.Left);
            Ui.Fixed(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, 8f), new Vector2(120f, 40f));

            var cap = Ui.Label("ManaCap", manaRow, "魔力", Theme.Caption, Theme.TextFaint, TextAlignmentOptions.Left);
            Ui.Fixed(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(14f, -26f), new Vector2(120f, 24f));
        }

        #endregion

        #region 底部：手牌与结束回合

        private void BuildFooter()
        {
            handRoot = Ui.Node("Hand", Root);

            // 结束回合：贴右缘、垂直居中偏下，是整屏第二醒目的按钮
            endTurnImage = Ui.Rounded("EndTurn", Root, Theme.RadiusM, Theme.AccentDeep, Theme.Accent, 3, true);
            Ui.Fixed(endTurnImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(EndTurnX, EndTurnY), new Vector2(200f, 74f));

            endTurnButton = endTurnImage.gameObject.AddComponent<Button>();
            endTurnButton.targetGraphic = endTurnImage;
            endTurnButton.onClick.AddListener(OnEndTurnClicked);

            endTurnLabel = Ui.Label("EndTurnLabel", endTurnImage.transform, "结束回合", Theme.H3, Theme.BgDeep, TextAlignmentOptions.Center);
            Ui.Stretch(endTurnLabel.rectTransform, 0f, 0f, 0f, 0f);

            // 手牌出空时，提示语正好落在出牌区的位置上——那块地方空着总得说明它为什么空
            emptyHint = Ui.Label("EmptyHand", Root, "手牌已空 · 点右侧「结束回合」继续", Theme.H3,
                Theme.WithAlpha(Theme.TextFaint, 0.92f), TextAlignmentOptions.Center);
            Ui.Fixed(emptyHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(HandCenterX, ZoneY), new Vector2(760f, 40f));
            emptyHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// 拖拽层 = 出牌释放区 + 目标指示环 + 被拖起来的牌。
        /// 它在最上层，平时完全不可见/不挡射线，一旦开始拖动才浮出来。
        /// </summary>
        private void BuildDragLayer()
        {
            dragLayer = Ui.Node("DragLayer", Root);

            // ---- 出牌释放区 ----
            dropZone = Ui.Node("DropZone", dragLayer);
            Ui.Fixed(dropZone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, ZoneY), new Vector2(ZoneW, ZoneH));

            dropZoneFill = Ui.Rounded("ZoneFill", dropZone, 26,
                Theme.WithAlpha(Theme.AccentDeep, 0.18f), Theme.WithAlpha(Theme.Accent, 0.55f), 3);
            Ui.Stretch(dropZoneFill.rectTransform, 0f, 0f, 0f, 0f);

            // 内圈虚线感的细边框，强化"这是一个投放口"
            dropZoneBorderImage = Ui.Rounded("ZoneInner", dropZone, 20,
                Theme.WithAlpha(Theme.PanelSunken, 0.10f), Theme.WithAlpha(Theme.Accent, 0.30f), 1);
            Ui.Stretch(dropZoneBorderImage.rectTransform, 12f, 12f, 12f, 12f);

            dropZoneCaption = Ui.Label("ZoneCaption", dropZone, string.Empty, Theme.Caption,
                Theme.WithAlpha(Theme.TextFaint, 0.9f), TextAlignmentOptions.Center);
            Ui.Fixed(dropZoneCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(ZoneW - 80f, 26f));

            dropZoneLabel = Ui.Label("ZoneLabel", dropZone, "拖 到 此 处 打 出", Theme.H3,
                Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(dropZoneLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -16f), new Vector2(ZoneW - 80f, 40f));

            dropZone.gameObject.SetActive(false);

            // ---- 目标指示环：拖起来时告诉玩家这张牌会打在谁身上 ----
            targetRing = Ui.Node("TargetRing", dragLayer);
            Ui.Fixed(targetRing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(StageX, PortraitY), new Vector2(PortraitSize + 76f, PortraitSize + 76f));

            targetRingImage = Ui.Panel("RingArt", targetRing, null, Color.white);
            Ui.Stretch(targetRingImage.rectTransform, 0f, 0f, 0f, 0f);

            targetHint = Ui.Label("TargetHint", targetRing, string.Empty, Theme.Caption,
                Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(targetHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(300f, 28f));

            targetRing.gameObject.SetActive(false);
        }

        #endregion

        #region 出牌区反馈

        /// <summary>拖起来的瞬间：出牌区淡入 + 目标环标出落点。</summary>
        private void ShowDropZone(CardInstance card)
        {
            bool enemyTarget = !IsSelfTargeted(card);
            Color accent = enemyTarget ? Theme.Danger : Theme.Accent;

            dropZone.gameObject.SetActive(true);
            zoneShownAt = Time.unscaledTime;

            var fill = Theme.WithAlpha(Theme.AccentDeep, 0.18f);
            var border = Theme.WithAlpha(Theme.Accent, 0.55f);
            dropZoneFill.sprite = SpriteForge.Panel(26, fill, border, 3);
            dropZoneFill.type = Image.Type.Sliced;
            dropZoneFill.color = Color.white;

            dropZoneBorderImage.sprite = SpriteForge.Panel(20,
                Theme.WithAlpha(Theme.PanelSunken, 0.10f), Theme.WithAlpha(Theme.Accent, 0.30f), 1);
            dropZoneBorderImage.type = Image.Type.Sliced;
            dropZoneBorderImage.color = Color.white;

            dropZoneLabel.text = "拖 到 此 处 打 出";
            dropZoneLabel.color = Theme.Text;
            dropZoneCaption.text = enemyTarget ? "命中 " + session.EnemyInfo.Name : "只对自己生效";
            dropZoneCaption.color = Theme.WithAlpha(Theme.TextFaint, 0.9f);

            // 入场：从 0.94 倍弹到 1 倍，配合整体缩放做一个"展开"感
            dropZone.localScale = Vector3.one * 0.94f;
            Tween.Scale(dropZone, 0.94f, 1f, 0.2f, Easing.OutBack);
            Tween.FadeIn(dropZoneFill, 0.18f, 0f, 0f, 1f);
            Tween.FadeIn(dropZoneLabel, 0.18f, 0f, 0f, 1f);
            Tween.FadeIn(dropZoneCaption, 0.18f, 0.04f, 0f, 0.9f);

            // 目标环
            Ui.Fixed(targetRing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(enemyTarget ? StageX : -StageX, PortraitY),
                new Vector2(PortraitSize + 76f, PortraitSize + 76f));
            targetRingImage.sprite = SpriteForge.Circle(256, Color.clear, Theme.WithAlpha(accent, 0.85f), 6);
            targetRingImage.color = Color.white;
            targetHint.text = enemyTarget ? "目标" : "自己";
            targetHint.color = Theme.Lighten(accent, 0.35f);
            targetRing.gameObject.SetActive(true);
            targetRing.localScale = Vector3.one * 0.7f;
            Tween.Scale(targetRing, 0.7f, 1f, 0.24f, Easing.OutBack);
        }

        /// <summary>拖动过程中刷新"能不能在这里松手"的反馈。</summary>
        private void UpdateDropFeedback(CardView view, bool inside)
        {
            var card = view.Instance;
            bool affordable = card.Cost <= session.Player.Mana;
            bool blocked = inside && !affordable;

            if (inside == dropHot && blocked == dropBlocked)
            {
                return;
            }

            dropHot = inside;
            dropBlocked = blocked;
            view.SetHot(inside && affordable);

            // 呼吸感：进入热区时脉冲一次，别一直闪
            if (inside && affordable)
            {
                Tween.Punch(dropZone, 0.030f, 0.22f);
            }

            Color accent = blocked ? Theme.Danger : Theme.Accent;
            Color fill = blocked
                ? Theme.WithAlpha(Theme.Danger, 0.22f)
                : (inside ? Theme.WithAlpha(Theme.AccentDeep, 0.36f) : Theme.WithAlpha(Theme.AccentDeep, 0.18f));
            Color border = blocked
                ? Theme.WithAlpha(Theme.Danger, 0.95f)
                : (inside ? Theme.Gold : Theme.WithAlpha(Theme.Accent, 0.55f));

            dropZoneFill.sprite = SpriteForge.Panel(26, fill, border, inside || blocked ? 4 : 3);
            dropZoneFill.type = Image.Type.Sliced;
            dropZoneFill.color = Color.white;

            if (blocked)
            {
                dropZoneLabel.text = "魔 力 不 足";
                dropZoneLabel.color = Theme.Danger;
                dropZoneCaption.text = "需要 " + card.Cost + " 点，现有 " + session.Player.Mana + " 点 · 松手退回";
                dropZoneCaption.color = Theme.WithAlpha(Theme.Danger, 0.85f);

                var flash = Theme.WithAlpha(Theme.Danger, 0.9f);
                targetRingImage.sprite = SpriteForge.Circle(256, Color.clear, flash, 6);
            }
            else if (inside)
            {
                bool enemyTarget = !IsSelfTargeted(card);
                dropZoneLabel.text = "松 手 · 对 " + (enemyTarget ? session.EnemyInfo.Name : "自己") + " 使用";
                dropZoneLabel.color = Theme.Gold;
                dropZoneCaption.text = "《" + card.Name + "》  ·  消耗 " + card.Cost + " 点魔力";
                dropZoneCaption.color = Theme.WithAlpha(Theme.TextDim, 0.95f);
            }
            else
            {
                dropZoneLabel.text = "拖 到 此 处 打 出";
                dropZoneLabel.color = Theme.Text;
                dropZoneCaption.text = !IsSelfTargeted(card) ? "命中 " + session.EnemyInfo.Name : "只对自己生效";
                dropZoneCaption.color = Theme.WithAlpha(Theme.TextFaint, 0.9f);
            }
        }

        private void HideDropZone()
        {
            dropHot = false;
            dropBlocked = false;

            if (dropZone != null && dropZone.gameObject.activeSelf)
            {
                Tween.Scale(dropZone, dropZone.localScale.x, 0.94f, 0.16f, Easing.OutQuad,
                    () => { if (dropZone != null) { dropZone.gameObject.SetActive(false); } });
            }

            if (targetRing != null && targetRing.gameObject.activeSelf)
            {
                Tween.Scale(targetRing, targetRing.localScale.x, 0.7f, 0.16f, Easing.OutQuad,
                    () => { if (targetRing != null) { targetRing.gameObject.SetActive(false); } });
            }
        }

        /// <summary>把屏幕坐标换算成拖拽层的 anchoredPosition（锚点同为底边中心）。</summary>
        private Vector2 PointerToAnchored(PointerEventData data)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, data.position, data.pressEventCamera, out local))
            {
                return Vector2.zero;
            }

            Rect rect = dragLayer.rect;
            float refX = Mathf.Lerp(rect.xMin, rect.xMax, 0.5f);
            float refY = Mathf.Lerp(rect.yMin, rect.yMax, 0f);
            return local - new Vector2(refX, refY);
        }

        /// <summary>指针是否落在出牌释放区内（判定放宽 ZoneSlack）。</summary>
        private bool PointerInsideZone(PointerEventData data)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, data.position, data.pressEventCamera, out local))
            {
                return false;
            }

            float halfW = ZoneW * 0.5f + ZoneSlack;
            float halfH = ZoneH * 0.5f + ZoneSlack;
            float cy = ZoneY;   // dragLayer 中心即屏幕中心，出牌区在中心下方 ZoneY

            // local 是以父矩形中心为原点的坐标（pivot 0.5/0.5）
            return Mathf.Abs(local.x) <= halfW && Mathf.Abs(local.y - cy) <= halfH;
        }

        #endregion

        #region 拖动接口实现

        public bool CanDragCard(CardView view)
        {
            return !busy
                   && dragView == null
                   && session != null
                   && session.Phase == BattlePhase.PlayerTurn
                   && !session.Finished;
        }

        public void OnCardDragBegin(CardView view, PointerEventData data)
        {
            int index = handViews.IndexOf(view);
            if (index < 0)
            {
                return;
            }

            dragView = view;
            dragIndex = index;
            hoverIndex = -1;

            // 打断这张牌可能还在跑的飞入动画：tween 闭包每帧写 anchoredPosition，
            // 不杀掉的话会把拖拽中的牌拽回手牌位（表现成"卡脱手"）。
            Tween.Kill(view.Root);

            // 抓住哪就从哪跟手：先记下手指相对卡片锚点的偏移（重排前读，否则读到的是合拢后的位）
            Vector2 pointer = PointerToAnchored(data);
            Vector2 cardPos = view.Root.anchoredPosition;
            dragGrab = pointer - cardPos;

            // 把这张牌从手牌数组里摘出去：手牌顺势合拢空位，
            // 出牌成功时手牌与 session.Hand 正好同步各少一张，顺序仍然一一对应。
            handViews.RemoveAt(index);
            LayoutHand(true);

            // 换到最上层（两块节点的尺寸与锚点完全一致，重父之后位置不跳）
            view.BeginDragVisual(dragLayer, cardPos);

            SetStatusFade(0.12f);
            ShowDropZone(view.Instance);
            GameApp.I.Audio.PlaySfx("ui");
        }

        /// <summary>拖拽期间把两侧 buff 状态行压暗——出牌区正好压在它们头上，不淡出会糊成一片。</summary>
        private void SetStatusFade(float alpha)
        {
            if (playerBuffsFade != null)
            {
                playerBuffsFade.alpha = alpha;
            }

            if (enemyStatusFade != null)
            {
                enemyStatusFade.alpha = alpha;
            }
        }

        public void OnCardDragMove(CardView view, PointerEventData data)
        {
            if (dragView != view)
            {
                return;
            }

            Vector2 anchored = PointerToAnchored(data);
            view.DragTo(new Vector2(anchored.x - dragGrab.x, anchored.y - dragGrab.y));
            UpdateDropFeedback(view, PointerInsideZone(data));
        }

        public void OnCardDragEnd(CardView view, PointerEventData data)
        {
            if (dragView != view)
            {
                return;
            }

            Debug.Log("[DragProbe] OnCardDragEnd fired: " + view.Instance.Name);
            bool inside = PointerInsideZone(data);
            bool affordable = view.Instance.Cost <= session.Player.Mana;

            HideDropZone();
            SetStatusFade(1f);

            if (inside && affordable)
            {
                PlayFromDrag(view);
                return;
            }

            // 取消：把牌插回原来的位置再回弹，手牌顺序不会乱
            if (inside && !affordable)
            {
                GameApp.I.Toast("魔力不足：" + view.Instance.Name + " 需要 " + view.Instance.Cost + " 点");
                GameApp.I.Audio.PlaySfx("venom");
            }

            var released = view;
            int backIndex = dragIndex;
            dragView = null;
            dragIndex = -1;

            released.ReturnHome(() =>
            {
                if (backIndex >= 0 && backIndex <= handViews.Count)
                {
                    handViews.Insert(backIndex, released);
                }
                else
                {
                    handViews.Add(released);
                }

                RefreshHand();
            });
        }

        /// <summary>
        /// 编辑器调试入口：把某张手牌直接摆成"正在被拖动"的样子，用于截图验收拖放交互与释放反馈。
        /// mode = over（出牌区内·可打出）/ blocked（出牌区内·魔力不足）/ out（出牌区外）。
        /// </summary>
        public string DebugDrag(string mode)
        {
            string m = (mode ?? string.Empty).Trim().ToLowerInvariant();

            if (m == "status")
            {
                if (dragView == null)
                {
                    return "no drag; hand=" + handViews.Count + " sessionHand=" + session.Player.Hand.Count;
                }

                var rt2 = dragView.Root;
                var corners = new Vector3[4];
                rt2.GetWorldCorners(corners);
                return "drag=" + dragView.Instance.Name
                     + " anchored=" + rt2.anchoredPosition
                     + " parent=" + rt2.parent.name
                     + " worldBL=" + corners[0] + " worldTR=" + corners[2]
                     + " scale=" + rt2.localScale
                     + " hand=" + handViews.Count + " sessionHand=" + session.Player.Hand.Count;
            }

            if (m == "verbose")
            {
                if (dragView == null)
                {
                    return "no drag";
                }

                var rt2 = dragView.Root;
                var p = rt2.parent as RectTransform;
                return "anchored=" + rt2.anchoredPosition
                     + " aMin=" + rt2.anchorMin + " aMax=" + rt2.anchorMax
                     + " pivot=" + rt2.pivot
                     + " offMin=" + rt2.offsetMin + " offMax=" + rt2.offsetMax
                     + " localPos=" + rt2.localPosition
                     + " parentRect=" + (p != null ? p.rect.ToString() : "null")
                     + " parentScale=" + (p != null ? p.lossyScale.ToString() : "null");
            }

            if (dragView != null)
            {
                return "already dragging";
            }

            int index = 0;

            if (m == "blocked")
            {
                // 找一张非零费牌，再把魔力清空，逼出"红色不可用"的释放反馈
                index = -1;
                for (int i = 0; i < handViews.Count; i++)
                {
                    if (handViews[i].Instance.Cost > 0)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    return "no non-zero-cost card in hand";
                }

                session.DebugSetMana(0);
            }
            else
            {
                session.DebugSetMana(30);
            }

            if (index < 0 || index >= handViews.Count)
            {
                return "index out of range: " + index + " / " + handViews.Count;
            }

            var view = handViews[index];
            // 打断这张牌可能还在跑的飞入/落位动画——否则后续帧的 tween 会把拖拽写入的位置覆盖回去
            Tween.Kill(view.Root);
            OnCardDragBegin(view, new PointerEventData(EventSystem.current));
            if (dragView == null)
            {
                return "begin refused";
            }

            bool inside = m != "out";
            // 卡锚是"卡底、参照父底边"：anchored.y = 中心坐标目标 + 半屏高。
            // 悬停在出牌区上沿之上一点，卡体罩住出牌区又不挡住文案。
            float halfH = dragLayer != null ? dragLayer.rect.height * 0.5f : 540f;
            float hoverY = ZoneY + ZoneH * 0.5f + 14f;
            Vector2 target = inside
                ? new Vector2(0f, hoverY + halfH)
                : new Vector2(430f, 240f + halfH);

            dragView.DragTo(target);
            UpdateDropFeedback(dragView, inside);

            if (m == "play")
            {
                // 完整走一遍"松手生效"：结算 + 卡片飞向目标 + 手牌刷新
                var rt3 = dragView.Root;
                string name3 = dragView.Instance.Name;
                PlayFromDrag(dragView);
                return "played " + name3 + " worldY=" + rt3.position.y;
            }

            var rt = dragView.Root;
            return "dragging " + view.Instance.Name + " mode=" + m
                 + " anchored=" + rt.anchoredPosition
                 + " parent=" + rt.parent.name
                 + " sizeDr=" + rt.sizeDelta
                 + " worldY=" + rt.position.y;
        }

        public void OnCardHover(CardView view, bool entered)
        {
            int index = handViews.IndexOf(view);
            if (index < 0)
            {
                return;
            }

            int next = entered ? index : (hoverIndex == index ? -1 : hoverIndex);
            if (next == hoverIndex)
            {
                return;
            }

            hoverIndex = next;
            LayoutHand();
        }

        public void OnCardTapped(CardView view)
        {
            // 轻点不出牌——但把出牌区抖一下，明确告诉玩家"要拖过去"
            if (!CanDragCard(view))
            {
                return;
            }

            GameApp.I.Audio.PlaySfx("ui");

            dropZone.gameObject.SetActive(true);
            dropZone.localScale = Vector3.one * 0.94f;
            Tween.Scale(dropZone, 0.94f, 1f, 0.2f, Easing.OutBack);

            dropZoneFill.sprite = SpriteForge.Panel(26,
                Theme.WithAlpha(Theme.Gold, 0.22f), Theme.WithAlpha(Theme.Gold, 0.9f), 3);
            dropZoneFill.type = Image.Type.Sliced;
            dropZoneFill.color = Color.white;

            dropZoneLabel.text = "把 这 张 牌 拖 到 这 里";
            dropZoneLabel.color = Theme.Gold;
            dropZoneCaption.text = "松手即打出，拖回去则取消";
            dropZoneCaption.color = Theme.WithAlpha(Theme.TextFaint, 0.9f);

            zoneShownAt = Time.unscaledTime;
            GameApp.I.Toast("按住卡牌拖到出牌区，松手打出");
        }

        #endregion

        #region 出牌

        /// <summary>从拖拽释放区成功出牌：先结算，再让卡片飞向目标。</summary>
        private void PlayFromDrag(CardView view)
        {
            var card = view.Instance;
            bool attack = !IsSelfTargeted(card);
            int index = dragIndex;

            dragView = null;
            dragIndex = -1;
            SetStatusFade(1f);

            if (index < 0 || !session.PlayCard(index))
            {
                // 结算失败（极端情况）：原样弹回去，别让卡片凭空消失
                handViews.Insert(Mathf.Clamp(index, 0, handViews.Count), view);
                view.ReturnHome(RefreshHand);
                return;
            }

            GameApp.I.Audio.PlaySfx(attack ? "slash" : "play");

            // 飞出目标：攻击牌飞向敌人立绘，自用牌飞回玩家立绘。
            // 卡锚点是"卡底、参照父底边"：anchored.y = 目标中心坐标 + 半屏高。
            // PortraitY=150（中心坐标）→ 卡底落在立绘中心上，视觉正好。
            float halfH = dragLayer != null ? dragLayer.rect.height * 0.5f : 540f;
            Vector2 target = attack
                ? new Vector2(StageX, PortraitY + halfH)
                : new Vector2(-StageX, PortraitY + halfH);

            Tween.Punch(endTurnImage.rectTransform, 0.06f, 0.2f);

            view.FlyOut(target, () =>
            {
                view.Destroy();
                RefreshAll();
            });

            DrainEvents();
            UpdateLog("你打出《" + card.Name + "》");

            if (session.Finished)
            {
                GameApp.I.Run(FinishAfterDelay(0.6f, session.Victory));
                return;
            }

            if (session.EndTurnRequested)
            {
                settlePending = true;
                settleTimer = 0.55f;
                GameApp.I.Toast("回合被强制结束");
            }
        }

        private void OnEndTurnClicked()
        {
            if (busy || session.Phase != BattlePhase.PlayerTurn || session.Finished)
            {
                return;
            }

            hoverIndex = -1;
            RefreshHand();
            GameApp.I.Run(EnemyTurnRoutine());
        }

        private void OnQuitClicked()
        {
            GameApp.I.Audio.PlaySfx("ui");
            GameApp.I.Director.SettleDefeat(BattleFacts.Capture(session, GameApp.I.RunSelfDamage));
            GameApp.I.PersistMeta();
            GameApp.I.Replace(new ResultScreen(false, session, session.Battle));
        }

        #endregion

        #region 刷新

        private void RefreshAll()
        {
            RefreshEnemy();
            RefreshPlayer();
            RefreshHand();
            RefreshCenter();
            RefreshStageChip();
        }

        private void RefreshCenter()
        {
            turnLabel.text = "第 " + session.TurnNumber + " 回合 · " +
                (session.Phase == BattlePhase.PlayerTurn ? "你的行动"
                    : (session.Phase == BattlePhase.EnemyTurn ? "敌方行动中…" : "对决结束"));
            drawPileCount.text = session.Player.Deck.Count.ToString();
            discardPileCount.text = session.Player.Discard.Count.ToString();

            turnLabel.color = Theme.SeriesColor(session.EnemyInfo.Accent);
        }

        private void RefreshHand()
        {
            // 拖动进行中绝不重建手牌：此刻手上"少一张"是暂态，
            // 一旦重建就会把布局与 session.Hand 的对应关系拧乱，还会把拖拽中的牌孤儿化。
            if (dragView != null)
            {
                return;
            }

            var hand = session.Player.Hand;

            // 拖拽中的牌不在 handViews 里，这里的手牌数与数组长度天然同步
            while (handViews.Count > hand.Count)
            {
                var last = handViews[handViews.Count - 1];
                last.Destroy();
                handViews.RemoveAt(handViews.Count - 1);
            }

            var fresh = new List<CardView>();
            while (handViews.Count < hand.Count)
            {
                var view = CardView.Create(handRoot, hand[handViews.Count], null);
                handViews.Add(view);
                fresh.Add(view);
            }

            for (int i = 0; i < handViews.Count; i++)
            {
                var view = handViews[i];
                if (view.Instance != hand[i])
                {
                    view.Destroy();
                    handViews[i] = CardView.Create(handRoot, hand[i], null);
                    view = handViews[i];
                }

                view.SetVisible(true);
                view.SetAffordable(hand[i].Cost <= session.Player.Mana);
                view.SetInteractive(!busy && session.Phase == BattlePhase.PlayerTurn);
                view.AttachDrag(this);
            }

            LayoutHand();

            // 新抽的牌从抽牌堆位置飞入手牌区（动画目标 = LayoutHand 排好的最终位）
            foreach (var view in fresh)
            {
                if (view.Root == null)
                {
                    continue;
                }

                var rt = view.Root;
                Vector2 target = rt.anchoredPosition;
                float targetRot = rt.localRotation.eulerAngles.z;
                if (targetRot > 180f)
                {
                    targetRot -= 360f;
                }

                // 从左下角抽牌堆飞入（抽牌堆在手牌坐标系里约 x=-840、y=148）
                rt.anchoredPosition = new Vector2(-860f, target.y + 140f);
                rt.localRotation = Quaternion.Euler(0f, 0f, -25f);
                rt.localScale = Vector3.one * (HandScale * 0.7f);
                Tween.LocalMove(rt, rt.anchoredPosition, target, 0.36f, Easing.OutCubic);
                Tween.Scale(rt, HandScale * 0.7f, HandScale, 0.36f, Easing.OutBack);
                Tween.To(0.36f, k => { if (rt != null) { rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, targetRot, k)); } }, Easing.OutCubic);
            }

            RefreshEndTurnButton();

            if (emptyHint != null)
            {
                emptyHint.gameObject.SetActive(hand.Count == 0 && !session.Finished);
            }
        }

        private void LayoutHand(bool animate = false)
        {
            // 以"实际摆在手上的张数"为准：拖动时被摘掉一张，手牌会顺势合拢
            int n = handViews.Count;
            if (n == 0)
            {
                return;
            }

            // 手牌锚在屏幕下沿：spacing 决定总宽度，卡与卡之间大幅叠压才像"扇形手牌"
            float spacing = n <= 5 ? 172f : Mathf.Min(172f, HandMaxWidth / (n - 1f));
            float total = spacing * (n - 1);
            float left = HandCenterX - total * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                float x = left + spacing * i;
                float arc = Mathf.Sin(t * Mathf.PI) * 22f;
                float rot = Mathf.Lerp(5.5f, -5.5f, t);
                float y = HandBottom + arc;

                // 悬停的那张抬起来并拉平，是"牌是活的"最直接的暗示
                if (i == hoverIndex && !busy)
                {
                    y += HandHoverLift;
                    rot *= 0.25f;
                }

                handViews[i].SetLayout(new Vector2(x, y), rot, HandScale, animate);
            }
        }

        private void RefreshEndTurnButton()
        {
            bool on = !busy && session.Phase == BattlePhase.PlayerTurn && !session.Finished;
            endTurnButton.interactable = on;
            endTurnLabel.color = on ? Theme.BgDeep : Theme.TextFaint;

            var fill = on ? Theme.AccentDeep : Theme.PanelRaised;
            var border = on ? Theme.Accent : Theme.Border;
            endTurnImage.sprite = SpriteForge.Panel(Theme.RadiusM, fill, border, 3);
            endTurnImage.type = Image.Type.Sliced;
            endTurnImage.color = Color.white;
        }

        /// <summary>整张牌只对自己生效时不显示为攻击。</summary>
        private static bool IsSelfTargeted(CardInstance card)
        {
            var def = card.Def;
            for (int i = 0; i < def.Ops.Count; i++)
            {
                switch (def.Ops[i].Kind)
                {
                    case "dmg":
                    case "dmgadd":
                    case "grow":
                    case "randdmg":
                    case "drain":
                    case "ssteal":
                    case "emanalse":
                    case "esteal":
                    case "stealcard":
                    case "purge":
                    case "etakenmul":
                    case "edmgdown":
                    case "randomplay":
                    case "copydraw":
                        return false;
                }
            }

            return true;
        }

        private void RebuildStatus(RectTransform row, BattleUnit unit, bool onEnemy)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(row.GetChild(i).gameObject);
            }

            float x = 0f;
            float y = 0f;

            if (unit.Shield > 0)
            {
                MakeChip(row, "护盾 " + unit.Shield, Theme.Shield, ref x, y);
            }

            for (int i = 0; i < unit.Buffs.Count; i++)
            {
                MakeChip(row, unit.Buffs[i].Describe() + " (" + unit.Buffs[i].TurnsLeft + ")", Theme.Gold, ref x, y);
            }

            for (int i = 0; i < unit.Hooks.Count; i++)
            {
                string label = string.IsNullOrEmpty(unit.Hooks[i].Label) ? "持续效果" : unit.Hooks[i].Label;
                MakeChip(row, label + " ×" + unit.Hooks[i].TurnsLeft, Theme.SeriesChrono, ref x, y);
            }

            if (unit.ImmunityCharges > 0)
            {
                MakeChip(row, "免疫 ×" + unit.ImmunityCharges, Theme.SeriesShadow, ref x, y);
            }

            if (unit.DodgeCharges > 0)
            {
                MakeChip(row, "闪避 ×" + unit.DodgeCharges, Theme.SeriesShadow, ref x, y);
            }

            if (unit.Mana > 0)
            {
                MakeChip(row, "魔力 " + unit.Mana, Theme.Mana, ref x, y);
            }

            // 居中排布
            float total = x;
            var rect = row;
            for (int i = 0; i < rect.childCount; i++)
            {
                var child = rect.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                float cx = child.anchoredPosition.x;
                child.anchoredPosition = new Vector2(cx - total * 0.5f, 0f);
            }
        }

        private static void MakeChip(RectTransform row, string text, Color color, ref float x, float y)
        {
            int width = Mathf.Clamp(22 + text.Length * 15, 76, 300);
            var chip = Ui.Rounded("Chip", row, 10,
                Theme.WithAlpha(Theme.Darken(color, 0.66f), 0.92f), Theme.WithAlpha(color, 0.7f), 1);
            // 锚在父节点中心，x 先按累加宽度摆放，最后统一左移一半做整体居中
            Ui.Fixed(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x + width * 0.5f, y), new Vector2(width, 32f));

            var label = Ui.Label("ChipText", chip.transform, text, Theme.Caption, Theme.Lighten(color, 0.35f), TextAlignmentOptions.Center);
            Ui.Stretch(label.rectTransform, 8f, 1f, 8f, 1f);

            x += width + 8f;
        }

        private void RebuildBuffs(RectTransform row, BattleUnit unit)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(row.GetChild(i).gameObject);
            }

            var items = new List<KeyValuePair<string, Color>>();

            // 护盾作为第一枚 chip 混进增益流，不再单独占一行
            if (unit.Shield > 0)
            {
                items.Add(new KeyValuePair<string, Color>("护盾 " + unit.Shield, Theme.Shield));
            }

            for (int i = 0; i < unit.Buffs.Count; i++)
            {
                items.Add(new KeyValuePair<string, Color>(
                    unit.Buffs[i].Describe() + "  " + unit.Buffs[i].TurnsLeft, Theme.Gold));
            }

            for (int i = 0; i < unit.Hooks.Count; i++)
            {
                string label = string.IsNullOrEmpty(unit.Hooks[i].Label) ? "持续" : unit.Hooks[i].Label;
                items.Add(new KeyValuePair<string, Color>(label + "  " + unit.Hooks[i].TurnsLeft, Theme.SeriesChrono));
            }

            // 流式排布：从左缘起依次摆，超出 380 就换行
            float x = 0f;
            float y = 0f;
            for (int i = 0; i < items.Count && i < 8; i++)
            {
                int width = Mathf.Clamp(24 + items[i].Key.Length * 14, 90, 240);
                if (x > 0f && x + width > 380f)
                {
                    x = 0f;
                    y -= 36f;
                }

                var chip = Ui.Rounded("Buff", row, 9,
                    Theme.WithAlpha(Theme.Darken(items[i].Value, 0.66f), 0.92f), Theme.WithAlpha(items[i].Value, 0.65f), 1);
                Ui.Fixed(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(x, y), new Vector2(width, 28f));

                var label = Ui.Label("BuffText", chip.transform, items[i].Key, 13,
                    Theme.Lighten(items[i].Value, 0.35f), TextAlignmentOptions.Center);
                Ui.Stretch(label.rectTransform, 5f, 1f, 5f, 1f);

                x += width + 8f;
            }
        }

        #endregion

        #region 敌方回合

        private IEnumerator EnemyTurnRoutine()
        {
            busy = true;
            RefreshHand();
            RefreshEndTurnButton();

            session.PlayerEndTurn();
            DrainEvents();
            RefreshAll();
            UpdateLog("敌方回合");

            yield return new WaitForSeconds(0.45f);

            session.EnemyTurnStart();
            DrainEvents();
            RefreshAll();

            int guard = 0;
            while (session.Phase == BattlePhase.EnemyTurn && !session.Finished && guard++ < 12)
            {
                if (!session.EnemyActOnce())
                {
                    break;
                }

                GameApp.I.Audio.PlaySfx("slash");
                DrainEvents();
                RefreshAll();
                yield return new WaitForSeconds(0.72f);
            }

            if (session.Finished)
            {
                busy = false;
                GameApp.I.Run(FinishAfterDelay(0.5f, session.Victory));
                yield break;
            }

            session.EnemyTurnEnd();
            DrainEvents();
            RefreshAll();
            yield return new WaitForSeconds(0.4f);

            session.PlayerTurnStart();
            DrainEvents();
            RefreshAll();
            UpdateLog("第 " + session.TurnNumber + " 回合，你的行动。");

            busy = false;
            RefreshHand();
            RefreshEndTurnButton();
        }

        private IEnumerator FinishAfterDelay(float delay, bool victory)
        {
            busy = true;
            RefreshHand();
            yield return new WaitForSeconds(delay);
            GoResult(victory);
        }

        private void GoResult(bool victory)
        {
            var app = GameApp.I;
            var director = app.Director;
            var facts = BattleFacts.Capture(session, app.RunSelfDamage);

            if (victory)
            {
                director.SettleVictory(facts);
                app.PersistMeta();
                app.Replace(director.JustClearedRun
                    ? (Screen)new ResultScreen(true, session, EnemyRoster.FinalBattle, true)
                    : new RewardScreen(session, director.LastWasMasterStage));
            }
            else
            {
                director.SettleDefeat(facts);
                app.PersistMeta();
                app.Replace(new ResultScreen(false, session, session.Battle));
            }
        }

        #endregion

        #region 事件与飘字

        protected override void OnDestroyed()
        {
            gameObjectUnused();
        }

        private void gameObjectUnused()
        {
        }

        private void DrainEvents()
        {
            var events = session.TakeEvents();
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                Vector2 at = e.OnEnemy
                    ? new Vector2(StageX, PortraitY + PortraitSize * 0.5f + 10f)
                    : new Vector2(-StageX, PortraitY + PortraitSize * 0.5f + 10f);

                switch (e.Kind)
                {
                    case EventKind.Damage:
                        SpawnFloat(e.Text, Theme.Danger, at + new Vector2(Random.Range(-40f, 40f), 0f), 1.35f);
                        Flash(e.OnEnemy);
                        GameApp.I.Audio.PlaySfx("slash");
                        Tween.Shake(Root, e.OnEnemy ? 6f : 11f, 0.3f);
                        break;
                    case EventKind.Heal:
                        SpawnFloat(e.Text, Theme.Accent, at + new Vector2(Random.Range(-30f, 30f), 0f), 1.1f);
                        GameApp.I.Audio.PlaySfx("heal");
                        break;
                    case EventKind.Shield:
                        SpawnFloat(e.Text, Theme.Shield, at + new Vector2(Random.Range(-30f, 30f), 24f), 1.0f);
                        break;
                    case EventKind.Mana:
                        SpawnFloat(e.Text, Theme.Mana, at + new Vector2(Random.Range(-30f, 30f), 44f), 0.95f);
                        GameApp.I.Audio.PlaySfx("mana");
                        break;
                    case EventKind.Draw:
                        GameApp.I.Audio.PlaySfx("draw");
                        break;
                    case EventKind.Play:
                        GameApp.I.Audio.PlaySfx("play");
                        UpdateLog(e.Text);
                        break;
                    case EventKind.Debuff:
                        SpawnFloat(e.Text, Theme.SeriesShadow, at + new Vector2(0f, 64f), 1.0f);
                        GameApp.I.Audio.PlaySfx("venom");
                        break;
                    case EventKind.Hook:
                        UpdateLog(e.Text);
                        break;
                }
            }
        }

        private void SpawnFloat(string text, Color color, Vector2 at, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var label = Ui.Label("Float", Root, text, Mathf.RoundToInt(Theme.H4 * scale), color, TextAlignmentOptions.Center);
            Ui.Fixed(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, new Vector2(520f, 44f));

            // 入场：从 0.5 倍弹到目标大小，干脆利落
            label.rectTransform.localScale = Vector3.one * 0.5f;
            Tween.Scale(label.rectTransform, 0.5f, 1f, 0.24f, Easing.OutBack);

            floats.Add(new Floating { label = label, bornAt = Time.unscaledTime, drift = 30f });
        }

        private void Flash(bool onEnemy)
        {
            if (onEnemy && enemyHitFlash != null)
            {
                enemyHitFlash.color = Theme.WithAlpha(Color.white, 0.7f);
            }
        }

        /// <summary>战报只在有事时短暂出现（胶囊下方一行），不占常驻版面。</summary>
        private void UpdateLog(string line)
        {
            if (string.IsNullOrEmpty(line) || banner == null)
            {
                return;
            }

            banner.text = line;
            banner.color = Theme.WithAlpha(Theme.TextDim, 0.95f);
            bannerUntil = Time.unscaledTime + 1.5f;
        }

        private void TickBanner()
        {
            if (banner == null || banner.color.a <= 0.01f)
            {
                return;
            }

            float left = bannerUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                var gone = banner.color;
                gone.a = Mathf.Max(0f, gone.a - 0.06f);
                banner.color = gone;
            }
            else if (left < 0.35f)
            {
                var c = banner.color;
                c.a = 0.95f * (left / 0.35f);
                banner.color = c;
            }
        }

        private void TickFloats(float dt)
        {
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                if (f.label == null)
                {
                    floats.RemoveAt(i);
                    continue;
                }

                float age = Time.unscaledTime - f.bornAt;
                if (age > 1.1f)
                {
                    Object.Destroy(f.label.gameObject);
                    floats.RemoveAt(i);
                    continue;
                }

                var rt = f.label.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + f.drift * dt);

                var c = f.label.color;
                c.a = Mathf.Clamp01(1f - (age - 0.55f) / 0.55f);
                f.label.color = c;
            }

            if (enemyHitFlash != null && enemyHitFlash.color.a > 0.01f)
            {
                var c = enemyHitFlash.color;
                c.a = Mathf.Max(0f, c.a - dt * 3.2f);
                enemyHitFlash.color = c;
            }
        }

        #endregion

        public override void Tick(float deltaTime)
        {
            if (startDelay > 0f)
            {
                startDelay -= deltaTime;
                if (startDelay > 0f)
                {
                    return;
                }

                GameApp.I.Audio.PlayMusic("battle1");
                DrainEvents();
                RefreshAll();
                GameApp.I.RunSelfDamage = 0;
            }

            TickFloats(deltaTime);
            TickBanner();
            TickDropZoneHint();

            if (settlePending)
            {
                settleTimer -= deltaTime;
                if (settleTimer <= 0f)
                {
                    settlePending = false;
                    if (!busy && session.Phase == BattlePhase.PlayerTurn && !session.Finished)
                    {
                        OnEndTurnClicked();
                    }
                }
            }
        }

        /// <summary>轻点提示出来的出牌区，1.8 秒没人用就自己收回去，免得挡住牌桌。</summary>
        private void TickDropZoneHint()
        {
            if (dragView != null || dropZone == null || !dropZone.gameObject.activeSelf)
            {
                return;
            }

            if (zoneShownAt > 0f && Time.unscaledTime - zoneShownAt > 1.8f)
            {
                zoneShownAt = 0f;
                HideDropZone();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public BattleSession DebugSession { get { return session; } }

        public void DebugRefresh()
        {
            DrainEvents();
            RefreshAll();
        }

        public void DebugSetMana(int amount)
        {
            session.DebugSetMana(amount);
            DebugRefresh();
        }
#endif
    }
}
