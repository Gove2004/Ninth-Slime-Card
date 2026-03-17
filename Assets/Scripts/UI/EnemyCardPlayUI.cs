using System;
using DG.Tweening;
using UnityEngine;

public class EnemyCardPlayUI : MonoBehaviour
{
    private Action disposablePlay;
    private Action disposableDraw;
    private CardUIItem cardUI;
    private Tween currentAnimation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        disposablePlay = EventCenter.Register("Enemy_PlayedCard", obj => OnEnemyPlayCard((BaseCard)obj));
        disposableDraw = EventCenter.Register("Enemy_DrewCard", obj => OnEnemyDrawCard((BaseCard)obj));

        cardUI = GetComponent<CardUIItem>();
        cardUI.gameObject.SetActive(false);

    }

    private void OnEnemyPlayCard(BaseCard cardObj)
    {
        if (cardObj == null) return;
        cardUI.SetData(cardObj);
        // 显示卡牌UI
        ShowPlayAnimation();
    }

    private void OnEnemyDrawCard(BaseCard cardObj)
    {
        if (cardObj == null) return;
        cardUI.SetData(cardObj);
        // 显示卡牌UI
        ShowDrawAnimation();
    }

    private float GetEnemyAnimationScale()
    {
        var enemy = BattleManager.Instance != null ? BattleManager.Instance.enemy as EnemyBoss : null;
        if (enemy == null) return 1f;

        float manaFactor = Mathf.InverseLerp(3f, 12f, ClampToFloat(enemy.mana));
        float handFactor = Mathf.InverseLerp(3f, 10f, enemy.Cards.Count);
        float t = Mathf.Clamp01(Mathf.Max(manaFactor, handFactor));
        return Mathf.Lerp(1f, 0.35f, t);
    }

    private static float ClampToFloat(ulong value)
    {
        if (value >= (ulong)int.MaxValue) return int.MaxValue;
        return value;
    }

    private void StopCurrentAnimation()
    {
        currentAnimation?.Kill(false);
        currentAnimation = null;
        cardUI.transform.DOKill(false);
    }

    private void ShowPlayAnimation()
    {
        // 这里可以添加显示动画等效果
        StopCurrentAnimation();
        cardUI.gameObject.SetActive(true);
        cardUI.transform.localRotation = Quaternion.identity;
        cardUI.transform.localScale = Vector3.zero;

        float scale = GetEnemyAnimationScale();
        float expandDuration = Mathf.Max(0.1f, 0.33f * scale);
        float holdDuration = Mathf.Max(0.08f, 0.33f * scale);
        float collapseDuration = Mathf.Max(0.1f, 0.33f * scale);

        var sequence = DOTween.Sequence();
        sequence.Append(cardUI.transform.DOScale(Vector3.one, expandDuration).SetEase(Ease.OutBack));
        sequence.AppendInterval(holdDuration);
        sequence.Append(cardUI.transform.DOScale(Vector3.zero, collapseDuration).SetEase(Ease.InBack));
        sequence.OnComplete(() =>
        {
            cardUI.gameObject.SetActive(false);
            currentAnimation = null;
        });
        sequence.OnKill(() =>
        {
            if (currentAnimation == sequence) currentAnimation = null;
        });
        currentAnimation = sequence;
    }

    private void ShowDrawAnimation()
    {
        // 这里可以添加显示动画等效果
        StopCurrentAnimation();
        cardUI.gameObject.SetActive(true);

        float scale = GetEnemyAnimationScale();
        float rotateDuration = Mathf.Max(0.12f, 0.5f * scale);

        cardUI.transform.localScale = Vector3.one;
        cardUI.transform.localRotation = Quaternion.identity;
        var tween = cardUI.transform.DORotate(new Vector3(0, 360, 0), rotateDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear).OnComplete(() =>
        {
            cardUI.gameObject.SetActive(false);
            currentAnimation = null;
        });
        tween.OnKill(() =>
        {
            if (currentAnimation == tween) currentAnimation = null;
        });
        currentAnimation = tween;
    }

    void OnDestroy()
    {
        StopCurrentAnimation();
        disposablePlay?.Invoke();
        disposableDraw?.Invoke();
    }
}
