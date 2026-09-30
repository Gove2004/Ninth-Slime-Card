using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomePage : MonoBehaviour
{
    public Button startGameButton;
    public Button settingsButton;
    public Button codexButton;
    public Button achievementsButton;
    public Button aboutButton;
    public Button gonggaoButton;

    public PanelScaleSHowHide startGamePanel;
    public PanelScaleSHowHide settingsPanel;
    public PanelScaleSHowHide codexPanel;
    public PanelScaleSHowHide achievementsPanel;
    public PanelScaleSHowHide aboutPanel;
    public PanelScaleSHowHide gonggaoPanel;

    public Button backFromAboutButton;
    public Button backFromSettingsButton;
    public Button backFromCodexButton;
    public Button backFromAchievementsButton;
    public Button backFromStartGameButton;
    public Button backFromGonggaoButton;

    public TextMeshProUGUI trophyText;
    public CodexPanel codexPanelRef;

    private void Start()
    {
        startGameButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnStartGameClicked));
        settingsButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnSettingsClicked));
        codexButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnCodexClicked));
        achievementsButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnAchievementsClicked));
        aboutButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnAboutClicked));
        gonggaoButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnGonggaoClicked));

        backFromAboutButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromAboutClicked));
        backFromSettingsButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromSettingsClicked));
        backFromCodexButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromCodexButton));
        backFromAchievementsButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromAchievementsClicked));
        backFromStartGameButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromStartGameClicked));
        backFromGonggaoButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnBackFromGonggaoClicked));

        startGamePanel.HidePanel();
        settingsPanel.HidePanel();
        codexPanel.HidePanel();
        achievementsPanel.HidePanel();
        aboutPanel.HidePanel();
        gonggaoPanel.HidePanel();

        if (PlayerPrefs.GetString("LastNoticeVersion", "") != Application.version)
        {
            PlayerPrefs.SetString("LastNoticeVersion", Application.version);
            gonggaoPanel.ShowPanel();
        }

        SettingsPanel.LoadVolume();
        RefreshTrophy();

        string bgmName = Random.value < 0.5f ? "标题界面bgm1" : "标题界面bgm2";
        AudioManager.Instance.PlayBGM(bgmName);
    }

    public void RefreshTrophy()
    {
        if (trophyText != null)
        {
            trophyText.text = GameCore.GetTrophy().ToString();
        }
    }

    private void OnStartGameClicked()
    {
        // 打开前强刷关卡入口文本（PanelScaleSHowHide 若不触发 OnEnable 也能拿到最新 runState）
        foreach (LevelSelect ls in startGamePanel.GetComponentsInChildren<LevelSelect>(true))
        {
            ls.RefreshDisplay();
        }
        startGamePanel.ShowPanel();
    }
    private void OnSettingsClicked() { settingsPanel.ShowPanel(); }
    private void OnCodexClicked() { codexPanel.ShowPanel(); codexPanelRef?.Show(); }
    private void OnAchievementsClicked() { achievementsPanel.ShowPanel(); }
    private void OnAboutClicked() { aboutPanel.ShowPanel(); }
    private void OnGonggaoClicked() { gonggaoPanel.ShowPanel(); }

    private void OnBackFromAboutClicked() { aboutPanel.HidePanel(); }
    private void OnBackFromSettingsClicked() { settingsPanel.HidePanel(); }
    private void OnBackFromCodexButton() { codexPanel.HidePanel(); }
    private void OnBackFromAchievementsClicked() { achievementsPanel.HidePanel(); }
    private void OnBackFromStartGameClicked() { startGamePanel.HidePanel(); }
    private void OnBackFromGonggaoClicked() { gonggaoPanel.HidePanel(); }
}
