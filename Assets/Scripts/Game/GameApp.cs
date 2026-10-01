using System;
using System.Collections.Generic;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>应用根。构建画布与分层，持有服务与流程状态，管理屏幕栈。</summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        public TapService Tap { get; private set; }
        public AudioService Audio { get; private set; }
        public RunDirector Director { get; private set; }
        public CardDatabase Cards { get; private set; }
        public AchievementDatabase Achievements { get; private set; }
        public MetaState Meta { get; private set; }
        public SaveData Save { get; private set; }
        /// <summary>本局累计自伤，用于"受着"成就。</summary>
        public int RunSelfDamage { get; set; }

        public RectTransform Root { get; private set; }
        public RectTransform BackgroundLayer { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public RectTransform FadeLayer { get; private set; }

        private readonly List<Screen> stack = new List<Screen>();
        private readonly List<ToastEntry> toasts = new List<ToastEntry>();
        private Canvas canvas;
        private Image fadeImage;
        private Image toastBox;
        private TextMeshProUGUI toastLabel;

        private struct ToastEntry
        {
            public float bornAt;
            public TextMeshProUGUI label;
            public Image box;
        }

        public Screen Current
        {
            get { return stack.Count > 0 ? stack[stack.Count - 1] : null; }
        }

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }

            I = this;
            Application.targetFrameRate = 60;

            BuildCamera();
            BuildEventSystem();
            BuildCanvas();
            BuildServices();
        }

        private void Start()
        {
            Push(new BootScreen());
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Current != null)
            {
                Current.Tick(dt);
            }

            TickToasts();
        }

        private void OnDestroy()
        {
            if (Tap != null)
            {
                Tap.Shutdown();
            }

            if (I == this)
            {
                I = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                PersistMeta();
            }
        }

        private void OnApplicationQuit()
        {
            PersistMeta();
        }

        #region 初始化

        private void BuildCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera));
                go.tag = "MainCamera";
                cam = go.GetComponent<Camera>();
            }

            // 场景里只有 GameApp 一个对象，相机与音频监听者都在运行时补齐。
            // 缺少 AudioListener 时 Unity 会持续刷 "There are no audio listeners in the scene"，
            // 且所有 AudioSource 都听不见——这里跟着相机一起建。
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.BgDeep;
        }

        private void BuildEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));

            // 工程使用新输入系统：用反射挂载对应模块，避免硬依赖与旧模块报错。
            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null)
            {
                go.AddComponent(moduleType);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
        }

        private void BuildCanvas()
        {
            var go = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Root = (RectTransform)go.transform;

            BackgroundLayer = Ui.Node("Background", Root);
            ScreenLayer = Ui.Node("Screens", Root);
            OverlayLayer = Ui.Node("Overlays", Root);
            FadeLayer = Ui.Node("Fade", Root);

            var bg = Ui.Solid("BgFill", BackgroundLayer, Theme.BgDeep);
            bg.raycastTarget = false;

            // 真·背景图（月夜城堡剪影）。拉伸铺满，剪影风格允许轻微纵横比偏差。
            Sprite bgArt = ArtLib.BgMain;
            if (bgArt != null)
            {
                var art = Ui.Panel("BgArt", BackgroundLayer, bgArt, new Color(0.86f, 0.9f, 0.92f, 1f));
                art.raycastTarget = false;
                Ui.Stretch(art.rectTransform, 0f, 0f, 0f, 0f);

                // 上下压暗渐变，保证 UI 文字可读
                var shadeTop = Ui.Panel("BgShadeTop", BackgroundLayer, SpriteForge.Glow(64, Theme.BgDeep), Theme.WithAlpha(Color.white, 0.55f));
                shadeTop.raycastTarget = false;
                Ui.Fixed(shadeTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(2200f, 500f));

                var shadeBottom = Ui.Panel("BgShadeBottom", BackgroundLayer, SpriteForge.Glow(64, Theme.BgDeep), Theme.WithAlpha(Color.white, 0.75f));
                shadeBottom.raycastTarget = false;
                Ui.Fixed(shadeBottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(2200f, 640f));
            }

            // 氛围补光：一团对角柔光，让剪影夜景不至于死板。
            var glowA = Ui.Panel("GlowA", BackgroundLayer,
                SpriteForge.Glow(256, Theme.Accent), Theme.WithAlpha(Color.white, 0.10f));
            Ui.Fixed(glowA.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(140f, -120f), new Vector2(1300f, 1300f));

            var vignette = Ui.Panel("Vignette", BackgroundLayer, SpriteForge.Vignette(512, Theme.BgDeep), Color.white);
            vignette.raycastTarget = false;
            Ui.Stretch(vignette.rectTransform, 0f, 0f, 0f, 0f);

            fadeImage = Ui.Solid("FadeFill", FadeLayer, Theme.WithAlpha(Theme.BgDeep, 0f));
            fadeImage.raycastTarget = false;

            toastBox = Ui.Rounded("ToastBox", OverlayLayer, Theme.RadiusM,
                Theme.WithAlpha(Theme.PanelRaised, 0.97f), Theme.WithAlpha(Theme.Accent, 0.55f), 2);
            Ui.Fixed(toastBox.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(780f, 68f));

            toastLabel = Ui.Label("Toast", toastBox.transform, string.Empty, Theme.H4, Theme.Text, TextAlignmentOptions.Center);
            Ui.Stretch(toastLabel.rectTransform, 22f, 8f, 22f, 8f);
            toastBox.gameObject.SetActive(false);
        }

        private void BuildServices()
        {
            Tween.Init(gameObject);
            Save = SaveService.Current;
            Meta = SaveService.ToMeta(Save);
            Cards = ContentService.LoadCards();
            Achievements = ContentService.LoadAchievements();
            Audio = new AudioService(gameObject);
            Tap = new TapService();

            Director = new RunDirector(Cards, Achievements, Meta, Environment.TickCount);
            Director.EnsureRun();
        }

        #endregion

        #region 屏幕栈

        public void Push(Screen screen)
        {
            if (screen == null)
            {
                return;
            }

            if (Current != null)
            {
                Current.SetVisible(false);
            }

            stack.Add(screen);
            screen.Attach(ScreenLayer);
            screen.SetVisible(true);
        }

        public void Pop()
        {
            if (stack.Count == 0)
            {
                return;
            }

            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            top.Destroy();

            if (Current != null)
            {
                Current.SetVisible(true);
            }
        }

        public void Replace(Screen screen)
        {
            while (stack.Count > 0)
            {
                var top = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                top.Destroy();
            }

            Push(screen);
        }

        public void PopTo<T>() where T : Screen
        {
            while (stack.Count > 1 && !(stack[stack.Count - 1] is T))
            {
                var top = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                top.Destroy();
            }

            if (Current != null)
            {
                Current.SetVisible(true);
            }
        }

        #endregion

        #region 覆盖层

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            toastLabel.text = message;
            toastBox.gameObject.SetActive(true);
            toastLabel.gameObject.SetActive(true);
            toastLabel.color = Theme.Text;
            toastBox.color = Color.white;

            toasts.Clear();
            toasts.Add(new ToastEntry { bornAt = Time.unscaledTime, label = toastLabel, box = toastBox });
        }

        private void TickToasts()
        {
            if (toasts.Count == 0)
            {
                return;
            }

            float age = Time.unscaledTime - toasts[0].bornAt;
            if (age > 2.2f)
            {
                toasts[0].label.gameObject.SetActive(false);
                toasts[0].box.gameObject.SetActive(false);
                toasts.Clear();
                return;
            }

            float alpha = age < 1.4f ? 1f : Mathf.Clamp01(1f - (age - 1.4f) / 0.8f);
            var textColor = Theme.Text;
            textColor.a = alpha;
            toasts[0].label.color = textColor;

            var boxColor = Color.white;
            boxColor.a = alpha;
            toasts[0].box.color = boxColor;
        }

        public void Fade(float from, float to, float duration, Action done)
        {
            StartCoroutine(FadeRoutine(from, to, duration, done));
        }

        /// <summary>屏幕不是 MonoBehaviour，协程统一挂到应用根上跑。</summary>
        public Coroutine Run(System.Collections.IEnumerator routine)
        {
            return routine == null ? null : StartCoroutine(routine);
        }

        public void Stop(Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        private System.Collections.IEnumerator FadeRoutine(float from, float to, float duration, Action done)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                fadeImage.color = Theme.WithAlpha(Theme.BgDeep, Mathf.Lerp(from, to, k));
                yield return null;
            }

            fadeImage.color = Theme.WithAlpha(Theme.BgDeep, to);
            if (done != null)
            {
                done();
            }
        }

        #endregion

        #region 持久化

        public void PersistMeta()
        {
            SaveService.FromMeta(Save, Meta);
            SaveService.Save();
        }

        /// <summary>成就达成后上报 TapTap（本地解锁记录已在 RunDirector 内写进 Meta）。</summary>
        public void ReportAchievement(string achievementId, string displayName)
        {
            Toast("成就达成 · " + displayName);
            Tap.UnlockAchievement(achievementId);
            PersistMeta();
        }

        /// <summary>结算后刷新一次"可兑换成就"并持久化。</summary>
        public void EvaluateAndFlush()
        {
            Director.CollectNewlyReady();
            PersistMeta();
        }

        #endregion
    }
}
