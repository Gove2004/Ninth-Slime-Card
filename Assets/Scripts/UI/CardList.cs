

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
        EventCenter.Register("CardSelected", (param) =>
        {
            // 取消原本选中的卡牌
            if (selectedCard != null)
            {
                Card2UIItem(selectedCard).Deselected();
            }
            selectedCard = param as BaseCard;
        });

        // 监听卡牌取消选择事件
        EventCenter.Register("CardDeselected", (param) =>
        {
            selectedCard = null;
        });

        EventCenter.Register("Player_DrawCard", (param) =>
        {
            DrawCard(param as BaseCard);
        });

        EventCenter.Register("Player_PlayCard", (param) =>
        {
            selectedCard = null;  // 出牌后取消选中状态
            PlayCard(param as BaseCard);
        });

        EventCenter.Register("Player_RemoveCard", (param) =>
        {
            PlayCard(param as BaseCard); // 复用 PlayCard 的逻辑来移除 UI
        });
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
        
        // Calculate angle per card based on spacing and radius
        float anglePerCard = 5f; 
        if (fanRadius > 0) 
        {
            anglePerCard = (cardSpacing / fanRadius) * Mathf.Rad2Deg;
        }
        anglePerCard = Mathf.Max(anglePerCard, 2f);

        float totalAngle = (count - 1) * anglePerCard;
        if (totalAngle > fanAngleMax) totalAngle = fanAngleMax;

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
        // 创建新的CardUIItem实例
        GameObject cardUIObj = Instantiate(cardPreb, container);
        CardUIItem cardUIItem = cardUIObj.GetComponent<CardUIItem>();
        cardUIItem.SetData(card);
        // 初始化引用
        cardUIItem.Init(this);

        cardUIItems.Add(cardUIItem);
    }

    public void PlayCard(BaseCard card)
    {
        var cui = Card2UIItem(card);

        if (cui != null)
        {
            cardUIItems.Remove(cui);
            Destroy(cui.gameObject);
        }
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
