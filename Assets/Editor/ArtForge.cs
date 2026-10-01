using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次性素材导入工具：把备份的旧版美术（159 张卡面插画、背景、立绘、图标）
/// 缩放后搬进 Assets/Resources/Art，供运行时 Resources.Load 使用。
/// 菜单：Tools/ArtForge/Import All
/// </summary>
public static class ArtForge
{
    private const string SrcRoot = ".workbuddy/backup/2026-10-01-refactor/05-oldart/Images-old";
    private const string OutRoot = "Assets/Resources/Art";

    [MenuItem("Tools/ArtForge/Import All")]
    public static void ImportAll()
    {
        int done = 0;
        done += ImportCards();
        done += ImportSingle(SrcRoot + "/BG/BaseBG.png", OutRoot + "/Bg/bg_main.jpg", 1440, false) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/Character/果冻_拿剑.png", OutRoot + "/Char/hero.png", 512, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/Character/敌人.png", OutRoot + "/Char/enemy.png", 512, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/UI/mana.png", OutRoot + "/Icon/mana.png", 256, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/UI/blood.png", OutRoot + "/Icon/blood.png", 256, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/UI/score.png", OutRoot + "/Icon/score.png", 512, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/Level/奖杯.png", OutRoot + "/Icon/trophy.png", 256, true) ? 1 : 0;
        done += ImportSingle(SrcRoot + "/Level/标准.png", OutRoot + "/Icon/level.png", 256, true) ? 1 : 0;

        AssetDatabase.Refresh();
        Debug.Log("[ArtForge] imported " + done + " files -> " + OutRoot);
    }

    /// <summary>卡面插画：全部缩放到 512 宽 JPG，输出名保留 Card_ 前缀（与 cards.csv 名称对齐）。</summary>
    private static int ImportCards()
    {
        string srcDir = SrcRoot + "/Card";
        string outDir = OutRoot + "/Cards";
        Directory.CreateDirectory(outDir);

        int count = 0;
        foreach (string file in Directory.GetFiles(srcDir, "Card_*.png"))
        {
            string outName = Path.GetFileNameWithoutExtension(file) + ".jpg";
            if (ResizeWrite(file, Path.Combine(outDir, outName), 512, false, 86))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ImportSingle(string srcRel, string outRel, int maxSize, bool keepAlpha)
    {
        string src = Path.Combine(Directory.GetParent(Application.dataPath).FullName, srcRel);
        string dst = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outRel);
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        return ResizeWrite(src, dst, maxSize, keepAlpha, 90);
    }

    /// <summary>用 GPU 双线性缩放后编码写出。keepAlpha=false 时输出 JPG（白底插画/背景），否则 PNG。</summary>
    private static bool ResizeWrite(string src, string dst, int maxSize, bool keepAlpha, int quality)
    {
        if (!File.Exists(src))
        {
            Debug.LogWarning("[ArtForge] missing " + src);
            return false;
        }

        var data = File.ReadAllBytes(src);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data))
        {
            Object.DestroyImmediate(tex);
            return false;
        }

        int w = tex.width;
        int h = tex.height;
        float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(w, h));
        int tw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
        int th = Mathf.Max(1, Mathf.RoundToInt(h * scale));

        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;

        var outTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        outTex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        outTex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);

        byte[] bytes = keepAlpha ? outTex.EncodeToPNG() : outTex.EncodeToJPG(quality);
        Object.DestroyImmediate(outTex);
        File.WriteAllBytes(dst, bytes);
        return true;
    }
}
