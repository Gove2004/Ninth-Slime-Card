using UnityEngine;
using Slime.Core;

namespace Slime.Game
{
    /// <summary>全局视觉令牌。所有界面只从这里取颜色与尺寸，保证风格统一。</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("#0B0E13");
        public static readonly Color BgDeep = Hex("#070910");
        public static readonly Color Panel = Hex("#151A23");
        public static readonly Color PanelRaised = Hex("#1D242F");
        public static readonly Color PanelSunken = Hex("#10141B");
        public static readonly Color Border = Hex("#2A3341");
        public static readonly Color BorderSoft = Hex("#1F2833");

        public static readonly Color Text = Hex("#E8EEF5");
        public static readonly Color TextDim = Hex("#8C99A8");
        public static readonly Color TextFaint = Hex("#5A6675");

        public static readonly Color Accent = Hex("#4CD97B");
        public static readonly Color AccentDeep = Hex("#2E8F52");
        public static readonly Color Danger = Hex("#FF5C5C");
        public static readonly Color Gold = Hex("#FFC93C");

        /// <summary>魔力（法力）专用色。</summary>
        public static readonly Color Mana = Hex("#4EA8FF");
        public static readonly Color Shield = Hex("#9FB4CC");
        public static readonly Color Hp = Hex("#E8556B");

        // ---- 八系列配色（战技 / 剑技 / 炎咒 / 冰咒 / 雷咒 / 光咒 / 暗咒 / 秘术）----
        public static readonly Color SeriesStrike = Hex("#8FA6BF");  // 战技 · 中性钢灰
        public static readonly Color SeriesBlade = Hex("#D9553F");   // 剑技 · 火焰赤
        public static readonly Color SeriesFire = Hex("#E8862A");    // 炎咒 · 橙金
        public static readonly Color SeriesIce = Hex("#4FA8D8");     // 冰咒 · 寒蓝
        public static readonly Color SeriesThunder = Hex("#E0C452");  // 雷咒 · 电黄
        public static readonly Color SeriesLight = Hex("#EFE3B0");    // 光咒 · 暖白
        public static readonly Color SeriesDark = Hex("#8465D6");     // 暗咒 · 幽紫
        public static readonly Color SeriesArcane = Hex("#4FC0A8");   // 秘术 · 青碧

        // 字号按「手机横屏能不能看清」定，不按桌面显示器能不能看清定。
        // 画布参考 1920×1080、match 0.5，在 2400×1080 的手机上缩放约 1.12 倍；
        // 原先 Caption=16 落到真机只有约 18 物理像素（≈6.5dp），小到必须凑近看。
        // 现在最小一档 24 → 真机约 27 物理像素（≈10dp），是中文正文的可读下限。
        public const int H1 = 56;
        public const int H2 = 44;
        public const int H3 = 34;
        public const int H4 = 28;
        public const int Body = 26;
        public const int Caption = 24;

        /// <summary>卡面描述用字：比 Caption 略小，但仍然高于可读下限。</summary>
        public const int CardText = 21;

        public const int GapS = 6;
        public const int GapM = 12;
        public const int GapL = 20;
        public const int GapXl = 32;

        public const int RadiusS = 8;
        public const int RadiusM = 14;
        public const int RadiusL = 22;

        public const float Fast = 0.12f;
        public const float Normal = 0.2f;
        public const float Slow = 0.34f;

        public static Color SeriesColor(CardSeries series)
        {
            switch (series)
            {
                case CardSeries.Strike: return SeriesStrike;
                case CardSeries.Blade: return SeriesBlade;
                case CardSeries.Fire: return SeriesFire;
                case CardSeries.Ice: return SeriesIce;
                case CardSeries.Thunder: return SeriesThunder;
                case CardSeries.Light: return SeriesLight;
                case CardSeries.Dark: return SeriesDark;
                case CardSeries.Arcane: return SeriesArcane;
                default: return TextFaint;
            }
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static Color Darken(Color c, float amount)
        {
            return new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
        }

        public static Color Lighten(Color c, float amount)
        {
            return new Color(
                c.r + (1f - c.r) * amount,
                c.g + (1f - c.g) * amount,
                c.b + (1f - c.b) * amount,
                c.a);
        }

        private static Color Hex(string hex)
        {
            Color parsed;
            if (ColorUtility.TryParseHtmlString(hex, out parsed))
            {
                return parsed;
            }

            return Color.magenta;
        }
    }
}
