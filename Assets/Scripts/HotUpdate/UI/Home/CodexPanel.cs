using System;
using System.Collections.Generic;
using System.Linq;
using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CodexPanel : MonoBehaviour
{
    public GameObject choiceItemPrefab;

    private const int PageSize = 8;
    private const int ColumnCount = 4;

    private static readonly string[] SeriesOrder =
        { "初始", "七罪", "血族", "坚固", "科技", "种子", "暗影", "时序" };

    private static readonly Color SelectedColor = new(0.9f, 0.78f, 0.35f);
    private static readonly Color NormalColor = new(1f, 1f, 1f, 0.92f);
    private static readonly Color LockedColor = new(0.4f, 0.4f, 0.4f, 0.6f); // Grayed out for locked cards

    private readonly List<GameObject> spawnedCards = new();
    private readonly List<CardConfigData> allCards = new();
    private readonly List<CardConfigData> filteredCards = new();

    private GridLayoutGroup grid;
    private TMP_Text pageText;
    private Button prevBtn;
    private Button nextBtn;
    private RectTransform filterContent;
    private RectTransform content;
    private RectTransform detailOverlay;
    private RectTransform detailCard;
    private Button overlayCloseBtn;

    private readonly Dictionary<string, bool> seriesButtonsBuilt = new();
    private string selectedSeries = "全部";
    private int pageIndex;
    private bool bound;

    public void Show()
    {
        gameObject.SetActive(true);
        if (!bound) BindUi();
        Reload();
    }

    private void BindUi()
    {
        var panel = transform.Find("Panel");
        if (panel == null) return;

        content = panel.Find("Content") as RectTransform;
        grid = content?.GetComponent<GridLayoutGroup>();

        var filterBar = panel.Find("FilterBar");
        filterContent = filterBar?.Find("FilterContent") as RectTransform;

        var navT = panel.Find("NavBottom");
        if (navT != null)
        {
            prevBtn = navT.Find("BtnPrev")?.GetComponent<Button>();
            nextBtn = navT.Find("BtnNext")?.GetComponent<Button>();
            pageText = navT.Find("PageText")?.GetComponent<TMP_Text>();
        }

        var overlayT = panel.Find("DetailOverlay");
        if (overlayT != null)
        {
            detailOverlay = overlayT as RectTransform;
            overlayCloseBtn = overlayT.GetComponent<Button>();
            detailCard = overlayT.Find("DetailCard") as RectTransform;
        }

        if (prevBtn != null) prevBtn.onClick.AddListener(PrevPage);
        if (nextBtn != null) nextBtn.onClick.AddListener(NextPage);
        if (overlayCloseBtn != null) overlayCloseBtn.onClick.AddListener(HideDetail);

        bound = true;
    }

    private void Reload()
    {
        allCards.Clear();
        var loaded = ConfigCore.LoadAll<CardConfigData>();
        if (loaded != null)
        {
            foreach (var c in loaded)
            {
                if (c != null && c.id > 0 && !string.IsNullOrWhiteSpace(c.名称))
                    allCards.Add(c);
            }
        }

        allCards.Sort(CompareCard);
        BuildSeriesButtons();
        ApplyFilter();
    }

    private void BuildSeriesButtons()
    {
        if (filterContent == null) return;

        for (int i = filterContent.childCount - 1; i >= 0; i--)
            Destroy(filterContent.GetChild(i).gameObject);

        var series = new List<string> { "全部" };
        series.AddRange(allCards
            .Select(c => NormSeries(c.系列))
            .Distinct()
            .OrderBy(SortIndex)
            .ThenBy(s => s));

        foreach (var label in series)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(filterContent, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 80;
            le.preferredHeight = 32;

            var img = go.GetComponent<Image>();
            img.color = label == selectedSeries ? SelectedColor : NormalColor;

            var btn = go.GetComponent<Button>();
            var captured = label;
            btn.onClick.AddListener(() =>
            {
                selectedSeries = captured;
                pageIndex = 0;
                RefreshSeriesColors();
                ApplyFilter();
            });

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 20; tmp.color = new Color(0.1f, 0.1f, 0.1f);
            tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        }
    }

    private void RefreshSeriesColors()
    {
        if (filterContent == null) return;
        foreach (Transform child in filterContent)
        {
            var btn = child.GetComponent<Button>();
            var tmp = child.GetComponentInChildren<TMP_Text>();
            if (btn != null && tmp != null)
            {
                bool sel = tmp.text == selectedSeries;
                btn.GetComponent<Image>().color = sel ? SelectedColor : NormalColor;
            }
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<CardConfigData> q = allCards;
        if (!string.IsNullOrEmpty(selectedSeries) && selectedSeries != "全部")
            q = q.Where(c => NormSeries(c.系列) == selectedSeries);

        filteredCards.Clear();
        filteredCards.AddRange(q);
        RenderPage();
    }

    private void RenderPage()
    {
        ClearCards();
        if (filteredCards.Count == 0)
        {
            if (pageText != null) pageText.text = "无匹配";
            SetNavActive(false);
            return;
        }

        int total = Mathf.Max(1, Mathf.CeilToInt(filteredCards.Count / (float)PageSize));
        pageIndex = Mathf.Clamp(pageIndex, 0, total - 1);

        int start = pageIndex * PageSize;
        int end = Mathf.Min(start + PageSize, filteredCards.Count);
        for (int i = start; i < end; i++)
            SpawnCard(filteredCards[i]);

        if (pageText != null) pageText.text = $"{pageIndex + 1} / {total}";
        SetNavActive(true);
        if (prevBtn != null) prevBtn.interactable = pageIndex > 0;
        if (nextBtn != null) nextBtn.interactable = pageIndex < total - 1;
    }

    private void SetNavActive(bool active)
    {
        if (prevBtn != null) prevBtn.interactable = active;
        if (nextBtn != null) nextBtn.interactable = active;
    }

    private void SpawnCard(CardConfigData config)
    {
        if (choiceItemPrefab == null || content == null) return;

        var go = Instantiate(choiceItemPrefab, content);
        go.name = config.名称;
        spawnedCards.Add(go);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = Vector3.one;

        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (grid != null)
        {
            le.preferredWidth = grid.cellSize.x;
            le.preferredHeight = grid.cellSize.y;
        }

        // Check unlock status BEFORE setting up the card
        bool isUnlocked = GameCore.IsCardUnlocked(config.id.ToString());
        if (!isUnlocked)
        {
            // Locked card - show grayed out state
            SetLockedCardState(go);
            return;
        }

        // Unlocked card - normal setup
        SetCardImage(go, config.名称);
        SetCardName(go, config);

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ShowDetail(config));
        }

        BindHover(go, config);
    }

    // NEW: Handle locked card visual state
    private void SetLockedCardState(GameObject go)
    {
        // Find Image component and set to gray/locked
        var imgT = go.transform.Find("Image");
        if (imgT != null)
        {
            var img = imgT.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = null; // No sprite
                img.color = LockedColor; // Grayed out
            }
        }

        // Find name text and show "??"
        var nameTmp = go.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        if (nameTmp != null)
        {
            nameTmp.text = "??";
            nameTmp.color = LockedColor;
        }

        // Make button non-interactable
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = false;
        }

        // Add lock icon overlay if available
        var lockIconGo = go.transform.Find("LockIcon");
        if (lockIconGo != null)
        {
            lockIconGo.gameObject.SetActive(true);
        }
        else
        {
            // Create lock icon as fallback if not exists in prefab
            var lockGo = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(go.transform, false);
            var rect = lockGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-5, -5);
            rect.sizeDelta = new Vector2(30, 30);
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.sprite = Resources.Load<Sprite>("Icons/lock_icon"); // Try to load from Resources
            if (lockImg.sprite == null)
            {
                // Fallback: use a solid color if no sprite found
                lockImg.color = new Color(1, 0, 0, 0.7f); // Red tint as fallback
            }
            lockImg.enabled = true;
        }
    }

    private void SetCardName(GameObject go, CardConfigData config)
    {
        var card = CardFactoryCore.CreateCard(config.id);
        var nameTmp = go.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        var descTmp = go.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();

        if (nameTmp != null)
        {
            nameTmp.fontSize = 14;
            nameTmp.color = Color.black;
            var nameRt = nameTmp.GetComponent<RectTransform>();
            if (nameRt != null) nameRt.anchoredPosition = new Vector2(0, 48);
            nameTmp.text = card != null ? $"{card.Name} - {card.Cost}" : $"{config.名称} - {config.费用}";
        }

        if (descTmp != null) descTmp.gameObject.SetActive(false);
    }

    private void ShowDetail(CardConfigData config)
    {
        if (detailOverlay == null) return;
        detailOverlay.gameObject.SetActive(true);

        if (detailCard != null && choiceItemPrefab != null)
        {
            for (int i = detailCard.childCount - 1; i >= 0; i--)
                Destroy(detailCard.GetChild(i).gameObject);

            var go = Instantiate(choiceItemPrefab, detailCard);
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            // Only show detail for unlocked cards
            if (GameCore.IsCardUnlocked(config.id.ToString()))
            {
                SetCardImage(go, config.名称);
            }
            else
            {
                // Locked detail - show gray placeholder
                var imgT = go.transform.Find("Image");
                if (imgT != null)
                {
                    var img = imgT.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = null;
                        img.color = LockedColor;
                    }
                }
            }

            var card = CardFactoryCore.CreateCard(config.id);
            var nameTmp = go.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            var descTmp = go.transform.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();

            if (nameTmp != null)
            {
                nameTmp.fontSize = 24;
                nameTmp.color = Color.white;
                nameTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
                if (GameCore.IsCardUnlocked(config.id.ToString()))
                {
                    nameTmp.text = card != null ? $"{card.Name} - {card.Cost}" : $"{config.名称} - {config.费用}";
                }
                else
                {
                    nameTmp.text = "??";
                    nameTmp.color = LockedColor;
                }
            }

            if (descTmp != null)
            {
                descTmp.gameObject.SetActive(true);
                descTmp.fontSize = 18;
                descTmp.color = Color.white;
                descTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
                if (GameCore.IsCardUnlocked(config.id.ToString()))
                {
                    descTmp.text = card != null ? card.Description() : config.描述;
                }
                else
                {
                    descTmp.text = "尚未解锁";
                }
            }

            var btn = go.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    private void HideDetail()
    {
        if (detailOverlay != null) detailOverlay.gameObject.SetActive(false);
    }

    private void BindHover(GameObject go, CardConfigData config)
    {
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        trigger.triggers ??= new List<EventTrigger.Entry>();
        trigger.triggers.Clear();

        AddEntry(trigger, EventTriggerType.PointerEnter, _ => { });
        AddEntry(trigger, EventTriggerType.PointerExit, _ => { });
    }

    private static void AddEntry(EventTrigger t, EventTriggerType type, Action<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(d => cb?.Invoke(d));
        t.triggers.Add(e);
    }

    private void PrevPage() { pageIndex--; RenderPage(); }
    private void NextPage() { pageIndex++; RenderPage(); }

    private void ClearCards()
    {
        foreach (var go in spawnedCards)
            if (go != null) Destroy(go);
        spawnedCards.Clear();
    }

    private void OnDisable()
    {
        ClearCards();
        HideDetail();
    }

    // Updated: Handle locked card image gracefully
    private static void SetCardImage(GameObject go, string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        var imgT = go.transform.Find("Image");
        if (imgT == null) return;
        var img = imgT.GetComponent<Image>();
        if (img == null) return;

        var sprite = ResCore.LoadAssetSync<Sprite>($"Card_{name}")?.GetAssetObject<Sprite>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
        else if (!GameCore.IsCardUnlocked(name))
        {
            // If not found and card is locked, leave grayed out (LockedColor is already set elsewhere)
            img.color = LockedColor;
        }
    }

    private static int CompareCard(CardConfigData a, CardConfigData b)
    {
        int sc = SortIndex(NormSeries(a.系列)).CompareTo(SortIndex(NormSeries(b.系列)));
        if (sc != 0) return sc;
        int mc = a.费用.CompareTo(b.费用);
        return mc != 0 ? mc : a.id.CompareTo(b.id);
    }

    private static string NormSeries(string s) => string.IsNullOrWhiteSpace(s) ? "其他" : s;
    private static int SortIndex(string s) { int i = Array.IndexOf(SeriesOrder, s); return i >= 0 ? i : SeriesOrder.Length + 1; }
}