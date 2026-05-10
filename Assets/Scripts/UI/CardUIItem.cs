
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardUIItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool JustUIShow = false;
    // UI元素引用
    public TextMeshProUGUI cardNameText;
    public TextMeshProUGUI cardDescriptionText;
    public GameObject tooltip;
    public CanvasGroup tooltipCanvasGroup;
    public Outline outline;
    public TextMeshProUGUI costText;

    public Image image;
    // Card数据
    public BaseCard cardData;
    private string lastDescription = "";
    private string lastDisplayName = "";
    private string lastImagePath = "";
    private ulong lastCost = 0;
    private bool hasLastCost = false;
    private bool lastMirageState = false;
    private Color defaultNameColor;
    private Color defaultCostColor;
    private Color defaultImageColor;
    private readonly Color mirageNameColor = new(0.72f, 0.92f, 1f, 1f);
    private readonly Color mirageCostColor = new(0.75f, 0.96f, 1f, 1f);
    private readonly Color mirageImageColor = new(0.78f, 0.9f, 1f, 1f);
    private readonly Vector3[] tooltipCorners = new Vector3[4];
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> CardSpriteCache = new();
    private const float VisualRefreshInterval = 0.1f;
    
    private bool dragging;
    public bool IsDragging => dragging; // Public property for CardList to access
    
    private Vector2 dragOffset;
    private Vector2 originalTooltipAnchored;
    
    // Layout
    public Vector2 targetPosition;
    public float targetRotation;
    public Vector3 targetScale = Vector3.one;
    public bool isHovered = false;

    private CardList cardList;
    private Canvas _parentCanvas;
    private bool visualRefreshRequested = true;
    private float nextVisualRefreshTime;
    private int externalAnimationLockCount;

    public void Init(CardList list)
    {
        this.cardList = list;
    }

    void Awake()
    {
        var tooltipRect = (RectTransform)tooltip.transform;
        originalTooltipAnchored = tooltipRect.anchoredPosition;
        tooltip.SetActive(false);
        outline.enabled = false;
        defaultNameColor = cardNameText.color;
        defaultCostColor = costText.color;
        defaultImageColor = image.color;

        if (cardData == null)
        {
            cardData = new 抽牌();
        }
        SetData(cardData);
    }

    void Start()
    {
        if (cardList == null)
        {
            cardList = GetComponentInParent<CardList>();
        }
        _parentCanvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (IsExternalAnimationActive)
        {
            RefreshVisualDataIfNeeded();
            return;
        }

        if (JustUIShow)
        {
            // Let layout groups fully control collection-view card placement.
            var rect = (RectTransform)transform;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            RefreshVisualDataIfNeeded();
            return;
        }

        // Smooth movement and rotation
        if (!dragging)
        {
            var rect = (RectTransform)transform;
            
            Vector2 finalPos = targetPosition;
            float finalRot = targetRotation;
            Vector3 finalScale = targetScale;

            if (isHovered)
            {
                finalPos.y += 80f; // Hover pop up
                finalRot = 0f;     // Reset rotation
                finalScale = Vector3.one * 1.5f; // Scale up
                transform.SetAsLastSibling();
                
                if (tooltip.activeSelf)
                {
                    KeepTooltipOnScreen();
                }
            }

            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, finalPos, Time.deltaTime * 15f);
            rect.localRotation = Quaternion.Lerp(rect.localRotation, Quaternion.Euler(0, 0, finalRot), Time.deltaTime * 15f);
            rect.localScale = Vector3.Lerp(rect.localScale, finalScale, Time.deltaTime * 15f);
        }
        else
        {
             // When dragging, reset rotation and scale slightly up
             var rect = (RectTransform)transform;
             rect.localRotation = Quaternion.Lerp(rect.localRotation, Quaternion.identity, Time.deltaTime * 20f);
             rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * 1.2f, Time.deltaTime * 15f);
        }

        RefreshVisualDataIfNeeded();
    }

    public bool IsExternalAnimationActive => externalAnimationLockCount > 0;

    public void BeginExternalAnimation()
    {
        externalAnimationLockCount++;
    }

    public void EndExternalAnimation()
    {
        if (externalAnimationLockCount > 0)
        {
            externalAnimationLockCount--;
        }
    }

    private void RefreshVisualDataIfNeeded()
    {
        if (!visualRefreshRequested && Time.unscaledTime < nextVisualRefreshTime) return;

        visualRefreshRequested = false;
        nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;
        RefreshVisualData();
    }

    private void RefreshVisualData()
    {
        if (cardData == null) return;
        string description = cardData.GetDynamicDescription();
        if (description != lastDescription)
        {
            cardDescriptionText.text = description;
            lastDescription = description;
        }
        string displayName = cardData.GetDisplayName();
        if (displayName != lastDisplayName)
        {
            cardNameText.text = displayName;
            lastDisplayName = displayName;
        }
        ulong displayCost = cardData.GetDisplayCost();
        if (!hasLastCost || displayCost != lastCost)
        {
            costText.text = displayCost.ToString();
            lastCost = displayCost;
            hasLastCost = true;
        }
        string displayImagePath = cardData.GetDisplayImagePath();
        if (displayImagePath != lastImagePath)
        {
            TryLoadCardImage(displayImagePath);
            lastImagePath = displayImagePath;
        }
        if (lastMirageState != cardData.IsMirageCard)
        {
            lastMirageState = cardData.IsMirageCard;
            ApplyMirageVisual(lastMirageState);
        }
    }

    private void RequestVisualRefresh(bool immediate = false)
    {
        visualRefreshRequested = true;
        if (immediate)
        {
            nextVisualRefreshTime = 0f;
        }
    }

    private void KeepTooltipOnScreen()
    {
        if (_parentCanvas == null) return;
        var tooltipRect = (RectTransform)tooltip.transform;

        Camera cam = null;
        if (_parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = _parentCanvas.worldCamera;

        tooltipRect.GetWorldCorners(tooltipCorners);

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, tooltipCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, tooltipCorners[2]);

        float shiftX = 0;

        if (bottomLeft.x < 0)
        {
            shiftX = -bottomLeft.x + 20; // Padding
        }
        else if (topRight.x > Screen.width)
        {
            shiftX = Screen.width - topRight.x - 20;
        }

        if (shiftX != 0)
        {
            Vector2 currentScreenPos = RectTransformUtility.WorldToScreenPoint(cam, tooltipRect.position);
            currentScreenPos.x += shiftX;

            Vector3 newWorldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(tooltipRect.parent as RectTransform, currentScreenPos, cam, out newWorldPos))
            {
                // Only adjust X to avoid interfering with DOTween Y animation
                Vector3 currentPos = tooltipRect.position;
                tooltipRect.position = new Vector3(newWorldPos.x, currentPos.y, currentPos.z);
            }
        }
    }


    public void SetData(BaseCard card)
    {
        cardData = card;
        if (card == null)
        {
            Debug.LogWarning("尝试设置空卡牌数据");
            return;
        }

        cardNameText.text = card.GetDisplayName();
        cardDescriptionText.text = card.GetDynamicDescription();
        lastDescription = cardDescriptionText.text;
        lastDisplayName = cardNameText.text;
        string displayImagePath = card.GetDisplayImagePath();
        TryLoadCardImage(displayImagePath);
        lastImagePath = displayImagePath;
        ulong displayCost = card.GetDisplayCost();
        costText.text = displayCost.ToString();
        lastCost = displayCost;
        hasLastCost = true;
        lastMirageState = card.IsMirageCard;
        ApplyMirageVisual(lastMirageState);
        RequestVisualRefresh(true);
    }

    private void TryLoadCardImage(string imagePath)
    {
        try
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                image.sprite = null;
                return;
            }

            if (!CardSpriteCache.TryGetValue(imagePath, out var sprite))
            {
                sprite = Resources.Load<Sprite>(imagePath);
                CardSpriteCache[imagePath] = sprite;
            }

            image.sprite = sprite;
        }
        catch
        {
            Debug.LogWarning($"无法加载卡牌图片: {imagePath}");
        }
    }

    private void ApplyMirageVisual(bool mirage)
    {
        cardNameText.color = mirage ? mirageNameColor : defaultNameColor;
        costText.color = mirage ? mirageCostColor : defaultCostColor;
        image.color = mirage ? mirageImageColor : defaultImageColor;
    }

    #region 点击选中卡牌
    public bool IsSelected = false;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (JustUIShow) return;

        if (!IsSelected)  // 第一次点击，选中卡牌
        {
            Selected();
            EventCenter.Publish(GameEvents.CardSelected, new CardSelectionEventContext(cardData));
        }
        else  // 再次点击，取消选中
        {
            Deselected();
            EventCenter.Publish(GameEvents.CardDeselected, new CardSelectionEventContext(cardData));
        }
    }

    public void Selected()
    {
        IsSelected = true;
        outline.enabled = true;
    }

    public void Deselected()
    {
        IsSelected = false;
        outline.enabled = false;   
    }
    #endregion


    # region 鼠标悬停动画
    Sequence sequence;
    public void OnPointerExit(PointerEventData eventData)
    {
        if (JustUIShow) return;

        isHovered = false;
        
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(true);
        }

        var tooltipRect = (RectTransform)tooltip.transform;
        sequence = DOTween.Sequence();
        // transform scale is handled in Update
        sequence.Join(tooltipRect.DOAnchorPosY(originalTooltipAnchored.y, 0.2f));
        sequence.Join(tooltipCanvasGroup.DOFade(0, 0.2f));

        sequence.AppendCallback(() =>
        {
            tooltip.SetActive(false);
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
         if (JustUIShow) return;

        isHovered = true;

        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(true);
        }

        tooltip.SetActive(true);

        var tooltipRect = (RectTransform)tooltip.transform;
        tooltipRect.anchoredPosition = originalTooltipAnchored;
        sequence = DOTween.Sequence();
        // transform scale is handled in Update
        sequence.Join(tooltipRect.DOAnchorPosY(originalTooltipAnchored.y + 20, 0.2f));
        sequence.Join(tooltipCanvasGroup.DOFade(1, 0.2f));
        RequestVisualRefresh(true);
    }
    #endregion


    public void OnBeginDrag(PointerEventData eventData)
    {
         if (JustUIShow) return;

        dragging = true;
        var rect = (RectTransform)transform;
        
        // Remove old layout/placeholder logic
        
        outline.enabled = true;
        tooltip.SetActive(false);
        ((RectTransform)tooltip.transform).anchoredPosition = originalTooltipAnchored;
        
        // Ensure dragged card renders on top
        transform.SetAsLastSibling();
        
        var parentRect = transform.parent as RectTransform;
        if (parentRect != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint);
            dragOffset = rect.anchoredPosition - localPoint;
        }
        else
        {
            dragOffset = Vector2.zero;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
         if (JustUIShow) return;

        var rect = (RectTransform)transform;
        var parentRect = transform.parent as RectTransform;
        if (parentRect != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint);
            rect.anchoredPosition = localPoint + dragOffset;
            
            // Notify layout system
            if (cardList != null)
            {
                cardList.OnCardDrag(this);
            }
        }
        else
        {
            transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
         if (JustUIShow) return;

        dragging = false;
        var player = BattleManager.Instance?.player as Player;
        bool canPlay = player != null && player.CanUseTurnActions && cardData != null && cardData.Cost <= player.mana;
        
        // Improved validation: Check if card is dragged high enough (e.g., > 150 pixels above original position or specific screen Y threshold)
        // Using a threshold relative to screen height is often safer for different resolutions.
        // Let's assume the hand area is at the bottom. We require the drag to be significantly upwards.
        
        bool draggedOut = false;
        
        // Option 1: Check distance from original position (vertical only)
        // if (transform.position.y - originalParent.position.y > 200) ... 
        
        // Option 2: Check absolute screen Y position. 
        // Assuming hand is at bottom, let's say it needs to be in the top 3/4 of the screen or above a certain line.
        float playThresholdY = Screen.height * 0.5f; // Must be above bottom 30% of screen
        
        if (eventData.position.y > playThresholdY)
        {
            draggedOut = true;
        }

        // Also respect the container check if needed, but Y threshold is usually better for "playing" cards.
        // Alternatively, use the old check BUT with a buffer? 
        // The user complained "once dragged... cannot cancel". This implies existing check is too loose (draggedOut is true too easily).
        // By adding a Y threshold, we ensure they must drag UP, not just wiggle left/right.

        if (canPlay && draggedOut)
        {
            player.UI_PlayCard(cardData);
        }
        else
        {
            // Just let the update loop handle the return animation
            outline.enabled = IsSelected;
        }
    }

    private void OnDestroy()
    {
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(false);
        }
    }

}
