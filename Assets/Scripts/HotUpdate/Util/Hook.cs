using System;
using System.Collections.Generic;

public class HookField<T>
{
    private T _value;
    public T Value
    {
        get => _value;
        set => Set(value);
    }

    private readonly List<Func<T, T>> _beforeHooks = new();
    private readonly List<Action<T>> _afterHooks = new();

    public HookField(T initialValue)
    {
        _value = initialValue;
    }

    public void Set(T newValue)
    {
        foreach (var before in _beforeHooks)
        {
            newValue = before(newValue);
        }

        _value = newValue;

        foreach (var after in _afterHooks)
        {
            after(_value);
        }
    }

    public void Change(Func<T, T> update)
    {
        var newValue = update(Value);
        Set(newValue);
    }

    public void AddBefore(Func<T, T> hook) => _beforeHooks.Add(hook);
    public void AddAfter(Action<T> hook) => _afterHooks.Add(hook);
    public void RemoveBefore(Func<T, T> hook) => _beforeHooks.Remove(hook);
    public void RemoveAfter(Action<T> hook) => _afterHooks.Remove(hook);

    public static implicit operator T(HookField<T> field) => field.Value;
}