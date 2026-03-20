using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementToast : MonoBehaviour
{
    private static AchievementToast instance;

    public static void EnsureInstance()
    {
        if (instance != null) return;

        instance = FindFirstObjectByType<AchievementToast>(FindObjectsInactive.Include);
        if (instance != null) return;

        var root = new GameObject("AchievementToastCanvas");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        root.AddComponent<GraphicRaycaster>();

        var toastObj = new GameObject("AchievementToast", typeof(RectTransform));
        toastObj.transform.SetParent(root.transform, false);
        instance = toastObj.AddComponent<AchievementToast>();
    }

    private readonly Queue<AchievementManager.AchievementUnlockedInfo> queue = new();
    private Action onUnlockedUnsub;

    private RectTransform panel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI descText;
    private CanvasGroup canvasGroup;

    private bool isShowing;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
    }

    private void OnEnable()
    {
        onUnlockedUnsub = EventCenter.Register(AchievementManager.AchievementUnlockedEvent, OnAchievementUnlocked);
    }

    private void OnDisable()
    {
        onUnlockedUnsub?.Invoke();
        onUnlockedUnsub = null;
    }

    private void OnAchievementUnlocked(object obj)
    {
        if (obj is not AchievementManager.AchievementUnlockedInfo info) return;
        queue.Enqueue(info);
        if (!isShowing)
        {
            StartCoroutine(PlayQueue());
        }
    }

    private IEnumerator PlayQueue()
    {
        isShowing = true;
        while (queue.Count > 0)
        {
            var info = queue.Dequeue();
            ShowText(info);
            yield return AnimateToast();
        }
        isShowing = false;
    }

    private void ShowText(AchievementManager.AchievementUnlockedInfo info)
    {
        titleText.text = "成就达成";
        nameText.text = info.name;
        descText.text = info.description;
    }

    private IEnumerator AnimateToast()
    {
        const float enterDuration = 0.22f;
        const float stayDuration = 2.0f;
        const float exitDuration = 0.25f;

        Vector2 hiddenPos = new Vector2(-20f, 40f);
        Vector2 showPos = new Vector2(-20f, 220f);

        float t = 0f;
        canvasGroup.alpha = 0f;
        panel.anchoredPosition = hiddenPos;

        while (t < enterDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / enterDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            canvasGroup.alpha = eased;
            panel.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, showPos, eased);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        panel.anchoredPosition = showPos;

        float stay = 0f;
        while (stay < stayDuration)
        {
            stay += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < exitDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / exitDuration);
            float eased = p * p;
            canvasGroup.alpha = 1f - eased;
            panel.anchoredPosition = Vector2.LerpUnclamped(showPos, hiddenPos, eased);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        panel.anchoredPosition = hiddenPos;
    }

    private void BuildUi()
    {
        var selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
        }

        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(transform, false);

        panel = panelObj.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.sizeDelta = new Vector2(420f, 160f);
        panel.anchoredPosition = new Vector2(-20f, 40f);

        var bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

        var outline = panelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.75f, 0.35f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        canvasGroup = panelObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        titleText = CreateText(panel, "Title", new Vector2(20f, -14f), 26, FontStyles.Bold, new Color(0.62f, 1f, 0.68f, 1f));
        nameText = CreateText(panel, "Name", new Vector2(20f, -56f), 32, FontStyles.Bold, Color.white);
        descText = CreateText(panel, "Desc", new Vector2(20f, -104f), 22, FontStyles.Normal, new Color(0.86f, 0.9f, 0.95f, 1f));
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 topLeft, int fontSize, FontStyles style, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(topLeft.x, topLeft.y - 40f);
        rect.offsetMax = new Vector2(-20f, topLeft.y);

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
}
