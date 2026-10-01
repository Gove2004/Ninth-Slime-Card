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

        // ---- 八系列配色 ----
        public static readonly Color SeriesStarter = Hex("#8FA6BF"); // 初始 · 中性钢灰
        public static readonly Color SeriesSin = Hex("#C9553F");     // 七罪 · 罪火
        public static readonly Color SeriesBlood = Hex("#D8344F");   // 血族 · 猩红
        public static readonly Color SeriesFortify = Hex("#C8A24A"); // 坚固 · 古铜
        public static readonly Color SeriesTech = Hex("#3FC8D8");    // 科技 · 青蓝
        public static readonly Color SeriesSeed = Hex("#7BC24A");    // 种子 · 苗绿
        public static readonly Color SeriesShadow = Hex("#8465D6");  // 暗影 · 幽紫
        public static readonly Color SeriesChrono = Hex("#D9A0E8");  // 时序 · 淡紫

        public const int H1 = 46;
        public const int H2 = 34;
        public const int H3 = 26;
        public const int H4 = 21;
        public const int Body = 19;
        public const int Caption = 16;

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
                case CardSeries.Starter: return SeriesStarter;
                case CardSeries.Sin: return SeriesSin;
                case CardSeries.Blood: return SeriesBlood;
                case CardSeries.Fortify: return SeriesFortify;
                case CardSeries.Tech: return SeriesTech;
                case CardSeries.Seed: return SeriesSeed;
                case CardSeries.Shadow: return SeriesShadow;
                case CardSeries.Chrono: return SeriesChrono;
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
