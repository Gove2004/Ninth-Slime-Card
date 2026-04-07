using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameEvents
{
    public const string BattleCardDrawnToHand = "Battle_CardDrawnToHand";
    public const string BattleCardPlayedFromHand = "Battle_CardPlayedFromHand";
    public const string BattleCardResolved = "Battle_CardResolved";
    public const string BattleStarted = "Battle_Started";
    public const string BattleEnded = "Battle_Ended";
    public const string CharacterTurnStarted = "Character_TurnStarted";
    public const string CharacterTurnEnded = "Character_TurnEnded";
    public const string PlayerHandChanged = "Player_HandChanged";
    public const string PlayerGainedMana = "Player_GainedMana";
    public const string PlayerHealed = "Player_Healed";
    public const string PlayerJailStateChanged = "Player_JailStateChanged";
    public const string PlayerCardRefreshed = "Player_CardRefreshed";
    public const string PlayerCardDrawnToHand = "Player_CardDrawnToHand";
    public const string PlayerCardGeneratedToHand = "Player_CardGeneratedToHand";
    public const string PlayerCardPlayedFromHand = "Player_CardPlayedFromHand";
    public const string PlayerCardRemovedFromHand = "Player_CardRemovedFromHand";
    public const string PlayerCardResolved = "Player_CardResolved";
    public const string PlayerDefeated = "Player_Defeated";
    public const string EnemyDefeated = "Enemy_Defeated";
    public const string EnemyBossPhaseChanged = "EnemyBoss_PhaseChanged";
    public const string EnemyCardPlayed = "Enemy_CardPlayed";
    public const string EnemyCardDrawn = "Enemy_CardDrawn";
    public const string EnemyActionAnimationCompleted = "Enemy_ActionAnimationCompleted";
    public const string CardSelected = "Card_Selected";
    public const string CardDeselected = "Card_Deselected";
    public const string AchievementUnlocked = "Achievement_Unlocked";
    public const string AchievementDrawDrawTriggered = "Achievement_DrawDrawCard";
    public const string AchievementOverheatTriggered = "Achievement_OverheatTriggered";
    public const string AchievementDoubleAgentTriggered = "Achievement_DoubleAgentTriggered";
    public const string AchievementClearEasyTriggered = "Achievement_ClearEasyTriggered";
    public const string AchievementClearHardTriggered = "Achievement_ClearHardTriggered";
    public const string AchievementClearHellTriggered = "Achievement_ClearHellTriggered";
}

public class CharacterEventContext
{
    public BaseCharacter Character { get; }

    public CharacterEventContext(BaseCharacter character)
    {
        Character = character;
    }
}

public sealed class CharacterValueEventContext : CharacterEventContext
{
    public ulong Amount { get; }

    public CharacterValueEventContext(BaseCharacter character, ulong amount) : base(character)
    {
        Amount = amount;
    }
}

public sealed class BattleEventContext
{
    public BattleManager BattleManager { get; }
    public BaseCharacter Player { get; }
    public BaseCharacter Enemy { get; }
    public int CurrentTurn { get; }

    public BattleEventContext(BattleManager battleManager, BaseCharacter player, BaseCharacter enemy, int currentTurn)
    {
        BattleManager = battleManager;
        Player = player;
        Enemy = enemy;
        CurrentTurn = currentTurn;
    }
}

public sealed class HandEventContext : CharacterEventContext
{
    public int HandCount { get; }

    public HandEventContext(BaseCharacter character, int handCount) : base(character)
    {
        HandCount = handCount;
    }
}

public sealed class CardEventContext : CharacterEventContext
{
    public BaseCard Card { get; }

    public CardEventContext(BaseCharacter character, BaseCard card)
        : base(character)
    {
        Card = card;
    }
}

public sealed class EnemyBossPhaseChangedEventContext : CharacterEventContext
{
    public int Phase { get; }

    public EnemyBossPhaseChangedEventContext(BaseCharacter character, int phase) : base(character)
    {
        Phase = phase;
    }
}

public sealed class CardSelectionEventContext
{
    public BaseCard Card { get; }

    public CardSelectionEventContext(BaseCard card)
    {
        Card = card;
    }
}

public sealed class EnemyAnimationEventContext : CharacterEventContext
{
    public string Tag { get; }

    public EnemyAnimationEventContext(BaseCharacter character, string tag) : base(character)
    {
        Tag = tag;
    }
}

public class EventCenter : MonoBehaviour
{
    [Header("调试设置")]
    public bool showDebugInfo = true;
    public string searchFilter = "";
    
    // 事件表
    private static Dictionary<string, List<Action<object>>> events = new Dictionary<string, List<Action<object>>>();
    
    #region 基本API
    public static Action Register(string eventName, Action<object> listener)
    {
        if (!events.ContainsKey(eventName))
            events[eventName] = new List<Action<object>>();
        
        events[eventName].Add(listener);
        return () => Unregister(eventName, listener);
    }

    public static Action Register(string eventName, Action listener)
    {
        return Register(eventName, _ => listener());
    }

    public static Action Register<T>(string eventName, Action<T> listener)
    {
        return Register(eventName, payload =>
        {
            if (payload is T typed)
            {
                listener(typed);
            }
        });
    }
    
    public static void Publish<T>(string eventName, T param = default)
    {
        if (events.ContainsKey(eventName))
        {
            foreach (var listener in events[eventName].ToList())
            {
                try { listener?.Invoke(param); } 
                catch (Exception e) { Debug.LogError($"事件错误: {e}"); }
            }
        }
    }
    
    public static void Publish(string eventName) => Publish<object>(eventName, null);
    
    public static void Unregister(string eventName, Action<object> listener)
    {
        if (events.ContainsKey(eventName))
            events[eventName].Remove(listener);
    }
    #endregion
    
    #region 调试信息
    public class EventInfo
    {
        public string name;
        public int listenerCount;
        public string[] listenerNames;
    }
    
    public EventInfo[] GetEventInfos()
    {
        if (!showDebugInfo) return new EventInfo[0];
        
        return events.Keys
            .Where(name => string.IsNullOrEmpty(searchFilter) || 
                   name.ToLower().Contains(searchFilter.ToLower()))
            .Select(name => new EventInfo
            {
                name = name,
                listenerCount = events[name].Count,
                listenerNames = events[name]
                    .Select(GetListenerName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToArray()
            })
            .OrderBy(e => e.name)
            .ToArray();
    }
    
    private string GetListenerName(Action<object> listener)
    {
        if (listener == null) return "null";
        
        try
        {
            var method = listener.Method;
            var target = listener.Target;
            
            if (target != null)
                return $"{target.GetType().Name}.{method.Name}";
            else
                return $"static {method.DeclaringType?.Name}.{method.Name}";
        }
        catch
        {
            return "unknown";
        }
    }
    
    public void SetSearchFilter(string filter)
    {
        searchFilter = filter;
    }
    
    public static void ClearAllEvents()
    {
        events.Clear();
    }
    
    public void ClearAll()
    {
        ClearAllEvents();
    }
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(EventCenter))]
public class EventCenterEditor : Editor
{
    private string searchText = "";
    private Vector2 scrollPos;
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        EventCenter center = (EventCenter)target;
        
        if (!center.showDebugInfo) return;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("事件订阅列表", EditorStyles.boldLabel);
        
        // 搜索框
        EditorGUILayout.BeginHorizontal();
        searchText = EditorGUILayout.TextField("搜索:", searchText);
        if (GUILayout.Button("清空", GUILayout.Width(50))) searchText = "";
        EditorGUILayout.EndHorizontal();
        
        center.SetSearchFilter(searchText);
        
        // 显示事件列表
        var events = center.GetEventInfos();
        if (events.Length == 0)
        {
            EditorGUILayout.HelpBox("没有找到事件", MessageType.Info);
        }
        else
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
            
            foreach (var eventInfo in events)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // 事件名和监听器数量
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(eventInfo.name, EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField($"{eventInfo.listenerCount} 个监听器", GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
                
                // 监听器列表
                if (eventInfo.listenerNames.Length > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (string listenerName in eventInfo.listenerNames)
                    {
                        EditorGUILayout.LabelField($"• {listenerName}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        // 操作按钮
        EditorGUILayout.Space(5);
        if (GUILayout.Button("清空所有事件"))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要清空所有事件吗？", "确定", "取消"))
            {
                center.ClearAll();
            }
        }
    }
}
#endif
