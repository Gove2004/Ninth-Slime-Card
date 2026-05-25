

using UnityEngine;
using UnityEngine.UI;

public class HomePage : MonoBehaviour
{
    public Button startGameButton;
    public Button settingsButton;
    public Button codexButton;
    public Button achievementsButton;
    public Button aboutButton;

    private void Start()
    {
        startGameButton.onClick.AddListener(OnStartGameClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        codexButton.onClick.AddListener(OnCodexClicked);
        achievementsButton.onClick.AddListener(OnAchievementsClicked);
        aboutButton.onClick.AddListener(OnAboutClicked);
    }

    private void OnStartGameClicked()
    {
        MessageToastCanvas.Instance.ShowMessage("Start Game clicked!");
    }

    private void OnSettingsClicked()
    {
        MessageToastCanvas.Instance.ShowMessage("Settings clicked!");
    }

    private void OnCodexClicked()
    {
        MessageToastCanvas.Instance.ShowMessage("Codex clicked!");
    }

    private void OnAchievementsClicked()
    {
        MessageToastCanvas.Instance.ShowMessage("Achievements clicked!");
    }

    private void OnAboutClicked()
    {
        MessageToastCanvas.Instance.ShowMessage("About clicked!");
    }
}