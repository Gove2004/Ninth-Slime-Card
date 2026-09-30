using System.Collections;
using UnityEngine;

/// <summary>
/// 存档服务的接口定义 — 用于解耦具体实现和依赖方
/// 避免直接使用 SaveManager.Instance 单例调用
/// </summary>
public interface ISaveService
{
    /// <summary>保存数据到指定key</summary>
    void Save(string key, object data);

    /// <summary>从指定key加载数据</summary>
    T Load<T>(string key);

    /// <summary>异步保存操作（用于后台线程）</summary>
    IEnumerator SaveAsync();

    /// <summary>清除所有存档</summary>
    void Clear();

    /// <summary>检查某个key是否存在</summary>
    bool Exists(string key);
}