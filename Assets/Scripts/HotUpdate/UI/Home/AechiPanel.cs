using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AechiPanel : MonoBehaviour
{
    public Transform content;
    public Button backButton;
    public PanelScaleSHowHide scalePanel;
    public GameObject aechiItemPrefab;

    private void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }
    }

    private void OnEnable()
    {
        RefreshItems();
    }

    public void RefreshItems()
    {
        if (AchievementManager.Instance == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        List<AchievementConfigData> configs = AchievementManager.Instance.GetAllAchievements();

        foreach (AchievementConfigData config in configs)
        {
            if (string.IsNullOrEmpty(config.成就ID))
            {
                continue;
            }

            GameObject go = Instantiate(aechiItemPrefab, content);
            AechiItem aechiItem = go.GetComponent<AechiItem>();
            if (aechiItem != null)
            {
                aechiItem.Setup(config, this);
            }
        }
    }

    public void OnItemPurchased()
    {
        HomePage homePage = Object.FindFirstObjectByType<HomePage>();
        if (homePage != null)
        {
            homePage.RefreshTrophy();
        }
    }

    private void OnBackClicked()
    {
        if (scalePanel != null)
        {
            scalePanel.HidePanel();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
