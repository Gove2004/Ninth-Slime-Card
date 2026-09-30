using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 事件系统的中心管理器 — 单例模式仅限于此管理容器，业务事件通过 GameEventSO 分发
/// 不直接持有业务数据，只负责事件的注册/查找/发布
/// </summary>
public class EventSystem : MonoBehaviour
{
    // 全局事件地图
    private Dictionary<string, GameEventSO> _events = new();

    // 单例实例（仅用于快速查找，推荐通过注入 GameEventSO 直接使用）
    public static EventSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[EventSystem] Duplicate instance destroying");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>注册新的命名事件通道</summary>
    public void RegisterEvent(string name, GameEventSO eventSO)
    {
        if (string.IsNullOrEmpty(name)) throw new System.ArgumentException("Event name cannot be empty");
        if (eventSO == null) throw new System.ArgumentNullException(nameof(eventSO));

        _events[name] = eventSO;
        Debug.Log($"[EventSystem] Registered event: {name}");
    }

    /// <summary>获取已注册的事件通道</summary>
    public GameEventSO GetEvent(string name) => _events.TryGetValue(name, out var e) ? e : null;

    /// <summary>检查事件是否存在</summary>
    public bool HasEvent(string name) => _events.ContainsKey(name);

    /// <summary>发布命名事件（向后兼容旧代码）</summary>
    public void Publish(string eventName, object data)
    {
        var evt = GetEvent(eventName);
        if (evt != null)
        {
            evt.Publish(data);
        }
        else
        {
            Debug.LogWarning($"[EventSystem] No registered event found with name: {eventName}");
        }
    }

    /// <summary>获取所有事件名称（调试用）</summary>
    public string[] GetEventNames() => _events.Keys.ToArray();

    /// <summary>清理所有事件引用</summary>
    public void Clear()
    {
        _events.Clear();
        Instance = null;
    }
}