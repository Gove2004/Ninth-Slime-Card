using System;
using DG.Tweening;
using UnityEngine;

public class TurnFocusCameraUI : MonoBehaviour
{
    [Header("Focus Points")]
    [SerializeField] private Transform playerFocusPoint;
    [SerializeField] private Transform enemyFocusPoint;

    [Header("World Tilt")]
    [SerializeField] private Transform worldTiltRoot;
    [SerializeField] private bool useWorldTilt = true;
    [SerializeField] private float playerTurnXOffset = 45f;
    [SerializeField] private float enemyTurnXOffset = -45f;

    [Header("UI Root")]
    [SerializeField] private RectTransform uiFocusRoot;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private bool useUiPan = false;

    [Header("Tuning")]
    [Range(0f, 1f)]
    [SerializeField] private float focusStrength = 0.4f;
    [SerializeField] private float tweenDuration = 0.45f;
    [SerializeField] private Ease tweenEase = Ease.OutCubic;
    [SerializeField] private float maxOffsetX = 220f;
    [SerializeField] private float maxOffsetY = 80f;
    [SerializeField] private bool useXAxisOnly = true;

    private Action onTurnStartUnsub;
    private Action onBattleStartedUnsub;
    private Action onBattleEndedUnsub;
    private Tween moveTween;
    private Tween worldMoveTween;
    private Vector2 baseAnchoredPosition;
    private bool hasBaseAnchoredPosition;
    private Vector3 baseWorldLocalPosition;
    private bool hasBaseWorldLocalPosition;

    public void BindTargets(Transform playerTarget, Transform enemyTarget)
    {
        playerFocusPoint = playerTarget;
        enemyFocusPoint = enemyTarget;
    }

    private void OnEnable()
    {
        onTurnStartUnsub = EventCenter.Register<CharacterEventContext>(GameEvents.CharacterTurnStarted, OnTurnStart);
        onBattleStartedUnsub = EventCenter.Register<BattleEventContext>(GameEvents.BattleStarted, _ =>
        {
            ResolveReferences();
            ApplyFocusByTurn(true, true);
        });
        onBattleEndedUnsub = EventCenter.Register<BattleEventContext>(GameEvents.BattleEnded, _ => ResetFocus(true));
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        onTurnStartUnsub?.Invoke();
        onBattleStartedUnsub?.Invoke();
        onBattleEndedUnsub?.Invoke();
        moveTween?.Kill();
        worldMoveTween?.Kill();
    }

    private void ResolveReferences()
    {
        if (useUiPan && viewportRect == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) viewportRect = canvas.GetComponent<RectTransform>();
        }

        if (useUiPan && uiFocusRoot == null)
        {
            BattleUI battleUI = FindFirstObjectByType<BattleUI>();
            if (battleUI != null) uiFocusRoot = battleUI.GetComponent<RectTransform>();
        }

        if (playerFocusPoint == null && BattleManager.Instance != null)
        {
            playerFocusPoint = BattleManager.Instance.playerTransformRef;
        }

        if (enemyFocusPoint == null && BattleManager.Instance != null)
        {
            enemyFocusPoint = BattleManager.Instance.enemyTransformRef;
        }

        if (!hasBaseAnchoredPosition && uiFocusRoot != null)
        {
            baseAnchoredPosition = uiFocusRoot.anchoredPosition;
            hasBaseAnchoredPosition = true;
        }

        if (worldTiltRoot == null)
        {
            Transform explicitWorld = GameObject.Find("World")?.transform;
            if (explicitWorld != null)
            {
                worldTiltRoot = explicitWorld;
            }
            else
            {
                worldTiltRoot = FindCommonAncestor(playerFocusPoint, enemyFocusPoint);
            }
        }

        if (!hasBaseWorldLocalPosition && worldTiltRoot != null)
        {
            baseWorldLocalPosition = worldTiltRoot.localPosition;
            hasBaseWorldLocalPosition = true;
        }
    }

    private void OnTurnStart(CharacterEventContext turnContext)
    {
        bool isPlayerTurn = false;
        if (BattleManager.Instance != null)
        {
            isPlayerTurn = BattleManager.Instance.IsPlayerTurn();
        }
        else if (turnContext?.Character != null)
        {
            isPlayerTurn = turnContext.Character is Player;
        }

        ApplyFocusByTurn(isPlayerTurn, false);
    }

    private void ApplyFocusByTurn(bool isPlayerTurn, bool instant)
    {
        ResolveReferences();
        ApplyWorldTiltByTurn(isPlayerTurn, instant);

        if (!useUiPan)
        {
            return;
        }

        if (uiFocusRoot == null || viewportRect == null || playerFocusPoint == null || enemyFocusPoint == null)
        {
            return;
        }

        if (!TryGetLocalPoint(playerFocusPoint, out Vector2 playerLocal))
        {
            return;
        }

        if (!TryGetLocalPoint(enemyFocusPoint, out Vector2 enemyLocal))
        {
            return;
        }

        Vector2 chosen = isPlayerTurn ? playerLocal : enemyLocal;
        Vector2 offset = -chosen * focusStrength;

        if (useXAxisOnly)
        {
            offset.y = 0f;
        }

        offset.x = Mathf.Clamp(offset.x, -Mathf.Abs(maxOffsetX), Mathf.Abs(maxOffsetX));
        offset.y = Mathf.Clamp(offset.y, -Mathf.Abs(maxOffsetY), Mathf.Abs(maxOffsetY));

        Vector2 targetAnchoredPosition = baseAnchoredPosition + offset;

        moveTween?.Kill();
        if (instant)
        {
            uiFocusRoot.anchoredPosition = targetAnchoredPosition;
            return;
        }

        moveTween = uiFocusRoot.DOAnchorPos(targetAnchoredPosition, Mathf.Max(0.01f, tweenDuration))
            .SetEase(tweenEase)
            .SetUpdate(true);
    }

    private void ApplyWorldTiltByTurn(bool isPlayerTurn, bool instant)
    {
        if (!useWorldTilt || worldTiltRoot == null) return;

        float targetX = isPlayerTurn ? playerTurnXOffset : enemyTurnXOffset;
        Vector3 targetPosition = baseWorldLocalPosition + new Vector3(targetX, 0f, 0f);

        worldMoveTween?.Kill();
        if (instant)
        {
            worldTiltRoot.localPosition = targetPosition;
            return;
        }

        worldMoveTween = worldTiltRoot.DOLocalMove(targetPosition, Mathf.Max(0.01f, tweenDuration))
            .SetEase(tweenEase)
            .SetUpdate(true);
    }

    private Transform FindCommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null) return null;

        Transform cursorA = a;
        while (cursorA != null)
        {
            Transform cursorB = b;
            while (cursorB != null)
            {
                if (cursorA == cursorB) return cursorA;
                cursorB = cursorB.parent;
            }
            cursorA = cursorA.parent;
        }

        return null;
    }

    private bool TryGetLocalPoint(Transform focusTransform, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (focusTransform == null || viewportRect == null) return false;

        Canvas canvas = viewportRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, focusTransform.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, screenPoint, cam, out localPoint);
    }

    public void ResetFocus(bool instant)
    {
        moveTween?.Kill();
        worldMoveTween?.Kill();

        if (useUiPan && uiFocusRoot != null)
        {
            if (instant)
            {
                uiFocusRoot.anchoredPosition = baseAnchoredPosition;
            }
            else
            {
                moveTween = uiFocusRoot.DOAnchorPos(baseAnchoredPosition, Mathf.Max(0.01f, tweenDuration))
                    .SetEase(tweenEase)
                    .SetUpdate(true);
            }
        }

        if (useWorldTilt && worldTiltRoot != null)
        {
            if (instant)
            {
                worldTiltRoot.localPosition = baseWorldLocalPosition;
            }
            else
            {
                worldMoveTween = worldTiltRoot.DOLocalMove(baseWorldLocalPosition, Mathf.Max(0.01f, tweenDuration))
                    .SetEase(tweenEase)
                    .SetUpdate(true);
            }
        }
    }
}
