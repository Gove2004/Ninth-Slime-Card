// using TMPro;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;

// public class DotShowList : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
// {
//     public bool isPlayer;
//     public DotShow dotShowPrefab;
//     public Button zhankaiButton;
//     public GameObject dotListContainer;
//     public RectTransform viewport;
//     public TextMeshProUGUI headerText;
//     public float headerHeight = 28f;
//     public ScrollRect scrollRect;
//     public Vector2 collapsedSize = new Vector2(90f, 36f);
//     public Vector2 minSize = new Vector2(200f, 140f);
//     public Vector2 maxSize = new Vector2(640f, 480f);
//     public float resizeHandleSize = 18f;
//     public Vector2 dotIconSize = new Vector2(24f, 24f);
//     public Vector2 dotIconSpacing = new Vector2(4f, 4f);
//     public int dotGridPaddingLeft = 6;
//     public int dotGridPaddingRight = 6;
//     public int dotGridPaddingTop = 6;
//     public int dotGridPaddingBottom = 6;

//     private bool isExpanded = true;
//     private bool isDragging;
//     private bool isResizing;
//     private float lastDragTime;
//     private Vector2 dragOffset;
//     private Vector2 resizeStartPointer;
//     private Vector2 resizeStartSize;
//     private Vector2 lastExpandedSize;
//     private bool draggedSincePointerDown;
//     private Vector2 pointerDownScreenPosition;
//     private bool pointerDownInHeader;
//     private RectTransform rootRect;
//     private RectTransform contentRect;
//     private RectTransform viewportRect;
//     private Vector2 resizeStartTopLeftLocal;
//     private int gridColumnCount = 1;

//     void Start()
//     {
//         rootRect = transform as RectTransform;
//         if (dotListContainer != null)
//         {
//             contentRect = dotListContainer.GetComponent<RectTransform>();
//         }

//         if (viewport != null)
//         {
//             viewportRect = viewport;
//         }

//         ResolveHeaderText();
//         SetupLayout();
//         EnsureDotItems(16);

//         if (rootRect != null)
//         {
//             lastExpandedSize = rootRect.sizeDelta;
//         }

//         if (zhankaiButton != null)
//         {
//             zhankaiButton.onClick.AddListener(OnToggleClicked);
//         }

//         UpdateHeaderText();
//         ApplyExpandedState();
//     }

//     void Update()
//     {
//         if (BattleManager.Instance == null) return;

//         gridColumnCount = CalculateColumnCount();

//         if (isPlayer)
//         {
//             UpdateDotShows(BattleManager.Instance.player?.dotBar);
//         }
//         else
//         {
//             UpdateDotShows(BattleManager.Instance.enemy?.dotBar);
//         }
//     }

//     #region Drag and Resize
// // 安卓禁用
//     public void OnBeginDrag(PointerEventData eventData)
//     {
//         if (rootRect == null) return;
//         var parentRect = rootRect.parent as RectTransform;
//         if (parentRect == null) return;

//         if (IsInResizeHandle(eventData))
//         {
//             isResizing = true;
//             RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out resizeStartPointer);
//             resizeStartSize = rootRect.sizeDelta;
//             resizeStartTopLeftLocal = GetTopLeftLocal(parentRect);
//             return;
//         }

//         if (!IsInHeaderArea(eventData))
//         {
//             isDragging = false;
//             return;
//         }

//         Vector2 localPoint;
//         RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint);
//         dragOffset = rootRect.anchoredPosition - localPoint;
//         isDragging = true;
//         draggedSincePointerDown = true;
//     }

//     public void OnDrag(PointerEventData eventData)
//     {
//         if (rootRect == null) return;
//         var parentRect = rootRect.parent as RectTransform;
//         if (parentRect == null) return;

//         if (isResizing)
//         {
//             Vector2 resizeLocalPoint;
//             RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out resizeLocalPoint);
//             Vector2 delta = resizeLocalPoint - resizeStartPointer;
//             Vector2 newSize = resizeStartSize + new Vector2(delta.x, -delta.y);
//             newSize.x = Mathf.Clamp(newSize.x, minSize.x, maxSize.x);
//             newSize.y = Mathf.Clamp(newSize.y, minSize.y, maxSize.y);
//             rootRect.sizeDelta = newSize;
//             Vector2 currentTopLeftLocal = GetTopLeftLocal(parentRect);
//             Vector2 offset = resizeStartTopLeftLocal - currentTopLeftLocal;
//             rootRect.anchoredPosition += offset;
//             if (isExpanded)
//             {
//                 lastExpandedSize = newSize;
//             }
//             RefreshViewportLayout();
//             return;
//         }

//         if (!isDragging) return;

//         Vector2 localPoint;
//         RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint);
//         rootRect.anchoredPosition = localPoint + dragOffset;
//     }

//     public void OnEndDrag(PointerEventData eventData)
//     {
//         isDragging = false;
//         isResizing = false;
//         lastDragTime = Time.unscaledTime;
//     }
//     #endregion

//     public void OnPointerDown(PointerEventData eventData)
//     {
//         draggedSincePointerDown = false;
//         pointerDownScreenPosition = eventData.position;
//         pointerDownInHeader = IsInHeaderArea(eventData);
//     }

//     private void OnToggleClicked()
//     {
//         if (draggedSincePointerDown) return;
//         if (!pointerDownInHeader) return;
//         if ((pointerDownScreenPosition - (Vector2)Input.mousePosition).sqrMagnitude > 4f) return;
//         isExpanded = !isExpanded;
//         ApplyExpandedState();
//         UpdateHeaderText();
//     }

//     private void ApplyExpandedState()
//     {
//         if (rootRect != null)
//         {
//             var parentRect = rootRect.parent as RectTransform;
//             Vector2? topLeftBefore = parentRect != null ? GetTopLeftLocal(parentRect) : (Vector2?)null;
//             if (isExpanded)
//             {
//                 Vector2 targetSize = lastExpandedSize;
//                 targetSize.x = Mathf.Clamp(targetSize.x, minSize.x, maxSize.x);
//                 targetSize.y = Mathf.Clamp(targetSize.y, minSize.y, maxSize.y);
//                 rootRect.sizeDelta = targetSize;
//             }
//             else
//             {
//                 lastExpandedSize = rootRect.sizeDelta;
//                 rootRect.sizeDelta = collapsedSize;
//             }
//             if (topLeftBefore.HasValue && parentRect != null)
//             {
//                 Vector2 topLeftAfter = GetTopLeftLocal(parentRect);
//                 rootRect.anchoredPosition += topLeftBefore.Value - topLeftAfter;
//             }
//         }

//         if (viewportRect != null)
//         {
//             viewportRect.gameObject.SetActive(isExpanded);
//         }
//         else if (dotListContainer != null)
//         {
//             dotListContainer.SetActive(isExpanded);
//         }

//         RefreshHeaderLayout();
//         RefreshViewportLayout();
//     }

//     private void UpdateDotShows(System.Collections.Generic.List<Dot> dots)
//     {
//         if (dotListContainer == null) return;
//         if (dots == null) return;

//         EnsureDotItems(dots.Count);

//         for (int i = 0; i < dotListContainer.transform.childCount; i++)
//         {
//             var dotShow = dotListContainer.transform.GetChild(i).GetComponent<DotShow>();
//             if (i < dots.Count)
//             {
//                 dotShow.gameObject.SetActive(true);
//                 dotShow.SetData(dots[i]);
//             }
//             else
//             {
//                 dotShow.gameObject.SetActive(false);
//             }
//         }
//         if (contentRect != null)
//         {
//             LayoutDotItems(dots.Count);
//         }
//     }

//     private void EnsureDotItems(int count)
//     {
//         if (dotListContainer == null || dotShowPrefab == null) return;
//         while (dotListContainer.transform.childCount < count)
//         {
//             Instantiate(dotShowPrefab, dotListContainer.transform);
//         }
//     }


//     private void ResolveHeaderText()
//     {
//         if (headerText != null) return;
//         var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
//         foreach (var text in texts)
//         {
//             if (dotListContainer != null && text.transform.IsChildOf(dotListContainer.transform)) continue;
//             headerText = text;
//             break;
//         }
//     }

//     private void UpdateHeaderText()
//     {
//         if (headerText == null) return;
//         string title = isPlayer ? "我方DOT" : "敌方DOT";
//         headerText.text = isExpanded ? $"{title} ▼" : "展开";
//     }

//     private void SetupLayout()
//     {
//         if (rootRect != null)
//         {
//             if (rootRect.sizeDelta.x < 1f || rootRect.sizeDelta.y < 1f)
//             {
//                 rootRect.sizeDelta = new Vector2(280f, 240f);
//             }
//             ApplyInitialPosition();
//         }

//         var image = GetComponent<Image>();
//         if (image != null)
//         {
//             image.color = new Color(0f, 0f, 0f, 0.6f);
//             image.raycastTarget = true;
//         }

//         if (viewportRect == null && dotListContainer != null)
//         {
//             var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
//             viewportObject.transform.SetParent(transform, false);
//             viewportRect = viewportObject.GetComponent<RectTransform>();
//             viewport = viewportRect;

//             var viewportImage = viewportObject.GetComponent<Image>();
//             viewportImage.color = new Color(0f, 0f, 0f, 0f);
//             viewportImage.raycastTarget = true;

//             dotListContainer.transform.SetParent(viewportRect, false);
//             contentRect = dotListContainer.GetComponent<RectTransform>();
//         }

//         if (contentRect != null)
//         {
//             contentRect.anchorMin = new Vector2(0f, 1f);
//             contentRect.anchorMax = new Vector2(1f, 1f);
//             contentRect.pivot = new Vector2(0f, 1f);
//             contentRect.anchoredPosition = Vector2.zero;
//             contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
//             ConfigureDotGrid();
//             gridColumnCount = CalculateColumnCount();
//         }

//         if (viewportRect == null && contentRect != null)
//         {
//             viewportRect = contentRect.parent as RectTransform;
//         }

//         if (scrollRect == null && rootRect != null)
//         {
//             scrollRect = GetComponent<ScrollRect>();
//             if (scrollRect == null)
//             {
//                 scrollRect = gameObject.AddComponent<ScrollRect>();
//             }
//         }

//         if (scrollRect != null)
//         {
//             scrollRect.viewport = viewportRect;
//             scrollRect.content = contentRect;
//             scrollRect.horizontal = false;
//             scrollRect.vertical = true;
//             scrollRect.movementType = ScrollRect.MovementType.Elastic;
//             scrollRect.inertia = true;
//             scrollRect.scrollSensitivity = 8f;
//             scrollRect.decelerationRate = 0.95f;
//             scrollRect.elasticity = 0.1f;
//         }

//         RefreshHeaderLayout();
//         RefreshViewportLayout();
//     }

//     private void ConfigureDotGrid()
//     {
//         if (contentRect == null) return;

//         var verticalLayout = contentRect.GetComponent<VerticalLayoutGroup>();
//         if (verticalLayout != null)
//         {
//             verticalLayout.enabled = false;
//         }

//         var horizontalLayout = contentRect.GetComponent<HorizontalLayoutGroup>();
//         if (horizontalLayout != null)
//         {
//             horizontalLayout.enabled = false;
//         }

//         var grid = contentRect.GetComponent<GridLayoutGroup>();
//         if (grid != null)
//         {
//             grid.enabled = false;
//         }

//         var fitter = contentRect.GetComponent<ContentSizeFitter>();
//         if (fitter != null)
//         {
//             fitter.enabled = false;
//         }
//     }

//     private int CalculateColumnCount()
//     {
//         if (contentRect == null) return 1;

//         float width = 0f;
//         if (viewportRect != null)
//         {
//             width = viewportRect.rect.width;
//         }
//         if (width <= 0.1f)
//         {
//             width = contentRect.rect.width;
//         }
//         if (width <= 0.1f && rootRect != null)
//         {
//             width = rootRect.rect.width;
//         }
//         if (width <= 0.1f)
//         {
//             width = 200f;
//         }

//         float usable = width - dotGridPaddingLeft - dotGridPaddingRight + dotIconSpacing.x;
//         float unit = dotIconSize.x + dotIconSpacing.x;
//         return Mathf.Max(1, Mathf.FloorToInt(usable / Mathf.Max(1f, unit)));
//     }

//     private void LayoutDotItems(int activeCount)
//     {
//         if (contentRect == null) return;

//         int columns = Mathf.Max(1, gridColumnCount);
//         float cellWidth = dotIconSize.x;
//         float cellHeight = dotIconSize.y;
//         float stepX = cellWidth + dotIconSpacing.x;
//         float stepY = cellHeight + dotIconSpacing.y;

//         for (int i = 0; i < activeCount; i++)
//         {
//             var child = dotListContainer.transform.GetChild(i);
//             var rect = child as RectTransform;
//             if (rect == null) continue;

//             int row = i / columns;
//             int col = i % columns;
//             float x = dotGridPaddingLeft + col * stepX;
//             float y = -(dotGridPaddingTop + row * stepY);

//             rect.anchorMin = new Vector2(0f, 1f);
//             rect.anchorMax = new Vector2(0f, 1f);
//             rect.pivot = new Vector2(0f, 1f);
//             rect.anchoredPosition = new Vector2(x, y);
//             rect.sizeDelta = dotIconSize;
//         }

//         int rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)columns));
//         float contentHeight = dotGridPaddingTop + dotGridPaddingBottom + rows * cellHeight + Mathf.Max(0, rows - 1) * dotIconSpacing.y;
//         contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);

//         LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
//     }

//     private void RefreshHeaderLayout()
//     {
//         if (headerText == null) return;
//         var headerRect = headerText.rectTransform;

//         if (isExpanded)
//         {
//             headerRect.anchorMin = new Vector2(0f, 1f);
//             headerRect.anchorMax = new Vector2(1f, 1f);
//             headerRect.pivot = new Vector2(0.5f, 1f);
//             headerRect.sizeDelta = new Vector2(0f, headerHeight);
//             headerRect.anchoredPosition = new Vector2(0f, 0f);
//         }
//         else
//         {
//             headerRect.anchorMin = Vector2.zero;
//             headerRect.anchorMax = Vector2.one;
//             headerRect.pivot = new Vector2(0.5f, 0.5f);
//             headerRect.sizeDelta = Vector2.zero;
//             headerRect.anchoredPosition = Vector2.zero;
//         }

//         headerText.alignment = TextAlignmentOptions.Center;
//         headerText.color = Color.white;
//     }

//     private void RefreshViewportLayout()
//     {
//         if (viewportRect == null) return;

//         if (isExpanded)
//         {
//             viewportRect.anchorMin = new Vector2(0f, 0f);
//             viewportRect.anchorMax = new Vector2(1f, 1f);
//             viewportRect.pivot = new Vector2(0.5f, 0.5f);
//             viewportRect.offsetMin = new Vector2(0f, 6f);
//             viewportRect.offsetMax = new Vector2(0f, -(headerHeight + 6f));
//         }
//         else
//         {
//             viewportRect.offsetMin = Vector2.zero;
//             viewportRect.offsetMax = Vector2.zero;
//         }
//     }

//     private bool IsInResizeHandle(PointerEventData eventData)
//     {
//         if (rootRect == null) return false;
//         Vector2 localPoint;
//         RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out localPoint);

//         Rect rect = rootRect.rect;
//         bool inX = localPoint.x >= rect.xMax - resizeHandleSize;
//         bool inY = localPoint.y <= rect.yMin + resizeHandleSize;
//         return inX && inY && isExpanded;
//     }

//     private bool IsInHeaderArea(PointerEventData eventData)
//     {
//         if (rootRect == null) return false;

//         Vector2 localPoint;
//         RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, eventData.position, eventData.pressEventCamera, out localPoint);

//         Rect rect = rootRect.rect;
//         if (!isExpanded) return rect.Contains(localPoint);

//         float dragHeaderHeight = Mathf.Clamp(headerHeight, 0f, rect.height);
//         float headerMinY = rect.yMax - dragHeaderHeight;
//         return localPoint.y >= headerMinY && localPoint.y <= rect.yMax;
//     }

//     private Vector2 GetTopLeftLocal(RectTransform parentRect)
//     {
//         if (rootRect == null || parentRect == null) return Vector2.zero;
//         Vector3[] corners = new Vector3[4];
//         rootRect.GetWorldCorners(corners);
//         Vector3 worldTopLeft = corners[1];
//         Vector3 local = parentRect.InverseTransformPoint(worldTopLeft);
//         return new Vector2(local.x, local.y);
//     }

//     private void ApplyInitialPosition()
//     {
//         if (rootRect == null || rootRect.anchoredPosition != Vector2.zero) return;
//         rootRect.anchoredPosition = isPlayer ? new Vector2(-250f, -100f) : new Vector2(250f, -100f);
//     }
// }
