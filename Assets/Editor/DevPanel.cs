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

                case "battle":
                    EnsureRun(app);
                    app.Replace(new BattleScreen());
                    break;

                case "battle-demo":
                    EnsureRun(app);
                    {
                        var battle = new BattleScreen();
                        app.Replace(battle);
                        PlayDemo(battle, 4);
                    }
                    break;

                case "battle-danger":
                    EnsureRun(app);
                    {
                        var battle = new BattleScreen();
                        app.Replace(battle);
                        HurtPlayer(battle, 7);
                    }
                    break;

                case "battle-full":
                    EnsureRun(app);
                    {
                        var battle = new BattleScreen();
                        app.Replace(battle);
                        FillHand(battle, 8);
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
                        // 把界面摆成"正在拖动一张牌"的样子，验收拖放反馈（可打出/魔力不足/区外）
                        var battle = new BattleScreen();
                        app.Replace(battle);
                        FillHand(battle, 6);
                        return battle.DebugDrag(key.Substring(5));
                    }

                case "reward":
                    EnsureRun(app);
                    {
                        var session = WinBattle(app);
                        app.Replace(new RewardScreen(session, app.Director.LastWasMasterStage));
                    }
                    break;

                case "reward-master":
                    EnsureRun(app);
                    {
                        // 推到关底宗师战再结算，用来验收"双轮选牌 + 牌组替换"
                        while (!EnemyRoster.IsMasterStage(app.Director.Battle)
                               && app.Director.Battle < EnemyRoster.FinalBattle)
                        {
                            WinBattle(app);
                        }

                        var session = WinBattle(app);
                        app.Replace(new RewardScreen(session, app.Director.LastWasMasterStage));
                    }
                    break;

                case "reward-full":
                    EnsureRun(app);
                    {
                        // 把牌组灌到上限，验收"牌组已满 → 替换"流程
                        var pool = app.Cards.All;
                        for (int i = 0; app.Director.DeckCount < app.Director.DeckCap && pool.Count > 0; i++)
                        {
                            app.Director.TakeReward(pool[i % pool.Count]);
                        }

                        var session = WinBattle(app);
                        app.Replace(new RewardScreen(session, app.Director.LastWasMasterStage));
                    }
                    break;

                case "result-win":
                    EnsureRun(app);
                    {
                        var session = WinBattle(app);
                        app.Replace(new ResultScreen(true, session, app.Director.Battle));
                    }
                    break;

                case "result-lose":
                    EnsureRun(app);
                    {
                        var session = LoseBattle(app);
                        app.Replace(new ResultScreen(false, session, app.Director.Battle));
                    }
                    break;

                case "codex-cards":
                    app.Replace(new CodexScreen(CodexScreen.Tab.Cards));
                    break;

                case "codex":
                    app.Replace(new CodexScreen(CodexScreen.Tab.Achievements));
                    break;

                case "settings":
                    app.Replace(new SettingsScreen());
                    break;

                case "audio":
                    return AudioCheck();

                default:
                    return "unknown screen: " + screen;
            }

            return "jumped:" + key;
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
            app.Meta.BestLevel = 6;
            app.Meta.BestBattle = 17;
            app.Meta.RunsPlayed = 14;
            app.Meta.Wins = 5;
            app.Meta.Losses = 9;

            app.Meta.AddCounter("score", 18400);
            app.Meta.AddCounter("draw", 520);
            app.Meta.AddCounter("play", 430);
            app.Meta.AddCounter("mana", 980);
            app.Meta.AddCounter("heal", 610);
            app.Meta.Flags.Add("draw_draw");
            app.Meta.Flags.Add("overheat");
            app.Meta.Flags.Add("clear_easy");

            var all = app.Cards.All;
            for (int i = 0; i < all.Count; i += 3)
            {
                app.Meta.Discover(all[i].Id);
            }

            for (int i = 0; i < all.Count; i += 7)
            {
                app.Director.DeckIds.Add(all[i].Id);
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

            app.Director.DeckIds.Clear();
            var all = app.Cards.All;
            for (int i = 0; i < all.Count; i++)
            {
                app.Meta.Discover(all[i].Id);
                if (all[i].Cost <= 3)
                {
                    app.Director.DeckIds.Add(all[i].Id);
                }
            }

            return "deck=" + app.Director.DeckIds.Count;
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
            var perSeries = new System.Collections.Generic.Dictionary<CardSeries, int>();
            for (int i = 0; i < db.All.Count; i++)
            {
                var def = db.All[i];
                if (def.Ops == null || def.Ops.Count == 0)
                {
                    noOps++;
                    sb.Append("\n  [no-ops] ").Append(def.Id).Append(' ').Append(def.Name);
                }

                string text = def.Text();
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

            // 2) 每张卡单独打出一次（取赛程中段的一战作为环境）
            int probeBattle = EnemyRoster.FinalBattle / 2;
            int crashed = 0;
            for (int i = 0; i < db.All.Count; i++)
            {
                var def = db.All[i];
                var deck = new System.Collections.Generic.List<int> { def.Id };
                var session = new BattleSession(db, probeBattle, 4242 + def.Id, deck, EnemyRoster.ForBattle(probeBattle));
                session.Begin();
                session.DebugSetMana(30);

                try
                {
                    for (int k = 0; k < 6 && session.Player.Hand.Count > 0 && !session.Finished; k++)
                    {
                        session.PlayCard(0);
                        session.DebugSetMana(30);
                    }

                    session.PlayerEndTurn();
                    session.EnemyTurnStart();
                    for (int k = 0; k < 8 && !session.Finished; k++)
                    {
                        if (!session.EnemyActOnce())
                        {
                            break;
                        }
                    }

                    session.EnemyTurnEnd();
                }
                catch (System.Exception e)
                {
                    crashed++;
                    sb.Append("\n  [crash] ").Append(def.Id).Append(' ').Append(def.Name)
                      .Append(" :: ").Append(e.GetType().Name).Append(' ').Append(e.Message);
                }
            }

            sb.Append("\ncardCrash=").Append(crashed);

            // 3) 单场收敛性：廿七战逐战取样 × 3 个种子，用固定策略打到底
            int stuck = 0;
            int wins = 0;
            int losses = 0;
            int maxTurns = 0;
            var lossMap = new System.Text.StringBuilder();

            for (int battle = 1; battle <= EnemyRoster.FinalBattle; battle++)
            {
                int battleLosses = 0;
                for (int seed = 0; seed < 3; seed++)
                {
                    var session = new BattleSession(db, battle, 9000 + battle * 97 + seed, null, EnemyRoster.ForBattle(battle));
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
                        battleLosses++;
                    }

                    if (session.TurnNumber > maxTurns)
                    {
                        maxTurns = session.TurnNumber;
                    }
                }

                if (battleLosses > 0)
                {
                    lossMap.Append(' ').Append(battle).Append(':').Append(battleLosses);
                }
            }

            sb.Append("\nbatches=").Append(EnemyRoster.FinalBattle * 3)
              .Append(" stuck=").Append(stuck)
              .Append(" wins=").Append(wins)
              .Append(" losses=").Append(losses)
              .Append(" maxTurns=").Append(maxTurns)
              .Append(" 败点(战:次)=").Append(lossMap.Length > 0 ? lossMap.ToString() : "无");

            // 4) 整局循环：从第 1 战打到通关，验证牌组上限 / 奖励产出 / 宗师战双轮
            sb.Append("\n").Append(RunLoopCheck(db, achievements));

            // 5) 音频资源：键名与目录是否对得上（失配会静默无声）
            sb.Append("\n").Append(AudioCheck());

            return sb.ToString();
        }

        /// <summary>
        /// 模拟玩家打到底：优先出攻击牌，出不动了再出功能牌，最后结束回合。
        /// 比"无脑出第一张"更接近真实操作，用来判断难度曲线是否合理。
        /// </summary>
        private static void RunToEnd(BattleSession session)
        {
            int guard = 0;
            while (!session.Finished && guard < 90)
            {
                guard++;

                for (int k = 0; k < 8 && !session.Finished; k++)
                {
                    int pick = PickCard(session);
                    if (pick < 0)
                    {
                        break;
                    }

                    session.PlayCard(pick);
                    if (session.EndTurnRequested)
                    {
                        break;
                    }
                }

                if (session.Finished)
                {
                    return;
                }

                session.PlayerEndTurn();
                session.EnemyTurnStart();
                for (int k = 0; k < 10 && !session.Finished; k++)
                {
                    if (!session.EnemyActOnce())
                    {
                        break;
                    }
                }

                if (session.Finished)
                {
                    return;
                }

                session.EnemyTurnEnd();
                session.PlayerTurnStart();
                session.TakeEvents();
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

        private static void AccumulateWeight(System.Collections.Generic.List<CardOp> ops, ref int weight)
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
                    case "emanalse":
                    case "esteal":
                    case "stealcard":
                        weight += 8;
                        break;
                }

                AccumulateWeight(op.Then, ref weight);
                AccumulateWeight(op.Else, ref weight);
                AccumulateWeight(op.Hook, ref weight);
            }
        }

        /// <summary>
        /// 跑一整局（廿七战 + 每战拿奖励），检查：
        /// 战斗数是否与卡池节奏匹配、牌组是否守住上限、宗师战是否真的给两轮。
        /// </summary>
        private static string RunLoopCheck(CardDatabase db, AchievementDatabase achievements)
        {
            var director = new RunDirector(db, achievements, new MetaState(), 31337);
            director.StartNewRun();

            int battles = 0;
            int picks = 0;
            int swaps = 0;
            int masterRounds = 0;
            int emptyChoices = 0;
            int retries = 0;
            int failedAt = 0;
            int peakDeck = director.DeckCount;

            while (battles < 60)
            {
                // 重试要吃不同种子，否则"重打一次"等于把同一场败仗回放四遍
                var session = director.CreateBattle(1000 + battles * 31 + retries * 7);
                RunToEnd(session);
                var facts = BattleFacts.Capture(session, 0);

                if (!session.Finished)
                {
                    break;
                }

                if (!session.Victory)
                {
                    director.SettleDefeat(facts);
                    retries++;
                    if (retries > 8)
                    {
                        failedAt = director.Battle;
                        break;
                    }

                    continue;
                }

                retries = 0;
                bool master = EnemyRoster.IsMasterStage(director.Battle);
                director.SettleVictory(facts);
                battles++;

                int rounds = master ? 2 : 1;
                if (master)
                {
                    masterRounds++;
                }

                for (int r = 0; r < rounds; r++)
                {
                    var options = director.BuildRewardChoices(3);
                    if (options.Count == 0)
                    {
                        emptyChoices++;
                        continue;
                    }

                    if (director.AtDeckCap)
                    {
                        // 换掉牌组里进攻价值最低的那张——玩家真会这么干
                        int worstId = director.DeckIds[0];
                        int worstScore = int.MaxValue;
                        for (int d = 0; d < director.DeckIds.Count; d++)
                        {
                            int score = Score(db.Get(director.DeckIds[d]));
                            if (score < worstScore)
                            {
                                worstScore = score;
                                worstId = director.DeckIds[d];
                            }
                        }

                        director.RemoveFromDeck(worstId);
                        swaps++;
                    }

                    // 模拟"普通玩家"：进攻价值优先，同分再看费用
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

                if (director.DeckCount > peakDeck)
                {
                    peakDeck = director.DeckCount;
                }

                if (director.JustClearedRun)
                {
                    break;
                }
            }

            return "run: battles=" + battles
                 + " picks=" + picks
                 + " swaps=" + swaps
                 + " masterStages=" + masterRounds
                 + " emptyChoices=" + emptyChoices
                 + " deck=" + director.DeckCount + "/" + director.DeckCap
                 + " peakDeck=" + peakDeck
                 + " discovered=" + director.DiscoveredCount + "/" + director.TotalCards
                 + " cleared=" + director.JustClearedRun
                 + " failedAt=" + failedAt;
        }

        // ------------------------------------------------------------ 演示操作

        private static void PlayDemo(BattleScreen battle, int plays)
        {
            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.DebugSetMana(12);
            for (int i = 0; i < plays; i++)
            {
                if (session.Player.Hand.Count == 0 || session.Finished)
                {
                    break;
                }

                int index = 0;
                for (int k = 0; k < session.Player.Hand.Count; k++)
                {
                    if (session.Player.Hand[k].Cost <= session.Player.Mana)
                    {
                        index = k;
                        break;
                    }
                }

                if (!session.PlayCard(index))
                {
                    break;
                }
            }

            battle.DebugRefresh();
        }

        private static void HurtPlayer(BattleScreen battle, int amount)
        {
            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.DebugGrantMana(6);
            session.Player.Hp = Mathf.Max(1, session.Player.Hp - amount);
            session.Player.Shield = 9;
            session.Player.AddBuff(BuffKind.DmgFlatUp, 3, 2);
            session.Player.AddHook(HookTiming.EndTurn, new System.Collections.Generic.List<CardOp>
            {
                new CardOp { Kind = "dmg", A = 2 }
            }, 3, "流血");
            session.Enemy.Shield = 6;
            session.Enemy.AddBuff(BuffKind.DmgFlatDown, 2, 2);
            battle.DebugRefresh();
        }

        /// <summary>把手牌灌满，用来验收最大手牌数下的扇形排布。</summary>
        private static void FillHand(BattleScreen battle, int count)
        {
            var session = battle.DebugSession;
            if (session == null)
            {
                return;
            }

            session.DebugSetMana(10);
            for (int i = 0; i < count + 6 && session.Player.Hand.Count < count; i++)
            {
                session.Draw(session.Player, 1, false);
            }

            session.Player.Shield = 5;
            session.Player.AddBuff(BuffKind.DmgFlatUp, 3, 2);
            session.Player.AddBuff(BuffKind.HealFlatUp, 2, 3);
            session.Player.AddHook(HookTiming.EndTurn, new System.Collections.Generic.List<CardOp>
            {
                new CardOp { Kind = "dmg", A = 2 }
            }, 3, "流血");
            session.Enemy.Shield = 6;
            session.Enemy.AddBuff(BuffKind.DmgFlatDown, 2, 2);
            battle.DebugRefresh();
        }

        private static BattleSession WinBattle(GameApp app)
        {
            var session = app.Director.CreateBattle(777);
            session.Enemy.Hp = 0;
            session.CheckEnd();
            var facts = BattleFacts.Capture(session, 0);
            app.Director.SettleVictory(facts);
            app.PersistMeta();
            return session;
        }

        private static BattleSession LoseBattle(GameApp app)
        {
            var session = app.Director.CreateBattle(778);
            session.Player.Hp = 0;
            session.CheckEnd();
            app.Director.SettleDefeat(BattleFacts.Capture(session, 12));
            app.PersistMeta();
            return session;
        }

        private static void EnsureRun(GameApp app)
        {
            if (app.Director.DeckIds.Count == 0)
            {
                app.Director.StartNewRun();
            }
        }
    }
}
