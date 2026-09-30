using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GoveKits.Runtime.Storage;

public class SettingsPanel : MonoBehaviour
{
    public Slider volumeSlider;
    public Dropdown langDropdown;
    public TextMeshProUGUI volValueText;

    private void OnEnable()
    {
        LoadFromPrefs();
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        if (langDropdown != null)
        {
            langDropdown.onValueChanged.AddListener(OnLangChanged);
        }
    }

    private void OnDisable()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        }
        if (langDropdown != null)
        {
            langDropdown.onValueChanged.RemoveListener(OnLangChanged);
        }
    }

    private void OnVolumeChanged(float v)
    {
        AudioCore.SetVolume(AudioChannel.BGM, v);
        PlayerPrefs.SetFloat("Volume", v);
        UpdateVolumeText(v);
    }

    private void OnLangChanged(int i)
    {
        string lang = (i == 0) ? "zh" : "en";
        PlayerPrefs.SetString("Language", lang);
        LanguageTable.ApplyLanguage(lang);
    }

    private void LoadFromPrefs()
    {
        float savedVol = PlayerPrefs.GetFloat("Volume", 1f);
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVol;
            UpdateVolumeText(savedVol);
        }

        string savedLang = PlayerPrefs.GetString("Language", "zh");
        int index = savedLang == "zh" ? 0 : 1;
        if (langDropdown != null)
        {
            langDropdown.value = index;
        }
        LanguageTable.ApplyLanguage(savedLang); // 首次加载时应用语言
    }

    private void UpdateVolumeText(float v)
    {
        if (volValueText != null)
        {
            volValueText.text = (v * 100).ToString("0") + "%";
        }
    }

    public void Close()
    {
        var panel = GetComponentInParent<PanelScaleSHowHide>();
        if (panel != null)
        {
            panel.HidePanel();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 启动时应用已保存的音量到 AudioCore（供 HomePage.Start 调用）
    /// </summary>
    public static void LoadVolume()
    {
        AudioCore.SetVolume(AudioChannel.BGM, PlayerPrefs.GetFloat("Volume", 1f));
    }
}
