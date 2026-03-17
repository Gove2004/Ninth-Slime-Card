using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamageEffectManager : MonoBehaviour
{
    public static DamageEffectManager Instance;

    [Header("Positions")]
    public Transform playerTransform;
    public Transform enemyTransform;
    public Canvas targetCanvas;
    public Camera uiCamera;
    public Camera shakeCamera;
    public RectTransform shakeRoot;

    [Header("Settings")]
    public Vector2 playerEffectOffset = Vector2.zero;
    public Vector2 enemyEffectOffset = Vector2.zero;
    [Header("Screen Shake")]
    public float minShakeStrength = 0.12f;
    public float maxShakeStrength = 0.65f;
    public float minShakeDuration = 0.08f;
    public float maxShakeDuration = 0.2f;
    public float shakeDamageUpperBound = 120f;
    public int shakeVibrato = 24;
    public float shakeRandomness = 90f;
    public bool shakeFadeOut = true;
    public bool shakeSnapping = false;
    public float uiShakeStrengthMultiplier = 48f;
    private Tween screenShakeTween;
    private bool hasReparentedCanvasChildren;
    private Vector2 shakeRootBaseAnchoredPosition;
    private bool hasShakeRootBaseAnchoredPosition;
    private RectTransform shakeRectTransform;
    private Vector2 shakeRectBaseAnchoredPosition;
    private bool hasShakeRectBaseAnchoredPosition;
    private Vector3 shakeCameraBasePosition;
    private bool hasShakeCameraBasePosition;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        FindPositions();
        EnsureShakeRoot();
        ApplyVibrationSetting(GameSettings.VibrationEnabled);

        EventCenter.Register("BattleStarted", (obj) => 
        {
            FindPositions();
            EnsureShakeRoot();
            SubscribeToCharacters();
        });
        
        // If battle already started (e.g. reload or late init)
        if (BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            SubscribeToCharacters();
        }
    }

    public void ApplyVibrationSetting(bool enabled)
    {
        if (!enabled)
        {
            StopScreenShake();
        }
    }

    private void OnDestroy()
    {
        StopScreenShake();
        if (BattleManager.Instance != null)
        {
            if (BattleManager.Instance.player != null)
            {
                BattleManager.Instance.player.DamageTaken -= OnPlayerDamage;
                BattleManager.Instance.player.HealTaken -= OnPlayerHeal;
            }
            if (BattleManager.Instance.enemy != null)
            {
                BattleManager.Instance.enemy.DamageTaken -= OnEnemyDamage;
                BattleManager.Instance.enemy.HealTaken -= OnEnemyHeal;
            }
        }
    }

    private void FindPositions()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }
        if (uiCamera == null)
        {
            uiCamera = targetCanvas != null ? targetCanvas.worldCamera : Camera.main;
        }
    }

    private void SubscribeToCharacters()
    {
        if (BattleManager.Instance.player != null)
        {
            BattleManager.Instance.player.DamageTaken -= OnPlayerDamage;
            BattleManager.Instance.player.DamageTaken += OnPlayerDamage;
            BattleManager.Instance.player.HealTaken -= OnPlayerHeal;
            BattleManager.Instance.player.HealTaken += OnPlayerHeal;
        }
        if (BattleManager.Instance.enemy != null)
        {
            BattleManager.Instance.enemy.DamageTaken -= OnEnemyDamage;
            BattleManager.Instance.enemy.DamageTaken += OnEnemyDamage;
            BattleManager.Instance.enemy.HealTaken -= OnEnemyHeal;
            BattleManager.Instance.enemy.HealTaken += OnEnemyHeal;
        }
    }

    private void OnPlayerHeal(ulong amount)
    {
        if (playerTransform == null) return;
        ShowHealEffect(playerTransform, amount, playerEffectOffset);
    }

    private void OnEnemyHeal(ulong amount)
    {
        if (enemyTransform == null) return;
        ShowHealEffect(enemyTransform, amount, enemyEffectOffset);
    }

    private void OnPlayerDamage(ulong amount, BaseCharacter source)
    {
        if (playerTransform == null) return;
        ShowEffect(playerTransform, amount, playerEffectOffset, source);
    }

    private void OnEnemyDamage(ulong amount, BaseCharacter source)
    {
        if (enemyTransform == null) return;
        ShowEffect(enemyTransform, amount, enemyEffectOffset, source);
    }

    public void ShowFloatingText(Transform targetDetails, string text, Color color, Vector3? offsetOverride = null)
    {
        var canvas = GetCanvas();
        if (canvas == null) return;

        GameObject popupObj = new GameObject("FloatingText_Center");
        popupObj.transform.SetParent(GetEffectParentTransform(canvas), false);
        
        RectTransform rect = popupObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, 100); // Full width, height 100
        
        var popup = popupObj.AddComponent<DamagePopup>();
        popup.Setup(text, color);
    }

    private void ShowEffect(Transform targetDetails, ulong amount, Vector2 offset, BaseCharacter source)
    {
        if (targetDetails == null) return;
        bool isPlayerSource = source is Player;
        Color effectColor = isPlayerSource ? Color.white : Color.green;
        string sfxKey = isPlayerSource ? "斩击" : "毒液";

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(sfxKey);
        PlayDamageScreenShake(amount);

        var canvas = GetCanvas();
        if (canvas == null) return;

        GameObject slashObj = new GameObject("SlashEffect");
        slashObj.transform.SetParent(GetEffectParentTransform(canvas), false);
        var slashRect = slashObj.AddComponent<RectTransform>();
        slashRect.sizeDelta = new Vector2(128, 128);
        slashRect.anchoredPosition = GetCanvasPosition(targetDetails.position) + offset;
        
        var slash = slashObj.AddComponent<SlashEffect>();
        var img = slashObj.AddComponent<UnityEngine.UI.Image>();
        img.sprite = CreateSlashSprite();
        img.color = effectColor;
        slashObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-45f, 45f));
        
        slash.Setup();

        GameObject popupObj = new GameObject("DamagePopup");
        popupObj.transform.SetParent(GetEffectParentTransform(canvas), false);
        var popupRect = popupObj.AddComponent<RectTransform>();
        popupRect.anchoredPosition = GetCanvasPosition(targetDetails.position) + offset;
        
        var popup = popupObj.AddComponent<DamagePopup>();
        popup.Setup(amount);
    }

    private void ShowHealEffect(Transform targetDetails, ulong amount, Vector2 offset)
    {
        if (targetDetails == null) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Heal");
        
        var canvas = GetCanvas();
        if (canvas == null) return;

        GameObject healObj = new GameObject("HealEffect");
        healObj.transform.SetParent(GetEffectParentTransform(canvas), false);
        var healRect = healObj.AddComponent<RectTransform>();
        healRect.sizeDelta = new Vector2(128, 128);
        healRect.anchoredPosition = GetCanvasPosition(targetDetails.position) + offset;
        
        var slash = healObj.AddComponent<SlashEffect>();
        var img = healObj.AddComponent<UnityEngine.UI.Image>();
        img.sprite = CreateHealSprite();
        img.color = Color.green;
        
        slash.Setup();

        GameObject popupObj = new GameObject("HealPopup");
        popupObj.transform.SetParent(GetEffectParentTransform(canvas), false);
        var popupRect = popupObj.AddComponent<RectTransform>();
        popupRect.anchoredPosition = GetCanvasPosition(targetDetails.position) + offset;
        
        var popup = popupObj.AddComponent<DamagePopup>();
        popup.Setup("+" + amount, Color.green);
    }

    private Canvas GetCanvas()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }
        return targetCanvas;
    }

    private Camera GetCamera(Canvas canvas)
    {
        if (uiCamera != null) return uiCamera;
        if (canvas != null && canvas.worldCamera != null) return canvas.worldCamera;
        return Camera.main;
    }

    private Camera GetShakeCamera()
    {
        if (shakeCamera != null) return shakeCamera;
        if (Camera.main != null) return Camera.main;
        if (uiCamera != null) return uiCamera;
        if (Camera.allCamerasCount > 0)
        {
            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled) return cameras[i];
            }
        }
        return null;
    }

    private RectTransform EnsureShakeRoot()
    {
        var canvas = GetCanvas();
        if (canvas == null) return null;

        if (shakeRoot == null)
        {
            var existing = canvas.transform.Find("ScreenShakeRoot");
            if (existing != null) shakeRoot = existing as RectTransform;
        }

        if (shakeRoot == null)
        {
            var rootObj = new GameObject("ScreenShakeRoot");
            shakeRoot = rootObj.AddComponent<RectTransform>();
            shakeRoot.SetParent(canvas.transform, false);
        }
        var canvasRect = canvas.transform as RectTransform;
        shakeRoot.anchorMin = new Vector2(0.5f, 0.5f);
        shakeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        shakeRoot.pivot = new Vector2(0.5f, 0.5f);
        shakeRoot.anchoredPosition = Vector2.zero;
        shakeRoot.localScale = Vector3.one;
        if (canvasRect != null)
        {
            shakeRoot.sizeDelta = canvasRect.rect.size;
        }

        if (!hasReparentedCanvasChildren)
        {
            int childCount = canvas.transform.childCount;
            var children = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
            {
                children[i] = canvas.transform.GetChild(i);
            }
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null || child == shakeRoot) continue;
                child.SetParent(shakeRoot, true);
            }
            hasReparentedCanvasChildren = true;
        }

        if (!hasShakeRootBaseAnchoredPosition)
        {
            shakeRootBaseAnchoredPosition = shakeRoot.anchoredPosition;
            hasShakeRootBaseAnchoredPosition = true;
        }

        return shakeRoot;
    }

    private Transform GetEffectParentTransform(Canvas canvas)
    {
        var root = EnsureShakeRoot();
        if (root != null) return root;
        return canvas.transform;
    }

    private RectTransform GetShakeRectTransform()
    {
        if (shakeRectTransform != null) return shakeRectTransform;
        var canvas = GetCanvas();
        if (canvas == null) return null;
        shakeRectTransform = canvas.transform as RectTransform;
        return shakeRectTransform;
    }

    private static float ClampToFloat(ulong value)
    {
        if (value >= (ulong)int.MaxValue) return int.MaxValue;
        return value;
    }

    private void PlayDamageScreenShake(ulong damageAmount)
    {
        if (damageAmount == 0) return;
        if (!GameSettings.VibrationEnabled) return;

        float damage = ClampToFloat(damageAmount);
        float upperBound = Mathf.Max(1f, shakeDamageUpperBound);
        float t = Mathf.Clamp01(damage / upperBound);
        float strength = Mathf.Lerp(minShakeStrength, maxShakeStrength, t);
        float duration = Mathf.Lerp(minShakeDuration, maxShakeDuration, t);

        var shakeRect = EnsureShakeRoot();
        if (shakeRect == null) shakeRect = GetShakeRectTransform();
        if (shakeRect != null)
        {
            if (!hasShakeRectBaseAnchoredPosition || shakeRectTransform != shakeRect)
            {
                shakeRectTransform = shakeRect;
                if (shakeRect == shakeRoot && hasShakeRootBaseAnchoredPosition)
                {
                    shakeRectBaseAnchoredPosition = shakeRootBaseAnchoredPosition;
                }
                else
                {
                    shakeRectBaseAnchoredPosition = shakeRect.anchoredPosition;
                }
                hasShakeRectBaseAnchoredPosition = true;
            }

            StopScreenShake();
            shakeRect.anchoredPosition = shakeRectBaseAnchoredPosition;
            float uiStrength = strength * Mathf.Max(1f, uiShakeStrengthMultiplier);
            screenShakeTween = shakeRect.DOShakeAnchorPos(duration, uiStrength, shakeVibrato, shakeRandomness, shakeSnapping, shakeFadeOut);
            screenShakeTween.OnKill(() =>
            {
                if (shakeRect == null) return;
                shakeRect.anchoredPosition = shakeRectBaseAnchoredPosition;
                if (screenShakeTween != null && !screenShakeTween.IsActive()) screenShakeTween = null;
            });
            screenShakeTween.OnComplete(() =>
            {
                if (shakeRect == null) return;
                shakeRect.anchoredPosition = shakeRectBaseAnchoredPosition;
                screenShakeTween = null;
            });
            return;
        }

        var cam = GetShakeCamera();
        if (cam == null) return;
        if (!hasShakeCameraBasePosition || shakeCamera != cam)
        {
            shakeCamera = cam;
            shakeCameraBasePosition = cam.transform.position;
            hasShakeCameraBasePosition = true;
        }

        StopScreenShake();
        cam.transform.position = shakeCameraBasePosition;
        screenShakeTween = cam.transform.DOShakePosition(duration, strength, shakeVibrato, shakeRandomness, shakeSnapping, shakeFadeOut);
        screenShakeTween.OnKill(() =>
        {
            if (cam == null) return;
            cam.transform.position = shakeCameraBasePosition;
            if (screenShakeTween != null && !screenShakeTween.IsActive()) screenShakeTween = null;
        });
        screenShakeTween.OnComplete(() =>
        {
            if (cam == null) return;
            cam.transform.position = shakeCameraBasePosition;
            screenShakeTween = null;
        });
    }

    private void StopScreenShake()
    {
        if (screenShakeTween == null) return;
        screenShakeTween.Kill(false);
        screenShakeTween = null;
        if (shakeRectTransform != null && hasShakeRectBaseAnchoredPosition)
        {
            shakeRectTransform.anchoredPosition = shakeRectBaseAnchoredPosition;
        }
        if (shakeCamera != null && hasShakeCameraBasePosition)
        {
            shakeCamera.transform.position = shakeCameraBasePosition;
        }
    }

    private Vector2 GetCanvasPosition(Vector3 worldPosition)
    {
        var canvas = GetCanvas();
        if (canvas == null) return Vector2.zero;
        var cam = GetCamera(canvas);
        var rect = canvas.transform as RectTransform;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, cam, out var localPos);
        return localPos;
    }

    private Sprite healSprite;
    private Sprite CreateHealSprite()
    {
        if (healSprite != null) return healSprite;

        int width = 128;
        int height = 128;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

        // Draw a circle/cross pattern
        Vector2 center = new Vector2(width / 2f, height / 2f);
        float radius = width / 3f;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist < radius)
                {
                    // Ring
                    if (dist > radius * 0.8f)
                    {
                         colors[y * width + x] = new Color(1, 1, 1, 0.8f);
                    }
                    // Plus sign inside
                    else if (Mathf.Abs(x - center.x) < 5 || Mathf.Abs(y - center.y) < 5)
                    {
                         colors[y * width + x] = new Color(1, 1, 1, 1f);
                    }
                }
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        healSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        return healSprite;
    }

    private Sprite slashSprite;
    private Sprite CreateSlashSprite()
    {
        if (slashSprite != null) return slashSprite;

        int width = 128;
        int height = 128;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

        // Draw a diagonal line
        for (int i = 0; i < width; i++)
        {
            int y = i;
            // Draw a thick line with fade on edges
            for (int k = -4; k <= 4; k++)
            {
                 int ny = y + k;
                 if(ny >= 0 && ny < height)
                 {
                    float alpha = 1.0f - (Mathf.Abs(k) / 5.0f);
                    colors[ny * width + i] = new Color(1, 1, 1, alpha);
                 }
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        slashSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        return slashSprite;
    }
}
