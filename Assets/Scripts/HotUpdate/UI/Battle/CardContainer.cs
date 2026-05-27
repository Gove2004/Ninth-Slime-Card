using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class CardContainer : MonoBehaviour
{
    [SerializeField] private CanvasGroup cardInfoPanel;
    private RectTransform cardInfoPanelRect;
    [SerializeField] private TextMeshProUGUI cardInfoNameText;
    [SerializeField] private TextMeshProUGUI cardInfoDescText;
    [SerializeField] private HandCardFanLayout cardContainer;
    [SerializeField] private GameObject cardItemPrefab;

    private readonly List<CardItem> cardItems = new List<CardItem>();
    private CardItem focusedCard;
    private Sequence focusSequence;

    private void Start()
    {
        cardInfoPanelRect = cardInfoPanel.GetComponent<RectTransform>();
        cardInfoPanel.alpha = 0f;
        cardInfoPanel.interactable = false;
        cardInfoPanel.blocksRaycasts = false;
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

    public void RefreshHand(IReadOnlyList<BaseCard> cards, bool interactable)
    {
        ClearAllCards();

        if (cards == null)
        {
            cardContainer.RebuildLayout();
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            AddCard(cards[i], interactable);
        }

        cardContainer.RebuildLayout();
    }

    public void OnCardFocused(CardItem cardItem)
    {
        EnsureFocusSequence();
        focusSequence.Append(cardInfoPanelRect.DOAnchorPos(new Vector3(100, 0, 0), 0.3f).SetEase(Ease.OutQuad));
        focusSequence.Join(cardInfoPanel.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));

        cardInfoNameText.text = cardItem.cardData.Name;
        cardInfoDescText.text = cardItem.cardData.Description();
        focusedCard = cardItem;
    }

    public void OnCardUnfocused(CardItem cardItem)
    {
        EnsureFocusSequence();
        focusSequence.Append(cardInfoPanelRect.DOAnchorPos(Vector3.zero, 0.3f).SetEase(Ease.OutQuad));
        focusSequence.Join(cardInfoPanel.DOFade(0f, 0.3f).SetEase(Ease.OutQuad));

        if (focusedCard == cardItem)
        {
            focusedCard = null;
        }
    }

    private void AddCard(BaseCard cardData, bool interactable)
    {
        GameObject cardObj = Instantiate(cardItemPrefab, cardContainer.transform);
        CardItem cardItem = cardObj.GetComponent<CardItem>();
        cardItems.Add(cardItem);
        cardItem.SetCardData(cardData);
        cardItem.SetInteractable(interactable);
        cardItem.OnCardFocused += OnCardFocused;
        cardItem.OnCardUnfocused += OnCardUnfocused;
        cardItem.OnCardBeginDrag += OnCardBeginDrag;
        cardItem.OnCardEndDrag += OnCardEndDrag;
        cardItem.OnCardUsed += OnCardUsed;
    }

    private void OnCardBeginDrag(CardItem cardItem)
    {
        cardContainer.SetDragging(true);
        if (focusedCard == cardItem)
        {
            focusedCard = null;
        }
    }

    private void OnCardUsed(CardItem cardItem)
    {
        bool success = BattleManager.Instance != null && BattleManager.Instance.RequestPlayCard(cardItem.cardData);
        if (!success)
        {
            cardItem.ReturnToStartPosition();
            cardContainer.RebuildLayout();
        }

        cardContainer.SetDragging(false);
    }

    private void OnCardEndDrag(CardItem cardItem)
    {
        cardContainer.SetDragging(false);
        cardContainer.RebuildLayout();
    }

    private void ClearAllCards()
    {
        focusedCard = null;

        for (int i = 0; i < cardItems.Count; i++)
        {
            CardItem cardItem = cardItems[i];
            if (cardItem == null)
            {
                continue;
            }

            cardItem.OnCardFocused -= OnCardFocused;
            cardItem.OnCardUnfocused -= OnCardUnfocused;
            cardItem.OnCardBeginDrag -= OnCardBeginDrag;
            cardItem.OnCardEndDrag -= OnCardEndDrag;
            cardItem.OnCardUsed -= OnCardUsed;
            Destroy(cardItem.gameObject);
        }

        cardItems.Clear();
    }

    private void OnDestroy()
    {
        if (focusSequence != null && focusSequence.IsActive())
        {
            focusSequence.Kill();
        }
    }
}
