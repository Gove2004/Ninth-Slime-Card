using UnityEngine;

/// <summary>
/// 音频服务的接口定义 — 替代 AudioManager.Instance 单例
/// 所有音频播放请求通过此接口发出，实现类负责具体音效播放
/// </summary>
public interface IAudioService
{
    /// <summary>播放音效</summary>
    void PlaySound(string assetId, float volume = 1f);

    /// <summary>播放音乐（可指定循环）</summary>
    void PlayMusic(string assetId, bool loop = true, float volume = 0.8f);

    /// <summary>停止所有音效</summary>
    void StopAllSounds();

    /// <summary>停止特定音乐</summary>
    void StopMusic(string assetId);

    /// <summary>设置背景音乐音量</summary>
    void SetMusicVolume(float volume);

    /// <summary>设置音效音量</summary>
    void SetSoundVolume(float volume);

    /// <summary>获取当前音乐音量</summary>
    float GetMusicVolume();

    /// <summary>获取当前音效音量</summary>
    float GetSoundVolume();

    /// <summary>检查是否存在该音效资源</summary>
    bool HasSound(string assetId);
}