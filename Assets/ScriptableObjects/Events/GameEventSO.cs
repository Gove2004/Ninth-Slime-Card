using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject-based event channel - replaces string-based EventCenter
/// Use Create Asset Menu to instantiate as .asset files in project
/// </summary>
[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    private readonly List<ListenerRecord> _listeners = new();

    /// <summary>
    /// 注册监听器
    /// </summary>
    public void Register(Action<object> listener)
    {
        if (listener == null) return;
        // 避免重复注册
        if (_listeners.Find(l => l.Listener == listener) != null) return;
        _listeners.Add(new ListenerRecord(listener));
    }

    /// <summary>
    /// 取消注册监听器
    /// </summary>
    public void Unregister(Action<object> listener)
    {
        var record = _listeners.Find(l => l.Listener == listener);
        if (record != null)
        {
            _listeners.Remove(record);
            record.Listener = null; // Help GC
        }
    }

    /// <summary>
    /// 发布事件数据给所有注册过的监听器
    /// </summary>
    public void Publish(object data)
    {
        // 遍历副本，防止在迭代期间修改列表
        foreach (var record in _listeners.ToList())
        {
            if (record.Listener != null)
            {
                try
                {
                    record.Listener(data);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Event handler threw exception: {e.Message}", this);
                }
            }
        }
    }

    /// <summary>
    /// 检查是否有监听器（用于调试）
    /// </summary>
    public bool HasListeners => _listeners.Count > 0;

    /// <summary>
    /// 获取当前监听器数量（调试用）
    /// </summary>
    public int ListenerCount => _listeners.Count;

    // 内部记录类，保存弱引用风格的监听器（简化版，实际可进一步用WeakReference）
    private class ListenerRecord
    {
        public Action<object> Listener { get; set; }
        public ListenerRecord(Action<object> listener) { Listener = listener; }
    }
}

/// <summary>
/// 带泛型参数的版本 — 类型安全，无需强制转换
/// </summary>
[CreateAssetMenu(menuName = "Events/Game Event (Typed)")]
public class GameEventSO<T> : ScriptableObject where T : class
{
    private readonly List<Action<T>> _listeners = new();

    public void Register(Action<T> listener)
    {
        if (listener == null) return;
        if (_listeners.Any(l => l.Target == listener.Target)) return;
        _listeners.Add(listener);
    }

    public void Unregister(Action<T> listener)
    {
        _listeners.RemoveAll(l => l.Target == listener.Target);
    }

    public void Publish(T data)
    {
        foreach (var l in _listeners.ToArray()) try { l(data); } catch (System.Exception e) { Debug.LogError(e); }
    }

    public bool HasListeners => _listeners.Count > 0;
    public int ListenerCount => _listeners.Count;
}