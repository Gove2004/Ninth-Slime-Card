using UnityEngine;

/// <summary>
/// 所有自定义 MonoBehaviour 的基类，提供清晰的生命周期钩子
/// 用于统一处理订阅、事件注册等逻辑，避免在 Update 轮询
/// </summary>
public class UnitBehaviour : MonoBehaviour
{
    // 生命周期钩子 - 按调用顺序排列
    protected virtual void AwakePre() { }
    protected virtual void Awake() => AwakePre();

    protected virtual void StartPre() { }
    protected virtual void Start() => StartPre();

    protected virtual void UpdatePre() { }
    protected virtual void Update() => UpdatePre();

    protected virtual void LateUpdatePre() { }
    protected virtual void LateUpdate() => LateUpdatePre();

    protected virtual void OnEnablePre() { }
    protected virtual void OnEnable() => OnEnablePre();

    protected virtual void OnDisablePre() { }
    protected virtual void OnDisable() => OnDisablePre();

    protected virtual void OnDestroyPre() { }
    protected virtual void OnDestroy() => OnDestroyPre();

    /// <summary>安全订阅 UnityEvent</summary>
    protected SafeSubscription SubscribeToUnityEvent(UnityEngine.Events.UnityEvent evt, System.Action handler)
    {
        if (evt == null || handler == null) return default;
        var action = new UnityEngine.Events.UnityAction(handler);
        evt.AddListener(action);
        return new SafeSubscription(evt, action);
    }

    /// <summary>取消订阅帮助结构体</summary>
    public struct SafeSubscription
    {
        private readonly UnityEngine.Events.UnityEvent _event;
        private readonly UnityEngine.Events.UnityAction _handler;

        public SafeSubscription(UnityEngine.Events.UnityEvent e, UnityEngine.Events.UnityAction h) { _event = e; _handler = h; }

        public void Dispose()
        {
            if (_event != null && _handler != null)
                _event.RemoveListener(_handler);
        }
    }

    /// <summary>清理所有订阅（在 OnDestroy 时调用）</summary>
    protected void CleanUpSubscriptions(params System.Action[] handlers)
    {
        // 如果有保存的订阅列表，可以在这里统一清理
        // 当前实现为空，供子类扩展
    }
}