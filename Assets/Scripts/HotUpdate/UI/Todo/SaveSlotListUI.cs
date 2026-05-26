// using System;
// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;

// public class SaveSlotListUI : MonoBehaviour
// {
//     public ScrollRect scrollRect;
//     public RectTransform contentRoot;
//     public Button itemTemplate;
//     public TextMeshProUGUI emptyTipText;

//     private void OnEnable()
//     {
//         Refresh();
//     }

//     public void Refresh()
//     {
//         EnsureReferences();
//         if (contentRoot == null || itemTemplate == null) return;
//         itemTemplate.gameObject.SetActive(false);

//         for (int i = contentRoot.childCount - 1; i >= 0; i--)
//         {
//             var child = contentRoot.GetChild(i);
//             if (child == null || child.gameObject == itemTemplate.gameObject) continue;
//             Destroy(child.gameObject);
//         }

//         var service = BattleSessionSaveService.Instance;
//         if (service == null) return;

//         var slots = service.GetSlotsSorted();
//         bool hasAny = slots != null && slots.Count > 0;
//         if (emptyTipText != null) emptyTipText.gameObject.SetActive(!hasAny);

//         for (int i = 0; i < slots.Count; i++)
//         {
//             var slot = slots[i];
//             if (slot == null) continue;
//             var item = Instantiate(itemTemplate, contentRoot);
//             item.gameObject.SetActive(true);
//             item.onClick.RemoveAllListeners();
//             string slotId = slot.slotId;
//             item.onClick.AddListener(() =>
//             {
//                 if (BattleSessionSaveService.Instance != null)
//                 {
//                     BattleSessionSaveService.Instance.TryLoadSlot(slotId);
//                 }
//             });

//             var text = item.GetComponentInChildren<TextMeshProUGUI>(true);
//             if (text != null)
//             {
//                 var localTime = new DateTime(slot.updatedAtTicks, DateTimeKind.Utc).ToLocalTime();
//                 ulong score = slot.snapshot != null ? slot.snapshot.enemyScore : 0;
//                 int turn = slot.snapshot != null ? slot.snapshot.currentTurn : 0;
//                 int difficultyLevel = slot.snapshot != null ? slot.snapshot.difficultyLevel : 1;
//                 string difficultyText = GetDifficultyText(difficultyLevel);
//                 text.text = $"{slot.label}\n难度:{difficultyText}  分数:{score}  回合:{turn}  {localTime:MM-dd HH:mm}";
//             }

//             CreateDeleteButton(item, slotId, text);
//         }
//     }

//     private void EnsureReferences()
//     {
//         if (scrollRect == null)
//         {
//             scrollRect = GetComponentInChildren<ScrollRect>(true);
//         }

//         if (contentRoot == null && scrollRect != null)
//         {
//             contentRoot = scrollRect.content;
//         }

//         if (itemTemplate == null && contentRoot != null)
//         {
//             for (int i = 0; i < contentRoot.childCount; i++)
//             {
//                 var child = contentRoot.GetChild(i);
//                 if (child == null) continue;
//                 if (child.name.Contains("模板") || child.name.Contains("Template"))
//                 {
//                     itemTemplate = child.GetComponent<Button>();
//                     if (itemTemplate != null) break;
//                 }
//             }

//             if (itemTemplate == null)
//             {
//                 itemTemplate = contentRoot.GetComponentInChildren<Button>(true);
//             }
//         }
//     }

//     private void CreateDeleteButton(Button item, string slotId, TextMeshProUGUI itemText)
//     {
//         if (item == null || string.IsNullOrEmpty(slotId)) return;

//         var deleteObject = new GameObject("DeleteButton", typeof(RectTransform), typeof(Image), typeof(Button));
//         deleteObject.transform.SetParent(item.transform, false);

//         var deleteRect = deleteObject.GetComponent<RectTransform>();
//         deleteRect.anchorMin = new Vector2(1f, 0.5f);
//         deleteRect.anchorMax = new Vector2(1f, 0.5f);
//         deleteRect.pivot = new Vector2(1f, 0.5f);
//         deleteRect.sizeDelta = new Vector2(84f, 34f);
//         deleteRect.anchoredPosition = new Vector2(-12f, 0f);

//         var deleteImage = deleteObject.GetComponent<Image>();
//         deleteImage.color = new Color(0.78f, 0.2f, 0.2f, 1f);

//         var deleteButton = deleteObject.GetComponent<Button>();
//         deleteButton.targetGraphic = deleteImage;
//         deleteButton.onClick.AddListener(() =>
//         {
//             var service = BattleSessionSaveService.Instance;
//             if (service == null) return;
//             if (service.DeleteSlot(slotId))
//             {
//                 Refresh();
//             }
//         });

//         var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
//         textObject.transform.SetParent(deleteObject.transform, false);
//         var deleteTextRect = textObject.GetComponent<RectTransform>();
//         deleteTextRect.anchorMin = Vector2.zero;
//         deleteTextRect.anchorMax = Vector2.one;
//         deleteTextRect.offsetMin = Vector2.zero;
//         deleteTextRect.offsetMax = Vector2.zero;

//         var deleteText = textObject.GetComponent<TextMeshProUGUI>();
//         deleteText.text = "删除";
//         deleteText.alignment = TextAlignmentOptions.Center;
//         deleteText.fontSize = 22;
//         deleteText.color = Color.white;
//         if (itemText != null && itemText.font != null)
//         {
//             deleteText.font = itemText.font;
//         }

//         if (itemText != null)
//         {
//             itemText.margin = new Vector4(itemText.margin.x, itemText.margin.y, 96f, itemText.margin.w);
//         }
//     }

//     private string GetDifficultyText(int difficultyLevel)
//     {
//         return difficultyLevel switch
//         {
//             1 => "简单",
//             2 => "困难",
//             3 => "地狱",
//             4 => "无尽",
//             _ => $"难度{difficultyLevel}"
//         };
//     }
// }
