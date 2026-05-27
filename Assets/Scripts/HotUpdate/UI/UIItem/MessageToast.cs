using DG.Tweening;
using TMPro;
using UnityEngine;

public class MessageToast : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    
    public void ShowMessage(string message, float duration = 2f)
    {
        messageText.text = message;

        transform.DOMoveY(transform.position.y + 150f, duration).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            Object.Destroy(gameObject);
        });
    }
}
