using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>程序化 UI 工具箱。界面全部在运行时构建，场景只保留根节点。</summary>
    public static class Ui
    {
        private static TMP_FontAsset font;
        private static Sprite white;

        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<TMP_FontAsset>("Fonts/Alibaba_PuHuiTi_2");
                }

                if (font == null)
                {
                    font = TMP_Settings.defaultFontAsset;
                }

                return font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    white = SpriteForge.Solid();
                }

                return white;
            }
        }

        #region 基础节点

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : White;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Rounded(string name, Transform parent, int radius, Color fill, Color border, int borderWidth, bool raycast = false)
        {
            var img = Panel(name, parent, SpriteForge.Panel(radius, fill, border, borderWidth), Color.white);
            img.type = Image.Type.Sliced;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Solid(string name, Transform parent, Color color, bool raycast = false)
        {
            var img = Panel(name, parent, White, color);
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, int size, Color color, TextAlignmentOptions align)
        {
            var rt = Node(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Button TextButton(string name, Transform parent, string caption, int fontSize, Color fill, Color textColor, Action onClick)
        {
            var img = Rounded(name, parent, Theme.RadiusM, fill, Theme.Lighten(fill, 0.18f), 2, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var label = Label("Caption", img.transform, caption, fontSize, textColor, TextAlignmentOptions.Center);
            label.margin = new Vector4(12f, 0f, 12f, 0f);
            label.characterSpacing = 2f;

            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (GameApp.I != null)
                    {
                        GameApp.I.Audio.PlaySfx("ui");
                    }

                    Tween.Punch(img.rectTransform, 0.10f, 0.22f);
                    onClick();
                });
            }

            return btn;
        }

        public static ScrollRect VerticalScroll(string name, Transform parent, out RectTransform content)
        {
            var viewportRt = Node(name, parent);
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.sprite = White;
            viewportImg.color = new Color(1f, 1f, 1f, 0.003f);
            viewportImg.raycastTarget = true;
            var mask = viewportRt.gameObject.AddComponent<RectMask2D>();

            content = Node("Content", viewportRt);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 100f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewportRt.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;
            scroll.inertia = true;
            return scroll;
        }

        #endregion

        #region 布局助手

        public static RectTransform Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Fixed(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, int spacing, RectOffset padding)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, int spacing, RectOffset padding)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return layout;
        }

        public static GridLayoutGroup Grid(RectTransform rt, Vector2 cellSize, Vector2 spacing, int columns)
        {
            var layout = rt.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cellSize;
            layout.spacing = spacing;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        public static LayoutElement FixedSize(GameObject go, float width, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = go.AddComponent<LayoutElement>();
            }

            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }

            return le;
        }

        #endregion

        #region 自适应

        /// <summary>安全区适配：横屏刘海与手势条。</summary>
        public static void ApplySafeArea(RectTransform rt, float designWidth, float designHeight)
        {
            var safe = UnityEngine.Screen.safeArea;
            float sw = UnityEngine.Screen.width;
            float sh = UnityEngine.Screen.height;

            if (sw <= 0f || sh <= 0f)
            {
                return;
            }

            Vector2 min = new Vector2(safe.xMin / sw, safe.yMin / sh);
            Vector2 max = new Vector2(safe.xMax / sw, safe.yMax / sh);

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
