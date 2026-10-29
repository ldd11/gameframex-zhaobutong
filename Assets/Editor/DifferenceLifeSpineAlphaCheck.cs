#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public static class DifferenceLifeSpineAlphaCheck
{
    const string SpineFolder = "Assets/Bundles/UI/UIDifferences/Spine/LoveDecode/";
    const string MaterialFolder = "Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/Materials/";
    const string Report = "Temp/FindDifferences-life-spine-alpha.txt";

    [MenuItem("Tools/Find Differences/Check Life Spine Alpha Only")]
    public static void Check()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode before checking Spine alpha.");
        GameObject prefab = null, preview = null, cameraNode = null;
        Texture2D atlasPixels = null, before = null, after = null;
        RenderTexture target = null;
        var previous = RenderTexture.active;
        try
        {
            var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(SpineFolder + "xin_SkeletonData.asset");
            var straight = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "UI-StraightAlphaTex/SkeletonGraphicDefault-Straight.mat");
            var pma = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "UI-PMATexture/SkeletonGraphicDefault.mat");
            if (!data || !straight || !pma) throw new Exception("Missing xin Spine or alpha materials.");
            prefab = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
            var hearts = prefab.GetComponentsInChildren<SkeletonGraphic>(true).Where(g => g.skeletonDataAsset == data).ToArray();
            if (hearts.Length != 3 || hearts.Any(g => g.material != straight))
                throw new Exception("All three xin SkeletonGraphics must use the Straight Alpha UI material.");
            var atlasMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpineFolder + "xin_Material.mat");
            if (!atlasMaterial || atlasMaterial.GetFloat("_StraightAlphaInput") != 1 || !atlasMaterial.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT"))
                throw new Exception("xin atlas material must enable Straight Alpha Input and its shader keyword.");
            atlasPixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlasPixels.LoadImage(File.ReadAllBytes(SpineFolder + "xin.png"));
            var texel = atlasPixels.GetPixel(300, atlasPixels.height - 1 - 60);
            if (texel.a > .01f || texel.r < .9f || texel.g < .9f || texel.b < .9f)
                throw new Exception("Expected the known transparent white texel in the Straight Alpha xin atlas.");

            preview = new GameObject("Life Spine alpha preview", typeof(RectTransform), typeof(Canvas));
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.position = new Vector3(10000, 10000, 0);
            var canvas = preview.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)preview.transform).sizeDelta = Vector2.one * 256;
            var graphic = SkeletonGraphic.NewSkeletonGraphicGameObject(data, preview.transform, pma);
            graphic.raycastTarget = false;
            graphic.AnimationState.SetAnimation(0, "xin2", false);
            graphic.Update(.08f); graphic.UpdateMesh(); graphic.freeze = true;
            var sampleWorld = MapUV(graphic, new Vector2(300.5f / atlasPixels.width, 1 - 60.5f / atlasPixels.height));
            var bounds = graphic.GetLastMesh().bounds;

            cameraNode = new GameObject("Life Spine alpha camera", typeof(Camera));
            cameraNode.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraNode.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = graphic.transform.TransformPoint(bounds.center) + new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .2f, .24f);
            target = new RenderTexture(512, 512, 24);
            camera.targetTexture = target; canvas.worldCamera = camera;

            Texture2D Render(Material material, string filename)
            {
                graphic.material = material; graphic.SetMaterialDirty();
                Canvas.ForceUpdateCanvases(); graphic.UpdateMesh(); camera.Render(); RenderTexture.active = target;
                var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                File.WriteAllBytes(filename, pixels.EncodeToPNG());
                return pixels;
            }
            before = Render(pma, "Temp/FindDifferences-life-spine-alpha-before.png");
            after = Render(straight, "Temp/FindDifferences-life-spine-alpha-after.png");
            var viewport = camera.WorldToViewportPoint(sampleWorld);
            var x = Mathf.Clamp((int)(viewport.x * 512), 0, 511);
            var y = Mathf.Clamp((int)(viewport.y * 512), 0, 511);
            var wrong = before.GetPixel(x, y); var fixedColor = after.GetPixel(x, y); var background = after.GetPixel(0, 0);
            if (wrong.r < .8f || wrong.g < .8f || wrong.b < .8f || Distance(fixedColor, background) > .06f)
                throw new Exception($"Transparent atlas texel must show white with the old material and background after repair: pixel=({x},{y}), before={wrong}, after={fixedColor}, background={background}.");
            if (after.GetPixels().Count(c => c.r > .6f && c.g < .35f && c.b < .35f) < 100)
                throw new Exception("The repaired render must retain the red heart, rather than hide the entire graphic.");
            File.WriteAllText(Report, $"PASS: all 3 xin UI materials and atlas Straight Alpha flag/keyword; xin2 at 0.08s; transparent white atlas texel maps to render pixel ({x},{y}); old={wrong}, fixed={fixedColor}, background={background}; red heart remains visible. Before/after PNGs written to Temp; no game or save data touched.");
            Debug.Log(File.ReadAllText(Report));
        }
        catch (Exception error) { File.WriteAllText(Report, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            RenderTexture.active = previous;
            if (prefab) PrefabUtility.UnloadPrefabContents(prefab);
            foreach (var item in new UnityEngine.Object[] { cameraNode, preview, target, atlasPixels, before, after })
                if (item) UnityEngine.Object.DestroyImmediate(item);
        }
    }

    static Vector3 MapUV(SkeletonGraphic graphic, Vector2 point)
    {
        var mesh = graphic.GetLastMesh(); var uv = mesh.uv; var vertices = mesh.vertices; var triangles = mesh.triangles;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
            var v0 = uv[b] - uv[a]; var v1 = uv[c] - uv[a]; var p = point - uv[a];
            var determinant = v0.x * v1.y - v0.y * v1.x;
            if (Mathf.Abs(determinant) < .000001f) continue;
            var u = (p.x * v1.y - p.y * v1.x) / determinant;
            var v = (v0.x * p.y - v0.y * p.x) / determinant;
            if (u >= 0 && v >= 0 && u + v <= 1)
                return graphic.transform.TransformPoint(vertices[a] * (1 - u - v) + vertices[b] * u + vertices[c] * v);
        }
        throw new Exception("Transparent sample UV was not present in the xin2 mesh.");
    }

    static float Distance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
}
#endif
