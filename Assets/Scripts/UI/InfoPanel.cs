using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    public GameObject introObj;
    public GameObject settingsObj;
    public GameObject teamObj;
    public GameObject achievementObj;
    public AchievementsPanel achievementsPanel;
    public Button closeButton;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Toggle vibrationToggle;
    private bool updatingSettingsUI;

    private void Start()
    {
        EnsureReferences();
        EnsureSettingReferences();
        SyncSettingsUI();
        BindSettingEvents();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        // 初始时隐藏面板，除非已经在编辑器中设置为隐藏
        gameObject.SetActive(false);
    }

    public void ShowIntro()
    {
        EnsureReferences();
        ShowSection(introObj);
    }

    public void ShowSettings()
    {
        EnsureReferences();
        EnsureSettingReferences();
        SyncSettingsUI();
        BindSettingEvents();
        ShowSection(settingsObj);
    }

    public void ShowTeam()
    {
        EnsureReferences();
        ShowSection(teamObj);
    }

    public void ShowAchievements()
    {
        EnsureReferences();
        EnsureAchievementReferences();
        ShowSection(achievementObj);
        if (achievementsPanel != null) achievementsPanel.Refresh();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void EnsureAchievementReferences()
    {
        if (achievementObj == null)
        {
            var child = transform.Find("成就面板");
            if (child != null) achievementObj = child.gameObject;
        }

        if (achievementsPanel == null && achievementObj != null)
        {
            achievementsPanel = achievementObj.GetComponent<AchievementsPanel>();
        }
    }

    private void EnsureReferences()
    {
        if (settingsObj == null)
        {
            var child = transform.Find("设置滚动视图");
            if (child != null) settingsObj = child.gameObject;
        }
        if (introObj == null)
        {
            introObj = FindChildByNameCandidates(new[] { "介绍", "游戏介绍", "Intro", "intro" });
        }
        if (teamObj == null)
        {
            teamObj = FindChildByNameCandidates(new[] { "团队", "制作组", "Team", "team" });
        }
        if (closeButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null) continue;
                var name = button.gameObject.name;
                if (name == "Close" || name == "关闭" || name == "closeButton")
                {
                    closeButton = button;
                    break;
                }
            }
            if (closeButton == null && buttons.Length == 1) closeButton = buttons[0];
        }
        if (introObj == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child == settingsObj || child == achievementObj) continue;
                var closeRoot = GetCloseButtonRootObject();
                if (closeRoot != null && child == closeRoot) continue;
                introObj = child;
                break;
            }
        }
    }

    private void EnsureSettingReferences()
    {
        var settingRoot = settingsObj != null ? settingsObj.transform : transform;
        if (musicVolumeSlider == null || sfxVolumeSlider == null)
        {
            var sliders = settingRoot.GetComponentsInChildren<Slider>(true);
            foreach (var slider in sliders)
            {
                if (slider == null) continue;
                var name = slider.gameObject.name;
                if (musicVolumeSlider == null && name.Contains("音乐音量")) musicVolumeSlider = slider;
                if (sfxVolumeSlider == null && name.Contains("音效音量")) sfxVolumeSlider = slider;
            }
        }

        if (vibrationToggle == null)
        {
            vibrationToggle = FindBestVibrationToggle(settingRoot);
        }
    }

    private Toggle FindBestVibrationToggle(Transform root)
    {
        if (root == null) return null;
        var toggles = root.GetComponentsInChildren<Toggle>(true);
        Toggle best = null;
        int bestScore = int.MinValue;
        foreach (var toggle in toggles)
        {
            if (toggle == null) continue;
            string name = toggle.gameObject.name;
            int score = 0;
            if (name == "震动效果开关") score += 100;
            if (name.Contains("震动")) score += 50;
            if (name.Contains("CheckToggle")) score += 10;
            if (name.Contains("Item")) score -= 20;
            if (score > bestScore)
            {
                bestScore = score;
                best = toggle;
            }
        }
        return best;
    }

    private void BindSettingEvents()
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.interactable = true;
            vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);
            vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
        }
    }

    private void SyncSettingsUI()
    {
        updatingSettingsUI = true;
        GameSettings.Initialize();
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(GameSettings.VibrationEnabled);
        updatingSettingsUI = false;
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (updatingSettingsUI) return;
        GameSettings.SetMusicVolume(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (updatingSettingsUI) return;
        GameSettings.SetSfxVolume(value);
    }

    private void OnVibrationChanged(bool isOn)
    {
        if (updatingSettingsUI) return;
        GameSettings.SetVibrationEnabled(isOn);
        if (DamageEffectManager.Instance != null)
        {
            DamageEffectManager.Instance.ApplyVibrationSetting(isOn);
        }
    }

    private void ShowSection(GameObject target)
    {
        gameObject.SetActive(true);
        var closeRoot = GetCloseButtonRootObject();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            bool keepVisible = child == target || (closeRoot != null && child == closeRoot);
            child.SetActive(keepVisible);
        }
    }

    private GameObject FindChildByNameCandidates(string[] candidates)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var childName = child.name;
            foreach (var candidate in candidates)
            {
                if (childName.Contains(candidate))
                {
                    return child.gameObject;
                }
            }
        }
        return null;
    }

    private GameObject GetCloseButtonRootObject()
    {
        if (closeButton == null) return null;
        var current = closeButton.transform;
        while (current != null && current.parent != transform)
        {
            current = current.parent;
        }
        return current != null ? current.gameObject : null;
    }
}
