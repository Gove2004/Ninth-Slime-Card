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
        GameCore.StartNewRun();
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
        int lv = GameCore.runState?.currentLv ?? 1;
        descText.text = playerWon ? $"已推进到 Lv.{lv}" : $"倒在了 Lv.{lv}，进度已重置。";
        gameObject.SetActive(true);
        scalePanel.ShowPanel();
    }
}
