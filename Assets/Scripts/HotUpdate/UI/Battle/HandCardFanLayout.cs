using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class HandCardFanLayout : MonoBehaviour
{
    public float fanAngle = 30f;
    public float radius = 200f;
    public bool invertDirection = false;
    public float cardSpacingOffset = -30f;

    [SerializeField] private float tweenDuration = 0.2f;
    [SerializeField] private float tweenStagger = 0.03f;

    private Sequence layoutSequence;

    private void OnTransformChildrenChanged()
    {
        if (!Application.isPlaying) return;
        RebuildLayout();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        RebuildLayout();
    }

    public void RebuildLayout()
    {
        if (layoutSequence != null && layoutSequence.IsActive())
        {
            layoutSequence.Kill();
        }

        layoutSequence = DOTween.Sequence();

        int childCount = transform.childCount;
        if (childCount == 0) return;

        if (childCount == 1)
        {
            Transform singleChild = transform.GetChild(0);
            layoutSequence.Insert(0f, singleChild.DOLocalMove(Vector3.zero, tweenDuration).SetEase(Ease.OutCubic));
            layoutSequence.Insert(0f, singleChild.DOLocalRotate(Vector3.zero, tweenDuration).SetEase(Ease.OutCubic));
            return;
        }

        float halfAngle = fanAngle / 2f;
        float angleStep = fanAngle / (childCount - 1);

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);

            float currentAngle = -halfAngle + angleStep * i;
            if (invertDirection) currentAngle = -currentAngle;

            float rad = currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * radius;
            float y = (1 - Mathf.Cos(rad)) * radius * 0.1f;
            float overlapFactor = (i - (childCount - 1) / 2.0f) * cardSpacingOffset;

            Vector3 targetPos = new Vector3(x + overlapFactor, y, 0f);
            Vector3 targetRot = new Vector3(0f, 0f, -currentAngle);

            float delay = i * tweenStagger;
            layoutSequence.Insert(delay, child.DOLocalMove(targetPos, tweenDuration).SetEase(Ease.OutCubic));
            layoutSequence.Insert(delay, child.DOLocalRotate(targetRot, tweenDuration).SetEase(Ease.OutCubic));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyFanShapeInstant();
        }
    }
#endif

    private void ApplyFanShapeInstant()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;

        if (childCount == 1)
        {
            Transform singleChild = transform.GetChild(0);
            singleChild.localPosition = Vector3.zero;
            singleChild.localRotation = Quaternion.identity;
            return;
        }

        float halfAngle = fanAngle / 2f;
        float angleStep = fanAngle / (childCount - 1);

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);

            float currentAngle = -halfAngle + angleStep * i;
            if (invertDirection) currentAngle = -currentAngle;

            float rad = currentAngle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * radius;
            float y = (1 - Mathf.Cos(rad)) * radius * 0.1f;
            float overlapFactor = (i - (childCount - 1) / 2.0f) * cardSpacingOffset;

            child.localPosition = new Vector3(x + overlapFactor, y, 0f);
            child.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        }
    }
}