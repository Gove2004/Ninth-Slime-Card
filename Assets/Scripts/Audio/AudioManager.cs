using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip drawClip;
    public AudioClip playClip;
    public AudioClip damageClip;
    public AudioClip playerDamageClip;
    public AudioClip enemyDamageClip;
    public AudioClip healClip;
    public AudioClip manaClip;

    [Header("BGM Clips")]
    public List<AudioClip> titleBGMClips = new List<AudioClip>();
    public List<AudioClip> battleBGMClips = new List<AudioClip>();

    [Header("Settings")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private Dictionary<string, AudioClip> proceduralClips = new Dictionary<string, AudioClip>();
    private bool bgmLoaded = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Ensure AudioSources exist
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            
            RegisterClips();
            EnsureBGMLoaded();
            GameSettings.Initialize();
            ApplySettings(GameSettings.MusicVolume, GameSettings.SfxVolume);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Subscribe to events
        EventCenter.Register("Player_DrawCard", (obj) => PlaySFX("Draw"));
        EventCenter.Register("Player_PlayCard", (obj) => PlaySFX("Play"));
        EventCenter.Register("CardDrawn", (obj) => PlaySFX("Draw")); // Enemy draw
        EventCenter.Register("CardPlayed", (obj) => PlaySFX("Play")); // Generic play
        
        // Damage/Heal events are handled via DamageEffectManager usually, but we can listen globally if we had a global event.
        // Or we can let DamageEffectManager call us.
        // But since we want to be decoupled, let's expose public methods and let DamageEffectManager call them, 
        // or hook into BattleManager if possible.
        // Actually, DamageEffectManager already listens to DamageTaken/HealTaken. 
        // We can hook there or let DamageEffectManager invoke audio.
        // For simplicity, let's expose PlayDamage and PlayHeal and modify DamageEffectManager to call them.
    }

    public void PlaySFX(string clipName)
    {
        if (proceduralClips.ContainsKey(clipName))
        {
            sfxSource.PlayOneShot(proceduralClips[clipName], sfxVolume);
        }
        else
        {
            Debug.LogWarning($"Audio clip not found: {clipName}");
        }
    }

    private void LoadBGMResources()
    {
        // Load Title BGM
        AudioClip t1 = Resources.Load<AudioClip>("Music/标题界面bgm1");
        AudioClip t2 = Resources.Load<AudioClip>("Music/标题界面bgm2");
        if (t1 != null) titleBGMClips.Add(t1);
        else Debug.LogError("Failed to load Music/标题界面bgm1");
        
        if (t2 != null) titleBGMClips.Add(t2);
        else Debug.LogError("Failed to load Music/标题界面bgm2");

        // Load Battle BGM
        AudioClip b1 = Resources.Load<AudioClip>("Music/战斗bgm1");
        AudioClip b2 = Resources.Load<AudioClip>("Music/战斗bgm2");
        if (b1 != null) battleBGMClips.Add(b1);
        else Debug.LogError("Failed to load Music/战斗bgm1");
        
        if (b2 != null) battleBGMClips.Add(b2);
        else Debug.LogError("Failed to load Music/战斗bgm2");

        Debug.Log($"[AudioManager] Loaded {titleBGMClips.Count} Title BGMs and {battleBGMClips.Count} Battle BGMs.");
    }

    public void PlayTitleBGM()
    {
        Debug.Log("[AudioManager] Request to play Title BGM");
        EnsureBGMLoaded();
        PlayRandomBGM(titleBGMClips);
    }

    public void PlayBattleBGM()
    {
        Debug.Log("[AudioManager] Request to play Battle BGM");
        EnsureBGMLoaded();
        PlayRandomBGM(battleBGMClips);
    }

    private void PlayRandomBGM(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0)
        {
            EnsureBGMLoaded();
            if (clips == null || clips.Count == 0)
            {
            Debug.LogWarning("[AudioManager] No BGM clips to play in the list.");
            return;
            }
        }

        musicSource.volume = musicVolume;
        musicSource.loop = true;
        
        int index = Random.Range(0, clips.Count);
        AudioClip clipToPlay = clips[index];

        if (musicSource.clip == clipToPlay && musicSource.isPlaying)
        {
            Debug.Log($"[AudioManager] Already playing {clipToPlay.name}");
            return; 
        }

        Debug.Log($"[AudioManager] Playing BGM: {clipToPlay.name}");
        musicSource.clip = clipToPlay;
        musicSource.Play();
    }

    public void RegisterClips()
    {
        Debug.Log($"[AudioManager] Registering Clips. Draw:{drawClip!=null}, Play:{playClip!=null}, Slash:{damageClip!=null}, PlayerSlash:{playerDamageClip!=null}, EnemySlash:{enemyDamageClip!=null}, Heal:{healClip!=null}, Mana:{manaClip!=null}");

        if (drawClip != null) proceduralClips["Draw"] = drawClip;
        if (playClip != null) proceduralClips["Play"] = playClip;
        var playerClip = playerDamageClip != null ? playerDamageClip : damageClip;
        var enemyClip = enemyDamageClip != null ? enemyDamageClip : damageClip;
        if (damageClip != null)
        {
            proceduralClips["Slash"] = damageClip;
        }
        if (playerClip != null)
        {
            proceduralClips["斩击"] = playerClip;
        }
        if (enemyClip != null)
        {
            proceduralClips["毒液"] = enemyClip;
        }
        if (healClip != null) proceduralClips["Heal"] = healClip;
        if (manaClip != null) proceduralClips["Mana"] = manaClip;
    }

    private void EnsureBGMLoaded()
    {
        if (bgmLoaded) return;
        titleBGMClips.Clear();
        battleBGMClips.Clear();
        LoadBGMResources();
        bgmLoaded = true;
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    public void ApplySettings(float newMusicVolume, float newSfxVolume)
    {
        SetMusicVolume(newMusicVolume);
        SetSfxVolume(newSfxVolume);
    }
}

public static class GameSettings
{
    private const string MusicVolumeKey = "Setting_MusicVolume";
    private const string SfxVolumeKey = "Setting_SfxVolume";
    private const string VibrationEnabledKey = "Setting_VibrationEnabled";

    public const float DefaultMusicVolume = 0.8f;
    public const float DefaultSfxVolume = 0.8f;
    public const bool DefaultVibrationEnabled = true;

    private static bool initialized;
    private static float musicVolume = DefaultMusicVolume;
    private static float sfxVolume = DefaultSfxVolume;
    private static bool vibrationEnabled = DefaultVibrationEnabled;

    public static float MusicVolume
    {
        get
        {
            EnsureInitialized();
            return musicVolume;
        }
    }

    public static float SfxVolume
    {
        get
        {
            EnsureInitialized();
            return sfxVolume;
        }
    }

    public static bool VibrationEnabled
    {
        get
        {
            EnsureInitialized();
            return vibrationEnabled;
        }
    }

    public static void Initialize()
    {
        if (initialized) return;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        vibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, DefaultVibrationEnabled ? 1 : 0) == 1;
        initialized = true;
    }

    public static void SetMusicVolume(float value)
    {
        EnsureInitialized();
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        ApplyAudioSettings();
    }

    public static void SetSfxVolume(float value)
    {
        EnsureInitialized();
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        ApplyAudioSettings();
    }

    public static void SetVibrationEnabled(bool enabled)
    {
        EnsureInitialized();
        vibrationEnabled = enabled;
        PlayerPrefs.SetInt(VibrationEnabledKey, vibrationEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void EnsureInitialized()
    {
        if (!initialized) Initialize();
    }

    private static void ApplyAudioSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ApplySettings(musicVolume, sfxVolume);
        }
    }
}
