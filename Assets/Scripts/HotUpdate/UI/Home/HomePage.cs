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

    public PanelScaleSHowHide startGamePanel;
    public PanelScaleSHowHide settingsPanel;
    public PanelScaleSHowHide codexPanel;
    public PanelScaleSHowHide achievementsPanel;
    public PanelScaleSHowHide aboutPanel;

    public Button backFromAboutButton;
    public Button backFromSettingsButton;
    public Button backFromCodexButton;
    public Button backFromAchievementsButton;
    public Button backFromStartGameButton;

    public TextMeshProUGUI trophyText;

    private void Start()
    {
        startGameButton.onClick.AddListener(OnStartGameClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        codexButton.onClick.AddListener(OnCodexClicked);
        achievementsButton.onClick.AddListener(OnAchievementsClicked);
        aboutButton.onClick.AddListener(OnAboutClicked);

        backFromAboutButton.onClick.AddListener(OnBackFromAboutClicked);
        backFromSettingsButton.onClick.AddListener(OnBackFromSettingsClicked);
        backFromCodexButton.onClick.AddListener(OnBackFromCodexButton);
        backFromAchievementsButton.onClick.AddListener(OnBackFromAchievementsClicked);
        backFromStartGameButton.onClick.AddListener(OnBackFromStartGameClicked);

        startGamePanel.HidePanel();
        settingsPanel.HidePanel();
        codexPanel.HidePanel();
        achievementsPanel.HidePanel();
        aboutPanel.HidePanel();

        RefreshTrophy();
    }

    public void RefreshTrophy()
    {
        if (trophyText != null)
        {
            trophyText.text = GameCore.GetTrophy().ToString();
        }
    }

    private void OnStartGameClicked() { startGamePanel.ShowPanel(); }
    private void OnSettingsClicked() { settingsPanel.ShowPanel(); MessageToastManager.Instance.ShowMessage("设置 还没做！"); }
    private void OnCodexClicked() { codexPanel.ShowPanel(); MessageToastManager.Instance.ShowMessage("图鉴 还没做！"); }
    private void OnAchievementsClicked() { achievementsPanel.ShowPanel(); }
    private void OnAboutClicked() { aboutPanel.ShowPanel(); }

    private void OnBackFromAboutClicked() { aboutPanel.HidePanel(); }
    private void OnBackFromSettingsClicked() { settingsPanel.HidePanel(); }
    private void OnBackFromCodexButton() { codexPanel.HidePanel(); }
    private void OnBackFromAchievementsClicked() { achievementsPanel.HidePanel(); }
    private void OnBackFromStartGameClicked() { startGamePanel.HidePanel(); }
}
