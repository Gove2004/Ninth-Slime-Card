using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class CardItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image cardImage;
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI costText;

    public event System.Action<CardItem> OnCardFocused;
    public event System.Action<CardItem> OnCardUnfocused;
    public event System.Action<CardItem> OnCardUsed;
    public event System.Action<CardItem> OnCardEndDrag;


    #region 交互动画
    
    /// <summary>
    /// 是否可交互的卡牌，部分场景下部可交互，比如敌人出牌，图鉴，选牌界面
    /// </summary>
    [SerializeField] private bool interactable = true;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable) return;

        OnCardFocused?.Invoke(this);
        PlayFocusAnimationShow();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!interactable) return;

        OnCardUnfocused?.Invoke(this);
        PlayFocusAnimationHide();
    }

    

    private Vector2 offsetToMouse;  // 鼠标与卡牌中心的偏移，用于拖动时保持相对位置


    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!interactable) return;

        // 计算鼠标与卡牌中心的偏移
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera, 
            out offsetToMouse);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactable) return;

        // 跟随鼠标移动
        transform.position = eventData.position - offsetToMouse;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!interactable) return;

        // 触发卡牌使用事件
        OnCardUsed?.Invoke(this);
    }



    private Sequence focusSequence;
    private void EnsureFocusSequence()
    {
        if (focusSequence == null)
        {
            focusSequence = DOTween.Sequence();
        }
        if (focusSequence.IsActive())
        {
            focusSequence.Kill(true);
        }
    }

    public void PlayFocusAnimationShow()
    {
        EnsureFocusSequence();
        focusSequence.Append(transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutQuad));
    }

    public void PlayFocusAnimationHide()
    {
        EnsureFocusSequence();
        focusSequence.Append(transform.DOScale(1f, 0.3f).SetEase(Ease.OutQuad));
    }

    #endregion

    public BaseCard cardData { get; private set; }

    public void SetCardData(BaseCard cardData)
    {
        this.cardData = cardData;
        // 设置卡牌显示
        cardImage.sprite = cardData.Image;
        cardNameText.text = cardData.Name;
        costText.text = cardData.Cost.ToString();
    }

    public void OnDestroy()
    {
        if (focusSequence != null && focusSequence.IsActive())
        {
            focusSequence.Kill();
        }
        OnCardFocused = null;
        OnCardUnfocused = null;
        OnCardUsed = null;
        OnCardEndDrag = null;
    }
}