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
        public int threshold;
        public string description;
    }

    public class AchievementStatus
    {
        public string id;
        public string name;
        public string description;
        public bool completed;
    }

    private const string TotalScoreKey = "Achievement_TotalScore";
    private const string TotalDrawKey = "Achievement_TotalDraw";
    private const string TotalPlayKey = "Achievement_TotalPlay";
    private const string TotalManaKey = "Achievement_TotalMana";
    private const string TotalHealKey = "Achievement_TotalHeal";
    private const string CustomAchievementPrefix = "Achievement_Custom_";

    public int TotalScore { get; private set; }
    public int TotalDraw { get; private set; }
    public int TotalPlay { get; private set; }
    public int TotalMana { get; private set; }
    public int TotalHeal { get; private set; }

    private readonly List<AchievementDefinition> definitions = new();
    private readonly HashSet<string> customCompleted = new();
    private Action onDrawUnsub;
    private Action onPlayUnsub;
    private Action onManaUnsub;
    private Action onHealUnsub;
    private Action onDrawDrawUnsub;
    private Action onSevenSinsUnsub;
    private Action onOverheatUnsub;
    private Action onStolenKillUnsub;

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
        RegisterEvents();
    }

    private void OnDestroy()
    {
        onDrawUnsub?.Invoke();
        onPlayUnsub?.Invoke();
        onManaUnsub?.Invoke();
        onHealUnsub?.Invoke();
        onDrawDrawUnsub?.Invoke();
        onSevenSinsUnsub?.Invoke();
        onOverheatUnsub?.Invoke();
        onStolenKillUnsub?.Invoke();
    }

    public void AddScore(int amount)
    {
        if (amount <= 0) return;
        TotalScore += amount;
        Save();
    }

    public void AddDraw(int amount)
    {
        if (amount <= 0) return;
        TotalDraw += amount;
        Save();
    }

    public void AddPlay(int amount)
    {
        if (amount <= 0) return;
        TotalPlay += amount;
        Save();
    }

    public void AddMana(int amount)
    {
        if (amount <= 0) return;
        TotalMana += amount;
        Save();
    }

    public void AddHeal(int amount)
    {
        if (amount <= 0) return;
        TotalHeal += amount;
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
        onDrawUnsub = EventCenter.Register("Player_DrawCard", (obj) => AddDraw(1));
        onPlayUnsub = EventCenter.Register("Player_PlayCard", (obj) => AddPlay(1));
        onManaUnsub = EventCenter.Register("Player_GainMana", (obj) =>
        {
            if (obj is int value) AddMana(value);
        });
        onHealUnsub = EventCenter.Register("Player_Heal", (obj) =>
        {
            if (obj is int value) AddHeal(value);
        });
        onDrawDrawUnsub = EventCenter.Register("Achievement_DrawDrawCard", (obj) => UnlockCustom("draw_draw"));
        onSevenSinsUnsub = EventCenter.Register("Achievement_SevenSinsAllEffects", (obj) => UnlockCustom("seven_sins_all"));
        onOverheatUnsub = EventCenter.Register("Player_PlayCardExecuted", (obj) =>
        {
            if (obj is BaseCard card && card.Cost >= 1024) UnlockCustom("overheat");
        });
        onStolenKillUnsub = EventCenter.Register("Achievement_KilledByStolenCard", (obj) => UnlockCustom("double_agent"));
    }

    private void BuildDefinitions()
    {
        if (definitions.Count > 0) return;

        AddDef("score_10", "初窥门径", AchievementType.Score, 10);
        AddDef("score_100", "小有成就", AchievementType.Score, 100);
        AddDef("score_1000", "得分专家", AchievementType.Score, 1000);
        AddDef("score_10000", "登峰造极", AchievementType.Score, 10000);

        AddDef("draw_1", "抽牌新手", AchievementType.Draw, 1);
        AddDef("draw_10", "抽牌熟练", AchievementType.Draw, 10);
        AddDef("draw_100", "抽牌达人", AchievementType.Draw, 100);
        AddDef("draw_1000", "抽牌大师", AchievementType.Draw, 1000);

        AddDef("play_1", "出牌新手", AchievementType.Play, 1);
        AddDef("play_10", "出牌熟练", AchievementType.Play, 10);
        AddDef("play_100", "出牌达人", AchievementType.Play, 100);
        AddDef("play_1000", "出牌大师", AchievementType.Play, 1000);

        AddDef("mana_5", "法力涌动", AchievementType.Mana, 5);
        AddDef("mana_50", "法力奔流", AchievementType.Mana, 50);
        AddDef("mana_500", "法力洪流", AchievementType.Mana, 500);
        AddDef("mana_5000", "法力无尽", AchievementType.Mana, 5000);

        AddDef("heal_20", "小愈合", AchievementType.Heal, 20);
        AddDef("heal_200", "疗愈者", AchievementType.Heal, 200);
        AddDef("heal_2000", "生命回响", AchievementType.Heal, 2000);
        AddDef("heal_20000", "不灭之躯", AchievementType.Heal, 20000);

        AddDef("draw_draw", "抽抽爆", AchievementType.Custom, 1, "用“抽牌”抽到“抽牌”");
        AddDef("seven_sins_all", "罪无可赦", AchievementType.Custom, 1, "使一张七宗罪触发其全部效果");
        AddDef("overheat", "过热", AchievementType.Custom, 1, "打出一张魔力消耗不小于1024的卡牌");
        AddDef("double_agent", "双面间谍", AchievementType.Custom, 1, "被从对手处偷到的卡牌或dot杀死");
    }

    private void AddDef(string id, string name, AchievementType type, int threshold, string description = null)
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
        int value = def.type switch
        {
            AchievementType.Score => TotalScore,
            AchievementType.Draw => TotalDraw,
            AchievementType.Play => TotalPlay,
            AchievementType.Mana => TotalMana,
            AchievementType.Heal => TotalHeal,
            AchievementType.Custom => customCompleted.Contains(def.id) ? 1 : 0,
            _ => 0
        };
        return value >= def.threshold;
    }

    private string GetDescription(AchievementDefinition def)
    {
        if (!string.IsNullOrEmpty(def.description)) return def.description;
        return def.type switch
        {
            AchievementType.Score => $"累计得分达到{def.threshold}",
            AchievementType.Draw => $"累计抽牌达到{def.threshold}",
            AchievementType.Play => $"累计出牌达到{def.threshold}",
            AchievementType.Mana => $"累计获得魔力达到{def.threshold}",
            AchievementType.Heal => $"累计生命恢复达到{def.threshold}",
            _ => ""
        };
    }

    private void Save()
    {
        PlayerPrefs.SetInt(TotalScoreKey, TotalScore);
        PlayerPrefs.SetInt(TotalDrawKey, TotalDraw);
        PlayerPrefs.SetInt(TotalPlayKey, TotalPlay);
        PlayerPrefs.SetInt(TotalManaKey, TotalMana);
        PlayerPrefs.SetInt(TotalHealKey, TotalHeal);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        TotalScore = PlayerPrefs.GetInt(TotalScoreKey, 0);
        TotalDraw = PlayerPrefs.GetInt(TotalDrawKey, 0);
        TotalPlay = PlayerPrefs.GetInt(TotalPlayKey, 0);
        TotalMana = PlayerPrefs.GetInt(TotalManaKey, 0);
        TotalHeal = PlayerPrefs.GetInt(TotalHealKey, 0);
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

    private void UnlockCustom(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (customCompleted.Contains(id)) return;
        customCompleted.Add(id);
        PlayerPrefs.SetInt(CustomAchievementPrefix + id, 1);
        PlayerPrefs.Save();
    }
}
