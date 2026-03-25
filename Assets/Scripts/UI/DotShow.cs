using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DotShow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image image;
    public TextMeshProUGUI descriptionText;
    public Vector2 durationOffset = new Vector2(-1f, 1f);
    public Vector2 durationSize = new Vector2(18f, 10f);
    public Vector2 hoverTooltipOffset = new Vector2(14f, -14f);
    public float hoverTooltipMaxWidth = 300f;

    private RectTransform rootRect;
    private RectTransform textRect;
    private Dot currentDot;
    private bool isHovered;

    private static RectTransform sharedTooltipRect;
    private static TextMeshProUGUI sharedTooltipText;
    private static CanvasGroup sharedTooltipCanvasGroup;
    private static Canvas sharedTooltipCanvas;
    private static bool sharedTooltipReady;


    void Awake()
    {
        ResolveRefs();
        EnsureSharedTooltip();
    }

    void Update()
    {
        if (IsTooltipBlocked())
        {
            if (isHovered)
            {
                isHovered = false;
                HideTooltip();
            }
            return;
        }

        bool shouldHover = currentDot != null && IsMouseOverSelf();
        if (shouldHover)
        {
            if (!isHovered)
            {
                isHovered = true;
                EnsureSharedTooltip();
                ShowTooltip();
            }
            RefreshTooltipText();
            UpdateTooltipPosition();
            return;
        }

        if (isHovered)
        {
            isHovered = false;
            HideTooltip();
        }
    }

    void OnDisable()
    {
        if (isHovered)
        {
            isHovered = false;
            HideTooltip();
        }
    }

    public void SetData(Dot dot)
    {
        ResolveRefs();
        ConfigureDurationText();
        EnsureSharedTooltip();
        currentDot = dot;

        Sprite icon = null;
        if (dot != null && dot.sourceCard != null)
        {
            string displayImagePath = dot.sourceCard.GetDisplayImagePath();
            if (!string.IsNullOrEmpty(displayImagePath))
            {
                icon = Resources.Load<Sprite>(displayImagePath);
            }
        }

        if (image != null)
        {
            image.sprite = icon;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = icon == null ? new Color(0f, 0f, 0f, 0.65f) : Color.white;
        }

        if (descriptionText != null)
        {
            int duration = dot != null ? Mathf.Max(0, dot.duration) : 0;
            descriptionText.text = duration.ToString();
        }

        if (isHovered)
        {
            RefreshTooltipText();
        }
    }

    private void ResolveRefs()
    {
        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }
        if (image == null)
        {
            image = GetComponent<Image>();
        }
        if (descriptionText == null)
        {
            descriptionText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (textRect == null && descriptionText != null)
        {
            textRect = descriptionText.rectTransform;
        }
        if (image != null)
        {
            image.raycastTarget = true;
        }
    }

    private void ConfigureDurationText()
    {
        if (descriptionText == null || textRect == null) return;

        textRect.anchorMin = new Vector2(1f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(1f, 0f);
        textRect.anchoredPosition = durationOffset;
        textRect.sizeDelta = durationSize;

        descriptionText.alignment = TextAlignmentOptions.BottomRight;
        if (descriptionText.fontSize < 10f)
        {
            descriptionText.fontSize = 10f;
        }
        descriptionText.textWrappingMode = TextWrappingModes.NoWrap;
        descriptionText.color = Color.white;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsTooltipBlocked()) return;
        if (currentDot == null) return;
        isHovered = true;
        EnsureSharedTooltip();
        RefreshTooltipText();
        ShowTooltip();
        UpdateTooltipPosition();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        HideTooltip();
    }

    private void EnsureSharedTooltip()
    {
        ResolveSharedTooltipCanvas();
        if (sharedTooltipCanvas == null) return;

        bool tooltipOnWrongCanvas = sharedTooltipRect != null && sharedTooltipRect.transform.parent != sharedTooltipCanvas.transform;
        if (tooltipOnWrongCanvas)
        {
            sharedTooltipRect = null;
            sharedTooltipText = null;
            sharedTooltipCanvasGroup = null;
            sharedTooltipReady = false;
        }

        if (sharedTooltipReady && sharedTooltipRect != null && sharedTooltipText != null) return;

        Transform existing = sharedTooltipCanvas.transform.Find("DotHoverTooltip");
        if (existing != null)
        {
            sharedTooltipRect = existing as RectTransform;
            sharedTooltipCanvasGroup = existing.GetComponent<CanvasGroup>();
            sharedTooltipText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            sharedTooltipReady = sharedTooltipRect != null && sharedTooltipText != null;
            HideTooltip();
            return;
        }

        GameObject tooltipRoot = new GameObject("DotHoverTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        sharedTooltipRect = tooltipRoot.GetComponent<RectTransform>();
        var tooltipBg = tooltipRoot.GetComponent<Image>();
        sharedTooltipCanvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
        tooltipRoot.transform.SetParent(sharedTooltipCanvas.transform, false);
        tooltipRoot.transform.SetAsLastSibling();

        sharedTooltipRect.anchorMin = new Vector2(0f, 1f);
        sharedTooltipRect.anchorMax = new Vector2(0f, 1f);
        sharedTooltipRect.pivot = new Vector2(0f, 1f);
        sharedTooltipRect.sizeDelta = new Vector2(220f, 80f);

        tooltipBg.color = new Color(0f, 0f, 0f, 0.85f);
        tooltipBg.raycastTarget = false;

        sharedTooltipCanvasGroup.alpha = 0f;
        sharedTooltipCanvasGroup.blocksRaycasts = false;
        sharedTooltipCanvasGroup.interactable = false;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(tooltipRoot.transform, false);
        var textRectTransform = textObj.GetComponent<RectTransform>();
        textRectTransform.anchorMin = new Vector2(0f, 0f);
        textRectTransform.anchorMax = new Vector2(1f, 1f);
        textRectTransform.pivot = new Vector2(0.5f, 0.5f);
        textRectTransform.offsetMin = new Vector2(8f, 8f);
        textRectTransform.offsetMax = new Vector2(-8f, -8f);

        sharedTooltipText = textObj.GetComponent<TextMeshProUGUI>();
        if (descriptionText != null && descriptionText.font != null)
        {
            sharedTooltipText.font = descriptionText.font;
            sharedTooltipText.fontSharedMaterial = descriptionText.fontSharedMaterial;
        }
        sharedTooltipText.color = Color.white;
        sharedTooltipText.fontSize = 16f;
        sharedTooltipText.textWrappingMode = TextWrappingModes.Normal;
        sharedTooltipText.alignment = TextAlignmentOptions.TopLeft;
        sharedTooltipText.raycastTarget = false;
        sharedTooltipText.text = string.Empty;

        sharedTooltipReady = true;
        HideTooltip();
    }

    private void ResolveSharedTooltipCanvas()
    {
        if (sharedTooltipCanvas != null && sharedTooltipCanvas.gameObject.activeInHierarchy) return;

        sharedTooltipCanvas = null;
        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        if (canvases != null && canvases.Length > 0)
        {
            for (int i = canvases.Length - 1; i >= 0; i--)
            {
                if (canvases[i] != null && canvases[i].gameObject.activeInHierarchy)
                {
                    sharedTooltipCanvas = canvases[i];
                    break;
                }
            }
        }

        if (sharedTooltipCanvas != null) return;

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allCanvases == null || allCanvases.Length == 0) return;
        for (int i = 0; i < allCanvases.Length; i++)
        {
            var canvas = allCanvases[i];
            if (canvas == null || !canvas.gameObject.activeInHierarchy) continue;
            if (canvas.GetComponent<GraphicRaycaster>() == null) continue;
            sharedTooltipCanvas = canvas;
            return;
        }
        sharedTooltipCanvas = allCanvases[0];
    }

    private void RefreshTooltipText()
    {
        if (!sharedTooltipReady || sharedTooltipText == null) return;

        string text = currentDot?.description?.Invoke();
        if (string.IsNullOrEmpty(text))
        {
            text = currentDot?.sourceCard?.GetDynamicDescription();
        }
        if (string.IsNullOrEmpty(text))
        {
            text = "无描述";
        }

        sharedTooltipText.text = text;
        sharedTooltipText.ForceMeshUpdate();
        Vector2 preferred = sharedTooltipText.GetPreferredValues(text, hoverTooltipMaxWidth - 16f, 0f);
        float width = Mathf.Clamp(preferred.x + 16f, 120f, hoverTooltipMaxWidth);
        float height = Mathf.Clamp(preferred.y + 16f, 32f, 260f);
        sharedTooltipRect.sizeDelta = new Vector2(width, height);
    }

    private void UpdateTooltipPosition()
    {
        if (!sharedTooltipReady || sharedTooltipRect == null || sharedTooltipCanvas == null) return;

        Camera cam = sharedTooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sharedTooltipCanvas.worldCamera;
        RectTransform canvasRect = sharedTooltipCanvas.transform as RectTransform;
        if (canvasRect == null) return;

        float scaleFactor = sharedTooltipCanvas.scaleFactor > 0f ? sharedTooltipCanvas.scaleFactor : 1f;
        Vector2 tooltipSizePx = sharedTooltipRect.rect.size * scaleFactor;
        Vector2 target = (Vector2)Input.mousePosition + hoverTooltipOffset;

        float minX = 6f;
        float maxX = Screen.width - tooltipSizePx.x - 6f;
        if (maxX < minX) maxX = minX;

        float minY = tooltipSizePx.y + 6f;
        float maxY = Screen.height - 6f;
        if (maxY < minY) minY = maxY;

        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.y = Mathf.Clamp(target.y, minY, maxY);

        Vector3 worldPoint;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, target, cam, out worldPoint)) return;
        sharedTooltipRect.position = worldPoint;
    }

    private bool IsMouseOverSelf()
    {
        if (rootRect == null) return false;

        ResolveSharedTooltipCanvas();
        Camera cam = null;
        if (sharedTooltipCanvas != null && sharedTooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = sharedTooltipCanvas.worldCamera;
        }
        return RectTransformUtility.RectangleContainsScreenPoint(rootRect, Input.mousePosition, cam);
    }

    private static bool IsTooltipBlocked()
    {
        return RougeUI.IsPanelOpen;
    }

    private static void ShowTooltip()
    {
        if (sharedTooltipRect == null || sharedTooltipCanvasGroup == null) return;
        sharedTooltipRect.gameObject.SetActive(true);
        sharedTooltipCanvasGroup.alpha = 1f;
    }

    private static void HideTooltip()
    {
        if (sharedTooltipRect == null || sharedTooltipCanvasGroup == null) return;
        sharedTooltipCanvasGroup.alpha = 0f;
        sharedTooltipRect.gameObject.SetActive(false);
    }
}
