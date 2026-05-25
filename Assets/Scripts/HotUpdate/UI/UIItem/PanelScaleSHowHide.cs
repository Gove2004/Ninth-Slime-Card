using DG.Tweening;
using UnityEngine;

public class PanelScaleSHowHide : MonoBehaviour
{
    public void ShowPanel()
    {
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
    }

    public void HidePanel()
    {
        transform.localScale = Vector3.one;
        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }
}
