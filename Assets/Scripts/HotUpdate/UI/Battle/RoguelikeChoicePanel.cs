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
    private bool hasShown;
    private readonly List<GameObject> spawnedItems = new();

    public void Start()
    {
        // 仅在尚未通过 Show() 打开时自隐藏，避免场景中保存为非激活时
        // Show() 激活后首次 Start() 反把面板关掉
        if (!hasShown)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show(Action onComplete)
    {
        this.onComplete = onComplete;
        currentRound = 0;
        hasShown = true;
        gameObject.SetActive(true);

        if (scalePanel != null)
        {
            scalePanel.ShowPanel();
        }

        if (addCardButton != null)
        {
            addCardButton.onClick.RemoveAllListeners();
            addCardButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnAddCardClicked));
        }

        if (removeCardButton != null)
        {
            removeCardButton.onClick.RemoveAllListeners();
            removeCardButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnRemoveCardClicked));
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(OnSkipClicked));
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

        GameObject go = Instantiate(choiceCardPrefab, GetSpawnRoot(cardChoiceArea));
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
                AudioManager.Instance?.PlaySound("UI");
                AddCardToDeck(config.id);
                currentRound++;
                ShowRound();
            });
        }
    }

    private void OnRemoveCardClicked()
    {
        // 牌组为空时无卡可删，保持主菜单可点，避免玩家卡死在面板里
        if (GameCore.runState == null || GameCore.runState.playerDeckIds.Count == 0)
        {
            MessageToastManager.Instance.ShowMessage("牌组为空，无卡可删");
            return;
        }

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

        GameObject go = Instantiate(choiceCardPrefab, GetSpawnRoot(deckChoiceArea));
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
                AudioManager.Instance?.PlaySound("UI");
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

    // 字段可直接接 ScrollView 根节点：生成卡时自动落到 Viewport/Content，
    // 使显隐/层级作用于整个 ScrollView，同时保证卡牌可滚动
    private static Transform GetSpawnRoot(Transform area)
    {
        if (area == null) return null;
        Transform content = area.Find("Viewport/Content");
        return content != null ? content : area;
    }

    private static Sprite LoadCardSprite(string cardName)
    {
        var handle = ResCore.LoadAssetSync<Sprite>($"Card_{cardName}");
        return handle?.GetAssetObject<Sprite>();
    }
}
