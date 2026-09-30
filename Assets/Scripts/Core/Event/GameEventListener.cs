using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// MonoBehaviour that automatically subscribes/unsubscribes to a GameEventSO on Enable/Disable.
/// Attach to any GameObject that needs to react to an event.
/// </summary>
[RequireComponent(typeof(UnitBehaviour))] // Optional base class for lifecycle management
public class GameEventListener : UnitBehaviour
{
    [SerializeField] private GameEventSO _event;
    [SerializeField] private UnityEvent _onEventRaised;
    [SerializeField] private bool _once = false;

    // 泛型版本支持数据传递（通过设置Type和Action）
    private Action<object> _callback;
    private Type _dataType;

    protected override void AwakePre() => OnAwake();

    private void OnAwake()
    {
        if (_event == null) return;

        // Create callback for the event
        _callback = obj =>
        {
            if (_onEventRaised != null) _onEventRaised.Invoke();

            if (_callback != null && _dataType != null && obj != null && _dataType.IsAssignableFrom(obj.GetType()))
            {
                // Try to invoke typed action if set
                try
                {
                    var method = typeof(GameEventListener).GetMethod("OnEventRaisedTyped");
                    if (method != null)
                    {
                        method.MakeGenericMethod(_dataType).Invoke(this, new object[] { obj });
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to invoke generic handler: {e.Message}");
                }
            }

            if (_once)
            {
                _event.Unregister(_callback);
                _callback = null;
                _event = null;
            }
        };

        _event.Register(_callback);
    }

    protected override void StartPre() => OnStart();

    private void OnStart()
    {
        // For backwards compatibility, check if we have a typed listener set via editor
        if (_dataType != null && _callback != null)
        {
            _event?.Register(_callback);
        }
    }

    protected override void OnEnablePre() => OnEnable();

    protected override void OnEnable()
    {
        if (_event != null && !_event.HasListeners) return;
        // Already registered in Awake
    }

    protected override void OnDisablePre() => OnDisable();

    protected override void OnDisable()
    {
        if (_event != null && _callback != null)
        {
            _event.Unregister(_callback);
            _callback = null;
        }
    }

    protected override void OnDestroyPre() => OnDestroy();

    protected override void OnDestroy()
    {
        // Clean up callback to prevent memory leaks
        _callback = null;
        _event = null;
    }

    /// <summary>
    /// 在编辑器中设置监听器和数据类型（供PropertyDrawer调用）
    /// </summary>
    public void SetListener(Action<object> callback, Type dataType)
    {
        _callback = callback;
        _dataType = dataType;
        if (_event != null)
        {
            _event.Unregister(_callback);
            _event.Register(_callback);
        }
    }

    /// <summary>暴露事件供外部设置</summary>
    public GameEventSO Event => _event;

    // 自动生成不同类型的 OnEventRaisedTyped 方法
    // 例如：OnEventRaisedTyped<String>, OnEventRaisedTyped<int> 等
    // 这里使用泛型方法配合反射来调用用户的处理器
    private void OnEventRaisedTyped<T>(T data) where T : class
    {
        // 这是占位符，实际使用时用户需要添加特定类型的处理函数
        // 或者在编辑器中自动生成对应的代码
        Debug.Log($"Received event data of type: {typeof(T).Name}");
    }

}