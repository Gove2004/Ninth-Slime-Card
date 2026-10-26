using System.Collections.Generic;
using Slime.Core;
using Slime.Game;
using UnityEngine;

namespace Slime.DevTools
{
    /// <summary>
    /// 编辑器开发面板：直接跳到任意界面、摆出演示状态，用于离线验收 UI 与玩法。
    /// 只存在于 Editor 程序集，不会进入真机包体。
    /// </summary>
    public static class DevPanel
    {
        public static string Jump(string screen)
        {
            var app = GameApp.I;
            if (app == null)
            {
                return "no GameApp in scene";
            }

            string key = (screen ?? string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "boot":
                    app.Replace(new BootScreen());
                    break;

                case "login":
                    app.Replace(new LoginScreen());
                    break;

                case "home":
                    app.Replace(new HomeScreen());
                    break;

                case "map":
                    EnsureRun(app);
                    app.Replace(new MapScreen());
                    break;

                case "battle":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Battle);
                    break;

                case "battle-elite":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Elite);
                    break;

                case "battle-boss":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Boss);
                    break;

                case "battle-demo":
                    EnsureRun(app);
                    {
                        var battle = EnterKind(app, NodeKind.Battle);
                        PlayDemo(battle, 4);
                    }
                    break;

                case "battle-danger":
                    EnsureRun(app);
                    {
                        var battle = EnterKind(app, NodeKind.Battle);
                        HurtPlayer(battle, 7);
                    }
                    break;

                case "battle-full":
                    EnsureRun(app);
                    {
                        var battle = EnterKind(app, NodeKind.Battle);
                        FillHand(battle, 8);
                    }
                    break;

                case "battle-mech":
                    EnsureRun(app);
                    {
                        // 挂满状态并弹出其中一个的说明，用于验收战斗内的机制弹层
                        var battle = EnterKind(app, NodeKind.Battle);
                        var session = battle.DebugSession;
                        session.Enemy.Shield = 12;
                        session.Enemy.SetStatus(StatusKind.Poison, 6);
                        session.Enemy.SetStatus(StatusKind.Freeze, 2);
                        session.Enemy.SetStatus(StatusKind.Vulnerable, 3);
                        battle.DebugRefresh();
                        MechanicPopup.ShowStatus(battle.Root, StatusKind.Poison);
                    }
                    break;

                case "drag-verbose":
                case "drag-status":
                    {
                        var battle = app.Current as BattleScreen;
                        return battle != null ? battle.DebugDrag(key.Substring(5)) : "not on battle screen";
                    }

                case "drag-over":
                case "drag-blocked":
                case "drag-out":
                case "drag-play":
                    EnsureRun(app);
                    {
                        // 把界面摆成"正在拖动一张牌"的样子，验收拖放反馈
                        var battle = EnterKind(app, NodeKind.Battle);
                        FillHand(battle, 6);
                        return battle.DebugDrag(key.Substring(5));
                    }

                case "treasure":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Treasure);
                    break;

                case "rest":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Rest);
                    break;

                case "shop":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Shop);
                    break;

                case "event":
                    EnsureRun(app);
                    EnterKind(app, NodeKind.Event);
                    break;

                case "reward":
                    EnsureRun(app);
                    {
                        var session = WinBattle(app, NodeKind.Battle);
                        app.Replace(new RewardScreen(session));
                    }
                    break;

                case "reward-elite":
                    EnsureRun(app);
                    {
                        // 精英战奖励是两轮，用来验收"再选一张"
                        var session = WinBattle(app, NodeKind.Elite);
                        app.Replace(new RewardScreen(session));
                    }
                    break;

                case "reward-full":
                    EnsureRun(app);
                    {
                        // 把牌组灌到上限，验收"牌组已满 → 替换"流程
                        var pool = app.Cards.All;
                        for (int i = 0; app.Director.DeckCount < DeckRules.DeckCap && pool.Count > 0; i++)
                        {
                            app.Director.TakeReward(pool[i % pool.Count]);
                        }

                        var session = WinBattle(app, NodeKind.Battle);
                        app.Replace(new RewardScreen(session));
                    }
                    break;

                case "result-win":
                    EnsureRun(app);
                    {
                        var session = WinBattle(app, NodeKind.Boss);
                        app.Replace(new ResultScreen(true, session));
                    }
                    break;

                case "result-lose":
                    EnsureRun(app);
                    {
                        var session = LoseBattle(app);
                        app.Replace(new ResultScreen(false, session));
                    }
                    break;

                case "codex-cards":
                    app.Replace(new CodexScreen(CodexScreen.Tab.Cards));
                    break;

                case "codex-mech":
                    app.Replace(new CodexScreen(CodexScreen.Tab.Mechanics));
                    break;

                case "codex-detail":
                    return OpenCodexDetail(app);

                case "codex":
                    app.Replace(new CodexScreen(CodexScreen.Tab.Achievements));
                    break;

                case "settings":
                    app.Replace(new SettingsScreen());
                    break;

                case "reward-preview":
                    EnsureRun(app);
                    {
                        // 固定候选 + 空牌组，专门验收「兑现牌缺铺垫」那条红色警告。
                        // 冰川要冻结、圣裁要护盾，空牌组里两张都该报打不出来。
                        // 牌组只覆盖分析视图，不动真实存档局。
                        var picked = new List<CardDef>();
                        AddByName(app, picked, "冰川");
                        AddByName(app, picked, "圣裁");
                        AddByName(app, picked, "斩击");
                        RewardScreen.ForcedChoices = picked;
                        RewardScreen.ForcedDeck = new List<CardDef>();

                        var session = WinBattle(app, NodeKind.Battle);
                        app.Replace(new RewardScreen(session));
                    }
                    break;

                case "audio":
                    return AudioCheck();

                default:
                    return "unknown screen: " + screen;
            }

            return "jumped:" + key;
        }

        /// <summary>
        /// 打开图鉴并弹出指定卡牌的详情。用卡名匹配，方便人工核对某条说明。
        /// 参数缺省时挑一张带「引爆」的牌，那是说明最长的形态。
        /// </summary>
        private static string OpenCodexDetail(GameApp app, string cardName = null)
        {
            CardDef target = null;
            for (int i = 0; i < app.Cards.All.Count; i++)
            {
                var def = app.Cards.All[i];
                if (cardName != null)
                {
                    if (def.Name == cardName)
                    {
                        target = def;
                        break;
                    }
                }
                else if (target == null && def.Script.Contains("detonate"))
                {
                    target = def;
                }
            }

            if (target == null)
            {
                return "card not found: " + (cardName ?? "detonate");
            }

            var screen = new CodexScreen(CodexScreen.Tab.Cards);
            app.Replace(screen);
            screen.ShowDetail(target);
            return "detail:" + target.Name;
        }

        /// <summary>
        /// 直接构造指定类型的节点并进入它，跳过地图上找这类节点的麻烦。
        /// 节点挂到当前地图上但不加入可达集合——这样离开时就停在原地，不会真的推进。
        /// </summary>
        private static BattleScreen EnterKind(GameApp app, NodeKind kind)
        {
            var director = app.Director;
            director.EnsureRun();

            var map = director.Map;
            if (map != null)
            {
                MapNode match = null;
                for (int i = 0; i < map.Nodes.Count; i++)
                {
                    if (map.Nodes[i].Kind == kind)
                    {
                        match = map.Nodes[i];
                        break;
                    }
                }

                if (match != null)
                {
                    director.Reachable.Add(match.Id);
                    director.EnterNode(match.Id);
                }
            }

            // 地图里找不到这类节点（比如刚进第一章第一层只有战斗）就手动补一个
            if (director.CurrentNode == null || director.CurrentNode.Kind != kind)
            {
                var node = new MapNode
                {
                    Id = 9000 + (int)kind,
                    Kind = kind,
                    Floor = director.Floor,
                    X = 0.5f,
                    Y = 0.5f
                };
                director.Map.Nodes.Add(node);
                director.Reachable.Add(node.Id);
                director.EnterNode(node.Id);
            }

            if (kind == NodeKind.Treasure || kind == NodeKind.Rest
                || kind == NodeKind.Shop || kind == NodeKind.Event)
            {
                switch (kind)
                {
                    case NodeKind.Treasure: app.Replace(new TreasureScreen()); break;
                    case NodeKind.Rest: app.Replace(new RestScreen()); break;
                    case NodeKind.Shop: app.Replace(new ShopScreen()); break;
                    default: app.Replace(new EventScreen()); break;
                }

                return null;
            }

            var battle = new BattleScreen();
            app.Replace(battle);
            return battle;
        }

        /// <summary>给图鉴/成就屏填充演示数据，便于验收排版。</summary>
        public static string SeedDemo()
        {
            var app = GameApp.I;
            if (app == null)
            {
                return "no GameApp in scene";
            }

            app.Meta.Trophy = 420;
            app.Meta.BestChapter = 3;
            app.Meta.RunsPlayed = 14;
            app.Meta.RunsCleared = 2;
            app.Meta.Wins = 5;
            app.Meta.Losses = 9;

            app.Meta.AddCounter("score", 18400);
            app.Meta.AddCounter("draw", 520);
            app.Meta.AddCounter("play", 430);
            app.Meta.AddCounter("energy", 980);
            app.Meta.AddCounter("heal", 610);
            app.Meta.Flags.Add("overheat");
            app.Meta.Flags.Add("fast_kill");
            app.Meta.Flags.Add("elite_slayer");

            var all = app.Cards.All;
            for (int i = 0; i < all.Count; i += 3)
            {
                app.Meta.Discover(all[i].Id);
            }

            app.Director.EnsureRun();
            for (int i = 0; i < all.Count; i += 7)
            {
                if (!app.Director.AtDeckCap)
                {
                    app.Director.TakeReward(all[i]);
                }
            }

            app.EvaluateAndFlush();
            return "seeded: cards=" + app.Cards.Count
                 + " discovered=" + app.Meta.DiscoveredCards.Count
                 + " achievements=" + (app.Achievements != null ? app.Achievements.All.Count : 0);
        }

        /// <summary>把牌组换成全员八系列，方便看各种卡面。</summary>
        public static string DeckAllSeries()
        {
            var app = GameApp.I;
            if (app == null)
            {
                return "no GameApp in scene";
            }

            app.Director.EnsureRun();
            app.Director.Deck.Clear();
            var all = app.Cards.All;
            for (int i = 0; i < all.Count; i++)
            {
                app.Meta.Discover(all[i].Id);
                if (all[i].Cost <= 3 && app.Director.Deck.Count < DeckRules.DeckCap)
                {
                    app.Director.TakeReward(all[i]);
                }
            }

            return "deck=" + app.Director.DeckCount;
        }

        // ------------------------------------------------------------ 自检

        /// <summary>
        /// 音频自检。资源实际放在 Resources/Audios/{Music,Sound} 下，一旦目录或键名对不上，
        /// PlaySfx/PlayMusic 会静默返回 null —— 这条检查就是为了让那种失败看得见。
        /// 新增音频时把键名同步补进下面两个列表。
        /// </summary>
        public static string AudioCheck()
        {
            string[] musicKeys = { "title1", "title2", "battle1", "battle2" };
            string[] sfxKeys = { "ui", "draw", "play", "slash", "mana", "heal", "venom" };

            int missing = 0;
            var sb = new System.Text.StringBuilder();
            sb.Append("音频自检（Resources/Audios）：");

            foreach (var key in musicKeys)
            {
                if (!ProbeClip("Audios/Music/" + key, sb, "M:" + key))
                {
                    missing++;
                }
            }

            foreach (var key in sfxKeys)
            {
                if (!ProbeClip("Audios/Sound/" + key, sb, "S:" + key))
                {
                    missing++;
                }
            }

            sb.Append("\n缺失 ").Append(missing).Append(" / 共 ").Append(musicKeys.Length + sfxKeys.Length);

            var listeners = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(UnityEngine.FindObjectsSortMode.None);
            var sources = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(UnityEngine.FindObjectsSortMode.None);
            sb.Append("\nlisteners=").Append(listeners.Length)
              .Append(" 音源=").Append(sources.Length);

            for (int i = 0; i < sources.Length; i++)
            {
                var s = sources[i];
                sb.Append("\n  ").Append(s.loop ? "music" : "sfx  ")
                  .Append(" clip=").Append(s.clip != null ? s.clip.name : "null")
                  .Append(" playing=").Append(s.isPlaying)
                  .Append(" vol=").Append(s.volume.ToString("F2"))
                  .Append(" mute=").Append(s.mute);
            }

            return sb.ToString();
        }

        private static bool ProbeClip(string path, System.Text.StringBuilder sb, string label)
        {
            var clip = UnityEngine.Resources.Load(path, typeof(UnityEngine.AudioClip)) as UnityEngine.AudioClip;
            sb.Append("\n  ").Append(clip != null ? "OK  " : "缺失 ").Append(label);
            if (clip != null)
            {
                sb.Append("  ").Append(clip.length.ToString("F2")).Append("s / ").Append(clip.frequency).Append("Hz");
            }

            return clip != null;
        }

        /// <summary>
        /// 规则自检：验证卡表解析完整性、每张卡都能安全打出、以及整局能正常收敛。
        /// 返回一段可读报告，供 MCP/命令行调用。
        /// </summary>
        public static string SelfTest()
        {
            var app = GameApp.I;
            var db = app != null ? app.Cards : Slime.Game.ContentService.LoadCards();
            var achievements = app != null ? app.Achievements : Slime.Game.ContentService.LoadAchievements();

            var sb = new System.Text.StringBuilder();
            sb.Append("cards=").Append(db.Count);
            sb.Append(" achievements=").Append(achievements != null ? achievements.All.Count : 0);

            // 1) 解析完整性
            int noOps = 0;
            int badText = 0;
            var perSeries = new Dictionary<CardSeries, int>();
            for (int i = 0; i < db.All.Count; i++)
            {
                var def = db.All[i];
                if (def.Ops == null || def.Ops.Count == 0)
                {
                    noOps++;
                    sb.Append("\n  [no-ops] ").Append(def.Id).Append(' ').Append(def.Name);
                }

                string text = def.Text(0);
                if (text != null && text.Contains("[数值"))
                {
                    badText++;
                    sb.Append("\n  [placeholder] ").Append(def.Id).Append(' ').Append(text);
                }

                int n;
                perSeries.TryGetValue(def.Series, out n);
                perSeries[def.Series] = n + 1;
            }

            sb.Append("\nnoOps=").Append(noOps).Append(" badText=").Append(badText);

            foreach (var series in Series.All)
            {
                int n;
                perSeries.TryGetValue(series, out n);
                sb.Append("\n  ").Append(Series.DisplayName(series)).Append(" = ").Append(n);
            }

            // 2) 每张卡单独打出一次，环境取第一章的一只普通怪
            var probeEnemy = ScaleEnemy(EnemyRoster.Default, 1, 1, false);
            int crashed = 0;
            for (int i = 0; i < db.All.Count; i++)
            {
                var def = db.All[i];
                var deck = new List<CardInstance> { CardInstance.Wrap(def) };
                var session = new BattleSession(db, probeEnemy, deck, 4242 + def.Id);
                session.Begin();

                try
                {
                    for (int k = 0; k < 6 && session.Player.Hand.Count > 0 && !session.Finished; k++)
                    {
                        session.Player.Energy = 30;
                        session.PlayCard(0);
                    }

                    if (!session.Finished)
                    {
                        session.EndPlayerTurn();
                        session.StartEnemyTurn();
                        session.RunEnemyTurn();
                    }
                }
                catch (System.Exception e)
                {
                    crashed++;
                    sb.Append("\n  [crash] ").Append(def.Id).Append(' ').Append(def.Name)
                      .Append(" :: ").Append(e.GetType().Name).Append(' ').Append(e.Message);
                }
            }

            sb.Append("\ncardCrash=").Append(crashed);

            // 3) 单怪收敛性：四章 × 各池子取样，用固定策略打到底
            int stuck = 0;
            int wins = 0;
            int losses = 0;
            int maxTurns = 0;
            var lossMap = new System.Text.StringBuilder();
            int batches = 0;

            for (int chapter = 1; chapter <= EnemyRoster.TotalChapters; chapter++)
            {
                var pool = EnemyRoster.NormalPool(chapter);
                for (int p = 0; p < pool.Count; p++)
                {
                    for (int seed = 0; seed < 2; seed++)
                    {
                        batches++;
                        var enemy = ScaleEnemy(pool[p], chapter, 6, false);
                        var session = new BattleSession(db, enemy, null, 9000 + chapter * 97 + p * 7 + seed);
                        session.Begin();
                        RunToEnd(session);

                        if (!session.Finished)
                        {
                            stuck++;
                        }
                        else if (session.Victory)
                        {
                            wins++;
                        }
                        else
                        {
                            losses++;
                            lossMap.Append(' ').Append(enemy.Id).Append(':')
                                   .Append(enemy.Name);
                        }

                        if (session.TurnNumber > maxTurns)
                        {
                            maxTurns = session.TurnNumber;
                        }
                    }
                }
            }

            sb.Append("\nbatches=").Append(batches)
              .Append(" stuck=").Append(stuck)
              .Append(" wins=").Append(wins)
              .Append(" losses=").Append(losses)
              .Append(" maxTurns=").Append(maxTurns);

            if (lossMap.Length > 0)
            {
                sb.Append("\n败点=").Append(lossMap.ToString());
            }

            // 4) 整局循环：从第一章打到通关或阵亡，验证地图推进 / 奖励产出 / 牌组上限
            sb.Append("\n").Append(RunLoopCheck(db, achievements));

            // 5) 音频资源：键名与目录是否对得上（失配会静默无声）
            sb.Append("\n").Append(AudioCheck());

            return sb.ToString();
        }

        /// <summary>
        /// 按章节/层数缩放一只怪，用于自检取样。
        /// 与 RunDirector 的缩放规则保持一致，否则自检打出来的数值跟实际对局不是一回事。
        /// </summary>
        private static EnemyDef ScaleEnemy(EnemyDef template, int chapter, int floor, bool elite)
        {
            var scaled = new EnemyDef
            {
                Id = template.Id,
                Name = template.Name,
                Title = template.Title,
                Family = template.Family,
                Blurb = template.Blurb,
                Sprite = template.Sprite,
                Chapter = template.Chapter,
                Accent = template.Accent,
                Seed = template.Seed,
                IsElite = elite,
                IsBoss = template.IsBoss,
                ActionsPerTurn = template.ActionsPerTurn,
                Hp = EnemyRoster.ScaleHp(template.Hp, chapter, floor, elite),
                PlayerHp = EnemyRoster.PlayerHpForChapter(chapter)
            };

            foreach (var pair in template.StartStatuses)
            {
                scaled.StartStatuses[pair.Key] = pair.Value;
            }

            for (int i = 0; i < template.Moves.Count; i++)
            {
                var m = template.Moves[i];
                scaled.Moves.Add(new EnemyMove
                {
                    Label = m.Label,
                    Kind = m.Kind,
                    Damage = m.Damage > 0 ? EnemyRoster.ScaleDamage(m.Damage, chapter, floor, elite) : 0,
                    Hits = m.Hits,
                    Shield = m.Shield > 0 ? m.Shield + (chapter - 1) * 3 : 0,
                    Heal = m.Heal > 0 ? m.Heal + (chapter - 1) * 2 : 0,
                    Status = m.Status,
                    StatusStacks = m.StatusStacks,
                    Weight = m.Weight,
                    Cooldown = m.Cooldown,
                    OnlyBelowHpPct = m.OnlyBelowHpPct
                });
            }

            return scaled;
        }

        /// <summary>
        /// 模拟玩家打到底：优先出攻击牌，出不动了再出功能牌，最后结束回合。
        /// 按相位驱动，而不是按"玩家回合一次"——被冻结时相位会被推进到敌方回合，
        /// 按回合数驱动的写法会在这里误判为战斗未结束。
        /// </summary>
        private static void RunToEnd(BattleSession session)
        {
            int guard = 0;
            while (!session.Finished && guard++ < 300)
            {
                if (session.Phase == BattlePhase.PlayerTurn)
                {
                    for (int k = 0; k < 12 && !session.Finished; k++)
                    {
                        int pick = PickCard(session);
                        if (pick < 0)
                        {
                            break;
                        }

                        session.PlayCard(pick);
                    }

                    if (!session.Finished)
                    {
                        session.EndPlayerTurn();
                    }

                    continue;
                }

                if (session.Phase == BattlePhase.EnemyTurn)
                {
                    session.StartEnemyTurn();
                    session.RunEnemyTurn();
                    if (!session.Finished)
                    {
                        session.StartPlayerTurn();
                    }

                    continue;
                }

                break;
            }
        }

        /// <summary>选牌：优先攻击权重高的，其次任意能出的牌。</summary>
        private static int PickCard(BattleSession session)
        {
            int bestAttack = -1;
            int bestWeight = 0;
            int any = -1;

            for (int i = 0; i < session.Player.Hand.Count; i++)
            {
                var card = session.Player.Hand[i];
                if (!session.CanPlay(card))
                {
                    continue;
                }

                if (any < 0)
                {
                    any = i;
                }

                int weight = AttackWeight(card.Def);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestAttack = i;
                }
            }

            return bestAttack >= 0 ? bestAttack : any;
        }

        /// <summary>卡牌的进攻权重：估个大概就够，测试机器人不需要精确评估。</summary>
        private static int AttackWeight(CardDef def)
        {
            int weight = 0;
            AccumulateWeight(def.Ops, ref weight);
            return weight;
        }

        /// <summary>构筑打分：进攻价值为主，费用为辅。</summary>
        private static int Score(CardDef def)
        {
            return def == null ? int.MinValue : AttackWeight(def) * 4 + def.Cost;
        }

        private static void AccumulateWeight(List<EffectOp> ops, ref int weight)
        {
            if (ops == null)
            {
                return;
            }

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                switch (op.Kind)
                {
                    case "dmg":
                    case "dmgadd":
                    case "randdmg":
                    case "grow":
                        weight += 10 + op.A;
                        break;
                    case "drain":
                    case "ssteal":
                    case "esteal":
                    case "stealcard":
                        weight += 8;
                        break;
                }

                AccumulateWeight(op.Then, ref weight);
                AccumulateWeight(op.Else, ref weight);
            }
        }

        /// <summary>
        /// 跑一整局（走地图直到通关或阵亡），检查：
        /// 章节推进是否顺畅、牌组是否守住上限、奖励是否产出、最深能到第几章。
        /// </summary>
        private static string RunLoopCheck(CardDatabase db, AchievementDatabase achievements)
        {
            var director = new RunDirector(db, achievements, new MetaState(), 31337);
            director.StartNewRun();

            int nodes = 0;
            int battles = 0;
            int picks = 0;
            int swaps = 0;
            int peakDeck = director.DeckCount;
            int deepestChapter = 1;
            bool cleared = false;

            while (nodes < 200)
            {
                if (director.Phase == RunPhase.Victory)
                {
                    cleared = true;
                    break;
                }

                if (director.Phase == RunPhase.Defeat)
                {
                    break;
                }

                var reachable = director.ReachableNodes();
                if (reachable.Count == 0)
                {
                    break;
                }

                // 简单策略：优先精英与战斗，血量低时优先篝火
                var node = reachable[0];
                bool critical = director.HpMax > 0 && director.Hp * 100 / director.HpMax < 40;
                for (int i = 0; i < reachable.Count; i++)
                {
                    var candidate = reachable[i];
                    if (critical && candidate.Kind == NodeKind.Rest)
                    {
                        node = candidate;
                        break;
                    }

                    if (!critical && (candidate.Kind == NodeKind.Elite || candidate.Kind == NodeKind.Boss))
                    {
                        node = candidate;
                        break;
                    }
                }

                director.EnterNode(node.Id);
                nodes++;

                if (director.Chapter > deepestChapter)
                {
                    deepestChapter = director.Chapter;
                }

                switch (director.Phase)
                {
                    case RunPhase.Battle:
                        {
                            var session = director.CreateBattle(1000 + nodes * 31);
                            RunToEnd(session);
                            battles++;

                            if (!session.Finished || !session.Victory)
                            {
                                director.SettleDefeat(session);
                                break;
                            }

                            director.SettleVictory(session);

                            // 领奖：进攻价值最高的那张
                            if (director.Phase == RunPhase.Reward)
                            {
                                var options = director.BuildRewardChoices(3);
                                if (options.Count > 0)
                                {
                                    if (director.AtDeckCap)
                                    {
                                        int worstId = director.Deck[0].Def.Id;
                                        int worstScore = int.MaxValue;
                                        for (int d = 0; d < director.Deck.Count; d++)
                                        {
                                            int score = Score(director.Deck[d].Def);
                                            if (score < worstScore)
                                            {
                                                worstScore = score;
                                                worstId = director.Deck[d].Def.Id;
                                            }
                                        }

                                        director.RemoveFromDeck(worstId);
                                        swaps++;
                                    }

                                    CardDef best = options[0];
                                    int bestScore = Score(best);
                                    for (int i = 1; i < options.Count; i++)
                                    {
                                        int score = Score(options[i]);
                                        if (score > bestScore)
                                        {
                                            bestScore = score;
                                            best = options[i];
                                        }
                                    }

                                    director.TakeReward(best);
                                    picks++;
                                }

                                director.LeaveNode();
                            }
                        }
                        break;

                    case RunPhase.Rest:
                        if (director.Hp * 100 < director.HpMax * 80)
                        {
                            director.RestHeal();
                        }
                        else
                        {
                            for (int d = 0; d < director.Deck.Count; d++)
                            {
                                if (director.Deck[d].Level == 0)
                                {
                                    director.UpgradeCard(d);
                                    break;
                                }
                            }
                        }

                        director.LeaveNode();
                        break;

                    case RunPhase.Treasure:
                        {
                            var card = director.RollTreasureCard();
                            if (card != null && !director.AtDeckCap)
                            {
                                director.TakeReward(card);
                                picks++;
                            }

                            director.LeaveNode();
                        }
                        break;

                    case RunPhase.Shop:
                        {
                            // 商店：买得起就买第一张，否则删一张最弱的
                            var pool = DeckRules.RewardPool(db, director.Chapter, false);
                            if (pool.Count > 0 && !director.AtDeckCap && director.Gold >= 60)
                            {
                                director.Gold -= 60;
                                director.TakeReward(pool[0]);
                                picks++;
                            }
                            else if (director.Gold >= 100 && director.DeckCount > 12)
                            {
                                int worstId = director.Deck[0].Def.Id;
                                int worstScore = int.MaxValue;
                                for (int d = 0; d < director.Deck.Count; d++)
                                {
                                    int score = Score(director.Deck[d].Def);
                                    if (score < worstScore)
                                    {
                                        worstScore = score;
                                        worstId = director.Deck[d].Def.Id;
                                    }
                                }

                                director.Gold -= 100;
                                director.RemoveFromDeck(worstId);
                                swaps++;
                            }

                            director.LeaveNode();
                        }
                        break;

                    case RunPhase.Event:
                        director.LeaveNode();
                        break;

                    default:
                        director.LeaveNode();
                        break;
                }

                if (director.DeckCount > peakDeck)
                {
                    peakDeck = director.DeckCount;
                }
            }

            return "run: nodes=" + nodes
                 + " battles=" + battles
                 + " picks=" + picks
                 + " swaps=" + swaps
                 + " deck=" + director.DeckCount + "/" + DeckRules.DeckCap
                 + " peakDeck=" + peakDeck
                 + " chapter=" + deepestChapter + "/" + EnemyRoster.TotalChapters
                 + " discovered=" + director.DiscoveredCount + "/" + director.TotalCards
                 + " cleared=" + cleared;
        }

        // ------------------------------------------------------------ 演示操作

        private static void PlayDemo(BattleScreen battle, int plays)
        {
            if (battle == null)
            {
                return;
            }

            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.Player.Energy = 12;
            for (int i = 0; i < plays; i++)
            {
                if (session.Player.Hand.Count == 0 || session.Finished)
                {
                    break;
                }

                if (!session.PlayCard(0))
                {
                    break;
                }
            }

            battle.DebugRefresh();
        }

        private static void HurtPlayer(BattleScreen battle, int amount)
        {
            if (battle == null)
            {
                return;
            }

            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.Player.Energy = session.Player.EnergyMax + 3;
            session.Player.Hp = Mathf.Max(1, session.Player.Hp - amount);
            session.Player.Shield = 9;
            session.Player.SetStatus(StatusKind.Strength, 3);
            session.Player.SetStatus(StatusKind.Poison, 2);
            session.Enemy.Shield = 6;
            session.Enemy.SetStatus(StatusKind.Weak, 2);
            battle.DebugRefresh();
        }

        /// <summary>把手牌灌满，用来验收最大手牌数下的扇形排布。</summary>
        private static void FillHand(BattleScreen battle, int count)
        {
            if (battle == null)
            {
                return;
            }

            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.Player.Energy = 10;
            for (int i = 0; i < count + 6 && session.Player.Hand.Count < count; i++)
            {
                session.Draw(session.Player, 1);
            }

            session.Player.Shield = 5;
            session.Player.SetStatus(StatusKind.Strength, 3);
            session.Player.SetStatus(StatusKind.Regen, 2);
            session.Enemy.Shield = 6;
            session.Enemy.SetStatus(StatusKind.Weak, 2);
            battle.DebugRefresh();
        }

        /// <summary>在指定类型的节点上直接结算一次胜利。</summary>
        private static BattleSession WinBattle(GameApp app, NodeKind kind)
        {
            EnterKind(app, kind);
            var session = app.Director.CreateBattle(777);
            session.Enemy.Hp = 0;
            session.CheckEnd();
            app.Director.SettleVictory(session);
            app.PersistMeta();
            return session;
        }

        private static BattleSession LoseBattle(GameApp app)
        {
            EnterKind(app, NodeKind.Battle);
            var session = app.Director.CreateBattle(778);
            session.Player.Hp = 0;
            session.CheckEnd();
            app.Director.SettleDefeat(session);
            app.PersistMeta();
            return session;
        }

        private static void EnsureRun(GameApp app)
        {
            app.Director.EnsureRun();
        }

        private static void AddByName(GameApp app, List<CardDef> into, string name)
        {
            for (int i = 0; i < app.Cards.All.Count; i++)
            {
                if (app.Cards.All[i].Name == name)
                {
                    into.Add(app.Cards.All[i]);
                    return;
                }
            }
        }
    }
}
