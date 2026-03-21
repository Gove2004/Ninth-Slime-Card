using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CardCollectionView : MonoBehaviour
{
    public RectTransform seriesButtonRoot;
    public Button seriesButtonTemplate;
    public RectTransform cardGridRoot;
    public GameObject cardItemTemplate;
    public GameObject cardPrefab;
    public Button prevPageButton;
    public Button nextPageButton;
    public Text pageText;
    public RectTransform manaButtonRoot;
    public Button manaButtonTemplate;
    public InputField searchInput;
    public int pageSize = 8;

    private readonly List<GameObject> cardItems = new();
    private readonly Dictionary<string, Button> seriesButtons = new();
    private readonly Dictionary<string, Button> manaButtons = new();
    private List<CardDatabase.CardData> allCards = new();
    private List<CardDatabase.CardData> filteredCards = new();
    private string selectedSeries = "全部";
    private int? selectedMana;
    private string searchText = string.Empty;
    private int pageIndex;
    private bool initialized;

    private void Awake()
    {
        EnsureReferences();
        BindEvents();
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            EnsureReferences();
            BindEvents();
            initialized = true;
        }
        ReloadAndRender();
    }

    private void BindEvents()
    {
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveListener(PrevPage);
            prevPageButton.onClick.AddListener(PrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(NextPage);
            nextPageButton.onClick.AddListener(NextPage);
        }
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(OnSearchChanged);
            searchInput.onValueChanged.AddListener(OnSearchChanged);
        }
    }

    private void ReloadAndRender()
    {
        allCards = CardDatabase.GetAllCardData();
        BuildSeriesButtons();
        BuildManaButtons();
        ApplyFilter();
    }

    private void BuildSeriesButtons()
    {
        if (seriesButtonRoot == null || seriesButtonTemplate == null) return;
        foreach (Transform child in seriesButtonRoot)
        {
            if (child != seriesButtonTemplate.transform) Destroy(child.gameObject);
        }
        seriesButtons.Clear();
        List<string> labels = new List<string> { "全部" };
        HashSet<string> exists = new();
        foreach (var card in allCards)
        {
            if (string.IsNullOrWhiteSpace(card.series)) continue;
            if (exists.Add(card.series)) labels.Add(card.series);
        }
        foreach (var label in labels)
        {
            var button = Instantiate(seriesButtonTemplate, seriesButtonRoot);
            button.gameObject.SetActive(true);
            button.name = $"系列_{label}";
            SetButtonLabel(button, label);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                selectedSeries = label;
                pageIndex = 0;
                ApplyFilter();
            });
            seriesButtons[label] = button;
        }
        seriesButtonTemplate.gameObject.SetActive(false);
    }

    private void BuildManaButtons()
    {
        if (manaButtonRoot == null || manaButtonTemplate == null) return;
        foreach (Transform child in manaButtonRoot)
        {
            if (child != manaButtonTemplate.transform) Destroy(child.gameObject);
        }
        manaButtons.Clear();
        List<string> labels = new List<string> { "全部" };
        var manaCosts = allCards.Select(card => (int)card.cost).Distinct().OrderBy(v => v);
        foreach (var mana in manaCosts) labels.Add(mana.ToString());
        foreach (var label in labels)
        {
            var button = Instantiate(manaButtonTemplate, manaButtonRoot);
            button.gameObject.SetActive(true);
            button.name = $"法力_{label}";
            SetButtonLabel(button, label);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                selectedMana = label == "全部" ? null : int.Parse(label);
                pageIndex = 0;
                ApplyFilter();
            });
            manaButtons[label] = button;
        }
        manaButtonTemplate.gameObject.SetActive(false);
    }

    private void ApplyFilter()
    {
        IEnumerable<CardDatabase.CardData> query = allCards;
        if (!string.IsNullOrEmpty(selectedSeries) && selectedSeries != "全部")
        {
            query = query.Where(card => card.series == selectedSeries);
        }
        if (selectedMana.HasValue)
        {
            query = query.Where(card => (int)card.cost == selectedMana.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string keyword = searchText.Trim();
            query = query.Where(card =>
                (!string.IsNullOrEmpty(card.name) && card.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(card.effect) && card.effect.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
        }
        filteredCards = query.ToList();
        UpdateFilterVisualState();
        RenderPage();
    }

    private void RenderPage()
    {
        if (cardGridRoot == null) return;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(filteredCards.Count / (float)pageSize));
        pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
        EnsureCardItemPool();
        int start = pageIndex * pageSize;
        for (int i = 0; i < pageSize; i++)
        {
            int index = start + i;
            var item = cardItems[i];
            if (index >= filteredCards.Count)
            {
                item.SetActive(false);
                continue;
            }
            item.SetActive(true);
            var card = filteredCards[index];
            ApplyCardItemData(item.transform, card);
        }
        if (pageText != null)
        {
            pageText.text = $"{pageIndex + 1} / {totalPages}";
        }
        if (prevPageButton != null) prevPageButton.interactable = pageIndex > 0;
        if (nextPageButton != null) nextPageButton.interactable = pageIndex < totalPages - 1;
    }

    private void EnsureCardItemPool()
    {
        EnsureCardPrefab();
        if (cardPrefab == null) return;
        ConfigureGridCellSize();
        while (cardItems.Count < pageSize)
        {
            var item = Instantiate(cardPrefab, cardGridRoot);
            item.SetActive(true);
            item.name = $"卡牌项_{cardItems.Count + 1}";
            item.transform.localScale = Vector3.one;
            cardItems.Add(item);
        }
        if (cardItemTemplate != null)
        {
            cardItemTemplate.SetActive(false);
        }
    }

    private void ApplyCardItemData(Transform root, CardDatabase.CardData card)
    {
        BaseCard baseCard = CardFactory.GetThisCard(card.name);
        if (baseCard != null)
        {
            var cardButton = root.GetComponent<CardButton>();
            if (cardButton != null)
            {
                cardButton.JustUIShow = true;
                var uiButton = cardButton.GetComponent<Button>();
                if (uiButton != null)
                {
                    uiButton.onClick.RemoveAllListeners();
                    uiButton.interactable = false;
                }
                cardButton.SetData(baseCard);
                return;
            }

            var cardUIItem = root.GetComponent<CardUIItem>();
            if (cardUIItem != null)
            {
                cardUIItem.JustUIShow = true;
                cardUIItem.SetData(baseCard);
                return;
            }
        }

        SetTextByNameOrIndex(root, "名称", card.name, 0);
        SetTextByNameOrIndex(root, "费用", card.cost.ToString(), 1);
        SetTextByNameOrIndex(root, "描述", card.effect, 2);
    }

    private void EnsureCardPrefab()
    {
        if (cardPrefab != null) return;

        var cardList = FindObjectOfType<CardList>(true);
        if (cardList != null && cardList.cardPreb != null)
        {
            cardPrefab = cardList.cardPreb;
            return;
        }

        var rougeUI = FindObjectOfType<RougeUI>(true);
        if (rougeUI != null && rougeUI.cardButtonPrefab != null)
        {
            cardPrefab = rougeUI.cardButtonPrefab;
            return;
        }

        if (cardItemTemplate != null)
        {
            cardPrefab = cardItemTemplate;
        }
    }

    private void ConfigureGridCellSize()
    {
        if (cardGridRoot == null || cardPrefab == null) return;
        var grid = cardGridRoot.GetComponent<GridLayoutGroup>();
        if (grid == null) return;

        var prefabRect = cardPrefab.GetComponent<RectTransform>();
        if (prefabRect == null) return;

        float prefabWidth = Mathf.Max(1f, prefabRect.rect.width);
        float prefabHeight = Mathf.Max(1f, prefabRect.rect.height);
        float aspect = prefabWidth / prefabHeight;

        int columns = 4;
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            columns = Mathf.Max(1, grid.constraintCount);
        }
        int rows = Mathf.Max(1, Mathf.CeilToInt(pageSize / (float)columns));

        float availableWidth = cardGridRoot.rect.width - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1);
        float availableHeight = cardGridRoot.rect.height - grid.padding.top - grid.padding.bottom - grid.spacing.y * (rows - 1);
        if (availableWidth <= 0f || availableHeight <= 0f) return;

        float cellWidth = availableWidth / columns;
        float cellHeight = cellWidth / aspect;
        float maxCellHeight = availableHeight / rows;
        if (cellHeight > maxCellHeight)
        {
            cellHeight = maxCellHeight;
            cellWidth = cellHeight * aspect;
        }
        grid.cellSize = new Vector2(cellWidth, cellHeight);
    }

    private void SetTextByNameOrIndex(Transform root, string childName, string value, int fallbackIndex)
    {
        var direct = root.Find(childName);
        if (direct != null)
        {
            var text = direct.GetComponent<Text>();
            if (text != null)
            {
                text.text = value;
                return;
            }
        }
        var texts = root.GetComponentsInChildren<Text>(true);
        if (fallbackIndex >= 0 && fallbackIndex < texts.Length)
        {
            texts[fallbackIndex].text = value;
        }
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<Text>(true);
        if (text != null) text.text = label;
    }

    private void UpdateFilterVisualState()
    {
        foreach (var pair in seriesButtons)
        {
            SetButtonSelected(pair.Value, pair.Key == selectedSeries);
        }
        foreach (var pair in manaButtons)
        {
            bool selected = selectedMana.HasValue ? pair.Key == selectedMana.Value.ToString() : pair.Key == "全部";
            SetButtonSelected(pair.Value, selected);
        }
    }

    private void SetButtonSelected(Button button, bool selected)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? new Color(0.9f, 0.78f, 0.35f, 1f) : Color.white;
        }
    }

    private void OnSearchChanged(string value)
    {
        searchText = value ?? string.Empty;
        pageIndex = 0;
        ApplyFilter();
    }

    private void PrevPage()
    {
        pageIndex--;
        RenderPage();
    }

    private void NextPage()
    {
        pageIndex++;
        RenderPage();
    }

    private void EnsureReferences()
    {
        if (seriesButtonRoot == null)
        {
            var t = transform.Find("顶部系列栏/系列按钮容器");
            if (t != null) seriesButtonRoot = t as RectTransform;
        }
        if (seriesButtonTemplate == null && seriesButtonRoot != null)
        {
            var t = seriesButtonRoot.Find("系列按钮模板");
            if (t != null) seriesButtonTemplate = t.GetComponent<Button>();
        }
        if (cardGridRoot == null)
        {
            var t = transform.Find("中部区域/卡牌网格");
            if (t != null) cardGridRoot = t as RectTransform;
        }
        if (cardItemTemplate == null && cardGridRoot != null)
        {
            var t = cardGridRoot.Find("卡牌项模板");
            if (t != null) cardItemTemplate = t.gameObject;
        }
        if (prevPageButton == null)
        {
            var t = transform.Find("中部区域/左翻页");
            if (t != null) prevPageButton = t.GetComponent<Button>();
        }
        if (nextPageButton == null)
        {
            var t = transform.Find("中部区域/右翻页");
            if (t != null) nextPageButton = t.GetComponent<Button>();
        }
        if (pageText == null)
        {
            var t = transform.Find("中部区域/页码文本");
            if (t != null) pageText = t.GetComponent<Text>();
        }
        if (manaButtonRoot == null)
        {
            var t = transform.Find("底部区域/法力值栏");
            if (t != null) manaButtonRoot = t as RectTransform;
        }
        if (manaButtonTemplate == null && manaButtonRoot != null)
        {
            var t = manaButtonRoot.Find("法力按钮模板");
            if (t != null) manaButtonTemplate = t.GetComponent<Button>();
        }
        if (searchInput == null)
        {
            var t = transform.Find("底部区域/搜索输入框");
            if (t != null) searchInput = t.GetComponent<InputField>();
        }
        if (searchInput != null)
        {
            if (searchInput.textComponent == null)
            {
                var text = searchInput.transform.Find("Text")?.GetComponent<Text>();
                if (text != null) searchInput.textComponent = text;
            }
            if (searchInput.placeholder == null)
            {
                var placeholder = searchInput.transform.Find("Placeholder")?.GetComponent<Graphic>();
                if (placeholder != null) searchInput.placeholder = placeholder;
            }
        }
    }
}
