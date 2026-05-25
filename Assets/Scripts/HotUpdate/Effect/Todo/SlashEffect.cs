using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SlashEffect : MonoBehaviour
{
    public Image image;

    public void Setup()
    {
        image = GetComponent<Image>();
        if (image == null) image = gameObject.AddComponent<Image>();
        
        image.raycastTarget = false;
        
        // Simple scale/fade animation
        transform.localScale = Vector3.one * 0.5f;
        transform.DOScale(Vector3.one * 2.0f, 0.2f); // Expand quickly
        
        // Ensure alpha is 1 before fading out, in case the image was created with alpha 0 or something else
        Color c = image.color;
        c.a = 1f;
        image.color = c;
        
        image.DOFade(0, 0.3f).OnComplete(() => Destroy(gameObject));
    }
}
