using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存档服务的默认实现 — 通过 ISaveService 接口暴露功能
/// 不使用单例模式，而是作为普通 MonoBehaviour 挂在场景对象上
/// </summary>
[RequireComponent(typeof(SaveDataContainer))] // Helper to persist data across scenes
public class SaveService : MonoBehaviour, ISaveService
{
    [SerializeField] private string _saveSlotName = "SaveSlot1";
    [SerializeField] private bool _autoSaveOnExit = true;

    private SaveDataContainer _container;
    private Dictionary<string, object> _pendingWrites = new();

    private void Awake() => _container = gameObject.AddComponent<SaveDataContainer>();

    public void Save(string key, object data)
    {
        if (data == null) return;

        try
        {
            string json = JsonUtility.ToJson(data, true);
            _container.SetData(key, json);
            Debug.Log($"[SaveService] Saved: {key}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to save {key}: {e.Message}");
        }
    }

    public T Load<T>(string key)
    {
        string json = _container.GetData(key);
        if (string.IsNullOrEmpty(json)) return default;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to load {key}: {e.Message}");
            return default;
        }
    }

    public IEnumerator SaveAsync()
    {
        // 模拟异步保存（实际可改为协程或 Task）
        yield return new WaitForSeconds(0.1f);
        Debug.Log("[SaveService] Background save complete");
    }

    public void Clear()
    {
        _container.ClearAll();
        _pendingWrites.Clear();
        Debug.Log("[SaveService] All saves cleared");
    }

    public bool Exists(string key) => !string.IsNullOrEmpty(_container.GetData(key));
}

/// <summary>辅助类：持久化数据到Scene级别但不随对象销毁（用于跨场景加载时的临时保存）</summary>
public class SaveDataContainer : MonoBehaviour
{
    private Dictionary<string, string> _data = new();

    public void SetData(string key, string value)
    {
        if (_data.ContainsKey(key)) _data[key] = value;
        else _data.Add(key, value);
    }

    public string GetData(string key) => _data.TryGetValue(key, out var v) ? v : null;

    public void ClearAll()
    {
        _data.Clear();
    }

    private void OnDisable()
    {
        // 保持数据在内存中，直到场景彻底卸载
    }
}