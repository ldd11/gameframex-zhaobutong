#if ENABLE_UI_UGUI
using System;
using System.IO;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceImageDecodeCheck
{
    [MenuItem("Tools/Find Differences/Check Image Decoding")]
    public static void Run()
    {
        // Independent lossless WebP fixture: red top half, half-transparent blue bottom half.
        var webp = Convert.FromBase64String("UklGRiAAAABXRUJQVlA4TBMAAAAvBcAAEA8Q+z//D/yPCvMT0f9gAA==");
        var size = new Vector2Int(6, 4);
        var source = new Texture2D(6, 4, TextureFormat.RGBA32, false);
        try
        {
            for (var y = 0; y < 4; y++)
                for (var x = 0; x < 6; x++) source.SetPixel(x, y, y < 2 ? new Color(0, 0, 1, 128 / 255f) : Color.red);
            source.Apply();
            Verify(webp, size, true);
            Verify(source.EncodeToPNG(), size, true);
            Verify(source.EncodeToJPG(100), size, false);
            Reject(webp, new Vector2Int(3, 2));
            Reject(new byte[12], size);
            Reject(new byte[] { 1, 2 }, size);
            var truncated = new byte[20]; Array.Copy(webp, truncated, truncated.Length); Reject(truncated, size);
            var native = (PluginImporter)AssetImporter.GetAtPath("Packages/com.netpyoung.webp/Plugins/Android/libs/arm64-v8a/libwebp.so");
            if (!native || !native.GetCompatibleWithPlatform(BuildTarget.Android) || native.GetPlatformData(BuildTarget.Android, "CPU") != "ARM64")
                throw new Exception("Android ARM64 WebP plugin is not enabled.");
            var report = "PASS: WebP/PNG/JPG decoded; orientation and WebP/PNG transparency correct; wrong-size, invalid and truncated input rejected; Android ARM64 plugin enabled.";
            File.WriteAllText("Temp/FindDifferences-image-decode.txt", report); Debug.Log(report);
        }
        catch (Exception error) { File.WriteAllText("Temp/FindDifferences-image-decode.txt", "FAIL: " + error); Debug.LogException(error); }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    static void Reject(byte[] bytes, Vector2Int size)
    {
        Texture2D unexpected = null;
        try { unexpected = DifferenceRemoteLevel.DecodeTexture(bytes, size); }
        catch (InvalidOperationException) { return; }
        catch (Exception error) when (!(error is DllNotFoundException) && !(error is EntryPointNotFoundException)) { return; }
        if (unexpected) UnityEngine.Object.DestroyImmediate(unexpected);
        throw new Exception("Invalid image was accepted.");
    }

    static void Verify(byte[] bytes, Vector2Int size, bool alpha)
    {
        var decoded = DifferenceRemoteLevel.DecodeTexture(bytes, size);
        var target = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        var readback = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(decoded, target); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); readback.Apply();
            var top = readback.GetPixel(2, 3); var bottom = readback.GetPixel(2, 0);
            if (top.r < .8f || top.b > .2f || bottom.b < .8f || bottom.r > .2f ||
                (alpha && (Mathf.Abs(bottom.a - 128 / 255f) > .02f || top.a < .98f)))
                throw new Exception("Decoded pixels have wrong orientation, colors or alpha.");
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(decoded); UnityEngine.Object.DestroyImmediate(readback);
        }
    }
}
#endif
