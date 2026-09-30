using System;
using UnityEngine;

/// <summary>
/// 带值变化的可观察浮点数SO — 用于血条、进度条等需要UI自动更新的数值
/// 任何监听此SO的组件都会在值变化时收到通知
/// </summary>
[CreateAssetMenu(menuName = "Variables/Float")]
public class FloatVariable : ScriptableObject
{
    [SerializeField] private float _value = 0f;

    /// <summary>当前值（带变更检测）</summary>
    public float Value
    {
        get => _value;
        set
        {
            if (Mathf.Approximately(_value, value)) return;
            float old = _value;
            _value = value;
            OnValueChanged?.Invoke(old, _value);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this); // 编辑器中修改时保存
#endif
        }
    }

    /// <summary>数值变更时的回调：(oldValue, newValue)</summary>
    public event Action<float, float> OnValueChanged;

    /// <summary>直接设置值（跳过变更事件，用于初始化）</summary>
    public void SetValueRaw(float v) => _value = v;

    /// <summary>增加数值</summary>
    public void Apply(float amount) => Value += amount;

    /// <summary>重置到默认值</summary>
    public void Reset(float defaultValue = 0f) => Value = defaultValue;

    /// <summary>限制在[min, max]范围内</summary>
    public void Clamp(float min, float max) => Value = Mathf.Clamp(Value, min, max);
}