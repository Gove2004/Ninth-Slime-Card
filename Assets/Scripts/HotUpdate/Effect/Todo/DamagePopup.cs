using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamagePopup : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private float moveY = 100f; // Upward movement distance
    private float duration = 1f;

    public void Setup(ulong damageAmount)
    {
        Setup("-" + damageAmount.ToString(), new Color(1f, 0.2f, 0.2f));
    }

    public void Setup(string text, Color color)
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMeshProUGUI>();

        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = 24;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.raycastTarget = false;

        // Animation
        // Check if we are using RectTransform for positioning (like center screen text)
        // If so, we might want to animate anchoredPosition instead, but DOMoveY works on world position which is fine usually.
        // However, for center screen text, let's make sure we don't drift too weirdly if canvas scales.
        // Using local move is safer.
        transform.DOLocalMoveY(transform.localPosition.y + moveY, duration).SetEase(Ease.OutCubic);
        textMesh.DOFade(0, duration).OnComplete(() => Destroy(gameObject));
    }
}
