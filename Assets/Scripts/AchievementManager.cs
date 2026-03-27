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
        public ulong displayThreshold;
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
    private const string TapTapAchievementSyncedPrefix = "Achievement_TapTapSynced_";
    private const string TapTapNumericSyncedPrefix = "Achievement_TapTapNumericSynced_";
    private const string BattleStartAchievementId = "test";
    private const int BattleStartAchievementTapTapSteps = 3;
    private const float PlayerPrefsFlushIntervalSeconds = 5f;
    private const float TapTapFlushIntervalSeconds = 5f;
    private const ulong TapTapStepAchievementMaxThreshold = 100000000UL;
    private const int TapTapIncrementChunkMaxStep = 10000;
    private const int TapTapIncrementChunkBudgetPerFlush = 8;
    private const ulong DrawAchievementCountMultiplier = 100000UL;
    private const ulong PlayAchievementCountMultiplier = 1000000UL;
    private const ulong ManaAchievementCountMultiplier = 10000UL;

    public ulong TotalScore { get; private set; }
    public ulong TotalDraw { get; private set; }
    public ulong TotalPlay { get; private set; }
    public ulong TotalMana { get; private set; }
    public ulong TotalHeal { get; private set; }

    private readonly List<AchievementDefinition> definitions = new();
    private readonly Dictionary<AchievementType, List<AchievementDefinition>> definitionsByType = new();
    private readonly Dictionary<string, AchievementDefinition> definitionsById = new(StringComparer.Ordinal);
    private readonly HashSet<string> customCompleted = new();
    private readonly HashSet<string> tapTapAchievementSynced = new(StringComparer.Ordinal);
    private readonly HashSet<string> pendingTapTapAchievementSync = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> tapTapSyncedNumericProgress = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> pendingTapTapIncrements = new();
    private bool pendingTitleTapTapResync;
    private bool hasCompletedTitleTapTapResync;
    private bool hasPendingPlayerPrefsSave;
    private float nextPlayerPrefsFlushTime;
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
        BuildDefinitions();
        Load();
        LoadCustom();
        LoadTapTapSyncState();
        AchievementToast.EnsureInstance();
        RegisterEvents();
        SubscribeToPlayerDamageSource();
        TapTapSdk.AchievementSucceeded += OnTapTapAchievementSuccess;
        TapTapSdk.AchievementFailed += OnTapTapAchievementFailure;
        nextPlayerPrefsFlushTime = Time.unscaledTime + PlayerPrefsFlushIntervalSeconds;
        nextTapTapFlushTime = Time.unscaledTime + TapTapFlushIntervalSeconds;
    }

    private void Update()
    {
        if (pendingTitleTapTapResync &&
            !hasCompletedTitleTapTapResync &&
            IsReadyToSyncTitleAchievements() &&
            TryResyncCompletedAchievementsToTapTapOnTitle())
        {
            pendingTitleTapTapResync = false;
            hasCompletedTitleTapTapResync = true;
        }

        if (hasPendingPlayerPrefsSave && Time.unscaledTime >= nextPlayerPrefsFlushTime)
        {
            FlushPlayerPrefs();
        }

        if (Time.unscaledTime >= nextTapTapFlushTime)
        {
            FlushPendingTapTapIncrements();
            nextTapTapFlushTime = Time.unscaledTime + TapTapFlushIntervalSeconds;
        }
    }

    private void OnDestroy()
    {
        FlushPendingTapTapIncrements(true);
        FlushPlayerPrefs();
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
        TapTapSdk.AchievementSucceeded -= OnTapTapAchievementSuccess;
        TapTapSdk.AchievementFailed -= OnTapTapAchievementFailure;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) return;
        FlushPendingTapTapIncrements(true);
        FlushPlayerPrefs();
    }

    private void OnApplicationQuit()
    {
        FlushPendingTapTapIncrements(true);
        FlushPlayerPrefs();
    }

    public void AddScore(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalScore;
        TotalScore = BaseCharacter.SaturatingAdd(TotalScore, amount);
        SyncTapTapNumericProgress(AchievementType.Score, before, TotalScore);
        TryNotifyNumericUnlock(AchievementType.Score, before, TotalScore);
        SaveCounter(TotalScoreKey, TotalScore);
    }

    public void AddDraw(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalDraw;
        ulong delta = BaseCharacter.SaturatingMultiply(amount, DrawAchievementCountMultiplier);
        TotalDraw = BaseCharacter.SaturatingAdd(TotalDraw, delta);
        SyncTapTapNumericProgress(AchievementType.Draw, before, TotalDraw);
        TryNotifyNumericUnlock(AchievementType.Draw, before, TotalDraw);
        SaveCounter(TotalDrawKey, TotalDraw);
    }

    public void AddPlay(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalPlay;
        ulong delta = BaseCharacter.SaturatingMultiply(amount, PlayAchievementCountMultiplier);
        TotalPlay = BaseCharacter.SaturatingAdd(TotalPlay, delta);
        SyncTapTapNumericProgress(AchievementType.Play, before, TotalPlay);
        TryNotifyNumericUnlock(AchievementType.Play, before, TotalPlay);
        SaveCounter(TotalPlayKey, TotalPlay);
    }

    public void AddMana(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalMana;
        ulong delta = BaseCharacter.SaturatingMultiply(amount, ManaAchievementCountMultiplier);
        TotalMana = BaseCharacter.SaturatingAdd(TotalMana, delta);
        SyncTapTapNumericProgress(AchievementType.Mana, before, TotalMana);
        TryNotifyNumericUnlock(AchievementType.Mana, before, TotalMana);
        SaveCounter(TotalManaKey, TotalMana);
    }

    public void AddHeal(ulong amount)
    {
        if (amount == 0) return;
        ulong before = TotalHeal;
        TotalHeal = BaseCharacter.SaturatingAdd(TotalHeal, amount);
        SyncTapTapNumericProgress(AchievementType.Heal, before, TotalHeal);
        TryNotifyNumericUnlock(AchievementType.Heal, before, TotalHeal);
        SaveCounter(TotalHealKey, TotalHeal);
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

    public void NotifyEnteredTitleScreen()
    {
        if (hasCompletedTitleTapTapResync) return;

        pendingTitleTapTapResync = true;
        if (!IsReadyToSyncTitleAchievements()) return;
        if (!TryResyncCompletedAchievementsToTapTapOnTitle()) return;

        pendingTitleTapTapResync = false;
        hasCompletedTitleTapTapResync = true;
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
            ulong displayThreshold = NumericMilestones[i];
            ulong threshold = ApplyCountMultiplier(type, displayThreshold);
            string title = (titles != null && i < titles.Length && !string.IsNullOrWhiteSpace(titles[i]))
                ? titles[i]
                : $"{displayPrefix}里程碑 {displayThreshold}";
            AddDef($"{idPrefix}_{displayThreshold}", title, type, threshold, null, displayThreshold);
        }
    }

    private void AddDef(string id, string name, AchievementType type, ulong threshold, string description = null, ulong? displayThreshold = null)
    {
        var definition = new AchievementDefinition
        {
            id = id,
            name = name,
            type = type,
            threshold = threshold,
            displayThreshold = displayThreshold ?? threshold,
            description = description
        };

        definitions.Add(definition);
        definitionsById[id] = definition;
        if (!definitionsByType.TryGetValue(type, out var typedDefinitions))
        {
            typedDefinitions = new List<AchievementDefinition>();
            definitionsByType[type] = typedDefinitions;
        }
        typedDefinitions.Add(definition);
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
            AchievementType.Score => $"累计造成伤害达到{def.displayThreshold}",
            AchievementType.Draw => $"累计抽牌达到{def.displayThreshold}",
            AchievementType.Play => $"累计出牌达到{def.displayThreshold}",
            AchievementType.Mana => $"累计获得魔力达到{def.displayThreshold}",
            AchievementType.Heal => $"累计生命恢复达到{def.displayThreshold}",
            _ => ""
        };
    }

    private void SaveCounter(string key, ulong value)
    {
        PlayerPrefs.SetString(key, value.ToString());
        MarkPlayerPrefsDirty();
    }

    private void Load()
    {
        TotalScore = LoadCounter(TotalScoreKey, AchievementType.Score);
        TotalDraw = LoadCounter(TotalDrawKey, AchievementType.Draw);
        TotalPlay = LoadCounter(TotalPlayKey, AchievementType.Play);
        TotalMana = LoadCounter(TotalManaKey, AchievementType.Mana);
        TotalHeal = LoadCounter(TotalHealKey, AchievementType.Heal);
    }

    private void LoadCustom()
    {
        customCompleted.Clear();
        foreach (var def in GetDefinitionsForType(AchievementType.Custom))
        {
            if (PlayerPrefs.GetInt(CustomAchievementPrefix + def.id, 0) == 1)
            {
                customCompleted.Add(def.id);
            }
        }
    }

    private void LoadTapTapSyncState()
    {
        tapTapAchievementSynced.Clear();
        tapTapSyncedNumericProgress.Clear();

        foreach (var def in definitions)
        {
            bool storedAsSynced = PlayerPrefs.GetInt(TapTapAchievementSyncedPrefix + def.id, 0) == 1;
            if (storedAsSynced && def.type == AchievementType.Custom)
            {
                tapTapAchievementSynced.Add(def.id);
            }

            if (def.type == AchievementType.Custom) continue;

            ulong progress = NormalizeStoredProgress(def.type, ParseUlongOrZero(PlayerPrefs.GetString(TapTapNumericSyncedPrefix + def.id, "0")));
            ulong currentValue = GetAchievementValue(def.type);
            if (progress > currentValue)
            {
                progress = currentValue;
            }
            if (progress > def.threshold)
            {
                progress = def.threshold;
            }
            if (progress > 0)
            {
                tapTapSyncedNumericProgress[def.id] = progress;
            }

            if (storedAsSynced && currentValue >= def.threshold)
            {
                tapTapAchievementSynced.Add(def.id);
            }
        }
    }

    public bool UnlockCustomAchievement(string id)
    {
        return UnlockCustom(id);
    }

    public bool HasAchievement(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return definitionsById.ContainsKey(id);
    }

    private bool UnlockCustom(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!definitionsById.TryGetValue(id, out var def) || def == null || def.type != AchievementType.Custom)
        {
            Debug.LogWarning($"尝试解锁未定义的特殊成就：{id}");
            return false;
        }
        if (customCompleted.Contains(id))
        {
            return false;
        }
        customCompleted.Add(id);
        NotifyAchievementUnlocked(def);
        PlayerPrefs.SetInt(CustomAchievementPrefix + id, 1);
        MarkPlayerPrefsDirty();
        return true;
    }

    private void TryNotifyNumericUnlock(AchievementType type, ulong beforeValue, ulong afterValue)
    {
        if (afterValue <= beforeValue) return;
        foreach (var def in GetDefinitionsForType(type))
        {
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

    private void SyncTapTapAchievement(AchievementDefinition def, bool force = false)
    {
        if (def == null) return;
        if (!TapTapSdk.IsInitialized) return;
        if (!force && IsTapTapAchievementSynced(def.id)) return;

        try
        {
            if (def.type == AchievementType.Custom)
            {
                int completionSteps = GetTapTapCustomCompletionSteps(def.id);
                if (completionSteps > 0)
                {
                    pendingTapTapAchievementSync.Add(def.id);
                    TapTapSdk.Instance.IncrementAchievement(def.id, completionSteps);
                    return;
                }

                pendingTapTapAchievementSync.Add(def.id);
                TapTapSdk.Instance.UnlockAchievement(def.id);
                return;
            }

            if (!IsCompleted(def)) return;
            if (def.threshold <= TapTapStepAchievementMaxThreshold) return;

            pendingTapTapAchievementSync.Add(def.id);
            TapTapSdk.Instance.UnlockAchievement(def.id);
        }
        catch (Exception ex)
        {
            pendingTapTapAchievementSync.Remove(def.id);
            Debug.LogWarning($"同步 TapTap 成就失败：{def.id}，错误：{ex.Message}");
        }
    }

    private void SyncTapTapNumericProgress(AchievementType type, ulong beforeValue, ulong afterValue)
    {
        if (afterValue <= beforeValue) return;

        foreach (var def in GetDefinitionsForType(type))
        {
            SyncTapTapNumericProgress(def, afterValue, false);
        }
    }

    private bool TryResyncCompletedAchievementsToTapTapOnTitle()
    {
        if (!TapTapSdk.IsInitialized) return false;

        foreach (var def in definitions)
        {
            try
            {
                if (def.type == AchievementType.Custom)
                {
                    if (IsCompleted(def))
                    {
                        SyncTapTapAchievement(def);
                    }
                    continue;
                }

                ulong currentValue = GetAchievementValue(def.type);
                SyncTapTapNumericProgress(def, currentValue, false);
                SyncTapTapAchievement(def);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"标题界面重同步 TapTap 成就失败：{def.id}，错误：{ex.Message}");
            }
        }

        FlushPendingTapTapIncrements(true);
        return true;
    }

    private void SyncTapTapNumericProgress(AchievementDefinition def, ulong currentValue, bool forceResync)
    {
        if (def == null) return;
        if (def.type == AchievementType.Custom) return;
        if (def.threshold > TapTapStepAchievementMaxThreshold) return;

        ulong targetProgress = currentValue >= def.threshold ? def.threshold : currentValue;
        if (targetProgress == 0)
        {
            pendingTapTapIncrements.Remove(def.id);
            return;
        }

        if (!forceResync)
        {
            QueueTapTapNumericProgress(def, targetProgress, false);
            return;
        }

        if (TapTapSdk.IsInitialized && TrySetTapTapNumericProgress(def, targetProgress, true))
        {
            return;
        }

        QueueTapTapNumericProgress(def, targetProgress, true);
    }

    private ulong GetAchievementValue(AchievementType type)
    {
        return type switch
        {
            AchievementType.Score => TotalScore,
            AchievementType.Draw => TotalDraw,
            AchievementType.Play => TotalPlay,
            AchievementType.Mana => TotalMana,
            AchievementType.Heal => TotalHeal,
            AchievementType.Custom => 0UL,
            _ => 0UL
        };
    }

    private ulong GetTapTapSyncedNumericProgress(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return 0;
        return tapTapSyncedNumericProgress.TryGetValue(achievementId, out ulong progress) ? progress : 0;
    }

    private void SetTapTapSyncedNumericProgress(string achievementId, ulong progress)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        tapTapSyncedNumericProgress[achievementId] = progress;
        PlayerPrefs.SetString(TapTapNumericSyncedPrefix + achievementId, progress.ToString());
        MarkPlayerPrefsDirty();
    }

    private bool IsTapTapAchievementSynced(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return false;
        return tapTapAchievementSynced.Contains(achievementId);
    }

    private void MarkTapTapAchievementSynced(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (tapTapAchievementSynced.Contains(achievementId)) return;
        tapTapAchievementSynced.Add(achievementId);
        pendingTapTapAchievementSync.Remove(achievementId);
        PlayerPrefs.SetInt(TapTapAchievementSyncedPrefix + achievementId, 1);
        MarkPlayerPrefsDirty();
    }

    private void ClearTapTapAchievementSynced(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        tapTapAchievementSynced.Remove(achievementId);
        pendingTapTapAchievementSync.Remove(achievementId);
        PlayerPrefs.DeleteKey(TapTapAchievementSyncedPrefix + achievementId);
        MarkPlayerPrefsDirty();
    }

    private void OnTapTapAchievementSuccess(TapSDK.Achievement.TapAchievementResult result)
    {
        if (result == null || string.IsNullOrEmpty(result.AchievementId)) return;
        if (!definitionsById.TryGetValue(result.AchievementId, out var def) || def == null) return;

        if (def.type == AchievementType.Custom || def.threshold > TapTapStepAchievementMaxThreshold)
        {
            MarkTapTapAchievementSynced(def.id);
        }
    }

    private void OnTapTapAchievementFailure(string achievementId, int errorCode, string errorMsg)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (!pendingTapTapAchievementSync.Contains(achievementId) && IsTapTapAchievementSynced(achievementId)) return;

        ClearTapTapAchievementSynced(achievementId);
        pendingTitleTapTapResync = true;
        hasCompletedTitleTapTapResync = false;
        Debug.LogWarning($"TapTap 成就同步失败：{achievementId}，错误码：{errorCode}，原因：{errorMsg}");
    }

    private bool TrySetTapTapNumericProgress(AchievementDefinition def, ulong targetProgress, bool forceResync)
    {
        if (def == null) return false;
        if (!TapTapSdk.IsInitialized) return false;
        if (TapTapSdk.Instance == null) return false;

        ulong syncedProgress = GetTapTapSyncedNumericProgress(def.id);
        if (!forceResync && targetProgress <= syncedProgress)
        {
            pendingTapTapIncrements.Remove(def.id);
            return true;
        }

        try
        {
            ulong delta = targetProgress - syncedProgress;
            if (delta == 0)
            {
                pendingTapTapIncrements.Remove(def.id);
                return true;
            }

            int incrementStep = delta > (ulong)TapTapIncrementChunkMaxStep
                ? TapTapIncrementChunkMaxStep
                : (int)delta;
            if (incrementStep <= 0)
            {
                return false;
            }

            TapTapSdk.Instance.IncrementAchievement(def.id, incrementStep);

            ulong newProgress = BaseCharacter.SaturatingAdd(syncedProgress, (ulong)incrementStep);
            if (newProgress > def.threshold)
            {
                newProgress = def.threshold;
            }

            SetTapTapSyncedNumericProgress(def.id, newProgress);
            ulong remainingDelta = targetProgress > newProgress ? targetProgress - newProgress : 0UL;
            if (remainingDelta == 0)
            {
                pendingTapTapIncrements.Remove(def.id);
            }
            else
            {
                pendingTapTapIncrements[def.id] = remainingDelta;
            }
            if (newProgress >= def.threshold)
            {
                MarkTapTapAchievementSynced(def.id);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"设置 TapTap 数值成就进度失败：{def.id}，progress：{targetProgress}，错误：{ex.Message}");
            return false;
        }
    }

    private void QueueTapTapNumericProgress(AchievementDefinition def, ulong targetProgress, bool forceResync)
    {
        if (def == null) return;
        if (def.type == AchievementType.Custom) return;
        if (def.threshold > TapTapStepAchievementMaxThreshold) return;

        ulong syncedProgress = forceResync ? 0UL : GetTapTapSyncedNumericProgress(def.id);
        if (targetProgress <= syncedProgress)
        {
            if (!forceResync)
            {
                pendingTapTapIncrements.Remove(def.id);
            }
            return;
        }

        pendingTapTapIncrements[def.id] = targetProgress - syncedProgress;
    }

    private void FlushPendingTapTapIncrements(bool flushAll = false)
    {
        if (pendingTapTapIncrements.Count == 0) return;
        if (!TapTapSdk.IsInitialized) return;

        int remainingChunks = flushAll ? int.MaxValue : TapTapIncrementChunkBudgetPerFlush;
        var keys = new List<string>(pendingTapTapIncrements.Keys);
        foreach (string id in keys)
        {
            if (remainingChunks <= 0) break;
            if (!pendingTapTapIncrements.TryGetValue(id, out ulong delta) || delta == 0)
            {
                pendingTapTapIncrements.Remove(id);
                continue;
            }

            if (!definitionsById.TryGetValue(id, out var def))
            {
                pendingTapTapIncrements.Remove(id);
                continue;
            }

            ulong syncedProgress = GetTapTapSyncedNumericProgress(id);
            ulong targetProgress = BaseCharacter.SaturatingAdd(syncedProgress, delta);
            if (targetProgress > def.threshold)
            {
                targetProgress = def.threshold;
            }

            if (TrySetTapTapNumericProgress(def, targetProgress, false))
            {
                remainingChunks--;
            }
        }
    }

    private IReadOnlyList<AchievementDefinition> GetDefinitionsForType(AchievementType type)
    {
        if (definitionsByType.TryGetValue(type, out var typedDefinitions))
        {
            return typedDefinitions;
        }

        return Array.Empty<AchievementDefinition>();
    }

    private void MarkPlayerPrefsDirty()
    {
        hasPendingPlayerPrefsSave = true;
        nextPlayerPrefsFlushTime = Time.unscaledTime + PlayerPrefsFlushIntervalSeconds;
    }

    private void FlushPlayerPrefs()
    {
        if (!hasPendingPlayerPrefsSave) return;
        PlayerPrefs.Save();
        hasPendingPlayerPrefsSave = false;
    }

    private static int GetTapTapCustomCompletionSteps(string achievementId)
    {
        return achievementId == BattleStartAchievementId ? BattleStartAchievementTapTapSteps : 0;
    }

    private static ulong ApplyCountMultiplier(AchievementType type, ulong value)
    {
        return BaseCharacter.SaturatingMultiply(value, GetCountMultiplier(type));
    }

    private static ulong GetCountMultiplier(AchievementType type)
    {
        return type switch
        {
            AchievementType.Draw => DrawAchievementCountMultiplier,
            AchievementType.Play => PlayAchievementCountMultiplier,
            AchievementType.Mana => ManaAchievementCountMultiplier,
            _ => 1UL
        };
    }

    private static ulong NormalizeStoredProgress(AchievementType type, ulong value)
    {
        ulong multiplier = GetCountMultiplier(type);
        if (value == 0 || multiplier <= 1UL) return value;
        if (value % multiplier == 0) return value;
        return BaseCharacter.SaturatingMultiply(value, multiplier);
    }

    private static ulong LoadCounter(string key, AchievementType type)
    {
        return NormalizeStoredProgress(type, ParseUlongOrZero(PlayerPrefs.GetString(key, "0")));
    }

    private static bool IsReadyToSyncTitleAchievements()
    {
        if (!TapTapSdk.IsInitialized) return false;
        if (GameManager.Instance == null) return false;
        return !GameManager.Instance.IsBattleActive;
    }

    private static ulong ParseUlongOrZero(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return ulong.TryParse(value, out var parsed) ? parsed : 0;
    }
}
