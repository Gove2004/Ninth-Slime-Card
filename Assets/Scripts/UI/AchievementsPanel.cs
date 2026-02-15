using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementsPanel : MonoBehaviour
{
    public TMP_FontAsset fontAsset;
    public Color completedColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public ScrollRect scrollRect;
    public RectTransform contentRect;
    public GameObject itemTemplate;
    private readonly List<GameObject> items = new();

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        EnsureReferences();
        ClearItems();
        var manager = AchievementManager.Instance;
        if (manager == null) return;
        if (scrollRect == null || contentRect == null || itemTemplate == null) return;
        var statuses = manager.GetAchievementStatuses();
        foreach (var status in statuses)
        {
            CreateItem(status);
        }
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureReferences()
    {
        if (scrollRect == null)
        {
            var root = transform.Find("AchievementScrollView");
            if (root != null) scrollRect = root.GetComponent<ScrollRect>();
        }
        if (contentRect == null && scrollRect != null)
        {
            var viewport = scrollRect.transform.Find("Viewport");
            if (viewport != null)
            {
                var content = viewport.Find("Content");
                if (content != null) contentRect = content.GetComponent<RectTransform>();
            }
        }
        if (itemTemplate == null && contentRect != null)
        {
            var template = contentRect.Find("AchievementItemTemplate");
            if (template != null) itemTemplate = template.gameObject;
        }
    }

    private void ClearItems()
    {
        foreach (var item in items)
        {
            if (item != null) Destroy(item);
        }
        items.Clear();
    }

    private void CreateItem(AchievementManager.AchievementStatus status)
    {
        var item = Instantiate(itemTemplate, contentRect);
        item.name = $"Achievement_{status.id}";
        item.SetActive(true);
        var text = item.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"{status.name}  {status.description}";
            text.font = GetFont();
        }
        var toggle = item.transform.Find("CheckToggle")?.GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = status.completed;
            toggle.interactable = false;
        }

        items.Add(item);
    }

    private TMP_FontAsset GetFont()
    {
        if (fontAsset != null) return fontAsset;
        return TMP_Settings.defaultFontAsset;
    }
}
