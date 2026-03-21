using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    private readonly Dictionary<GameObject, CardDatabase.CardData> itemCardMap = new();
    private List<CardDatabase.CardData> allCards = new();
    private List<CardDatabase.CardData> filteredCards = new();
    private string selectedSeries = "全部";
    private int? selectedMana;
    private string searchText = string.Empty;
    private int pageIndex;
    private bool initialized;
    private RectTransform hoverTooltipRoot;
    private Text hoverTooltipText;
    private RectTransform detailOverlayRoot;
    private RectTransform detailCardAnchor;
    private Text detailRemarkText;
    private GameObject detailCardInstance;
    private CardDatabase.CardData hoveredCard;

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
        EnsureReturnButtonVisible();
        EnsureOverlayUI();
        ReloadAndRender();
    }

    private void OnDisable()
    {
        HideHoverTooltip();
        HideDetailOverlay();
    }

    private void Update()
    {
        if (hoverTooltipRoot != null && hoverTooltipRoot.gameObject.activeSelf)
        {
            Vector2 localPoint;
            var rootRect = transform as RectTransform;
            if (rootRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Input.mousePosition, null, out localPoint))
            {
                var desired = localPoint;
                var size = hoverTooltipRoot.sizeDelta;
                var pivot = hoverTooltipRoot.pivot;
                float minX = rootRect.rect.xMin + size.x * pivot.x;
                float maxX = rootRect.rect.xMax - size.x * (1f - pivot.x);
                float minY = rootRect.rect.yMin + size.y * pivot.y;
                float maxY = rootRect.rect.yMax - size.y * (1f - pivot.y);
                if (minX > maxX) (minX, maxX) = (maxX, minX);
                if (minY > maxY) (minY, maxY) = (maxY, minY);
                hoverTooltipRoot.anchoredPosition = new Vector2(
                    Mathf.Clamp(desired.x, minX, maxX),
                    Mathf.Clamp(desired.y, minY, maxY));
            }
        }
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
        itemCardMap.Clear();
        int start = pageIndex * pageSize;
        for (int i = 0; i < pageSize; i++)
        {
            int index = start + i;
            var item = cardItems[i];
            if (index >= filteredCards.Count)
            {
                item.SetActive(false);
                BindCardItemEvents(item, null);
                continue;
            }
            item.SetActive(true);
            var card = filteredCards[index];
            ApplyCardItemData(item.transform, card);
            itemCardMap[item] = card;
            BindCardItemEvents(item, card);
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
        SetTextByNameOrIndex(root, "描述", GetCardDescription(card), 2);
    }

    private void BindCardItemEvents(GameObject item, CardDatabase.CardData card)
    {
        if (item == null) return;
        var trigger = item.GetComponent<EventTrigger>();
        if (trigger == null) trigger = item.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        if (card == null) return;

        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnCardPointerEnter(card));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnCardPointerExit(card));
        AddTriggerEntry(trigger, EventTriggerType.PointerClick, _ => OnCardPointerClick(card));
    }

    private void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback?.Invoke(data));
        trigger.triggers.Add(entry);
    }

    private void OnCardPointerEnter(CardDatabase.CardData card)
    {
        hoveredCard = card;
        ShowHoverTooltip(card);
    }

    private void OnCardPointerExit(CardDatabase.CardData card)
    {
        if (hoveredCard == card) hoveredCard = null;
        HideHoverTooltip();
    }

    private void OnCardPointerClick(CardDatabase.CardData card)
    {
        ShowDetailOverlay(card);
    }

    private void ShowHoverTooltip(CardDatabase.CardData card)
    {
        EnsureOverlayUI();
        if (hoverTooltipRoot == null || hoverTooltipText == null || card == null) return;
        var description = GetCardDescription(card);
        hoverTooltipText.text = string.IsNullOrWhiteSpace(description) ? "无描述" : description;
        hoverTooltipRoot.gameObject.SetActive(true);
    }

    private void HideHoverTooltip()
    {
        if (hoverTooltipRoot != null)
        {
            hoverTooltipRoot.gameObject.SetActive(false);
        }
    }

    private void ShowDetailOverlay(CardDatabase.CardData card)
    {
        EnsureOverlayUI();
        if (detailOverlayRoot == null || detailCardAnchor == null || detailRemarkText == null || card == null) return;
        detailOverlayRoot.gameObject.SetActive(true);
        detailRemarkText.text = string.IsNullOrWhiteSpace(card.remark) ? "暂无趣闻" : card.remark;
        CreateOrRefreshDetailCard(card);
    }

    private void HideDetailOverlay()
    {
        if (detailOverlayRoot != null)
        {
            detailOverlayRoot.gameObject.SetActive(false);
        }
    }

    private void CreateOrRefreshDetailCard(CardDatabase.CardData card)
    {
        EnsureCardPrefab();
        if (cardPrefab == null || detailCardAnchor == null || card == null) return;
        if (detailCardInstance == null)
        {
            detailCardInstance = Instantiate(cardPrefab, detailCardAnchor);
            detailCardInstance.name = "图鉴详情卡牌";
            var rect = detailCardInstance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one * 1.45f;
            }
        }
        detailCardInstance.SetActive(true);
        ApplyCardItemData(detailCardInstance.transform, card);
        BindCardItemEvents(detailCardInstance, card);
    }

    private void EnsureOverlayUI()
    {
        if (detailOverlayRoot != null && hoverTooltipRoot != null) return;
        var rootRect = transform as RectTransform;
        if (rootRect == null) return;

        if (detailOverlayRoot == null)
        {
            var overlayObj = new GameObject("图鉴详情遮罩", typeof(RectTransform), typeof(Image), typeof(Button));
            overlayObj.transform.SetParent(transform, false);
            detailOverlayRoot = overlayObj.GetComponent<RectTransform>();
            detailOverlayRoot.anchorMin = Vector2.zero;
            detailOverlayRoot.anchorMax = Vector2.one;
            detailOverlayRoot.offsetMin = Vector2.zero;
            detailOverlayRoot.offsetMax = Vector2.zero;
            var overlayImage = overlayObj.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.65f);
            var closeButton = overlayObj.GetComponent<Button>();
            closeButton.onClick.AddListener(HideDetailOverlay);

            var cardAnchorObj = new GameObject("详情卡牌锚点", typeof(RectTransform));
            cardAnchorObj.transform.SetParent(overlayObj.transform, false);
            detailCardAnchor = cardAnchorObj.GetComponent<RectTransform>();
            detailCardAnchor.anchorMin = new Vector2(0.5f, 0.5f);
            detailCardAnchor.anchorMax = new Vector2(0.5f, 0.5f);
            detailCardAnchor.pivot = new Vector2(0.5f, 0.5f);
            detailCardAnchor.sizeDelta = new Vector2(240f, 360f);
            detailCardAnchor.anchoredPosition = Vector2.zero;

            var remarkObj = new GameObject("趣闻文本", typeof(RectTransform), typeof(Text));
            remarkObj.transform.SetParent(overlayObj.transform, false);
            var remarkRect = remarkObj.GetComponent<RectTransform>();
            remarkRect.anchorMin = new Vector2(0.62f, 0.22f);
            remarkRect.anchorMax = new Vector2(0.96f, 0.78f);
            remarkRect.pivot = new Vector2(0.5f, 0.5f);
            remarkRect.sizeDelta = Vector2.zero;
            remarkRect.anchoredPosition = Vector2.zero;
            detailRemarkText = remarkObj.GetComponent<Text>();
            detailRemarkText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            detailRemarkText.fontSize = 24;
            detailRemarkText.alignment = TextAnchor.UpperLeft;
            detailRemarkText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailRemarkText.verticalOverflow = VerticalWrapMode.Overflow;
            detailRemarkText.color = Color.white;
            detailRemarkText.raycastTarget = false;

            detailOverlayRoot.gameObject.SetActive(false);
        }

        if (hoverTooltipRoot == null)
        {
            var tooltipObj = new GameObject("图鉴悬停描述", typeof(RectTransform), typeof(Image));
            tooltipObj.transform.SetParent(transform, false);
            hoverTooltipRoot = tooltipObj.GetComponent<RectTransform>();
            hoverTooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            hoverTooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            hoverTooltipRoot.pivot = new Vector2(1f, 0f);
            hoverTooltipRoot.sizeDelta = new Vector2(360f, 170f);
            var tooltipBg = tooltipObj.GetComponent<Image>();
            tooltipBg.color = new Color(0f, 0f, 0f, 0.82f);
            tooltipBg.raycastTarget = false;

            var textObj = new GameObject("文本", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(tooltipObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(12f, 12f);
            textRect.offsetMax = new Vector2(-12f, -12f);
            hoverTooltipText = textObj.GetComponent<Text>();
            hoverTooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hoverTooltipText.fontSize = 20;
            hoverTooltipText.alignment = TextAnchor.MiddleCenter;
            hoverTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hoverTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            hoverTooltipText.color = Color.white;
            hoverTooltipText.raycastTarget = false;

            hoverTooltipRoot.gameObject.SetActive(false);
        }
    }

    private string GetCardDescription(CardDatabase.CardData card)
    {
        if (card == null) return string.Empty;
        BaseCard baseCard = CardFactory.GetThisCard(card.name);
        if (baseCard != null)
        {
            return baseCard.GetDynamicDescription();
        }
        return card.effect;
    }

    private void EnsureReturnButtonVisible()
    {
        var buttons = transform.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null) continue;
            var name = button.gameObject.name;
            if (name.Contains("返回") || name.Contains("Back") || name.Contains("back"))
            {
                button.gameObject.SetActive(true);
                break;
            }
        }
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
