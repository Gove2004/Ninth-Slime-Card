

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardList : MonoBehaviour
{
    public List<CardUIItem> cardUIItems = new List<CardUIItem>();
    public GameObject cardPreb;
    public Transform container;

    [Header("Layout Settings")]
    public float cardSpacing = 100f; // Reduced from 150f to fit 7 cards
    public float moveSpeed = 15f;
    public float cardWidth = 180f; // Default width, can be adjusted or auto-detected

    [Header("Fan Layout Settings")]
    public float fanRadius = 2500f;
    public float fanAngleMax = 30f;
    public float yOffset = -50f; // Center card y-position
    public float edgePadding = 24f;

    // 选中的卡牌
    public bool AblePlay => selectedCard != null && BattleManager.Instance?.player is Player player && player.mana >= selectedCard.Cost;
    public BaseCard selectedCard;

    void Start()
    {
        // 尝试禁用容器上的 LayoutGroup，因为我们将手动控制布局
        LayoutGroup layoutGroup = container.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }
        
        // ... (rest of Start)

        // 监听卡牌选择事件
        EventCenter.Register<CardSelectionEventContext>(GameEvents.CardSelected, context =>
        {
            // 取消原本选中的卡牌
            if (selectedCard != null)
            {
                Card2UIItem(selectedCard).Deselected();
            }
            selectedCard = context.Card;
        });

        // 监听卡牌取消选择事件
        EventCenter.Register<CardSelectionEventContext>(GameEvents.CardDeselected, _ =>
        {
            selectedCard = null;
        });

        EventCenter.Register<HandEventContext>(GameEvents.PlayerHandChanged, _ =>
        {
            SyncFromBattleHand();
        });

        EventCenter.Register<CardEventContext>(GameEvents.PlayerCardRefreshed, context =>
        {
            RefreshCard(context.Card);
        });

        SyncFromBattleHand();
    }

    void Update()
    {
        UpdateCardLayout();
    }

    private void UpdateCardLayout()
    {
        if (cardUIItems.Count == 0) return;

        // --- Auto-detect card width ---
        if (cardUIItems.Count > 0)
        {
            var firstRect = (RectTransform)cardUIItems[0].transform;
            float w = firstRect != null ? firstRect.rect.width : cardWidth;
            if (w > 0f) cardWidth = w;
        }

        int count = cardUIItems.Count;
        float totalAngle = GetAdaptiveTotalAngle(count);

        float yCenter = -fanRadius + yOffset;

        for (int i = 0; i < count; i++)
        {
            CardUIItem item = cardUIItems[i];
            if (item == null) continue;

            float t = 0.5f;
            if (count > 1) t = (float)i / (count - 1);

            // Interpolate angle from Left (negative) to Right (positive)
            float angle = Mathf.Lerp(-totalAngle / 2f, totalAngle / 2f, t);
            float rad = angle * Mathf.Deg2Rad;

            // Calculate position on the arc
            float x = fanRadius * Mathf.Sin(rad);
            float y = fanRadius * Mathf.Cos(rad) + yCenter;

            item.targetPosition = new Vector2(x, y);
            item.targetRotation = -angle; // Invert rotation for correct fan tilt 

            // Only set sibling index if not dragging and not hovered
            if (!item.IsDragging && !item.isHovered) 
            {
                item.transform.SetSiblingIndex(i);
            }
        }
    }

    private float GetAdaptiveTotalAngle(int count)
    {
        if (count <= 1) return 0f;
        if (fanRadius <= 0.001f) return 0f;

        float desiredAnglePerCard = (cardSpacing / fanRadius) * Mathf.Rad2Deg;
        float desiredTotalAngle = (count - 1) * desiredAnglePerCard;
        float widthLimitedAngle = GetWidthLimitedTotalAngle();
        float maxAllowed = Mathf.Min(fanAngleMax, widthLimitedAngle);

        return Mathf.Clamp(desiredTotalAngle, 0f, maxAllowed);
    }

    private float GetWidthLimitedTotalAngle()
    {
        RectTransform containerRect = container as RectTransform;
        if (containerRect == null || fanRadius <= 0.001f) return fanAngleMax;

        float halfContainerWidth = containerRect.rect.width * 0.5f;
        if (halfContainerWidth <= 1f) return fanAngleMax;
        float halfCardWidth = cardWidth * 0.5f;
        float maxX = Mathf.Max(0f, halfContainerWidth - halfCardWidth - edgePadding);
        float normalized = Mathf.Clamp01(maxX / fanRadius);
        float halfAngle = Mathf.Asin(normalized) * Mathf.Rad2Deg;
        return halfAngle * 2f;
    }

    // 当卡牌被拖拽时调用
    public void OnCardDrag(CardUIItem draggedItem)
    {
        if (draggedItem == null) return;

        // 根据拖拽卡牌的 X 位置更新列表顺序
        int currentIndex = cardUIItems.IndexOf(draggedItem);
        if (currentIndex < 0) return;

        float currentX = ((RectTransform)draggedItem.transform).anchoredPosition.x;

        // 检查是否需要向左交换
        if (currentIndex > 0)
        {
            CardUIItem leftItem = cardUIItems[currentIndex - 1];
            // 阈值可以稍微调整，比如 cardSpacing / 2
            if (currentX < ((RectTransform)leftItem.transform).anchoredPosition.x)
            {
                SwapCards(currentIndex, currentIndex - 1);
                return; // 一次只交换一个，避免混乱
            }
        }

        // 检查是否需要向右交换
        if (currentIndex < cardUIItems.Count - 1)
        {
            CardUIItem rightItem = cardUIItems[currentIndex + 1];
            if (currentX > ((RectTransform)rightItem.transform).anchoredPosition.x)
            {
                SwapCards(currentIndex, currentIndex + 1);
                return;
            }
        }
    }

    private void SwapCards(int indexA, int indexB)
    {
        CardUIItem temp = cardUIItems[indexA];
        cardUIItems[indexA] = cardUIItems[indexB];
        cardUIItems[indexB] = temp;
    }

    public void DrawCard(BaseCard card)
    {
        if (card == null) return;

        var existing = Card2UIItem(card);
        if (existing != null)
        {
            existing.SetData(card);
            return;
        }

        cardUIItems.Add(CreateCardUIItem(card));
    }

    public void PlayCard(BaseCard card)
    {
        bool removed = false;
        for (int i = cardUIItems.Count - 1; i >= 0; i--)
        {
            var cui = cardUIItems[i];
            if (cui == null || cui.cardData != card) continue;
            cardUIItems.RemoveAt(i);
            Destroy(cui.gameObject);
            removed = true;
        }

        if (removed && selectedCard == card)
        {
            selectedCard = null;
        }
    }

    public void RefreshCard(BaseCard card)
    {
        var cui = Card2UIItem(card);
        if (cui == null) return;
        cui.SetData(card);
    }

    public void Clear()
    {
        foreach (var item in cardUIItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        cardUIItems.Clear();
        selectedCard = null;
    }

    public void SyncFromBattleHand()
    {
        var player = BattleManager.Instance?.player;
        if (player == null)
        {
            Clear();
            return;
        }

        var orderedItems = new List<CardUIItem>(player.Cards.Count);
        foreach (var card in player.Cards)
        {
            var existing = Card2UIItem(card);
            if (existing == null)
            {
                existing = CreateCardUIItem(card);
            }
            else
            {
                existing.SetData(card);
            }

            orderedItems.Add(existing);
        }

        for (int i = 0; i < cardUIItems.Count; i++)
        {
            var item = cardUIItems[i];
            if (item != null && !orderedItems.Contains(item))
            {
                Destroy(item.gameObject);
            }
        }

        cardUIItems = orderedItems;

        if (selectedCard != null && !player.Cards.Contains(selectedCard))
        {
            selectedCard = null;
        }
    }



    private CardUIItem CreateCardUIItem(BaseCard card)
    {
        GameObject cardUIObj = Instantiate(cardPreb, container);
        CardUIItem cardUIItem = cardUIObj.GetComponent<CardUIItem>();
        cardUIItem.SetData(card);
        cardUIItem.Init(this);
        return cardUIItem;
    }

    private CardUIItem Card2UIItem(BaseCard card)
    {
        foreach (var cui in cardUIItems)
        {
            if (cui.cardData == card)
            {
                return cui;
            }
        }
        return null;
    }
}
