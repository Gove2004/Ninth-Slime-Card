using System.Collections;
using UnityEngine;

/// <summary>
/// 音频服务实现 — 通过 IAudioService 接口暴露功能
/// 不使用单例，由场景中的 AudioController 实例持有
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour, IAudioService
{
    [SerializeField] private string _musicAssetId = "Background_Music";
    [SerializeField] private string _uiSoundAssetId = "UI_Notify";

    private AudioSource _audioSource;
    private AudioClip _currentMusic;
    private float _musicVolume = 0.8f;
    private float _soundVolume = 1.0f;

    private void Awake() => _audioSource = GetComponent<AudioSource>();

    public void PlaySound(string assetId, float volume = 1f)
    {
        // 简化实现：实际应根据 assetId 加载对应的 AudioClip 并播放
        Debug.Log($"[AudioManager] Playing sound: {assetId}, vol={volume * _soundVolume}");
        // _audioSource.PlayOneShot(AssetLoader.Load<AudioClip>(assetId), volume * _soundVolume);
    }

    public void PlayMusic(string assetId, bool loop = true, float volume = 0.8f)
    {
        if (_currentMusic != null) _audioSource.Stop();
        Debug.Log($"[AudioManager] Playing music: {assetId}, loop={loop}, vol={volume}");
        // _currentMusic = AssetLoader.Load<AudioClip>(assetId);
        // _audioSource.clip = _currentMusic;
        // _audioSource.loop = loop;
        // _audioSource.volume = volume * _musicVolume;
        // _audioSource.Play();
    }

    public void StopAllSounds()
    {
        _audioSource.Stop();
        _currentMusic = null;
        Debug.Log("[AudioManager] Stopped all sounds");
    }

    public void StopMusic(string assetId)
    {
        if (_currentMusic != null) _audioSource.Stop();
        _currentMusic = null;
    }

    public void SetMusicVolume(float volume) => _musicVolume = Mathf.Clamp01(volume);

    public void SetSoundVolume(float volume) => _soundVolume = Mathf.Clamp01(volume);

    public float GetMusicVolume() => _musicVolume;

    public float GetSoundVolume() => _soundVolume;

    public bool HasSound(string assetId) => !string.IsNullOrEmpty(assetId); // 简化检查
}