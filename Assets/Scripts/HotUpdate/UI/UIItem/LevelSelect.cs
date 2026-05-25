using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelSelect : MonoBehaviour, IPointerClickHandler
{
    public string levelName;
    public Color levelColor = Color.white;  // 关卡颜色，可以用来区分不同类型的关卡， 按理说应当使用图片的方式来区分，但这里为了简单起见，直接使用颜色
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private Image levelSpriteRenderer;

    public void Start()
    {
        levelNameText.text = levelName;
        levelSpriteRenderer.color = levelColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(levelName) || levelName == "...")
        {
            MessageToastManager.Instance.ShowMessage("敬请期待！");
            return;
        }

        GameCore.SetCurrentLevel(levelName);

        ResCore.LoadSceneAsync("Battle");
    }
}

