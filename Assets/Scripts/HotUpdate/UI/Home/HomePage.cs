

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

    private void Start()
    {
        startGameButton.onClick.AddListener(OnStartGameClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        codexButton.onClick.AddListener(OnCodexClicked);
        achievementsButton.onClick.AddListener(OnAchievementsClicked);
        aboutButton.onClick.AddListener(OnAboutClicked);

        backFromAboutButton.onClick.AddListener(OnBackFromAboutClicked);
        backFromSettingsButton.onClick.AddListener(OnBackFromSettingsClicked);
        backFromCodexButton.onClick.AddListener(OnBackFromCodexClicked);
        backFromAchievementsButton.onClick.AddListener(OnBackFromAchievementsClicked);
        backFromStartGameButton.onClick.AddListener(OnBackFromStartGameClicked);

        // 初始状态
        startGamePanel.HidePanel();
        settingsPanel.HidePanel();
        codexPanel.HidePanel();
        achievementsPanel.HidePanel();
        aboutPanel.HidePanel();
    }

    // 显示
    private void OnStartGameClicked() { startGamePanel.ShowPanel(); }
    private void OnSettingsClicked() { settingsPanel.ShowPanel(); MessageToastManager.Instance.ShowMessage("设置 还没做！"); }
    private void OnCodexClicked() { codexPanel.ShowPanel(); MessageToastManager.Instance.ShowMessage("图鉴 还没做！"); }
    private void OnAchievementsClicked() { achievementsPanel.ShowPanel(); MessageToastManager.Instance.ShowMessage("成就 还没做！"); }
    private void OnAboutClicked() { aboutPanel.ShowPanel(); }

    // 隐藏
    private void OnBackFromAboutClicked() { aboutPanel.HidePanel(); }
    private void OnBackFromSettingsClicked() { settingsPanel.HidePanel(); }
    private void OnBackFromCodexClicked() { codexPanel.HidePanel(); }
    private void OnBackFromAchievementsClicked() { achievementsPanel.HidePanel(); }
    private void OnBackFromStartGameClicked() { startGamePanel.HidePanel(); }
}