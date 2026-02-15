using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanel : MonoBehaviour
{
    public GameObject introObj;
    public GameObject teamObj;
    public GameObject achievementObj;
    public AchievementsPanel achievementsPanel;
    public Button closeButton;

    private void Start()
    {
        EnsureReferences();
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        // 初始时隐藏面板，除非已经在编辑器中设置为隐藏
        gameObject.SetActive(false);
    }

    public void ShowIntro()
    {
        gameObject.SetActive(true);
        if (introObj != null) introObj.SetActive(true);
        if (teamObj != null) teamObj.SetActive(false);
        if (achievementObj != null) achievementObj.SetActive(false);
    }

    public void ShowTeam()
    {
        gameObject.SetActive(true);
        if (introObj != null) introObj.SetActive(false);
        if (teamObj != null) teamObj.SetActive(true);
        if (achievementObj != null) achievementObj.SetActive(false);
    }

    public void ShowAchievements()
    {
        gameObject.SetActive(true);
        if (introObj != null) introObj.SetActive(false);
        if (teamObj != null) teamObj.SetActive(false);
        EnsureAchievementReferences();
        if (achievementObj != null) achievementObj.SetActive(true);
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
    }
}
