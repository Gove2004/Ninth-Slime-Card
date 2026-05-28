using GoveKits.Runtime.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleResultOverlay : MonoBehaviour
{
    public PanelScaleSHowHide scalePanel;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button restartButton;
    public Button homeButton;

    public void Start()
    {
        restartButton.onClick.AddListener(OnRestartButtonClicked);
        homeButton.onClick.AddListener(OnHomeButtonClicked);
        gameObject.SetActive(false);
    }

    private void OnRestartButtonClicked()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.RestartBattleScene();
            return;
        }

        ResCore.LoadSceneAsync("Battle");
    }

    private void OnHomeButtonClicked()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ReturnHome();
            return;
        }

        ResCore.LoadSceneAsync("Home");
    }

    public void Show(bool playerWon)
    {
        titleText.text = playerWon ? "战斗胜利" : "战斗失败";
        descText.text = playerWon ? "要继续推进的话，下一步可以接奖励和结算。" : "这次史莱姆没撑住，再试一次。";
        gameObject.SetActive(true);
        scalePanel.ShowPanel();
    }
}
