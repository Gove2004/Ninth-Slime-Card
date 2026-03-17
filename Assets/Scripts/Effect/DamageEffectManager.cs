using UnityEngine;
using TMPro;

public class DamageEffectManager : MonoBehaviour
{
    public static DamageEffectManager Instance;

    [Header("Positions")]
    public Transform playerTransform;
    public Transform enemyTransform;
    public Canvas targetCanvas;
    public Camera uiCamera;

    [Header("Settings")]
    public Vector2 playerEffectOffset = Vector2.zero;
    public Vector2 enemyEffectOffset = Vector2.zero;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        FindPositions();

        EventCenter.Register("BattleStarted", (obj) => 
        {
            FindPositions();
            SubscribeToCharacters();
        });
        
        // If battle already started (e.g. reload or late init)
        if (BattleManager.Instance != null && BattleManager.Instance.player != null)
        {
            SubscribeToCharacters();
        }
    }

    private void OnDestroy()
    {
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
        popupObj.transform.SetParent(canvas.transform, false);
        
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

        var canvas = GetCanvas();
        if (canvas == null) return;

        GameObject slashObj = new GameObject("SlashEffect");
        slashObj.transform.SetParent(canvas.transform, false);
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
        popupObj.transform.SetParent(canvas.transform, false);
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
        healObj.transform.SetParent(canvas.transform, false);
        var healRect = healObj.AddComponent<RectTransform>();
        healRect.sizeDelta = new Vector2(128, 128);
        healRect.anchoredPosition = GetCanvasPosition(targetDetails.position) + offset;
        
        var slash = healObj.AddComponent<SlashEffect>();
        var img = healObj.AddComponent<UnityEngine.UI.Image>();
        img.sprite = CreateHealSprite();
        img.color = Color.green;
        
        slash.Setup();

        GameObject popupObj = new GameObject("HealPopup");
        popupObj.transform.SetParent(canvas.transform, false);
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
