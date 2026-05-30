using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelSelect : MonoBehaviour, IPointerClickHandler
{
    public string levelName;
    public Color levelColor = Color.white;
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private Image levelSpriteRenderer;

    public void Start()
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        bool hasRun = GameCore.HasActiveRun();
        string displayName = hasRun ? $"继续 Lv.{GameCore.runState.currentLv}" : "开始游戏";

        if (levelNameText != null)
        {
            levelNameText.text = displayName;
        }

        if (levelSpriteRenderer != null)
        {
            levelSpriteRenderer.color = levelColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameCore.HasActiveRun())
        {
            GameCore.StartNewRun();
        }

        GameCore.SetCurrentLevel($"Lv.{GameCore.runState.currentLv}");
        GameCore.SaveRunState();
        ResCore.LoadSceneAsync("Battle");
    }
}
