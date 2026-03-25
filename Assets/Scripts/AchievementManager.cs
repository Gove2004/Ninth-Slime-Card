using System;
using System.Collections.Generic;
using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }
    public enum AchievementType
    {
        Score,
        Draw,
        Play,
        Mana,
        Heal,
        Custom
    }

    public class AchievementDefinition
    {
        public string id;
        public string name;
        public AchievementType type;
        public ulong threshold;
        public string description;
    }

    public class AchievementStatus
    {
        public string id;
        public string name;
        public string description;
        public bool completed;
    }

    public class AchievementUnlockedInfo
    {
        public string id;
        public string name;
        public string description;
    }

    private const string TotalScoreKey = "Achievement_TotalScore";
    private const string TotalDrawKey = "Achievement_TotalDraw";
    private const string TotalPlayKey = "Achievement_TotalPlay";
    private const string TotalManaKey = "Achievement_TotalMana";
    private const string TotalHealKey = "Achievement_TotalHeal";
    private const string CustomAchievementPrefix = "Achievement_Custom_";
    private const string BattleStartAchievementId = "test";
    private const int BattleStartAchievementTapTapSteps = 3;
    private const float TapTapFlushIntervalSeconds = 5f;
    private const ulong TapTapStepAchievementMaxThreshold = 100000000UL;
    private const int TapTapIncrementChunkMaxStep = 10000;

    public ulong TotalScore { get; private set; }
    public ulong TotalDraw { get; private set; }
    public ulong TotalPlay { get; private set; }
    public ulong TotalMana { get; private set; }
    public ulong TotalHeal { get; private set; }

    private readonly List<AchievementDefinition> definitions = new();
    private readonly HashSet<string> customCompleted = new();
    private readonly Dictionary<string, ulong> pendingTapTapIncrements = new();
    private bool pendingStartupTapTapResync = true;
    private float nextTapTapFlushTime;
    private static readonly ulong[] NumericMilestones =
    {
        10UL,
        100UL,
        1000UL,
        10000UL,
        100000UL,
        1000000UL,
        10000000UL,
        100000000UL,
        1000000000UL,
        10000000000UL,
        100000000000UL,
        1000000000000UL
    };
    private static readonly string[] ScoreTitles =
    {
        "小试牛刀",
        "越打越顺",
        "火力全开",
        "刀刀见血",
        "战意沸腾",
        "人形轰炸机",
        "势不可挡",
        "毁灭节拍",
        "战场主宰",
        "一击清屏",
        "神挡杀神",
        "牛逼哄哄"
    };
    private static readonly string[] DrawTitles =
    {
        "摸牌起手",
        "牌感在线",
        "连抽不断",
        "手速飞快",
        "牌库老友",
        "抽牌成瘾",
        "过牌机器",
        "卡海漫游",
        "抽到手软",
        "牌局导演",
        "天胡常客",
        "万牌归宗"
    };
    private static readonly string[] PlayTitles =
    {
        "初次落子",
        "连招入门",
        "出牌流畅",
        "节奏拉满",
        "回合艺术家",
        "操作怪",
        "手牌指挥官",
        "连锁反应",
        "战术宗师",
        "一回合剧本",
        "牌局编年史",
        "万法归一"
    };
    private static readonly string[] ManaTitles =
    {
        "蓝量起步",
        "法力汹涌",
        "源能奔流",
        "魔潮涌动",
        "能量暴涨",
        "法力熔炉",
        "奥术引擎",
        "无穷供能",
        "蓝海翻腾",
        "永动核心",
        "星核供电",
        "法力天灾"
    };
    private static readonly string[] HealTitles =
    {
        "止血成功",
        "小有起色",
        "稳住阵脚",
        "起死回生",
        "再战三百回",
        "续航怪物",
        "血条重铸",
        "不灭意志",
        "生机洪流",
        "永生错觉",
        "涅槃往复",
        "不朽之躯"
    };
    private Action onDrawUnsub;
    private Action onPlayUnsub;
    private Action onManaUnsub;
    private Action onHealUnsub;
    private Action onDrawDrawUnsub;
    private Action onOverheatUnsub;
    private Action onPlayerCardResolvedUnsub;
    private Action onDoubleAgentUnsub;
    private Action onEnemyDeadUnsub;
    private Action onClearEasyUnsub;
    private Action onClearHardUnsub;
    private Action onClearHellUnsub;
    private Action onBattleStartedUnsub;
    private Action onBattleEndedUnsub;
    private BaseCharacter trackedPlayerDamageSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        BuildDefinitions();
        LoadCustom();
        AchievementToast.EnsureInstance();
        UnlockCustom(BattleStartAchievementId);
        RegisterEvents();
        SubscribeToPlayerDamageSource();
        nextTapTapFlushTime = Time.unscaledTime + TapTapFlushIntervalSeconds;
    }

    private void Update()
    {
        if (pendingStartupTapTapResync && TryResyncCompletedAchievementsToTapTapOnStartup())
        {
            pendingStartupTapTapResync = false;
        }

        if (Time.unscaledTime < nextTapTapFlushTime) return;
        FlushPendingTapTapIncrements();
        nextTapTapFlushTime = Time.unscaledTime + TapTapFlushIntervalSeconds;
    }

    private void OnDestroy()
    {
        FlushPendingTapTapIncrements();
        onDrawUnsub?.Invoke();
        onPlayUnsub?.Invoke();
        onManaUnsub?.Invoke();
        onHealUnsub?.Invoke();
        onDrawDrawUnsub?.Invoke();
        onOverheatUnsub?.Invoke();
        onPlayerCardResolvedUnsub?.Invoke();
        onDoubleAgentUnsub?.Invoke();
        onEnemyDeadUnsub?.Invoke();
        onClearEasyUnsub?.Invoke();
        onClearHardUnsub?.Invoke();
        onClearHellUnsub?.Invoke();
        onBattleStartedUnsub?.Invoke();
        onBattleEndedUnsub?.Invoke();
        UnsubscribeFromPlayerDamageSource();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) return;
        FlushPendingTapTapIncrements();
    }

    private void OnApplicationQuit()
    {
        FlushPendingTapTapIncrements();
    }

    public void AddScore(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalScore;
        TotalScore = BaseCharacter.SaturatingAdd(TotalScore, amount);
        SyncTapTapNumericProgress(AchievementType.Score, before, TotalScore);
        TryNotifyNumericUnlock(AchievementType.Score, before, TotalScore);
        Save();
    }

    public void AddDraw(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalDraw;
        TotalDraw = BaseCharacter.SaturatingAdd(TotalDraw, amount);
        SyncTapTapNumericProgress(AchievementType.Draw, before, TotalDraw);
        TryNotifyNumericUnlock(AchievementType.Draw, before, TotalDraw);
        Save();
    }

    public void AddPlay(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalPlay;
        TotalPlay = BaseCharacter.SaturatingAdd(TotalPlay, amount);
        SyncTapTapNumericProgress(AchievementType.Play, before, TotalPlay);
        TryNotifyNumericUnlock(AchievementType.Play, before, TotalPlay);
        Save();
    }

    public void AddMana(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalMana;
        TotalMana = BaseCharacter.SaturatingAdd(TotalMana, amount);
        SyncTapTapNumericProgress(AchievementType.Mana, before, TotalMana);
        TryNotifyNumericUnlock(AchievementType.Mana, before, TotalMana);
        Save();
    }

    public void AddHeal(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalHeal;
        TotalHeal = BaseCharacter.SaturatingAdd(TotalHeal, amount);
        SyncTapTapNumericProgress(AchievementType.Heal, before, TotalHeal);
        TryNotifyNumericUnlock(AchievementType.Heal, before, TotalHeal);
        Save();
    }

    public List<AchievementStatus> GetAchievementStatuses()
    {
        var list = new List<AchievementStatus>();
        foreach (var def in definitions)
        {
            list.Add(new AchievementStatus
            {
                id = def.id,
                name = def.name,
                description = GetDescription(def),
                completed = IsCompleted(def)
            });
        }
        return list;
    }

    private void RegisterEvents()
    {
        onDrawUnsub = EventCenter.Register<CardEventContext>(GameEvents.PlayerCardDrawnToHand, context =>
        {
            AddDraw(1);
            TryUnlockDrawDraw(context);
        });
        onPlayUnsub = EventCenter.Register<CardEventContext>(GameEvents.PlayerCardPlayedFromHand, _ => AddPlay(1));
        onManaUnsub = EventCenter.Register<CharacterValueEventContext>(GameEvents.PlayerGainedMana, context =>
        {
            AddMana(context.Amount);
        });
        onHealUnsub = EventCenter.Register<CharacterValueEventContext>(GameEvents.PlayerHealed, context =>
        {
            AddHeal(context.Amount);
        });
        onDrawDrawUnsub = EventCenter.Register(GameEvents.AchievementDrawDrawTriggered, () => UnlockCustom("draw_draw"));
        onOverheatUnsub = EventCenter.Register(GameEvents.AchievementOverheatTriggered, () => UnlockCustom("overheat"));
        onDoubleAgentUnsub = EventCenter.Register(GameEvents.AchievementDoubleAgentTriggered, () => UnlockCustom("double_agent"));
        onPlayerCardResolvedUnsub = EventCenter.Register<CardEventContext>(GameEvents.PlayerCardResolved, context =>
        {
            if (context.Card != null && context.Card.Cost >= 1024) EventCenter.Publish(GameEvents.AchievementOverheatTriggered);
        });
        onEnemyDeadUnsub = EventCenter.Register<CharacterEventContext>(GameEvents.EnemyDefeated, _ =>
        {
            if (GameManager.Instance == null) return;

            switch (GameManager.Instance.difficultyLevel)
            {
                case 1:
                    EventCenter.Publish(GameEvents.AchievementClearEasyTriggered);
                    break;
                case 2:
                    EventCenter.Publish(GameEvents.AchievementClearHardTriggered);
                    break;
                case 3:
                    EventCenter.Publish(GameEvents.AchievementClearHellTriggered);
                    break;
            }
        });
        onClearEasyUnsub = EventCenter.Register(GameEvents.AchievementClearEasyTriggered, () => UnlockCustom("clear_easy"));
        onClearHardUnsub = EventCenter.Register(GameEvents.AchievementClearHardTriggered, () => UnlockCustom("clear_hard"));
        onClearHellUnsub = EventCenter.Register(GameEvents.AchievementClearHellTriggered, () => UnlockCustom("clear_hell"));
        onBattleStartedUnsub = EventCenter.Register<BattleEventContext>(GameEvents.BattleStarted, _ =>
        {
            UnlockCustom(BattleStartAchievementId);
            SubscribeToPlayerDamageSource();
        });
        onBattleEndedUnsub = EventCenter.Register<BattleEventContext>(GameEvents.BattleEnded, _ => UnsubscribeFromPlayerDamageSource());
    }

    private void TryUnlockDrawDraw(CardEventContext context)
    {
        if (context?.Card == null) return;
        BaseCard drawnCard = context.Card;
        if (drawnCard.Name != "抽牌") return;

        BaseCard sourceCard = BaseCharacter.ActiveCardContext ?? BaseCharacter.ActiveDotContext?.sourceCard;
        if (sourceCard == null) return;
        if (sourceCard.Name != "抽牌") return;

        EventCenter.Publish(GameEvents.AchievementDrawDrawTriggered);
    }

    private void SubscribeToPlayerDamageSource()
    {
        UnsubscribeFromPlayerDamageSource();

        if (BattleManager.Instance == null) return;
        trackedPlayerDamageSource = BattleManager.Instance.player;
        if (trackedPlayerDamageSource == null) return;

        trackedPlayerDamageSource.DamageDealt -= OnPlayerDamageDealt;
        trackedPlayerDamageSource.DamageDealt += OnPlayerDamageDealt;
        trackedPlayerDamageSource.DamageTaken -= OnPlayerDamageTaken;
        trackedPlayerDamageSource.DamageTaken += OnPlayerDamageTaken;
    }

    private void UnsubscribeFromPlayerDamageSource()
    {
        if (trackedPlayerDamageSource == null) return;
        trackedPlayerDamageSource.DamageDealt -= OnPlayerDamageDealt;
        trackedPlayerDamageSource.DamageTaken -= OnPlayerDamageTaken;
        trackedPlayerDamageSource = null;
    }

    private void OnPlayerDamageDealt(ulong amount, BaseCharacter victim)
    {
        if (amount == 0) return;
        if (BattleManager.Instance == null) return;
        if (!ReferenceEquals(victim, BattleManager.Instance.enemy)) return;

        AddScore(amount);
    }

    private void OnPlayerDamageTaken(ulong amount, BaseCharacter source)
    {
        if (amount == 0) return;
        if (trackedPlayerDamageSource == null) return;

        BaseCard sourceCard = trackedPlayerDamageSource.LastDamageCard;
        if (sourceCard == null || !sourceCard.IsStolenFromOpponent) return;

        EventCenter.Publish(GameEvents.AchievementDoubleAgentTriggered);
    }

    private void BuildDefinitions()
    {
        if (definitions.Count > 0) return;

        AddPowerOfTenDefs("score", "伤害", AchievementType.Score, ScoreTitles);
        AddPowerOfTenDefs("draw", "抽牌", AchievementType.Draw, DrawTitles);
        AddPowerOfTenDefs("play", "出牌", AchievementType.Play, PlayTitles);
        AddPowerOfTenDefs("mana", "法力", AchievementType.Mana, ManaTitles);
        AddPowerOfTenDefs("heal", "治疗", AchievementType.Heal, HealTitles);

        AddDef(BattleStartAchievementId, "dddd", AchievementType.Custom, 1, "进入战斗");
        AddDef("draw_draw", "抽抽爆", AchievementType.Custom, 1, "用“抽牌”抽到“抽牌”");
        AddDef("overheat", "过热", AchievementType.Custom, 1, "打出一张魔力消耗不小于1024的卡牌");
        AddDef("double_agent", "双面间谍", AchievementType.Custom, 1, "受到从对手处偷到的卡牌的伤害");
        
        AddDef("clear_easy", "初战告捷", AchievementType.Custom, 1, "完成简单模式");
        AddDef("clear_hard", "逆境破局", AchievementType.Custom, 1, "完成困难模式");
        AddDef("clear_hell", "地狱征服者", AchievementType.Custom, 1, "完成地狱模式");
    }

    private void AddPowerOfTenDefs(string idPrefix, string displayPrefix, AchievementType type, string[] titles)
    {
        for (int i = 0; i < NumericMilestones.Length; i++)
        {
            var threshold = NumericMilestones[i];
            string title = (titles != null && i < titles.Length && !string.IsNullOrWhiteSpace(titles[i]))
                ? titles[i]
                : $"{displayPrefix}里程碑 {threshold}";
            AddDef($"{idPrefix}_{threshold}", title, type, threshold);
        }
    }

    private void AddDef(string id, string name, AchievementType type, ulong threshold, string description = null)
    {
        definitions.Add(new AchievementDefinition
        {
            id = id,
            name = name,
            type = type,
            threshold = threshold,
            description = description
        });
    }

    private bool IsCompleted(AchievementDefinition def)
    {
        ulong value = def.type switch
        {
            AchievementType.Score => TotalScore,
            AchievementType.Draw => TotalDraw,
            AchievementType.Play => TotalPlay,
            AchievementType.Mana => TotalMana,
            AchievementType.Heal => TotalHeal,
            AchievementType.Custom => customCompleted.Contains(def.id) ? 1UL : 0UL,
            _ => 0
        };
        return value >= def.threshold;
    }

    private string GetDescription(AchievementDefinition def)
    {
        if (!string.IsNullOrEmpty(def.description)) return def.description;
        return def.type switch
        {
            AchievementType.Score => $"累计造成伤害达到{def.threshold}",
            AchievementType.Draw => $"累计抽牌达到{def.threshold}",
            AchievementType.Play => $"累计出牌达到{def.threshold}",
            AchievementType.Mana => $"累计获得魔力达到{def.threshold}",
            AchievementType.Heal => $"累计生命恢复达到{def.threshold}",
            _ => ""
        };
    }

    private void Save()
    {
        PlayerPrefs.SetString(TotalScoreKey, TotalScore.ToString());
        PlayerPrefs.SetString(TotalDrawKey, TotalDraw.ToString());
        PlayerPrefs.SetString(TotalPlayKey, TotalPlay.ToString());
        PlayerPrefs.SetString(TotalManaKey, TotalMana.ToString());
        PlayerPrefs.SetString(TotalHealKey, TotalHeal.ToString());
        PlayerPrefs.Save();
    }

    private void Load()
    {
        TotalScore = ParseUlongOrZero(PlayerPrefs.GetString(TotalScoreKey, "0"));
        TotalDraw = ParseUlongOrZero(PlayerPrefs.GetString(TotalDrawKey, "0"));
        TotalPlay = ParseUlongOrZero(PlayerPrefs.GetString(TotalPlayKey, "0"));
        TotalMana = ParseUlongOrZero(PlayerPrefs.GetString(TotalManaKey, "0"));
        TotalHeal = ParseUlongOrZero(PlayerPrefs.GetString(TotalHealKey, "0"));
    }

    private void LoadCustom()
    {
        customCompleted.Clear();
        foreach (var def in definitions)
        {
            if (def.type != AchievementType.Custom) continue;
            if (PlayerPrefs.GetInt(CustomAchievementPrefix + def.id, 0) == 1)
            {
                customCompleted.Add(def.id);
            }
        }
    }

    public bool UnlockCustomAchievement(string id)
    {
        return UnlockCustom(id);
    }

    private bool UnlockCustom(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var def = definitions.Find(d => d.id == id);
        if (customCompleted.Contains(id))
        {
            SyncTapTapAchievement(def);
            return false;
        }
        customCompleted.Add(id);
        NotifyAchievementUnlocked(def);
        PlayerPrefs.SetInt(CustomAchievementPrefix + id, 1);
        PlayerPrefs.Save();
        return true;
    }

    private void TryNotifyNumericUnlock(AchievementType type, ulong beforeValue, ulong afterValue)
    {
        if (afterValue <= beforeValue) return;
        foreach (var def in definitions)
        {
            if (def.type != type) continue;
            if (beforeValue < def.threshold && afterValue >= def.threshold)
            {
                NotifyAchievementUnlocked(def);
            }
        }
    }

    private void NotifyAchievementUnlocked(AchievementDefinition def)
    {
        if (def == null) return;
        EventCenter.Publish(GameEvents.AchievementUnlocked, new AchievementUnlockedInfo
        {
            id = def.id,
            name = def.name,
            description = GetDescription(def)
        });
        SyncTapTapAchievement(def);
    }

    private void SyncTapTapAchievement(AchievementDefinition def)
    {
        if (def == null) return;
        if (!TapTapSdk.IsInitialized) return;

        try
        {
            if (def.type != AchievementType.Custom) return;
            int completionSteps = GetTapTapCustomCompletionSteps(def.id);
            if (completionSteps > 0)
            {
                TapTapSdk.Instance.IncrementAchievement(def.id, completionSteps);
                return;
            }

            TapTapSdk.Instance.UnlockAchievement(def.id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"同步 TapTap 成就失败：{def.id}，错误：{ex.Message}");
        }
    }

    private void SyncTapTapNumericProgress(AchievementType type, ulong beforeValue, ulong afterValue)
    {
        if (afterValue <= beforeValue) return;

        foreach (var def in definitions)
        {
            if (def.type != type) continue;
            if (def.type == AchievementType.Custom) continue;
            if (def.threshold > TapTapStepAchievementMaxThreshold) continue;

            ulong clampedBefore = beforeValue >= def.threshold ? def.threshold : beforeValue;
            ulong clampedAfter = afterValue >= def.threshold ? def.threshold : afterValue;
            if (clampedAfter <= clampedBefore) continue;

            ulong delta = clampedAfter - clampedBefore;
            QueueTapTapIncrement(def.id, delta);
        }
    }

    private bool TryResyncCompletedAchievementsToTapTapOnStartup()
    {
        if (!TapTapSdk.IsInitialized) return false;

        foreach (var def in definitions)
        {
            if (!IsCompleted(def)) continue;

            try
            {
                SyncTapTapAchievement(def);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"启动时重同步 TapTap 成就失败：{def.id}，错误：{ex.Message}");
            }
        }

        FlushPendingTapTapIncrements();
        return true;
    }

    private void QueueTapTapIncrement(string achievementId, ulong delta)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (delta == 0) return;

        if (pendingTapTapIncrements.TryGetValue(achievementId, out ulong existing))
        {
            pendingTapTapIncrements[achievementId] = BaseCharacter.SaturatingAdd(existing, delta);
            return;
        }

        pendingTapTapIncrements[achievementId] = delta;
    }

    private void FlushPendingTapTapIncrements()
    {
        if (pendingTapTapIncrements.Count == 0) return;
        if (!TapTapSdk.IsInitialized) return;

        var keys = new List<string>(pendingTapTapIncrements.Keys);
        foreach (string id in keys)
        {
            if (!pendingTapTapIncrements.TryGetValue(id, out ulong delta) || delta == 0)
            {
                pendingTapTapIncrements.Remove(id);
                continue;
            }

            ulong remaining = IncrementTapTapByChunks(id, delta);
            if (remaining == 0)
            {
                pendingTapTapIncrements.Remove(id);
            }
            else
            {
                pendingTapTapIncrements[id] = remaining;
            }
        }
    }

    private ulong IncrementTapTapByChunks(string achievementId, ulong delta)
    {
        if (string.IsNullOrEmpty(achievementId)) return 0;

        ulong remaining = delta;
        while (remaining > 0)
        {
            int step = remaining > (ulong)TapTapIncrementChunkMaxStep ? TapTapIncrementChunkMaxStep : (int)remaining;
            try
            {
                TapTapSdk.Instance.IncrementAchievement(achievementId, step);
                remaining -= (ulong)step;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"同步 TapTap 数值成就进度失败：{achievementId}，step：{step}，错误：{ex.Message}");
                break;
            }
        }

        return remaining;
    }

    private static int GetTapTapCustomCompletionSteps(string achievementId)
    {
        return achievementId == BattleStartAchievementId ? BattleStartAchievementTapTapSteps : 0;
    }

    private static ulong ParseUlongOrZero(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return ulong.TryParse(value, out var parsed) ? parsed : 0;
    }
}
