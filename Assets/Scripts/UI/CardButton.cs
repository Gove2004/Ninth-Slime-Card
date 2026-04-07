
using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    public BaseCard cardData = new 抽牌();
    private string lastDescription = "";
    private ulong lastCost = 0;
    private bool hasLastCost = false;
    private const float VisualRefreshInterval = 0.1f;
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> CardSpriteCache = new();
    private bool visualRefreshRequested = true;
    private float nextVisualRefreshTime;

    void Awake()
    {
        tooltip.SetActive(false);
        outline.enabled = false;

        SetData(cardData);
    }

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            action?.Invoke();
        });
    }



    void Update()
    {
        if (JustUIShow) return;
        if (!visualRefreshRequested && Time.unscaledTime < nextVisualRefreshTime) return;

        visualRefreshRequested = false;
        nextVisualRefreshTime = Time.unscaledTime + VisualRefreshInterval;

        if (cardData == null) return;
        string description = cardData.GetDynamicDescription();
        if (description != lastDescription)
        {
            cardDescriptionText.text = description;
            lastDescription = description;
        }
        if (!hasLastCost || cardData.Cost != lastCost)
        {
            costText.text = cardData.Cost.ToString();
            lastCost = cardData.Cost;
            hasLastCost = true;
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


    public void SetData(BaseCard card)
    {
        if (card == null)
        {
            Debug.LogWarning("尝试设置空卡牌数据");
            return;
        }
        cardData = card;

        cardNameText.text = card.Name;
        cardDescriptionText.text = card.GetDynamicDescription();
        lastDescription = cardDescriptionText.text;
        try
        {
            if (!string.IsNullOrEmpty(card.ImagePath))
            {
                if (!CardSpriteCache.TryGetValue(card.ImagePath, out var sprite))
                {
                    sprite = Resources.Load<Sprite>(card.ImagePath);
                    CardSpriteCache[card.ImagePath] = sprite;
                }
                image.sprite = sprite;
            }
            else
            {
                image.sprite = null;
            }
        }
        catch
        {
            Debug.LogWarning($"无法加载卡牌图片: {card.ImagePath}");
        }
        costText.text = card.Cost.ToString();
        lastCost = card.Cost;
        hasLastCost = true;
        RequestVisualRefresh(true);
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
        
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(true);
        }

        sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Append(transform.DOScale(Vector3.one, 0.2f));
        sequence.Join(tooltip.transform.DOMoveY(tooltip.transform.position.y - 20, 0.2f));
        sequence.Join(tooltipCanvasGroup.DOFade(0, 0.2f));

        sequence.AppendCallback(() =>
        {
            tooltip.SetActive(false);
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
         if (JustUIShow) return;

        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(true);
        }

        tooltip.SetActive(true);

        sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Append(transform.DOScale(Vector3.one * 1.1f, 0.2f));
        sequence.Join(tooltip.transform.DOMoveY(tooltip.transform.position.y + 20, 0.2f));
        sequence.Join(tooltipCanvasGroup.DOFade(1, 0.2f));
        RequestVisualRefresh(true);
    }
    #endregion



    // 作为按钮
    private Action action;

    public void SetAction(Action action) { this.action = action; }

    private void OnDestroy()
    {
        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(false);
        }
    }
}
