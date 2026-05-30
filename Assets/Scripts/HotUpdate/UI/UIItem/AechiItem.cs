using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AechiItem : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text progressText;
    public TMP_Text buttonText;
    public Slider progressSlider;
    public Toggle toggle;
    public Button purchaseButton;

    private AchievementConfigData config;
    private AechiPanel panel;

    public void Setup(AchievementConfigData config, AechiPanel panel)
    {
        this.config = config;
        this.panel = panel;

        if (nameText != null)
        {
            nameText.text = config.名称;
            nameText.raycastTarget = false;
        }

        if (descriptionText != null)
        {
            descriptionText.text = config.成就描述;
            descriptionText.raycastTarget = false;
        }

        if (progressText != null)
        {
            progressText.raycastTarget = false;
        }

        if (toggle != null)
        {
            toggle.interactable = false;
        }

        if (purchaseButton != null)
        {
            purchaseButton.interactable = true;
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
        }

        if (buttonText != null)
        {
            buttonText.text = $"-{config.奖杯需求}";
        }

        Refresh();
    }

    public void Refresh()
    {
        bool unlocked = AchievementManager.Instance != null && AchievementManager.Instance.IsUnlocked(config.成就ID);

        if (progressSlider != null)
        {
            progressSlider.value = unlocked ? 1f : 0f;
        }

        if (progressText != null)
        {
            progressText.text = unlocked ? $"{config.成就步数}/{config.成就步数}" : $"0/{config.成就步数}";
        }

        if (toggle != null)
        {
            toggle.isOn = unlocked;
        }

        if (purchaseButton != null)
        {
            purchaseButton.gameObject.SetActive(!unlocked);
        }
    }

    private void OnPurchaseClicked()
    {
        if (AchievementManager.Instance == null)
        {
            return;
        }

        if (AchievementManager.Instance.TryPurchase(config))
        {
            Refresh();

            if (panel != null)
            {
                panel.OnItemPurchased();
            }
        }
    }
}
