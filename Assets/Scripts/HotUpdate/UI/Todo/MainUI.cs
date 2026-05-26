// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;

// public class MainUI : MonoBehaviour
// {
//     public Button startBattleButton;
//     public Button saveButton;
//     public Button introButton;
//     public Button settingsButton;
//     public Button teamButton;
//     public Button achievementButton;
//     public Button collectionButton;
//     public InfoPanel infoPanel;
//     public TextMeshProUGUI maxScoreText;
//     public TMP_Dropdown dropdown;


//     public void Start()
//     {
//         EnsureReferences();
//         if (startBattleButton != null) startBattleButton.onClick.AddListener(OnStartBattleClicked);
//         if (introButton != null) introButton.onClick.AddListener(OnIntroClicked);
//         if (settingsButton != null && settingsButton != introButton) settingsButton.onClick.AddListener(OnSettingsClicked);
//         if (teamButton != null) teamButton.onClick.AddListener(OnTeamClicked);
//         if (saveButton == null)
//         {
//             var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             foreach (var button in buttons)
//             {
//                 if (button != null && button.gameObject.name == "存档")
//                 {
//                     saveButton = button;
//                     break;
//                 }
//             }
//         }
//         if (saveButton != null)
//         {
//             saveButton.onClick.AddListener(OnSaveClicked);
//         }
//         if (achievementButton == null)
//         {
//             var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             foreach (var button in buttons)
//             {
//                 if (button != null && button.gameObject.name == "成就")
//                 {
//                     achievementButton = button;
//                     break;
//                 }
//             }
//         }
//         if (achievementButton != null)
//         {
//             achievementButton.onClick.AddListener(OnAchievementClicked);
//         }
//         if (collectionButton == null)
//         {
//             var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             foreach (var button in buttons)
//             {
//                 if (button != null && button.gameObject.name == "图鉴")
//                 {
//                     collectionButton = button;
//                     break;
//                 }
//             }
//         }
//         if (collectionButton != null)
//         {
//             collectionButton.onClick.AddListener(OnCollectionClicked);
//         }

//         if (dropdown != null)
//         {
//             EnsureDifficultyOptions();
//             dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
//             OnDropdownValueChanged(dropdown.value);
//         }
//     }

//     public void OnEnable()
//     {
//         UpdateMaxScore();
//     }

//     private void OnIntroClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowIntro();
//         }
//     }

//     private void OnTeamClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowTeam();
//         }
//     }

//     private void OnSaveClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowSaveList();
//         }
//     }

//     private void OnSettingsClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowSettings();
//         }
//     }

//     private void OnAchievementClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowAchievements();
//         }
//     }

//     private void OnCollectionClicked()
//     {
//         if (infoPanel != null)
//         {
//             infoPanel.ShowCollection();
//         }
//     }


//     public void UpdateMaxScore()
//     {
//         if (maxScoreText == null) return;
//         if (GameManager.Instance == null) return;
//         maxScoreText.text = $"最高分数: {GameManager.Instance.maxScore}";
//     }

//     public void OnStartBattleClicked()
//     {
//         if (GameManager.Instance == null) return;
//         GameManager.Instance.SwitchSecne(true);
//     }

//     public void OnDropdownValueChanged(int index)
//     {
//         Debug.Log($"Dropdown value changed: {index}");
//         if (GameManager.Instance == null) return;
//         GameManager.Instance.SetDiff(index + 1);
//     }

//     private void EnsureReferences()
//     {
//         if (settingsButton == null)
//         {
//             var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//             foreach (var button in buttons)
//             {
//                 if (button != null && button.gameObject.name == "设置")
//                 {
//                     settingsButton = button;
//                     break;
//                 }
//             }
//         }
//         if (dropdown == null)
//         {
//             dropdown = GetComponentInChildren<TMP_Dropdown>(true);
//             if (dropdown == null)
//             {
//                 var dropdowns = FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//                 if (dropdowns.Length > 0) dropdown = dropdowns[0];
//             }
//         }
//         if (maxScoreText == null)
//         {
//             maxScoreText = GetComponentInChildren<TextMeshProUGUI>(true);
//         }
//     }

//     private void EnsureDifficultyOptions()
//     {
//         if (dropdown == null) return;
//         string[] targetOptions = { "简单", "困难", "地狱", "无尽" };
//         if (dropdown.options.Count == targetOptions.Length) return;
//         dropdown.ClearOptions();
//         dropdown.AddOptions(new System.Collections.Generic.List<string>(targetOptions));
//     }
// }
