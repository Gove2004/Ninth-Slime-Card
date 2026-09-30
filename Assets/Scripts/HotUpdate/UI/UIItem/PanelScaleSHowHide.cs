using UnityEngine;

public class PanelScaleSHowHide : MonoBehaviour
{
    public void ShowPanel()
    {
        gameObject.SetActive(true);
        // 移除 transform.DOScale 动画，直接使用正常尺寸
        transform.localScale = Vector3.one;
    }

    public void HidePanel()
    {
        // 直接隐藏，不再做缩放动画
        gameObject.SetActive(false);
        transform.localScale = Vector3.one;
    }
}