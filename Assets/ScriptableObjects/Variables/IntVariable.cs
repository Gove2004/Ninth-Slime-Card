using System;
using UnityEngine;

/// <summary>
/// 带值变化的可观察整数SO — 用于奖杯数、关卡数、金币等计数型数据
/// </summary>
[CreateAssetMenu(menuName = "Variables/Int")]
public class IntVariable : ScriptableObject
{
    [SerializeField] private int _value = 0;

    /// <summary>当前值（带变更检测）</summary>
    public int Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            int old = _value;
            _value = value;
            OnValueChanged?.Invoke(old, _value);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>数值变更时的回调：(oldValue, newValue)</summary>
    public event Action<int, int> OnValueChanged;

    /// <summary>直接设置值（跳过变更事件）</summary>
    public void SetValueRaw(int v) => _value = v;

    /// <summary>增加数值</summary>
    public void Apply(int amount) => Value += amount;

    /// <summary>减少数值</summary>
    public void Subtract(int amount) => Value -= amount;

    /// <summary>重置到默认值</summary>
    public void Reset(int defaultValue = 0) => Value = defaultValue;

    /// <summary>限制在[min, max]范围内</summary>
    public void Clamp(int min, int max) => Value = Mathf.Clamp(Value, min, max);

    /// <summary>是否为正数</summary>
    public bool IsPositive => _value > 0;

    /// <summary>是否为零</summary>
    public bool IsZero => _value == 0;
}