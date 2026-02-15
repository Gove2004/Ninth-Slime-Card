using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUI : MonoBehaviour
{
    public Button startBattleButton;
    public Button introButton;
    public Button teamButton;
    public Button achievementButton;
    public InfoPanel infoPanel;
    public TextMeshProUGUI maxScoreText;
    public TMP_Dropdown dropdown;


    public void Start()
    {
        EnsureReferences();
        if (startBattleButton != null) startBattleButton.onClick.AddListener(OnStartBattleClicked);
        if (introButton != null) introButton.onClick.AddListener(OnIntroClicked);
        if (teamButton != null) teamButton.onClick.AddListener(OnTeamClicked);
        if (achievementButton == null)
        {
            var buttons = FindObjectsOfType<Button>(true);
            foreach (var button in buttons)
            {
                if (button != null && button.gameObject.name == "成就")
                {
                    achievementButton = button;
                    break;
                }
            }
        }
        if (achievementButton != null)
        {
            achievementButton.onClick.AddListener(OnAchievementClicked);
        }

        if (dropdown != null) dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    public void OnEnable()
    {
        UpdateMaxScore();
    }

    private void OnIntroClicked()
    {
        if (infoPanel != null)
        {
            infoPanel.ShowIntro();
        }
    }

    private void OnTeamClicked()
    {
        if (infoPanel != null)
        {
            infoPanel.ShowTeam();
        }
    }

    private void OnAchievementClicked()
    {
        if (infoPanel != null)
        {
            infoPanel.ShowAchievements();
        }
    }


    public void UpdateMaxScore()
    {
        if (maxScoreText == null) return;
        if (GameManager.Instance == null) return;
        maxScoreText.text = $"最高分数: {GameManager.Instance.maxScore}";
    }

    public void OnStartBattleClicked()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SwitchSecne(true);
    }

    public void OnDropdownValueChanged(int index)
    {
        // 这里可以根据index来设置不同的选项
        Debug.Log($"Dropdown value changed: {index}");
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetDiff(index+1);
    }

    private void EnsureReferences()
    {
        if (dropdown == null)
        {
            dropdown = GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                var dropdowns = FindObjectsOfType<TMP_Dropdown>(true);
                if (dropdowns.Length > 0) dropdown = dropdowns[0];
            }
        }
        if (maxScoreText == null)
        {
            maxScoreText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
