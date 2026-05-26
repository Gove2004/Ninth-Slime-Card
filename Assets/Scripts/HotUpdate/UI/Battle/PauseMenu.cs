using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button overButton;
    [SerializeField] private Button resumeButton;

    [SerializeField] private PanelScaleSHowHide scalePanel;

    private void Start()
    {
        pauseButton.onClick.AddListener(OnPauseButtonClicked);
        overButton.onClick.AddListener(OnOverButtonClicked);
        resumeButton.onClick.AddListener(OnResumeButtonClicked);

        this.gameObject.SetActive(false); // 初始时隐藏暂停菜单界面
    }

    private void OnPauseButtonClicked()
    {
        scalePanel.ShowPanel(); // 显示暂停菜单界面
    }

    private void OnOverButtonClicked()
    {
        MessageToastManager.Instance.ShowMessage("立即结算还没做 。。。"); // 显示游戏结束的提示信息
    }

    private void OnResumeButtonClicked()
    {
        scalePanel.HidePanel(); // 隐藏暂停菜单界面
    }
}
