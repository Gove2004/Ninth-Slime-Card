using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamagePopup : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private RectTransform rectTransform;
    private Tween moveTween;
    private Tween fadeTween;
    private System.Action<DamagePopup> releaseAction;
    private float moveY = 100f; // Upward movement distance
    private float duration = 1f;

    public void Setup(ulong damageAmount)
    {
        Setup("-" + damageAmount.ToString(), new Color(1f, 0.2f, 0.2f));
    }

    public void Setup(ulong damageAmount, System.Action<DamagePopup> onComplete)
    {
        Setup("-" + damageAmount.ToString(), new Color(1f, 0.2f, 0.2f), onComplete);
    }

    public void Setup(string text, Color color)
    {
        Setup(text, color, null);
    }

    public void Setup(string text, Color color, System.Action<DamagePopup> onComplete)
    {
        EnsureComponents();
        releaseAction = onComplete;
        gameObject.SetActive(true);
        KillTweens();

        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = 24;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.raycastTarget = false;
        var textColor = textMesh.color;
        textColor.a = 1f;
        textMesh.color = textColor;
        rectTransform.localScale = Vector3.one;

        Vector2 startPosition = rectTransform.anchoredPosition;
        moveTween = rectTransform.DOAnchorPosY(startPosition.y + moveY, duration).SetEase(Ease.OutCubic);
        fadeTween = textMesh.DOFade(0f, duration).OnComplete(Release);
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnDisable()
    {
        KillTweens();
        releaseAction = null;
    }

    private void EnsureComponents()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshProUGUI>();
        }
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMeshProUGUI>();
        }
    }

    private void KillTweens()
    {
        moveTween?.Kill(false);
        fadeTween?.Kill(false);
        moveTween = null;
        fadeTween = null;
    }

    private void Release()
    {
        var callback = releaseAction;
        releaseAction = null;
        callback?.Invoke(this);
    }
}
