using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SlashEffect : MonoBehaviour
{
    public Image image;
    private Tween scaleTween;
    private Tween fadeTween;
    private System.Action<SlashEffect> releaseAction;

    public void Setup()
    {
        EnsureImage();
        image.raycastTarget = false;

        KillTweens();
        transform.localScale = Vector3.one * 0.5f;
        scaleTween = transform.DOScale(Vector3.one * 2.0f, 0.2f);

        Color c = image.color;
        c.a = 1f;
        image.color = c;
        fadeTween = image.DOFade(0, 0.3f).OnComplete(Release);
    }

    public void Setup(Sprite sprite, Color color, float rotationZ, System.Action<SlashEffect> onComplete)
    {
        EnsureImage();
        releaseAction = onComplete;
        image.sprite = sprite;
        image.color = color;
        transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        Setup();
    }

    private void Awake()
    {
        EnsureImage();
    }

    private void OnDisable()
    {
        KillTweens();
        releaseAction = null;
    }

    private void EnsureImage()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }
    }

    private void KillTweens()
    {
        scaleTween?.Kill(false);
        fadeTween?.Kill(false);
        scaleTween = null;
        fadeTween = null;
    }

    private void Release()
    {
        var callback = releaseAction;
        releaseAction = null;
        callback?.Invoke(this);
    }
}
