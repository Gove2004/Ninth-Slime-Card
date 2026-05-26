

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

    // 卡牌预制体，用于动态生成卡牌
    [SerializeField] private GameObject cardContainer;
    [SerializeField] private GameObject cardItemPrefab;
    private List<CardItem> cardItems = new List<CardItem>();
    

    // 当前选中的卡牌
    private CardItem focusedCard;


    private void Start()
    {
        cardInfoPanelRect = cardInfoPanel.GetComponent<RectTransform>();
        // 初始状态隐藏卡牌信息面板
        cardInfoPanel.alpha = 0f;
        cardInfoPanel.interactable = false;
        cardInfoPanel.blocksRaycasts = false;

        for (int i = 0; i < 2; i++)
        {
            // 创建示例卡牌数据
            BaseCard cardData = new 普通攻击示例();
            // 添加卡牌到容器
            OnAddCard(cardData);
        }
        for (int i = 0; i < 2; i++)
        {
            // 创建示例卡牌数据
            BaseCard cardData = new 不普通攻击();
            // 添加卡牌到容器
            OnAddCard(cardData);
        }
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

    public void OnCardFocused(CardItem cardItem)
    {
        EnsureFocusSequence();
        focusSequence.Append(cardInfoPanelRect.DOAnchorPos(new Vector3(150, 0, 0), 0.3f).SetEase(Ease.OutQuad));
        focusSequence.Join(cardInfoPanel.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));

        cardInfoNameText.text = cardItem.cardData.Name;
        cardInfoDescText.text = cardItem.cardData.GetDynamicDescription();

        focusedCard = cardItem;
    }

    public void OnCardUnfocused(CardItem cardItem)
    {
        EnsureFocusSequence();

        focusSequence.Append(cardInfoPanelRect.DOAnchorPos(new Vector3(0, 0, 0), 0.3f).SetEase(Ease.OutQuad));
        focusSequence.Join(cardInfoPanel.DOFade(0f, 0.3f).SetEase(Ease.OutQuad));

        if (focusedCard == cardItem)
        {
            focusedCard = null;
        }
    }


    public void OnAddCard(BaseCard cardData)
    {
        // 实例化卡牌预制体并设置数据
        GameObject cardObj = Instantiate(cardItemPrefab, transform);
        CardItem cardItem = cardObj.GetComponent<CardItem>();
        cardItems.Add(cardItem);
        // 设置卡牌数据
        cardItem.SetCardData(cardData);
        // 订阅卡牌事件
        cardItem.OnCardFocused += OnCardFocused;
        cardItem.OnCardUnfocused += OnCardUnfocused;
        // cardItem.OnCardUsed += OnRemoveCard;
        // cardItem.OnCardEndDrag += OnRemoveCard;
    }


    public void OnRemoveCard(CardItem cardItem)
    {
        // 从列表中移除并销毁卡牌对象
        if (cardItems.Contains(cardItem))
        {
            cardItems.Remove(cardItem);
            Destroy(cardItem.gameObject);
        }
    }
}