using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Slime.Game
{
    /// <summary>
    /// 轻量补间引擎：无第三方依赖，由 TweenRunner（GameApp 宿主）驱动。
    /// Tween.Scale / LocalMove / GraphicColor / Punch / Shake 覆盖 UI 绝大多数动效需求。
    /// </summary>
    public static class Tween
    {
        private static TweenRunner runner;

        public static void Init(GameObject host)
        {
            if (runner == null && host != null)
            {
                runner = host.AddComponent<TweenRunner>();
            }
        }

        public static Tweener To(float dur, Action<float> tick, Func<float, float> ease = null, Action done = null, float delay = 0f)
        {
            if (runner == null)
            {
                if (delay <= 0f)
                {
                    tick?.Invoke(1f);
                    done?.Invoke();
                }

                return null;
            }

            return runner.Add(dur, ease ?? Easing.OutCubic, tick, done, delay);
        }

        public static Tweener Scale(RectTransform rt, float from, float to, float dur, Func<float, float> ease = null, Action done = null, float delay = 0f)
        {
            var t = To(dur, k => { if (rt != null) { rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, k); } }, ease, done, delay);
            if (t != null) { t.Owner = rt; }
            return t;
        }

        public static Tweener LocalMove(RectTransform rt, Vector2 from, Vector2 to, float dur, Func<float, float> ease = null, Action done = null, float delay = 0f)
        {
            var t = To(dur, k => { if (rt != null) { rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); } }, ease, done, delay);
            if (t != null) { t.Owner = rt; }
            return t;
        }

        /// <summary>取消某个节点上的全部补间（位置/缩放/颜色），常用于用户操作打断动画。</summary>
        public static void Kill(RectTransform rt)
        {
            if (rt != null && runner != null)
            {
                runner.Kill(rt);
            }
        }

        public static Tweener GraphicColor(Graphic g, Color to, float dur, Func<float, float> ease = null, Action done = null, float delay = 0f)
        {
            if (g == null)
            {
                return null;
            }

            Color from = g.color;
            return To(dur, k => { if (g != null) { g.color = Color.LerpUnclamped(from, to, k); } }, ease, done, delay);
        }

        /// <summary>快速放大再弹回，用于按钮/徽章的确认反馈。</summary>
        public static void Punch(RectTransform rt, float strength = 0.14f, float dur = 0.26f, float delay = 0f)
        {
            if (rt == null || runner == null)
            {
                return;
            }

            float baseScale = rt.localScale.x;
            To(dur, k => { if (rt != null) { rt.localScale = Vector3.one * (baseScale * (1f + Mathf.Sin(k * Mathf.PI) * strength)); } }, Easing.Linear, null, delay);
        }

        /// <summary>位置抖动（受击反馈）。结束后恢复原位。</summary>
        public static void Shake(RectTransform rt, float strength = 9f, float dur = 0.32f, int vibrato = 16)
        {
            if (rt == null || runner == null)
            {
                return;
            }

            Vector2 origin = rt.anchoredPosition;
            To(dur, k =>
            {
                if (rt == null)
                {
                    return;
                }

                float decay = 1f - k;
                float x = Mathf.Sin(k * vibrato * Mathf.PI * 2f) * strength * decay;
                rt.anchoredPosition = origin + new Vector2(x, 0f);
            }, Easing.Linear, () => { if (rt != null) { rt.anchoredPosition = origin; } });
        }

        // ------------------------------------------------------------ 入场动效

        /// <summary>缩放入场：从 0.72 倍弹到 1 倍，可选延迟。返回用的补间。</summary>
        public static void PopIn(RectTransform rt, float delay = 0f, float from = 0.72f, float dur = 0.34f)
        {
            if (rt == null)
            {
                return;
            }

            if (runner == null)
            {
                rt.localScale = Vector3.one;
                return;
            }

            rt.localScale = Vector3.one * from;
            To(dur, k => { if (rt != null) { rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, k); } }, Easing.OutBack, null, delay);
        }

        /// <summary>
        /// 缩放入场，但保留节点自身的基础缩放（卡牌在手里是 0.9 倍，不能弹回 1.0）。
        /// </summary>
        public static void PopInScaled(RectTransform rt, float target, float delay = 0f, float from = 0.72f)
        {
            if (rt == null)
            {
                return;
            }

            if (runner == null)
            {
                rt.localScale = Vector3.one * target;
                return;
            }

            float start = target * from;
            To(0.34f, k => { if (rt != null) { rt.localScale = Vector3.one * Mathf.LerpUnclamped(start, target, k); } }, Easing.OutBack, null, delay);
        }

        /// <summary>淡入：透明度 from → to，同样支持延迟，方便做交错（stagger）。</summary>
        public static void FadeIn(Graphic g, float dur = 0.28f, float delay = 0f, float from = 0f, float to = 1f)
        {
            if (g == null)
            {
                return;
            }

            if (runner == null)
            {
                var c0 = g.color;
                c0.a = to;
                g.color = c0;
                return;
            }

            var baseColor = g.color;
            baseColor.a = from;
            g.color = baseColor;
            To(dur, k =>
            {
                if (g == null)
                {
                    return;
                }

                var c = g.color;
                c.a = Mathf.LerpUnclamped(from, to, k);
                g.color = c;
            }, Easing.OutQuad, null, delay);
        }

        /// <summary>
        /// 从偏移位滑入到当前 anchoredPosition。用于列表项 / 面板的行进式入场。
        /// </summary>
        public static void SlideIn(RectTransform rt, Vector2 offset, float dur = 0.34f, float delay = 0f)
        {
            if (rt == null || runner == null)
            {
                return;
            }

            Vector2 home = rt.anchoredPosition;
            rt.anchoredPosition = home + offset;
            To(dur, k => { if (rt != null) { rt.anchoredPosition = Vector2.LerpUnclamped(home + offset, home, k); } }, Easing.OutCubic, null, delay);
        }
    }

    /// <summary>单条补间实例。</summary>
    public sealed class Tweener
    {
        public float Age;
        public float Dur;
        public float Delay;
        public Func<float, float> Ease;
        public Action<float> Tick;
        public Action Done;
        public bool Dead;

        /// <summary>这条补间在驱动哪个节点（用于按目标取消，比如拖拽打断飞入动画）。</summary>
        public RectTransform Owner;
    }

    /// <summary>补间驱动器，挂在 GameApp 的 GameObject 上。</summary>
    public sealed class TweenRunner : MonoBehaviour
    {
        private readonly List<Tweener> live = new List<Tweener>(32);

        public Tweener Add(float dur, Func<float, float> ease, Action<float> tick, Action done, float delay = 0f)
        {
            var t = new Tweener
            {
                Age = 0f,
                Dur = Mathf.Max(0.01f, dur),
                Delay = Mathf.Max(0f, delay),
                Ease = ease,
                Tick = tick,
                Done = done
            };

            live.Add(t);
            return t;
        }

        /// <summary>取消指定节点上所有未完成的补间（不触发 Done），拖拽开始时用它打断飞入动画。</summary>
        public void Kill(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].Owner == rt)
                {
                    live.RemoveAt(i);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var t = live[i];

                // 延迟阶段只消耗时间、不写值——这样"错峰入场"不会让对象先闪到起点。
                if (t.Delay > 0f)
                {
                    t.Delay -= dt;
                    if (t.Delay > 0f)
                    {
                        continue;
                    }

                    t.Delay = 0f;
                }

                t.Age += dt;
                float k = Mathf.Clamp01(t.Age / t.Dur);
                float e = t.Ease != null ? t.Ease(k) : k;

                try
                {
                    t.Tick?.Invoke(e);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tween] tick error: " + ex.Message);
                    t.Dead = true;
                }

                if (k >= 1f)
                {
                    t.Dead = true;
                    t.Done?.Invoke();
                }

                if (t.Dead)
                {
                    live.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>常用缓动。</summary>
    public static class Easing
    {
        public static float Linear(float k) { return k; }
        public static float OutCubic(float k) { float f = k - 1f; return f * f * f + 1f; }
        public static float OutQuad(float k) { return 1f - (1f - k) * (1f - k); }

        public static float OutBack(float k)
        {
            const float c = 1.70158f;
            float f = k - 1f;
            return f * f * ((c + 1f) * f + c) + 1f;
        }
    }
}
