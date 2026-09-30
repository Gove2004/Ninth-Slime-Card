using System;
using UnityEngine;

/// <summary>
/// 事件管理器的接口定义 — 替代 String-based EventCenter
/// 提供类型安全的订阅/发布机制
/// </summary>
public interface IEventManager
{
    /// <summary>注册监听器</summary>
    void Register(string eventName, Action<object> listener);

    /// <summary>取消注册监听器</summary>
    void Unregister(string eventName, Action<object> listener);

    /// <summary>发布事件数据</summary>
    void Publish(string eventName, object data);

    /// <summary>检查某个事件是否有监听器</summary>
    bool HasListeners(string eventName);

    /// <summary>获取事件类型列表（调试用）</summary>
    string[] GetEventNames();
}