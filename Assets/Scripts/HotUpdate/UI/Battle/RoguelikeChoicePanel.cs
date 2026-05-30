using System;
using System.Collections.Generic;
using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoguelikeChoicePanel : MonoBehaviour
{
    public PanelScaleSHowHide scalePanel;
    public TMP_Text headerText;
    public Button addCardButton;
    public Button removeCardButton;
    public Button skipButton;
    public Transform cardChoiceArea;
    public Transform deckChoiceArea;
    public GameObject choiceCardPrefab;

    private const int TotalRounds = 5;
    private int currentRound;
    private Action onComplete;
    private readonly List<GameObject> spawnedItems = new();

    public void Start()
    {
        gameObject.SetActive(false);
    }

    public void Show(Action onComplete)
    {
        this.onComplete = onComplete;
        currentRound = 0;
        gameObject.SetActive(true);

        if (scalePanel != null)
        {
            scalePanel.ShowPanel();
        }

        if (addCardButton != null)
        {
            addCardButton.onClick.AddListener(OnAddCardClicked);
        }

        if (removeCardButton != null)
        {
            removeCardButton.onClick.AddListener(OnRemoveCardClicked);
        }

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        ShowRound();
    }

    private void ShowRound()
    {
        ClearSpawnedItems();

        if (currentRound >= TotalRounds)
        {
            FinishChoices();
            return;
        }

        if (headerText != null)
        {
            headerText.text = $"选择奖励 ({currentRound + 1}/{TotalRounds})";
        }

        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        SetAreaActive(cardChoiceArea, false);
        SetAreaActive(deckChoiceArea, false);

        if (addCardButton != null) addCardButton.gameObject.SetActive(true);
        if (removeCardButton != null) removeCardButton.gameObject.SetActive(true);
        if (skipButton != null) skipButton.gameObject.SetActive(true);

        if (addCardButton != null)
        {
            addCardButton.transform.parent.SetAsLastSibling();
        }
    }

    private void OnAddCardClicked()
    {
        if (addCardButton != null) addCardButton.gameObject.SetActive(false);
        if (removeCardButton != null) removeCardButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);

        ShowCardChoices();
    }

    private void ShowCardChoices()
    {
        SetAreaActive(cardChoiceArea, true);
        SetAreaActive(deckChoiceArea, false);

        if (cardChoiceArea != null) cardChoiceArea.SetAsLastSibling();

        List<CardConfigData> allCards = ConfigCore.LoadAll<CardConfigData>();
        List<CardConfigData> choices = new();
        List<CardConfigData> pool = new(allCards);

        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            choices.Add(pool[index]);
            pool.RemoveAt(index);
        }

        foreach (CardConfigData config in choices)
        {
            SpawnChoiceCard(config);
        }
    }

    private void SpawnChoiceCard(CardConfigData config)
    {
        if (choiceCardPrefab == null || cardChoiceArea == null)
        {
            return;
        }

        GameObject go = Instantiate(choiceCardPrefab, cardChoiceArea);
        spawnedItems.Add(go);

        BaseCard cardInstance = CardFactoryCore.CreateCard(config.id);

        SetCardImage(go, config.名称);

        TMP_Text nameText = go.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text descText = go.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();

        if (cardInstance != null)
        {
            if (nameText != null) nameText.text = cardInstance.Name;
            if (descText != null) descText.text = cardInstance.Description();
        }
        else
        {
            if (nameText != null) nameText.text = config.名称;
            if (descText != null) descText.text = config.描述;
        }

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() =>
            {
                AddCardToDeck(config.id);
                currentRound++;
                ShowRound();
            });
        }
    }

    private void OnRemoveCardClicked()
    {
        if (addCardButton != null) addCardButton.gameObject.SetActive(false);
        if (removeCardButton != null) removeCardButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);

        ShowDeckRemoval();
    }

    private void ShowDeckRemoval()
    {
        SetAreaActive(cardChoiceArea, false);
        SetAreaActive(deckChoiceArea, true);

        if (deckChoiceArea != null) deckChoiceArea.SetAsLastSibling();

        if (GameCore.runState == null)
        {
            return;
        }

        Dictionary<int, int> cardCounts = new();
        foreach (int id in GameCore.runState.playerDeckIds)
        {
            if (!cardCounts.ContainsKey(id))
            {
                cardCounts[id] = 0;
            }
            cardCounts[id]++;
        }

        foreach (var pair in cardCounts)
        {
            BaseCard card = CardFactoryCore.CreateCard(pair.Key);
            if (card == null)
            {
                continue;
            }

            SpawnDeckCard(card, pair.Value);
        }
    }

    private void SpawnDeckCard(BaseCard card, int count)
    {
        if (choiceCardPrefab == null || deckChoiceArea == null)
        {
            return;
        }

        GameObject go = Instantiate(choiceCardPrefab, deckChoiceArea);
        spawnedItems.Add(go);

        SetCardImage(go, card.Name);

        TMP_Text nameText = go.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text descText = go.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();

        if (nameText != null) nameText.text = $"{card.Name} x{count}";
        if (descText != null) descText.text = card.Description();

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            int cardId = card.Id;
            btn.onClick.AddListener(() =>
            {
                RemoveCardFromDeck(cardId);
                currentRound++;
                ShowRound();
            });
        }
    }

    private static void SetCardImage(GameObject go, string cardName)
    {
        Transform imageTransform = go.transform.Find("Image");
        if (imageTransform == null)
        {
            return;
        }

        Image img = imageTransform.GetComponent<Image>();
        if (img == null)
        {
            return;
        }

        Sprite sprite = LoadCardSprite(cardName);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
    }

    private void OnSkipClicked()
    {
        currentRound++;
        ShowRound();
    }

    private void AddCardToDeck(int cardId)
    {
        if (GameCore.runState != null)
        {
            GameCore.runState.playerDeckIds.Add(cardId);
        }

        BaseCard card = CardFactoryCore.CreateCard(cardId);
        MessageToastManager.Instance.ShowMessage(card != null ? $"加入牌组：{card.Name}" : "加入牌组");
    }

    private void RemoveCardFromDeck(int cardId)
    {
        if (GameCore.runState == null)
        {
            return;
        }

        int index = GameCore.runState.playerDeckIds.IndexOf(cardId);
        if (index >= 0)
        {
            GameCore.runState.playerDeckIds.RemoveAt(index);
            BaseCard card = CardFactoryCore.CreateCard(cardId);
            MessageToastManager.Instance.ShowMessage(card != null ? $"移除牌组：{card.Name}" : "移除牌组");
        }
    }

    private void FinishChoices()
    {
        ClearSpawnedItems();
        gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    private void ClearSpawnedItems()
    {
        foreach (GameObject go in spawnedItems)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        spawnedItems.Clear();
    }

    private void SetAreaActive(Transform area, bool active)
    {
        if (area == null) return;

        area.gameObject.SetActive(active);

        CanvasGroup cg = area.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = area.gameObject.AddComponent<CanvasGroup>();
        }

        cg.blocksRaycasts = active;
        cg.interactable = active;
    }

    private static Sprite LoadCardSprite(string cardName)
    {
        var handle = ResCore.LoadAssetSync<Sprite>($"Card_{cardName}");
        return handle?.GetAssetObject<Sprite>();
    }
}
