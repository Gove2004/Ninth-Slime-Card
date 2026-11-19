using System.Collections.Generic;
using UnityEngine;

namespace Slime.Game
{
    /// <summary>
    /// 真美术素材门面：Assets/Resources/Art 下的卡面插画、背景、立绘、图标。
    /// 资源按约定路径直接放在该目录下，取不到时返回 null，调用方回退到程序化美术。
    /// </summary>
    public static class ArtLib
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>卡面插画，名称与 cards.csv 对齐。缺图时返回 null，由调用方回退到程序化卡面。</summary>
        public static Sprite Card(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                return null;
            }

            return Load("Art/Cards/Card_" + cardName);
        }

        /// <summary>
        /// 怪物立绘，按 EnemyDef.Sprite 取 Art/Monsters 下的同名图。
        /// 缺少某只怪的美术时返回 null，调用方回退到程序化史莱姆。
        /// </summary>
        public static Sprite Monster(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
            {
                return null;
            }

            return Load("Art/Monsters/" + spriteName);
        }

        /// <summary>场景背景图，key 对应 Art/Bg 下的图名（如 bg_battle）。取不到返回 null，调用方回退默认背景。</summary>
        public static Sprite Bg(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Load("Art/Bg/" + key);
        }

        public static Sprite BgMain { get { return Load("Art/Bg/bg_main"); } }

        /// <summary>节日装饰小图（Art/Decor 下，透明底）。缺图返回 null，调用方回退程序化绘制。</summary>
        public static Sprite Decor(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Load("Art/Decor/" + key);
        }
        public static Sprite Hero { get { return Load("Art/Char/hero"); } }
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

        /// <summary>
        /// UI 骨架素材（Art/Ui 下，黑底已抠透明），按九宫格边距切成 Sliced 可用的 sprite。
        /// 走 Texture2D + Sprite.Create 而不是 Resources.Load&lt;Sprite&gt;：九宫格 border
        /// 是运行时参数，不依赖纹理导入器的 meta 设置。
        /// </summary>
        public static Sprite UiArt(string key, Vector4 border)
        {
            string cacheKey = "uiart_" + key + "_" + border;
            Sprite sprite;
            if (cache.TryGetValue(cacheKey, out sprite))
            {
                return sprite;
            }

            var tex = Resources.Load<Texture2D>("Art/Ui/" + key);
            if (tex == null)
            {
                return null;
            }

            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
            cache[cacheKey] = sprite;
            return sprite;
        }
    }
}
