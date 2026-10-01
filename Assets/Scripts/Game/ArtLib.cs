using System.Collections.Generic;
using UnityEngine;

namespace Slime.Game
{
    /// <summary>
    /// 真美术素材门面：Assets/Resources/Art 下的卡面插画、背景、立绘、图标。
    /// 由 Editor/ArtForge 一次性导入。取不到时返回 null，调用方回退到程序化美术。
    /// </summary>
    public static class ArtLib
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>卡面插画（159 张，名称与 cards.csv 完全对齐）。</summary>
        public static Sprite Card(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                return null;
            }

            return Load("Art/Cards/Card_" + cardName);
        }

        public static Sprite BgMain { get { return Load("Art/Bg/bg_main"); } }
        public static Sprite Hero { get { return Load("Art/Char/hero"); } }
        public static Sprite Enemy { get { return Load("Art/Char/enemy"); } }
        public static Sprite Mana { get { return Load("Art/Icon/mana"); } }
        public static Sprite Blood { get { return Load("Art/Icon/blood"); } }
        public static Sprite Trophy { get { return Load("Art/Icon/trophy"); } }
        public static Sprite Score { get { return Load("Art/Icon/score"); } }

        private static Sprite Load(string path)
        {
            Sprite sprite;
            if (cache.TryGetValue(path, out sprite))
            {
                return sprite;
            }

            sprite = Resources.Load<Sprite>(path);
            cache[path] = sprite;
            return sprite;
        }
    }
}
