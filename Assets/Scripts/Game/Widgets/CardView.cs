using System;
using System.Text;
using Slime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 一张可交互的卡牌。卡面全部程序化拼装：系列色边框、费用徽章、
    /// 系列插画、卡名，以及把 [数值N] 换成真实数字的效果文本。
    /// </summary>
    public sealed class CardView
    {
        public const float W = 216f;
        public const float H = 312f;

        public RectTransform Root { get; private set; }
        public CardInstance Instance { get; private set; }
        public CardDef Def { get { return Instance != null ? Instance.Def : null; } }

        private readonly Action<CardView> onClick;

        private Image frame;
        private Image artImage;
        private Image costBadge;
        private Image divider;
        private Image seriesBar;
        private TextMeshProUGUI costLabel;
        private TextMeshProUGUI nameLabel;
        private TextMeshProUGUI descLabel;
        private Button button;

        private bool affordable = true;
        private bool selected;
        private bool dimmed;
        private bool interactive = true;
        private float baseScale = 1f;

        // 拖动状态
        private bool dragging;
        private bool scaleLocked;
        private Vector2 homePosition;
        private float homeRotation;
        private CardDrag dragBridge;

        /// <summary>手牌里的基准位（回弹时的落点）。</summary>
        public Vector2 HomePosition { get { return homePosition; } }

        public float HomeRotation { get { return homeRotation; } }

        public bool IsDragging { get { return dragging; } }

        public Vector2 Size { get { return new Vector2(W, H); } }

        private CardView(CardInstance inst, RectTransform parent, Action<CardView> click)
        {
            Instance = inst;
            onClick = click;

            var def = inst.Def;
            Color series = Theme.SeriesColor(def.Series);

            frame = Ui.Rounded("Card", parent, 18, CardFill(series, true), Theme.WithAlpha(series, 0.85f), 3, true);
            Root = frame.rectTransform;
            Ui.Fixed(Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));

            // 满幅插画：铺满整张卡（按原图比例放大后裁切，绝不拉伸），外面套 RectMask2D 收边
            var artClip = Ui.Node("ArtClip", Root);
            Ui.Fixed(artClip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W - 6f, H - 6f));
            artClip.gameObject.AddComponent<RectMask2D>();

            Sprite realArt = ArtLib.Card(def.Name);
            artImage = Ui.Panel("Art", artClip,
                realArt != null ? realArt : SpriteForge.SeriesArt(256, def.Series, series, def.Id), Color.white);
            artImage.preserveAspect = false;
            if (realArt == null)
            {
                artImage.color = Theme.WithAlpha(series, 0.92f);
            }

            // cover 尺寸：短边铺满，长边溢出被裁
            float innerW = W - 6f;
            float innerH = H - 6f;
            float srcAspect = realArt != null && realArt.rect.height > 0f
                ? realArt.rect.width / realArt.rect.height
                : 1f;
            float coverW = innerW;
            float coverH = innerH;
            if (srcAspect > innerW / innerH)
            {
                coverW = innerH * srcAspect;
            }
            else
            {
                coverH = innerW / srcAspect;
            }

            Ui.Fixed(artImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(coverW, coverH));

            // 顶部系列色条：压在插画最上缘，保留系列识别
            seriesBar = Ui.Panel("SeriesBar", Root, SpriteForge.Panel(9, series, Theme.Lighten(series, 0.3f), 1), Color.white);
            seriesBar.type = Image.Type.Sliced;
            Ui.Fixed(seriesBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(W - 20f, 7f));

            // 底部渐变压暗，保证卡名与描述在亮底插画上也读得清
            var scrim = Ui.Panel("Scrim", Root, SpriteForge.Scrim(32, 256, Theme.Darken(series, 0.9f), 0f, 0.97f), Color.white);
            Ui.Fixed(scrim.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(W - 6f, H * 0.60f));

            divider = Ui.Panel("Divider", Root, SpriteForge.Panel(2, Theme.WithAlpha(series, 0.55f), Color.clear, 0), Color.white);
            Ui.Fixed(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(W - 64f, 2f));

            nameLabel = Ui.Label("Name", Root, def.Name, 26, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(nameLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(W - 20f, 34f));

            descLabel = Ui.Label("Desc", Root, Wrap(def.Text(), series), 15,
                Theme.WithAlpha(Theme.Text, 0.92f), TextAlignmentOptions.Top);
            Ui.Fixed(descLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(W - 26f, 56f));
            descLabel.lineSpacing = 2f;
            // 长描述自动缩字号，避免溢出卡面
            descLabel.enableAutoSizing = true;
            descLabel.fontSizeMin = 10f;
            descLabel.fontSizeMax = 15f;

            // 费用徽章（左上）：六边宝石压在插画上，加深色外圈拉开层次
            costBadge = Ui.Panel("CostBadge", Root, SpriteForge.Gem(72, series, Theme.Darken(Theme.BgDeep, 0.1f)), Color.white);
            Ui.Fixed(costBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(38f, -44f), new Vector2(68f, 68f));

            costLabel = Ui.Label("CostText", costBadge.transform, def.Cost.ToString(), 32, Theme.Lighten(series, 0.45f), TextAlignmentOptions.Center);
            Ui.Stretch(costLabel.rectTransform, 0f, 1f, 0f, 0f);

            button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (interactive && onClick != null)
                {
                    onClick(this);
                }
            });

            Refresh();
        }

        public static CardView Create(RectTransform parent, CardInstance inst, Action<CardView> onClick)
        {
            return inst == null ? null : new CardView(inst, parent, onClick);
        }

        public static CardView Create(RectTransform parent, CardDef def, Action<CardView> onClick)
        {
            return def == null ? null : new CardView(CardInstance.Wrap(def), parent, onClick);
        }

        // ------------------------------------------------------------ 拖动

        /// <summary>接上拖拽桥，让这张牌可以被拖到出牌区释放。</summary>
        public CardView AttachDrag(ICardDragHost host)
        {
            if (Root == null)
            {
                return this;
            }

            dragBridge = Root.gameObject.GetComponent<CardDrag>();
            if (dragBridge == null)
            {
                dragBridge = Root.gameObject.AddComponent<CardDrag>();
            }

            dragBridge.View = this;
            dragBridge.Host = host;
            return this;
        }

        /// <summary>拖起：换到最上层的拖拽层，拉平角度、回到 100% 大小，并点亮边框。</summary>
        public void BeginDragVisual(RectTransform layer, Vector2 anchored)
        {
            if (Root == null)
            {
                return;
            }

            dragging = true;
            if (layer != null && Root.parent != layer)
            {
                Root.SetParent(layer, false);
            }

            Ui.Fixed(Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchored, new Vector2(W, H));
            Root.SetAsLastSibling();

            // 角度拉平、放大到原尺寸——"从手里被拿起来"的感觉全靠这一步
            float rot = Root.localRotation.eulerAngles.z;
            if (rot > 180f)
            {
                rot -= 360f;
            }

            float scale = Root.localScale.x;
            scaleLocked = true;
            Tween.To(0.16f, k =>
            {
                if (Root == null)
                {
                    return;
                }

                Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(rot, 0f, k));
                Root.localScale = Vector3.one * Mathf.Lerp(scale, 1f, k);
            }, Easing.OutCubic);

            RefreshFrame(true);
        }

        /// <summary>跟手：直接写 anchoredPosition（父节点与手牌同尺寸，坐标系可直接复用）。</summary>
        public void DragTo(Vector2 anchored)
        {
            if (Root != null && dragging)
            {
                Root.anchoredPosition = anchored;
            }
        }

        /// <summary>拖到出牌区上方时再放大一点点，表示"松手就生效"。</summary>
        public void SetHot(bool hot)
        {
            if (Root == null || !dragging)
            {
                return;
            }

            Tween.Scale(Root, Root.localScale.x, hot ? 1.06f : 1f, 0.14f, Easing.OutQuad);
        }

        /// <summary>没放进出牌区：按抛物线弹回手牌原位的角度与大小。</summary>
        public void ReturnHome(Action done)
        {
            if (Root == null)
            {
                return;
            }

            dragging = false;
            Vector2 from = Root.anchoredPosition;
            float rotFrom = Root.localRotation.eulerAngles.z;
            if (rotFrom > 180f)
            {
                rotFrom -= 360f;
            }

            float scaleFrom = Root.localScale.x;
            Tween.To(0.26f, k =>
            {
                if (Root == null)
                {
                    return;
                }

                // 位置用回弹缓动、旋转用线性，卡片落回手里时会有一点点"甩"的余韵
                Vector2 pos = Vector2.LerpUnclamped(from, homePosition, k);
                Root.anchoredPosition = new Vector2(pos.x, pos.y + Mathf.Sin(k * Mathf.PI) * 26f);
                Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(rotFrom, homeRotation, k));
                Root.localScale = Vector3.one * Mathf.Lerp(scaleFrom, baseScale, Easing.OutQuad(k));
            }, Easing.OutCubic, () =>
            {
                scaleLocked = false;
                if (Root != null)
                {
                    Root.localRotation = Quaternion.Euler(0f, 0f, homeRotation);
                    Root.localScale = Vector3.one * baseScale;
                }

                RefreshFrame(false);
                done?.Invoke();
            });
        }

        /// <summary>成功出牌：卡片朝目标飞出去并缩小消失。</summary>
        public void FlyOut(Vector2 target, Action done)
        {
            if (Root == null)
            {
                done?.Invoke();
                return;
            }

            dragging = false;
            Vector2 from = Root.anchoredPosition;
            Root.SetAsLastSibling();

            Tween.To(0.30f, k =>
            {
                if (Root == null)
                {
                    return;
                }

                Root.anchoredPosition = Vector2.LerpUnclamped(from, target, Easing.OutCubic(k));
                float s = Mathf.LerpUnclamped(1f, 0.22f, k * k);
                Root.localScale = Vector3.one * s;
                Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 26f, k));
            }, Easing.Linear, () =>
            {
                if (Root != null)
                {
                    Root.localScale = Vector3.one;
                }

                done?.Invoke();
            });
        }

        /// <summary>把牌直接摁回手牌（用于拖拽被取消后的收尾）。</summary>
        public void ResetToHome()
        {
            if (Root == null)
            {
                return;
            }

            dragging = false;
            scaleLocked = false;
            Root.anchoredPosition = homePosition;
            Root.localRotation = Quaternion.Euler(0f, 0f, homeRotation);
            Root.localScale = Vector3.one * baseScale;
            RefreshFrame(false);
        }

        /// <summary>拖动中/悬停时点亮边框，把"这张牌现在是活的"说清楚。</summary>
        private void RefreshFrame(bool drag)
        {
            var def = Instance.Def;
            Color series = Theme.SeriesColor(def.Series);
            Color border = drag || selected ? Theme.Lighten(series, 0.42f) : Theme.WithAlpha(series, affordable ? 0.85f : 0.35f);
            int width = drag || selected ? 4 : 3;
            frame.sprite = SpriteForge.Panel(18, CardFill(series, true), border, width);
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }

        private static Color CardFill(Color series, bool bright)
        {
            Color deep = Theme.Darken(series, 0.78f);
            return bright ? Theme.Lighten(deep, 0.04f) : Theme.Darken(deep, 0.22f);
        }

        /// <summary>把描述里的数字染成系列色，一眼能读出差量。</summary>
        public static string Wrap(string text, Color accent)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string hex = ColorUtility.ToHtmlStringRGB(Theme.Lighten(accent, 0.35f));
            var sb = new StringBuilder(text.Length + 32);
            bool open = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool digit = c >= '0' && c <= '9';
                if (digit && !open)
                {
                    sb.Append("<color=#").Append(hex).Append(">");
                    open = true;
                }
                else if (!digit && open)
                {
                    sb.Append("</color>");
                    open = false;
                }

                sb.Append(c);
            }

            if (open)
            {
                sb.Append("</color>");
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------ 状态

        public CardView SetAffordable(bool value)
        {
            if (affordable != value)
            {
                affordable = value;
                Refresh();
            }

            return this;
        }

        public CardView SetSelected(bool value)
        {
            if (selected != value)
            {
                selected = value;
                Refresh();
            }

            return this;
        }

        public CardView SetDimmed(bool value)
        {
            if (dimmed != value)
            {
                dimmed = value;
                Refresh();
            }

            return this;
        }

        public CardView SetInteractive(bool value)
        {
            interactive = value;
            if (button != null)
            {
                button.interactable = value && !dimmed;
            }

            if (!value && selected)
            {
                SetSelected(false);
            }

            return this;
        }

        public void SetPosition(Vector2 anchored, float rotation, float scale)
        {
            Ui.Fixed(Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchored, new Vector2(W, H));
            Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
            baseScale = scale;
            homePosition = anchored;
            homeRotation = rotation;
            ApplyScale();
        }

        /// <summary>
        /// 手牌重排。animate = true 时从旧位滑到新位（拖走一张牌后其余牌合拢的那种顺滑感），
        /// 需要立即就位时（刷新手牌、新牌飞入）传 false。
        /// </summary>
        public void SetLayout(Vector2 anchored, float rotation, float scale, bool animate)
        {
            if (Root == null)
            {
                SetPosition(anchored, rotation, scale);
                return;
            }

            Vector2 from = Root.anchoredPosition;
            float rotFrom = Root.localRotation.eulerAngles.z;
            if (rotFrom > 180f)
            {
                rotFrom -= 360f;
            }

            float scaleFrom = Root.localScale.x;
            SetPosition(anchored, rotation, scale);

            if (!animate || dragging || (from - anchored).sqrMagnitude < 0.01f)
            {
                return;
            }

            Tween.To(0.2f, k =>
            {
                if (Root == null)
                {
                    return;
                }

                Root.anchoredPosition = Vector2.LerpUnclamped(from, anchored, k);
                Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(rotFrom, rotation, k));
                Root.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scale, k);
            }, Easing.OutCubic);
        }

        /// <summary>基础缩放 × 状态缩放，两处各自生效、互不覆盖。拖动期间由拖动逻辑接管缩放。</summary>
        private void ApplyScale()
        {
            if (Root == null || scaleLocked || dragging)
            {
                return;
            }

            float state = selected ? 1.07f : (dimmed ? 0.96f : 1f);
            Root.localScale = Vector3.one * (baseScale * state);
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        public void Destroy()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        private void Refresh()
        {
            var def = Instance.Def;
            Color series = Theme.SeriesColor(def.Series);

            bool dim = dimmed;
            Color fill = CardFill(series, !dim);
            if (!affordable && !dim)
            {
                fill = Theme.Darken(fill, 0.28f);
            }

            if (selected)
            {
                fill = Theme.Lighten(fill, 0.14f);
            }

            Color border = dim ? Theme.WithAlpha(series, 0.22f) : Theme.WithAlpha(series, affordable ? 0.85f : 0.35f);
            if (selected)
            {
                border = Theme.Lighten(series, 0.42f);
            }

            frame.sprite = SpriteForge.Panel(18, fill, border, selected ? 4 : 3);
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;

            Color gemColor = dim || !affordable ? Theme.Darken(series, 0.45f) : series;
            costBadge.sprite = SpriteForge.Gem(72, gemColor, Theme.Darken(Theme.BgDeep, 0.1f));
            costBadge.color = Color.white;

            // 满幅插画：不可用时整体压暗，而不是变灰块
            artImage.color = dim || !affordable
                ? new Color(0.45f, 0.45f, 0.50f, 1f)
                : Color.white;

            seriesBar.sprite = SpriteForge.Panel(9,
                dim ? Theme.Darken(series, 0.42f) : series, Theme.Lighten(series, 0.3f), 1);
            seriesBar.type = Image.Type.Sliced;
            seriesBar.color = Color.white;

            costLabel.color = dim ? Theme.TextFaint : (affordable ? Theme.Lighten(series, 0.42f) : Theme.Danger);
            nameLabel.color = dim ? Theme.TextFaint : Theme.Text;
            descLabel.color = dim ? Theme.TextFaint : Theme.WithAlpha(Theme.Text, 0.90f);
            divider.color = Theme.WithAlpha(series, dim ? 0.2f : 0.55f);

            int cost = Instance.Cost;
            string costText = cost.ToString();
            if (costLabel.text != costText)
            {
                costLabel.text = costText;
            }

            ApplyScale();
        }
    }
}
