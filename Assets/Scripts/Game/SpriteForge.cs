using System.Collections.Generic;
using Slime.Core;
using UnityEngine;

namespace Slime.Game
{
    /// <summary>史莱姆表情。表情参与缓存键，同一角色可有多种状态。</summary>
    public enum SlimeFace
    {
        Neutral = 0,
        Happy = 1,
        Worried = 2,
        Fierce = 3
    }

    /// <summary>
    /// 程序化美术工厂。整套视觉资产在运行时按令牌生成，无外部贴图依赖，
    /// 风格可复现、可参数化、随主题改动自动整体统一。
    /// </summary>
    public static class SpriteForge
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static void ClearCache()
        {
            cache.Clear();
        }

        #region 画布

        private struct Canvas
        {
            public int W;
            public int H;
            public Color32[] Pixels;

            public static Canvas Create(int w, int h)
            {
                var c = new Canvas();
                c.W = w;
                c.H = h;
                c.Pixels = new Color32[w * h];
                return c;
            }

            public void Blend(int x, int y, Color color, float alpha)
            {
                if (x < 0 || y < 0 || x >= W || y >= H)
                {
                    return;
                }

                // 覆盖度 = 几何覆盖率 × 颜色自身不透明度。
                // 少了后者的话，任何 a=0 的填充都会画成不透明黑块。
                float src = alpha * color.a;
                if (src <= 0f)
                {
                    return;
                }

                if (src > 1f)
                {
                    src = 1f;
                }

                int i = y * W + x;
                Color32 dst = Pixels[i];
                float da = dst.a / 255f;

                float outA = src + da * (1f - src);
                if (outA <= 0.0001f)
                {
                    Pixels[i] = new Color32(0, 0, 0, 0);
                    return;
                }

                float r = (color.r * src + (dst.r / 255f) * da * (1f - src)) / outA;
                float g = (color.g * src + (dst.g / 255f) * da * (1f - src)) / outA;
                float b = (color.b * src + (dst.b / 255f) * da * (1f - src)) / outA;

                Pixels[i] = new Color32(
                    (byte)Mathf.Clamp(r * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(g * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(b * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(outA * 255f, 0f, 255f));
            }

            /// <summary>挖空：按 alpha 削减已有像素的不透明度（用于咬缺、镂空）。</summary>
            public void Erase(int x, int y, float alpha)
            {
                if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0f)
                {
                    return;
                }

                if (alpha > 1f)
                {
                    alpha = 1f;
                }

                int i = y * W + x;
                Color32 dst = Pixels[i];
                float da = (dst.a / 255f) * (1f - alpha);
                Pixels[i] = new Color32(dst.r, dst.g, dst.b, (byte)Mathf.Clamp(da * 255f, 0f, 255f));
            }

            public Texture2D ToTexture()
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(Pixels);
                tex.Apply();
                return tex;
            }
        }

        private static float RoundedRectSdf(float px, float py, float halfW, float halfH, float radius)
        {
            float r = Mathf.Min(radius, Mathf.Min(halfW, halfH));
            float qx = Mathf.Abs(px) - (halfW - r);
            float qy = Mathf.Abs(py) - (halfH - r);
            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Sprite Build(string key, Canvas canvas, Vector4 border, float pixelsPerUnit)
        {
            var tex = canvas.ToTexture();
            var rect = new Rect(0f, 0f, canvas.W, canvas.H);
            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        #endregion

        #region 系列插画

        /// <summary>
        /// 程序化系列插画。卡面中央的"美术"就是它——八条系列各有独立造型，
        /// 而不是一个通用圆环换色。
        /// </summary>
        public static Sprite SeriesArt(int size, CardSeries series, Color color, int seed)
        {
            size = Mathf.Clamp(size, 64, 320);
            string key = "art_" + size + "_" + (int)series + "_" + ColorKey(color) + "_" + seed;
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float c = size * 0.5f;
            Color bright = Theme.Lighten(color, 0.48f);
            Color deep = Theme.Darken(color, 0.55f);
            float maxW = size * 0.30f;

            // 底层环境光：让插画"发亮"，而不是贴在黑底上
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c;
                    float dy = (y + 0.5f - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < 1f)
                    {
                        canvas.Blend(x, y, color, Mathf.Pow(1f - d, 2.6f) * 0.5f);
                    }
                }
            }

            switch (series)
            {
                case CardSeries.Starter:
                    // 初始：交叉双刃 + 中心菱形
                    DrawBlade(canvas, c, c, maxW * 1.35f, deep, color, -1);
                    DrawBlade(canvas, c, c, maxW * 1.35f, deep, color, 1);
                    DrawPoly(canvas,
                        new[] { c, c + maxW * 0.30f, c, c - maxW * 0.30f },
                        new[] { c + maxW * 0.46f, c, c - maxW * 0.46f, c }, bright);
                    break;

                case CardSeries.Sin:
                    // 七罪：断裂的锁环 + 四枚尖角
                    DrawRing(canvas, c, c, maxW * 1.10f, maxW * 0.74f, deep);
                    DrawRing(canvas, c, c, maxW * 1.02f, maxW * 0.82f, color);
                    EraseDisc(canvas, c + maxW * 1.34f, c - maxW * 0.10f, maxW * 0.52f);
                    EraseDisc(canvas, c - maxW * 1.34f, c + maxW * 0.10f, maxW * 0.40f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = Mathf.PI * 0.25f + Mathf.PI * 0.5f * i;
                        DrawHorn(canvas, c + Mathf.Cos(a) * maxW * 1.16f, c + Mathf.Sin(a) * maxW * 1.16f,
                            maxW * 0.34f, a, bright);
                    }

                    DrawDisc(canvas, c, c, maxW * 0.20f, bright, 0f);
                    break;

                case CardSeries.Blood:
                {
                    // 血族：倒置血滴 + 双牙
                    float apex = c - size * 0.50f;
                    float cy = c + size * 0.06f;
                    float r = maxW * 0.86f;
                    FillShape(canvas, c, cy + r, apex, t =>
                    {
                        float y = Mathf.Lerp(cy + r, apex, t);
                        float hw = 0f;
                        float dy = y - cy;
                        if (Mathf.Abs(dy) < r)
                        {
                            hw = Mathf.Sqrt(r * r - dy * dy);
                        }

                        if (y < cy + r - r * 0.25f)
                        {
                            float k = Mathf.Clamp01((y - apex) / Mathf.Max(1f, (cy + r - r * 0.25f) - apex));
                            hw = Mathf.Max(hw, maxW * 0.60f * Mathf.Pow(k, 0.72f));
                        }

                        return hw;
                    }, deep, color, 0.72f);
                    DrawDisc(canvas, c - r * 0.32f, cy + r * 0.18f, r * 0.22f, bright, 0f);

                    // 双牙
                    DrawPoly(canvas, new[] { c - maxW * 0.52f, c - maxW * 0.24f, c - maxW * 0.38f },
                        new[] { c - maxW * 1.02f, c - maxW * 1.02f, c - maxW * 1.38f }, bright);
                    DrawPoly(canvas, new[] { c + maxW * 0.24f, c + maxW * 0.52f, c + maxW * 0.38f },
                        new[] { c - maxW * 1.02f, c - maxW * 1.02f, c - maxW * 1.38f }, bright);
                    break;
                }

                case CardSeries.Fortify:
                {
                    // 坚固：盾牌 + 十字铆钉
                    float[] px = { c - maxW, c + maxW, c + maxW, c, c - maxW };
                    float[] py = { c + maxW * 0.85f, c + maxW * 0.85f, c - maxW * 0.10f, c - maxW * 1.30f, c - maxW * 0.10f };
                    DrawPoly(canvas, px, py, deep);
                    float[] px2 = { c - maxW * 0.78f, c + maxW * 0.78f, c + maxW * 0.78f, c, c - maxW * 0.78f };
                    float[] py2 = { c + maxW * 0.62f, c + maxW * 0.62f, c - maxW * 0.06f, c - maxW * 1.02f, c - maxW * 0.06f };
                    DrawPoly(canvas, px2, py2, color);
                    DrawPoly(canvas, new[] { c - maxW * 0.13f, c + maxW * 0.13f, c + maxW * 0.13f, c - maxW * 0.13f },
                        new[] { c + maxW * 0.42f, c + maxW * 0.42f, c - maxW * 0.40f, c - maxW * 0.40f }, bright);
                    DrawPoly(canvas, new[] { c - maxW * 0.46f, c + maxW * 0.46f, c + maxW * 0.46f, c - maxW * 0.46f },
                        new[] { c + maxW * 0.16f, c + maxW * 0.16f, c - maxW * 0.10f, c - maxW * 0.10f }, bright);
                    break;
                }

                case CardSeries.Tech:
                {
                    // 科技：外环电路 + 中心节点 + 四条引线
                    DrawRing(canvas, c, c, maxW * 1.16f, maxW * 0.94f, deep);
                    DrawRing(canvas, c, c, maxW * 0.46f, maxW * 0.30f, color);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = Mathf.PI * 0.5f * i + Mathf.PI * 0.25f;
                        float x0 = c + Mathf.Cos(a) * maxW * 0.50f;
                        float y0 = c + Mathf.Sin(a) * maxW * 0.50f;
                        float x1 = c + Mathf.Cos(a) * maxW * 1.04f;
                        float y1 = c + Mathf.Sin(a) * maxW * 1.04f;
                        DrawLine(canvas, x0, y0, x1, y1, maxW * 0.10f, color);
                        DrawDisc(canvas, x1, y1, maxW * 0.17f, bright, 0f);
                    }

                    DrawPoly(canvas,
                        new[] { c, c + maxW * 0.36f, c, c - maxW * 0.36f },
                        new[] { c + maxW * 0.36f, c, c - maxW * 0.36f, c }, bright);
                    break;
                }

                case CardSeries.Seed:
                {
                    // 种子：骰面（方体 + 点数）+ 一片嫩芽
                    float s = maxW * 0.92f;
                    DrawPoly(canvas,
                        new[] { c - s, c + s, c + s, c - s },
                        new[] { c - s, c - s, c + s, c + s }, deep);
                    float[][] pips =
                    {
                        new[] { -0.52f, 0.52f }, new[] { 0f, 0f }, new[] { 0.52f, -0.52f }
                    };
                    for (int i = 0; i < pips.Length; i++)
                    {
                        DrawDisc(canvas, c + pips[i][0] * s, c + pips[i][1] * s, s * 0.17f, bright, 0f);
                    }

                    // 嫩芽从骰顶钻出
                    DrawLine(canvas, c, c + s, c + maxW * 0.10f, c + maxW * 1.40f, maxW * 0.08f, color);
                    FillShape(canvas, c + maxW * 0.34f, c + maxW * 1.34f, c + maxW * 0.78f,
                        t => maxW * 0.30f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f) + 0.4f,
                        color, bright, 0.6f);
                    break;
                }

                case CardSeries.Shadow:
                {
                    // 暗影：新月 + 一只眼
                    DrawDisc(canvas, c, c, maxW * 1.10f, deep, 0f);
                    EraseDisc(canvas, c + maxW * 0.62f, c + maxW * 0.28f, maxW * 0.94f);
                    DrawDisc(canvas, c - maxW * 0.16f, c + maxW * 0.18f, maxW * 0.74f, color, 0f);
                    EraseDisc(canvas, c + maxW * 0.10f, c + maxW * 0.26f, maxW * 0.52f);
                    DrawPoly(canvas,
                        new[] { c - maxW * 0.62f, c + maxW * 0.24f, c - maxW * 0.30f },
                        new[] { c + maxW * 0.06f, c + maxW * 0.06f, c + maxW * 0.02f }, Theme.BgDeep);
                    DrawDisc(canvas, c - maxW * 0.34f, c + maxW * 0.04f, maxW * 0.16f, bright, 0f);
                    break;
                }

                default:
                {
                    // 时序：沙漏
                    DrawPoly(canvas,
                        new[] { c - maxW, c + maxW, c + maxW * 0.22f, c - maxW * 0.22f },
                        new[] { c + maxW * 1.20f, c + maxW * 1.20f, c + maxW * 0.10f, c + maxW * 0.10f }, deep);
                    DrawPoly(canvas,
                        new[] { c - maxW, c + maxW, c + maxW * 0.22f, c - maxW * 0.22f },
                        new[] { c - maxW * 1.20f, c - maxW * 1.20f, c - maxW * 0.10f, c - maxW * 0.10f }, deep);

                    // 上部流沙 + 下部堆积
                    for (float y = c + maxW * 0.16f; y < c + maxW * 1.16f; y += 1f)
                    {
                        float k = (y - (c + maxW * 0.16f)) / (maxW * 1.0f);
                        float hw = maxW * 0.86f * (1f - k);
                        DrawPoly(canvas, new[] { c - hw, c + hw, c + hw, c - hw },
                            new[] { y, y, y + 1f, y + 1f }, Theme.WithAlpha(color, 0.85f));
                    }

                    for (float y = c - maxW * 1.14f; y < c - maxW * 0.12f; y += 1f)
                    {
                        float k = (y - (c - maxW * 1.14f)) / (maxW * 1.0f);
                        float hw = maxW * 0.86f * Mathf.Pow(k, 0.72f);
                        DrawPoly(canvas, new[] { c - hw, c + hw, c + hw, c - hw },
                            new[] { y, y, y + 1f, y + 1f }, Theme.WithAlpha(color, 0.9f));
                    }

                    DrawLine(canvas, c, c + maxW * 0.30f, c, c - maxW * 0.24f, maxW * 0.07f, bright);
                    DrawPoly(canvas, new[] { c - maxW * 1.26f, c + maxW * 1.26f, c + maxW * 1.26f, c - maxW * 1.26f },
                        new[] { c + maxW * 1.20f, c + maxW * 1.20f, c + maxW * 1.40f, c + maxW * 1.40f }, bright);
                    DrawPoly(canvas, new[] { c - maxW * 1.26f, c + maxW * 1.26f, c + maxW * 1.26f, c - maxW * 1.26f },
                        new[] { c - maxW * 1.40f, c - maxW * 1.40f, c - maxW * 1.20f, c - maxW * 1.20f }, bright);
                    break;
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>细长刀身（初始系列的交叉双刃）。</summary>
        private static void DrawBlade(Canvas canvas, float cx, float cy, float len, Color outer, Color inner, int dir)
        {
            float ang = dir * Mathf.PI * 0.26f;
            float dx = Mathf.Cos(ang + Mathf.PI * 0.5f);
            float dy = Mathf.Sin(ang + Mathf.PI * 0.5f);
            float nx = -dy;
            float ny = dx;

            for (float t = -len; t <= len; t += 1f)
            {
                float hw = Mathf.Lerp(len * 0.055f, 0f, Mathf.Abs(t) / len) + len * 0.055f;
                float bx = cx + dx * t;
                float by = cy + dy * t;
                Color col = Mathf.Abs(t) < len * 0.10f ? inner : outer;
                DrawLine(canvas, bx - nx * hw, by - ny * hw, bx + nx * hw, by + ny * hw, 1f, col);
            }

            DrawLine(canvas, cx - nx * len * 0.30f, cy - ny * len * 0.30f,
                cx + nx * len * 0.30f, cy + ny * len * 0.30f, len * 0.14f, inner);
        }

        private static void DrawHorn(Canvas canvas, float cx, float cy, float len, float angle, Color color)
        {
            float dx = Mathf.Cos(angle);
            float dy = Mathf.Sin(angle);
            for (float t = 0f; t <= len; t += 0.8f)
            {
                float k = 1f - t / len;
                DrawDisc(canvas, cx + dx * t - dy * k * len * 0.34f, cy + dy * t + dx * k * len * 0.34f,
                    Mathf.Max(0.9f, len * 0.14f * k), color, 0f);
            }
        }

        private static void DrawLine(Canvas canvas, float x0, float y0, float x1, float y1, float width, Color color)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            int steps = Mathf.Max(1, (int)(len * 2f));
            float half = Mathf.Max(0.5f, width * 0.5f);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float px = x0 + dx * t;
                float py = y0 + dy * t;
                DrawPoly(canvas,
                    new[] { px - half, px + half, px + half, px - half },
                    new[] { py - half, py - half, py + half, py + half }, color);
            }
        }

        /// <summary>按"每行半宽"轮廓填充形状，边到边做内芯渐变。</summary>
        private static void FillShape(Canvas canvas, float cx, float yStart, float yEnd, System.Func<float, float> halfWidth, Color outer, Color inner, float innerScale)
        {
            int a = Mathf.FloorToInt(Mathf.Min(yStart, yEnd));
            int b = Mathf.CeilToInt(Mathf.Max(yStart, yEnd));
            float span = Mathf.Max(1f, Mathf.Abs(yEnd - yStart));

            for (int y = a; y <= b; y++)
            {
                float t = Mathf.Clamp01((y - yStart) / (yEnd - yStart < 0f ? -span : span));
                float hw = halfWidth(t);
                if (hw <= 0.5f)
                {
                    continue;
                }

                for (int x = Mathf.FloorToInt(cx - hw - 1f); x <= Mathf.CeilToInt(cx + hw + 1f); x++)
                {
                    float edge = Mathf.Abs(x + 0.5f - cx) - hw;
                    float cov = Mathf.Clamp01(0.5f - edge);
                    if (cov <= 0f)
                    {
                        continue;
                    }

                    float u = Mathf.Abs(x + 0.5f - cx) / hw;
                    Color col = (innerScale > 0f && u < innerScale)
                        ? Color.Lerp(inner, outer, Mathf.Pow(u / innerScale, 1.4f))
                        : outer;
                    canvas.Blend(x, y, col, cov);
                }
            }
        }

        #endregion

        #region 系列纹章

        /// <summary>系列纹章：比插画更简化的单色符号，用于标签、图鉴分组与卡背。</summary>
        public static Sprite SeriesGlyph(int size, Color color, CardSeries series)
        {
            size = Mathf.Clamp(size, 40, 256);
            string key = "glyph_" + size + "_" + ColorKey(color) + "_" + (int)series;
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float c = size * 0.5f;
            float r = size * 0.30f;

            switch (series)
            {
                case CardSeries.Starter:
                    // 简化的交叉刃
                    DrawLine(canvas, c - r * 0.82f, c - r * 0.82f, c + r * 0.82f, c + r * 0.82f, r * 0.20f, color);
                    DrawLine(canvas, c + r * 0.82f, c - r * 0.82f, c - r * 0.82f, c + r * 0.82f, r * 0.20f, color);
                    DrawDisc(canvas, c, c, r * 0.26f, color, 0f);
                    break;

                case CardSeries.Sin:
                    DrawRing(canvas, c, c, r * 1.02f, r * 0.60f, color);
                    EraseDisc(canvas, c + r * 1.18f, c, r * 0.48f);
                    DrawHorn(canvas, c + r * 0.20f, c + r * 0.96f, r * 0.56f, Mathf.PI * 0.5f, color);
                    break;

                case CardSeries.Blood:
                    FillShape(canvas, c, c + r * 0.96f, c - r * 1.30f,
                        t => r * 0.72f * Mathf.Pow(Mathf.Sin(Mathf.PI * t * 0.5f + 0.35f), 0.8f),
                        color, Theme.Lighten(color, 0.5f), 0.5f);
                    break;

                case CardSeries.Fortify:
                    DrawPoly(canvas,
                        new[] { c - r, c + r, c + r, c, c - r },
                        new[] { c + r * 0.82f, c + r * 0.82f, c - r * 0.10f, c - r * 1.24f, c - r * 0.10f }, color);
                    DrawPoly(canvas,
                        new[] { c - r * 0.20f, c + r * 0.20f, c + r * 0.20f, c - r * 0.20f },
                        new[] { c + r * 0.42f, c + r * 0.42f, c - r * 0.62f, c - r * 0.62f }, Theme.BgDeep);
                    break;

                case CardSeries.Tech:
                    DrawRing(canvas, c, c, r * 1.10f, r * 0.84f, color);
                    DrawRing(canvas, c, c, r * 0.40f, r * 0.18f, color);
                    DrawLine(canvas, c, c + r * 0.40f, c, c + r * 1.06f, r * 0.14f, color);
                    DrawLine(canvas, c, c - r * 0.40f, c, c - r * 1.06f, r * 0.14f, color);
                    break;

                case CardSeries.Seed:
                    DrawPoly(canvas,
                        new[] { c - r * 0.94f, c + r * 0.94f, c + r * 0.94f, c - r * 0.94f },
                        new[] { c - r * 0.94f, c - r * 0.94f, c + r * 0.94f, c + r * 0.94f }, color);
                    DrawDisc(canvas, c - r * 0.40f, c + r * 0.40f, r * 0.17f, Theme.BgDeep, 0f);
                    DrawDisc(canvas, c, c, r * 0.17f, Theme.BgDeep, 0f);
                    DrawDisc(canvas, c + r * 0.40f, c - r * 0.40f, r * 0.17f, Theme.BgDeep, 0f);
                    break;

                case CardSeries.Shadow:
                    DrawDisc(canvas, c, c, r * 1.06f, color, 0f);
                    EraseDisc(canvas, c + r * 0.60f, c + r * 0.26f, r * 0.92f);
                    DrawDisc(canvas, c - r * 0.22f, c + r * 0.16f, r * 0.22f, Theme.Lighten(color, 0.6f), 0f);
                    break;

                default:
                    DrawPoly(canvas,
                        new[] { c - r * 0.94f, c + r * 0.94f, c + r * 0.94f, c - r * 0.94f },
                        new[] { c + r * 1.16f, c + r * 1.16f, c + r * 0.42f, c + r * 0.42f }, color);
                    DrawPoly(canvas,
                        new[] { c - r * 0.94f, c + r * 0.94f, c + r * 0.94f, c - r * 0.94f },
                        new[] { c - r * 1.16f, c - r * 1.16f, c - r * 0.42f, c - r * 0.42f }, color);
                    DrawLine(canvas, c, c + r * 0.56f, c, c - r * 0.56f, r * 0.14f, color);
                    break;
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>挖一个圆孔（用于纹章镂空）。</summary>
        private static void EraseDisc(Canvas canvas, float cx, float cy, float r)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1);
            int x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1);
            int y1 = Mathf.CeilToInt(cy + r + 1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a > 0f)
                    {
                        canvas.Erase(x, y, a);
                    }
                }
            }
        }

        private static void DrawRing(Canvas canvas, float cx, float cy, float outer, float inner, Color color)
        {
            int x0 = Mathf.FloorToInt(cx - outer - 1);
            int x1 = Mathf.CeilToInt(cx + outer + 1);
            int y0 = Mathf.FloorToInt(cy - outer - 1);
            int y1 = Mathf.CeilToInt(cy + outer + 1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f);
                    if (a > 0f)
                    {
                        canvas.Blend(x, y, color, a);
                    }
                }
            }
        }

        private static void DrawStar(Canvas canvas, float cx, float cy, float outer, float inner, int points, float rotation, Color color)
        {
            int n = points * 2;
            var px = new float[n];
            var py = new float[n];

            for (int i = 0; i < n; i++)
            {
                float radius = (i % 2 == 0) ? outer : inner;
                float a = rotation + Mathf.PI * 2f * i / n;
                px[i] = cx + Mathf.Cos(a) * radius;
                py[i] = cy + Mathf.Sin(a) * radius;
            }

            DrawPoly(canvas, px, py, color);
        }

        /// <summary>简单多边形填充（3×3 超采样抗锯齿）。</summary>
        private static void DrawPoly(Canvas canvas, float[] xs, float[] ys, Color color)
        {
            if (xs == null || ys == null || xs.Length < 3 || xs.Length != ys.Length)
            {
                return;
            }

            float minX = xs[0];
            float maxX = xs[0];
            float minY = ys[0];
            float maxY = ys[0];

            for (int i = 1; i < xs.Length; i++)
            {
                minX = Mathf.Min(minX, xs[i]);
                maxX = Mathf.Max(maxX, xs[i]);
                minY = Mathf.Min(minY, ys[i]);
                maxY = Mathf.Max(maxY, ys[i]);
            }

            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(canvas.W - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(canvas.H - 1, Mathf.CeilToInt(maxY));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float px = x + (sx + 0.5f) / 3f;
                            float py = y + (sy + 0.5f) / 3f;
                            if (PointInPoly(px, py, xs, ys))
                            {
                                hits++;
                            }
                        }
                    }

                    if (hits > 0)
                    {
                        canvas.Blend(x, y, color, hits / 9f);
                    }
                }
            }
        }

        private static bool PointInPoly(float px, float py, float[] xs, float[] ys)
        {
            bool inside = false;
            int n = xs.Length;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((ys[i] > py) != (ys[j] > py)) &&
                    (px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        #endregion

        #region 通用形状

        /// <summary>圆角矩形（可九宫格拉伸）。radius 同时作为九宫格边距。</summary>
        public static Sprite RoundedRect(int w, int h, int radius, Color fill, Color border, int borderWidth, bool topLight = true)
        {
            string key = string.Format("rr_{0}_{1}_{2}_{3}_{4}_{5}_{6}", w, h, radius, ColorKey(fill), ColorKey(border), borderWidth, topLight ? 1 : 0);
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(w, h);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f - halfH;
                float v = 1f - (y + 0.5f) / h;

                Color body = fill;
                if (topLight)
                {
                    body = Color.Lerp(Theme.Darken(fill, 0.10f), Theme.Lighten(fill, 0.07f), v);
                }

                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - halfW;
                    float d = RoundedRectSdf(px, py, halfW, halfH, radius);

                    float aa = Mathf.Clamp(0.5f - d, 0f, 1f);
                    if (aa <= 0f)
                    {
                        continue;
                    }

                    if (borderWidth > 0 && d > -borderWidth)
                    {
                        canvas.Blend(x, y, border, aa);
                    }
                    else
                    {
                        Color pixel = body;
                        if (topLight)
                        {
                            // 压花：顶部内高光 + 底部内阴影 + 左右轻微收暗，
                            // 让同一块面板有"受光面"而不是平涂色块。
                            float topEdge = halfH - py;
                            float bottomEdge = py + halfH;
                            float hi = Mathf.Clamp01(1f - topEdge / 2.6f);
                            pixel = Color.Lerp(pixel, Theme.Lighten(pixel, 0.16f), hi * 0.9f);
                            float lo = Mathf.Clamp01(1f - bottomEdge / 3.2f);
                            pixel = Color.Lerp(pixel, Theme.Darken(pixel, 0.22f), lo * 0.85f);

                            float side = Mathf.Clamp01((halfW - Mathf.Abs(px)) / 5f);
                            pixel = Color.Lerp(Theme.Darken(pixel, 0.10f), pixel, side);
                        }

                        canvas.Blend(x, y, pixel, aa);
                    }
                }
            }

            Vector4 nine = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            return Build(key, canvas, nine, 100f);
        }

        public static Sprite Panel(int radius, Color fill, Color border, int borderWidth)
        {
            int size = Mathf.Max(radius * 3, 48);
            return RoundedRect(size, size, radius, fill, border, borderWidth, true);
        }

        public static Sprite Circle(int diameter, Color fill, Color border, int borderWidth)
        {
            return RoundedRect(diameter, diameter, diameter / 2, fill, border, borderWidth, true);
        }

        public static Sprite Solid()
        {
            const string key = "solid";
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(4, 4);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    canvas.Blend(x, y, Color.white, 1f);
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>竖向渐变：默认顶部透明、底部压暗，用作满幅插画上的文字托底。</summary>
        public static Sprite Scrim(int w, int h, Color color, float topAlpha, float bottomAlpha)
        {
            w = Mathf.Max(8, w);
            h = Mathf.Max(8, h);
            int ta = Mathf.RoundToInt(Mathf.Clamp01(topAlpha) * 100f);
            int ba = Mathf.RoundToInt(Mathf.Clamp01(bottomAlpha) * 100f);
            string key = "scrim_" + w + "x" + h + "_" + ColorKey(color) + "_" + ta + "_" + ba;
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(w, h);
            for (int y = 0; y < h; y++)
            {
                // 纹理 y=0 在底部
                float t = h <= 1 ? 0f : y / (float)(h - 1);
                float a = Mathf.Lerp(bottomAlpha, topAlpha, t * t);
                for (int x = 0; x < w; x++)
                {
                    canvas.Blend(x, y, color, a);
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>径向柔光，用于爆发特效与高亮。边缘平滑过渡，不会出现硬圈。</summary>
        public static Sprite Glow(int size, Color color)
        {
            size = Mathf.Max(64, size);
            string key = "glow_" + size + "_" + ColorKey(color);
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= 1f)
                    {
                        continue;
                    }

                    // 内核实、外缘极柔：三次方衰减 + 中心轻微提亮
                    float a = Mathf.Pow(1f - dist, 3.2f);
                    a += Mathf.Pow(Mathf.Clamp01(1f - dist * 3.2f), 2f) * 0.35f;
                    canvas.Blend(x, y, color, Mathf.Clamp01(a));
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>全屏暗角：四周压暗、中心透光，用来聚拢视线、去掉"平铺网页"感。</summary>
        public static Sprite Vignette(int size, Color color)
        {
            size = Mathf.Max(128, size);
            string key = "vig_" + size + "_" + ColorKey(color);
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 0.52f)
                    {
                        continue;
                    }

                    float a = Mathf.Pow(Mathf.Clamp01((d - 0.52f) / 0.48f), 1.7f) * 0.9f;
                    canvas.Blend(x, y, color, a);
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>
        /// 条状填充（血条/进度条）：顶部 40% 打一层柔光、底部压暗，
        /// 比平涂圆角多一层"液体"质感。可九宫格横向拉伸。
        /// </summary>
        public static Sprite Bar(int w, int h, int radius, Color fill)
        {
            string key = string.Format("bar_{0}_{1}_{2}_{3}", w, h, radius, ColorKey(fill));
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(w, h);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f - halfH;
                float v = 1f - (y + 0.5f) / h;

                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - halfW;
                    float d = RoundedRectSdf(px, py, halfW, halfH, radius);
                    float aa = Mathf.Clamp(0.5f - d, 0f, 1f);
                    if (aa <= 0f)
                    {
                        continue;
                    }

                    Color body = Color.Lerp(Theme.Darken(fill, 0.30f), Theme.Lighten(fill, 0.14f), v);

                    // 顶部柔光带
                    float gloss = Mathf.Clamp01(1f - (halfH - py) / (h * 0.42f)) * 0.30f;
                    if (gloss > 0f)
                    {
                        body = Color.Lerp(body, Color.white, gloss);
                    }

                    canvas.Blend(x, y, body, aa);
                }
            }

            Vector4 nine = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            return Build(key, canvas, nine, 100f);
        }

        /// <summary>六边宝石：费用与魔力计量用，带顶光与底部内影。</summary>
        public static Sprite Gem(int size, Color color, Color border)
        {
            size = Mathf.Clamp(size, 24, 128);
            string key = "gem_" + size + "_" + ColorKey(color) + "_" + ColorKey(border);
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float c = size * 0.5f;
            float r = size * 0.46f;
            float inner = r * 0.80f;

            float[] ox = new float[6];
            float[] oy = new float[6];
            float[] ix = new float[6];
            float[] iy = new float[6];
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI / 3f * i + Mathf.PI / 6f;
                ox[i] = c + Mathf.Cos(a) * r;
                oy[i] = c + Mathf.Sin(a) * r;
                ix[i] = c + Mathf.Cos(a) * inner;
                iy[i] = c + Mathf.Sin(a) * inner;
            }

            DrawPoly(canvas, ox, oy, border);
            DrawPoly(canvas, ix, iy, Theme.Darken(color, 0.18f));

            // 上半受光
            float[] tx = new float[6];
            float[] ty = new float[6];
            for (int i = 0; i < 6; i++)
            {
                tx[i] = c + (ix[i] - c) * (iy[i] > c ? 0.55f : 1f);
                ty[i] = iy[i] > c ? c : iy[i];
            }

            DrawPoly(canvas, tx, ty, color);

            // 高光点与底部内影
            DrawDisc(canvas, c - inner * 0.34f, c + inner * 0.30f, inner * 0.26f, Theme.WithAlpha(Color.white, 0.55f), 0f);
            DrawDisc(canvas, c, c - inner * 0.55f, inner * 0.70f, Theme.WithAlpha(Theme.Darken(color, 0.55f), 0.35f), 0f);

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>纹饰分隔线：两端渐隐的细线 + 中心菱形，替代裸的 1px 实线。</summary>
        public static Sprite Ornament(int w, int h, Color color)
        {
            w = Mathf.Max(24, w);
            h = Mathf.Max(6, h);
            string key = "orn_" + w + "_" + h + "_" + ColorKey(color);
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(w, h);
            float cy = h * 0.5f;
            Color bright = Theme.Lighten(color, 0.35f);

            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f) / w;
                float fade = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.55f);
                if (fade <= 0.02f)
                {
                    continue;
                }

                // 中段稍粗，端点渐细
                float thick = 0.8f + fade * 0.9f;
                for (int y = Mathf.FloorToInt(cy - thick); y <= Mathf.CeilToInt(cy + thick); y++)
                {
                    float cov = Mathf.Clamp01(thick - Mathf.Abs(y + 0.5f - cy));
                    if (cov > 0f)
                    {
                        canvas.Blend(x, y, x > w * 0.42f && x < w * 0.58f ? bright : color, cov * fade * 0.9f);
                    }
                }
            }

            // 中心菱形
            float cx = w * 0.5f;
            float dw = Mathf.Min(h * 0.55f, 5f);
            DrawPoly(canvas,
                new[] { cx - dw, cx, cx + dw, cx },
                new[] { cy, cy + dw * 0.85f, cy, cy - dw * 0.85f }, bright);
            DrawPoly(canvas,
                new[] { cx - dw * 0.45f, cx, cx + dw * 0.45f, cx },
                new[] { cy, cy + dw * 0.38f, cy, cy - dw * 0.38f }, color);

            return Build(key, canvas, Vector4.zero, 100f);
        }

        /// <summary>元素纹理：同心环纹，用于格子底纹。</summary>
        public static Sprite ElementSeal(int size, Color color, int rings)
        {
            string key = "seal_" + size + "_" + ColorKey(color) + "_" + rings;
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var canvas = Canvas.Create(size, size);
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > 1f)
                    {
                        continue;
                    }

                    float ring = Mathf.Abs(Mathf.Sin(dist * Mathf.PI * rings));
                    float a = Mathf.Clamp01(1f - dist) * (0.25f + 0.75f * ring);
                    canvas.Blend(x, y, color, a * 0.9f);
                }
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        #endregion

        #region 吉祥物

        /// <summary>程序化史莱姆：超椭圆身体 + 高光 + 表情。Boss 与玩家头像都用它。</summary>
        public static Sprite Slime(int size, Color body, Color eye, int seed, bool crowned, SlimeFace face = SlimeFace.Neutral)
        {
            size = Mathf.Clamp(size, 48, 512);
            string key = "slime_" + size + "_" + ColorKey(body) + "_" + ColorKey(eye) + "_" + seed
                + "_" + (crowned ? 1 : 0) + "_" + (int)face;
            Sprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var rng = new System.Random(seed);
            var canvas = Canvas.Create(size, size);
            float half = size * 0.5f;
            float baseR = half * 0.78f;
            float wobbleA = 0.038f + (float)rng.NextDouble() * 0.022f;
            float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            int lobes = 3 + rng.Next(2);

            Color rim = Theme.Darken(body, 0.48f);
            Color core = Theme.Lighten(body, 0.18f);

            for (int y = 0; y < size; y++)
            {
                float py = y + 0.5f - half;
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float theta = Mathf.Atan2(py, px);

                    float wobble = 1f + wobbleA * Mathf.Sin(theta * lobes + phase);
                    float radius = baseR * wobble;

                    // 底部略压扁，做出黏在地面的感觉
                    if (py < 0f)
                    {
                        radius *= 1f + (-py / half) * 0.06f;
                    }

                    float d = dist - radius;
                    float aa = Mathf.Clamp(0.5f - d, 0f, 1f);
                    if (aa <= 0f)
                    {
                        continue;
                    }

                    float t = Mathf.Clamp01((radius - dist) / radius);
                    Color shade = Color.Lerp(rim, core, Mathf.Pow(t, 0.55f));

                    // 顶部高光：柔和的椭圆亮斑
                    float hx = (px + baseR * 0.30f) / (baseR * 0.52f);
                    float hy = (py - baseR * 0.42f) / (baseR * 0.34f);
                    float highlight = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy));
                    shade = Color.Lerp(shade, Color.white, highlight * highlight * 0.50f);

                    // 边缘收暗，形成清晰轮廓
                    float edge = Mathf.Clamp01((d + size * 0.05f) / (size * 0.05f));
                    if (edge > 0f)
                    {
                        shade = Color.Lerp(shade, rim, edge * 0.6f);
                    }

                    canvas.Blend(x, y, shade, aa);
                }
            }

            DrawFace(canvas, half, baseR, eye, face);

            if (crowned)
            {
                DrawCrown(canvas, half, baseR);
            }

            return Build(key, canvas, Vector4.zero, 100f);
        }

        private static void DrawFace(Canvas canvas, float half, float baseR, Color eye, SlimeFace face)
        {
            float eyeR = baseR * 0.135f;
            float eyeY = baseR * 0.12f;
            float eyeX = baseR * 0.31f;
            float pupilR = eyeR * 0.54f;

            if (face == SlimeFace.Happy)
            {
                DrawArc(canvas, half - eyeX, half + eyeY, eyeR, eye);
                DrawArc(canvas, half + eyeX, half + eyeY, eyeR, eye);
            }
            else
            {
                DrawDisc(canvas, half - eyeX, half + eyeY, eyeR, eye, 0f);
                DrawDisc(canvas, half + eyeX, half + eyeY, eyeR, eye, 0f);
                DrawDisc(canvas, half - eyeX - pupilR * 0.22f, half + eyeY + pupilR * 0.20f, pupilR, Color.white, 0f);
                DrawDisc(canvas, half + eyeX - pupilR * 0.22f, half + eyeY + pupilR * 0.20f, pupilR, Color.white, 0f);

                if (face == SlimeFace.Fierce)
                {
                    DrawBrow(canvas, half - eyeX, half + eyeY + eyeR * 1.6f, eyeR * 2.6f, 1, eye);
                    DrawBrow(canvas, half + eyeX, half + eyeY + eyeR * 1.6f, eyeR * 2.6f, -1, eye);
                }
            }

            // 嘴：由表情决定曲率正负（负=微笑，正=苦恼）
            float mouthY = half - baseR * 0.27f;
            float mouthW = baseR * 0.23f;
            float curve;
            switch (face)
            {
                case SlimeFace.Happy: curve = -0.14f; break;
                case SlimeFace.Worried: curve = 0.12f; break;
                case SlimeFace.Fierce: curve = 0.04f; break;
                default: curve = 0f; break;
            }

            for (int x = (int)(half - mouthW); x <= (int)(half + mouthW); x++)
            {
                float n = (x - half) / mouthW;
                float y = mouthY + curve * baseR * (1f - n * n);
                for (int dy = -1; dy <= 1; dy++)
                {
                    canvas.Blend(x, Mathf.RoundToInt(y) + dy, eye, 0.9f);
                }
            }

            if (face == SlimeFace.Happy)
            {
                var blush = Theme.WithAlpha(Theme.SeriesSin, 0.32f);
                DrawDisc(canvas, half - baseR * 0.60f, half - baseR * 0.13f, baseR * 0.15f, blush, 0f);
                DrawDisc(canvas, half + baseR * 0.60f, half - baseR * 0.13f, baseR * 0.15f, blush, 0f);
            }
        }

        /// <summary>弯月形笑眼（开口朝下的上弧）。</summary>
        private static void DrawArc(Canvas canvas, float cx, float cy, float r, Color color)
        {
            int steps = Mathf.Max(16, (int)(r * 6f));
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI + Mathf.PI * i / steps;
                float px = cx + Mathf.Cos(a) * r * 1.10f;
                float py = cy - Mathf.Sin(a) * r * 0.55f - r * 0.35f;
                DrawDisc(canvas, px, py, r * 0.30f, color, 0f);
            }
        }

        /// <summary>怒眉：dir=1 左眉上扬，dir=-1 右眉上扬。</summary>
        private static void DrawBrow(Canvas canvas, float cx, float cy, float len, int dir, Color color)
        {
            int steps = Mathf.Max(10, (int)len);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float px = cx - len * 0.5f + len * t;
                float py = cy + dir * (t - 0.5f) * len * 0.36f;
                DrawDisc(canvas, px, py, Mathf.Max(1f, len * 0.10f), color, 0f);
            }
        }

        private static void DrawDisc(Canvas canvas, float cx, float cy, float r, Color color, float softness)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1);
            int x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1);
            int y1 = Mathf.CeilToInt(cy + r + 1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    float aa = Mathf.Clamp(0.5f - d, 0f, 1f);
                    if (aa > 0f)
                    {
                        canvas.Blend(x, y, color, aa);
                    }
                }
            }
        }

        /// <summary>王冠：一条冠带 + 三枚主尖 + 两点小尖 + 顶端宝珠。</summary>
        private static void DrawCrown(Canvas canvas, float half, float baseR)
        {
            Color gold = Theme.Gold;
            Color goldDark = Theme.Darken(Theme.Gold, 0.34f);

            int bandY = (int)(half + baseR * 0.72f);
            int bandH = Mathf.Max(4, (int)(baseR * 0.12f));
            int halfW = (int)(baseR * 0.78f);

            // 冠带
            for (int x = -halfW; x <= halfW; x++)
            {
                for (int y = 0; y < bandH; y++)
                {
                    canvas.Blend((int)half + x, bandY + y, y == bandH - 1 ? goldDark : gold, 1f);
                }
            }

            // 三枚主尖 + 两点小尖
            float[] offsets = { -0.88f, 0f, 0.88f, -0.44f, 0.44f };
            float[] scales = { 1f, 1.42f, 1f, 0.55f, 0.55f };

            for (int i = 0; i < offsets.Length; i++)
            {
                int cx = (int)half + (int)(offsets[i] * halfW);
                int h = (int)(baseR * 0.30f * scales[i]);
                int w = Mathf.Max(3, (int)(baseR * 0.13f * scales[i]));

                for (int y = 0; y < h; y++)
                {
                    float k = 1f - y / (float)h;
                    int spread = Mathf.Max(1, (int)(w * k));
                    for (int x = -spread; x <= spread; x++)
                    {
                        canvas.Blend(cx + x, bandY + bandH + y, y > h - 3 ? Theme.Lighten(gold, 0.3f) : gold, 1f);
                    }
                }

                // 顶端宝珠
                DrawDisc(canvas, cx, bandY + bandH + h + 1f, Mathf.Max(1.6f, w * 0.6f), Theme.Lighten(gold, 0.42f), 0f);
            }
        }

        #endregion

        private static string ColorKey(Color c)
        {
            // 含 alpha，避免同色不同透明度互相串味
            return ((int)(c.r * 255f)) + "_" + ((int)(c.g * 255f)) + "_" + ((int)(c.b * 255f)) + "_" + ((int)(c.a * 255f));
        }
    }
}
