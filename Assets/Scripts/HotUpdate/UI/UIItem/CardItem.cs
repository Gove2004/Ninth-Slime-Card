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
    public event System.Action<CardItem> OnCardBeginDrag;
    public event System.Action<CardItem> OnCardUsed;
    public event System.Action<CardItem> OnCardEndDrag;

    [SerializeField] private bool interactable = true;
    [SerializeField] private float playThreshold = 0.58f;

    private RectTransform rect;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 startAnchoredPos;
    private Quaternion startRotation;
    private Vector3 startScale;
    private Vector2 dragOffset;
    private Sequence focusSequence;

    public BaseCard cardData { get; private set; }

    public void SetCardData(BaseCard cardData)
    {
        this.cardData = cardData;
        cardImage.sprite = cardData.Image;
        cardNameText.text = cardData.Name;
        costText.text = cardData.Cost.ToString();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
    }

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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!interactable) return;

        rect = (RectTransform)transform;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        startAnchoredPos = rect.anchoredPosition;
        startRotation = rect.localRotation;
        startScale = rect.localScale;

        if (focusSequence != null && focusSequence.IsActive())
        {
            focusSequence.Kill(true);
        }

        RectTransform parentRect = originalParent as RectTransform;
        if (parentRect != null)
        {
            rect.SetParent(parentRect.parent, true);
            rect.SetAsLastSibling();
        }

        rect.DOKill();
        transform.DOKill();
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 1.1f;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, rect.position);
        dragOffset = screenPoint - eventData.position;
        OnCardBeginDrag?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactable || rect == null) return;
        rect.position = eventData.position + dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!interactable) return;

        bool shouldPlay = eventData.position.y > Screen.height * playThreshold;
        if (shouldPlay)
        {
            OnCardUsed?.Invoke(this);
            return;
        }

        ReturnToStartPosition();
        OnCardEndDrag?.Invoke(this);
    }

    public void ReturnToStartPosition()
    {
        if (rect == null)
        {
            rect = (RectTransform)transform;
        }

        RectTransform parentRect = originalParent as RectTransform;
        if (parentRect != null && rect.parent != parentRect)
        {
            rect.SetParent(parentRect, true);
            rect.SetSiblingIndex(Mathf.Min(originalSiblingIndex, parentRect.childCount - 1));
        }

        rect.DOAnchorPos(startAnchoredPos, 0.18f).SetEase(Ease.OutQuad);
        rect.DOLocalRotateQuaternion(startRotation, 0.18f).SetEase(Ease.OutQuad);
        rect.DOScale(startScale, 0.18f).SetEase(Ease.OutQuad);
    }

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
        focusSequence.Append(transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutQuad));
    }

    public void PlayFocusAnimationHide()
    {
        EnsureFocusSequence();
        focusSequence.Append(transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad));
    }

    public void OnDestroy()
    {
        if (focusSequence != null && focusSequence.IsActive())
        {
            focusSequence.Kill();
        }

        OnCardFocused = null;
        OnCardUnfocused = null;
        OnCardBeginDrag = null;
        OnCardUsed = null;
        OnCardEndDrag = null;
    }
}
