using System;
using System.Collections.Generic;
using System.IO;
using Slime.Core;
using UnityEngine;

namespace Slime.Game
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 4;
        /// <summary>MetaState 的 key=value 文本快照，避免给 JsonUtility 写一堆 List 包装类。</summary>
        public string meta = string.Empty;
        public string nickname = string.Empty;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
    }

    /// <summary>本地存档。纯本地 JSON，不依赖任何后端。</summary>
    public static class SaveService
    {
        private const string FileName = "slime_save.json";
        private static SaveData cached;

        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static SaveData Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Load();
                }

                return cached;
            }
        }

        private static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null)
                    {
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] load failed: " + e.Message);
            }

            return new SaveData();
        }

        public static void Save()
        {
            if (cached == null)
            {
                return;
            }

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(cached, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] write failed: " + e.Message);
            }
        }

        public static void ResetAll()
        {
            cached = new SaveData();
            Save();
        }

        public static MetaState ToMeta(SaveData data)
        {
            return data == null ? new MetaState() : MetaState.Deserialize(data.meta);
        }

        public static void FromMeta(SaveData target, MetaState meta)
        {
            if (target == null || meta == null)
            {
                return;
            }

            target.meta = meta.Serialize();
        }

        public static void DeleteFile()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] delete failed: " + e.Message);
            }

            cached = null;
        }
    }

    /// <summary>音频。音频资源位于 Resources/Audios/{Music,Sound}，显式地址加载，不使用任何资源包系统。</summary>
    public sealed class AudioService
    {
        /// <summary>Resources 下的音频根目录。取名字时不要再带 Music/ 或 Sound/ 前缀。</summary>
        private const string Root = "Audios/";
        private const string MusicDir = "Music/";
        private const string SoundDir = "Sound/";

        private readonly AudioSource music;
        private readonly AudioSource sfx;
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        public AudioService(GameObject host)
        {
            music = host.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.spatialBlend = 0f;

            sfx = host.AddComponent<AudioSource>();
            sfx.loop = false;
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            var data = SaveService.Current;
            MusicVolume = data.musicVolume;
            SfxVolume = data.sfxVolume;

            ApplyVolume();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            SaveService.Current.musicVolume = MusicVolume;
            ApplyVolume();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            SaveService.Current.sfxVolume = SfxVolume;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            music.volume = MusicVolume;
            sfx.volume = SfxVolume;
        }

        /// <summary>
        /// 按名字取 clip。资源实际放在 Audios/Music 与 Audios/Sound 两个子目录下，
        /// 所以先查子目录，再兜底查平铺在 Audios 根下的情况——避免以后挪文件又静默失声。
        /// </summary>
        private AudioClip Get(string dir, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            string key = dir + name;
            AudioClip clip;
            if (cache.TryGetValue(key, out clip))
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(Root + dir + name);
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>(Root + name);
            }

            if (clip == null)
            {
                Debug.LogWarning("[Audio] 找不到音频资源：" + key + "（已尝试 " + Root + dir + name + " 与 " + Root + name + "）");
            }

            cache[key] = clip;
            return clip;
        }

        public void PlayMusic(string name)
        {
            var clip = Get(MusicDir, name);
            if (clip == null)
            {
                return;
            }

            if (music.clip == clip && music.isPlaying)
            {
                return;
            }

            music.clip = clip;
            music.Play();
        }

        public void PlaySfx(string name)
        {
            var clip = Get(SoundDir, name);
            if (clip == null || SfxVolume <= 0.001f)
            {
                return;
            }

            // 音量已由 sfx.volume 统一控制，这里不再重复乘一次
            sfx.PlayOneShot(clip);
        }
    }

    /// <summary>内容库：从 Resources/Configs 读取 CSV，不经过任何资源更新管线。</summary>
    public static class ContentService
    {
        public static CardDatabase LoadCards()
        {
            var asset = Resources.Load<TextAsset>("Configs/cards");
            if (asset == null)
            {
                Debug.LogWarning("[Content] cards.csv 缺失，使用内置兜底卡池。");
                return CardDatabase.CreateFallback();
            }

            var db = CardDatabase.FromCsv(asset.text);
            if (db.Count == 0)
            {
                Debug.LogWarning("[Content] cards.csv 解析为空，使用内置兜底卡池。");
                return CardDatabase.CreateFallback();
            }

            return db;
        }

        public static AchievementDatabase LoadAchievements()
        {
            var asset = Resources.Load<TextAsset>("Configs/achievements");
            if (asset == null)
            {
                Debug.LogWarning("[Content] achievements.csv 缺失，成就页将为空。");
                return AchievementDatabase.FromCsv(string.Empty);
            }

            return AchievementDatabase.FromCsv(asset.text);
        }
    }
}
