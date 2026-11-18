using System;
using Slime.Core;

namespace Slime.Game
{
    /// <summary>
    /// 新春装饰开关。窗口内（1 月 20 日 ～ 2 月 28 日，任何年份）自动点亮；
    /// 设置里可强制常开或关闭，偏好存 MetaState.FestivalPref。
    /// 装饰只做表现层：不参与任何结算，也不改全局配色——信息永远比节日气氛重要。
    /// </summary>
    public static class Festival
    {
        public const int PrefFollowDate = 0;
        public const int PrefAlwaysOn = 1;
        public const int PrefOff = 2;

        /// <summary>窗口起点：1 月 20 日。</summary>
        private static readonly DateWindow Window = new DateWindow(1, 20, 2, 28);

        private struct DateWindow
        {
            private readonly int startMonth;
            private readonly int startDay;
            private readonly int endMonth;
            private readonly int endDay;

            public DateWindow(int sm, int sd, int em, int ed)
            {
                startMonth = sm;
                startDay = sd;
                endMonth = em;
                endDay = ed;
            }

            public bool Contains(DateTime now)
            {
                int t = now.Month * 100 + now.Day;
                return t >= startMonth * 100 + startDay && t <= endMonth * 100 + endDay;
            }
        }

        private static MetaState Meta
        {
            get { return GameApp.I != null ? GameApp.I.Meta : null; }
        }

        public static bool InWindow()
        {
            return Window.Contains(DateTime.Now);
        }

        public static bool IsActive
        {
            get
            {
                var meta = Meta;
                if (meta != null && meta.FestivalPref == PrefAlwaysOn)
                {
                    return true;
                }

                if (meta != null && meta.FestivalPref == PrefOff)
                {
                    return false;
                }

                return InWindow();
            }
        }

        /// <summary>偏好模式的展示名（设置页与 DevPanel 共用）。</summary>
        public static string PrefName(int pref)
        {
            switch (pref)
            {
                case PrefAlwaysOn: return "常开";
                case PrefOff: return "关闭";
                default: return "跟随日期" + (InWindow() ? "（窗口内）" : "（窗口外）");
            }
        }
    }
}
