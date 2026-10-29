#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceHintDisabledLookCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Find Differences/Check Hint Disabled Look Only")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before checking the disabled hint look.");
        const string report = "Temp/FindDifferences-hint-disabled-look.txt";
        Directory.CreateDirectory("Temp");
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var disabled = typeof(UIDifferences).GetField("hintDisabledMaterial")?.GetValue(ui) as Material;
            Check(disabled && disabled.shader && disabled.shader.isSupported, "The authored disabled hint material and supported shader must exist.");
            Check(!ShaderUtil.GetShaderMessages(disabled.shader).Any(message => message.severity.ToString() == "Error"),
                "The disabled hint shader has compiler errors.");
            foreach (var property in new[] { "_MainTex", "_Color", "_StencilComp", "_Stencil", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask" })
                Check(disabled.HasProperty(property), "The disabled UI material must retain property " + property);
            var graphics = ui.hintButton.GetComponentsInChildren<Graphic>(true);
            Check(graphics.Length == 5, "Expected exactly five authored hint graphics before creating the dynamic hand.");
            var materials = graphics.Select(graphic => graphic.material).ToArray();
            var colors = graphics.Select(graphic => graphic.color).ToArray();
            var texts = graphics.OfType<Text>().Select(text => text.text).ToArray();
            Call(ui, "InitializeHintButtonLook");
            Check(ui.hintButton.colors.disabledColor == Color.white,
                "The disabled material must not be darkened again by the button's ColorTint.");
            var hand = DifferenceHintHand.Create(ui.hintButton, ui.hintHandSprite);
            var handGraphics = hand.GetComponentsInChildren<Graphic>(true);
            var handMaterials = handGraphics.Select(graphic => graphic.material).ToArray();
            var handColors = handGraphics.Select(graphic => graphic.color).ToArray();
            ui.backButton.gameObject.SetActive(true);
            var materialCount = Resources.FindObjectsOfTypeAll<Material>().Length;
            for (var pass = 0; pass < 4; pass++)
            {
                Call(ui, "SetHintInteractable", false);
                Check(!ui.hintButton.interactable, "The disabled hint button must reject interaction.");
                Check(graphics.All(graphic => graphic.material == disabled), "All five static hint graphics must use the dimming material.");
                Call(ui, "InitializeHintButtonLook");
                Call(ui, "SetHintInteractable", true);
                Check(ui.hintButton.interactable, "The enabled hint button must accept interaction.");
                for (var i = 0; i < graphics.Length; i++)
                    Check(graphics[i].material == materials[i] && graphics[i].color == colors[i], "Enabled hint graphics must restore their original materials and colors.");
                Call(ui, "SetHintInteractable", false);
                Call(ui, "StopTutorial");
                Check(ui.hintButton.interactable && graphics.Select(graphic => graphic.material).SequenceEqual(materials),
                    "Leaving the tutorial must restore the hint button and all original materials.");
                Check(graphics.OfType<Text>().Select(text => text.text).SequenceEqual(texts), "Toggling the hint state must not change Count or Label text.");
                for (var i = 0; i < handGraphics.Length; i++)
                    Check(handGraphics[i].material == handMaterials[i] && handGraphics[i].color == handColors[i],
                        "The dynamic HintHand subtree must retain its own materials and colors.");
            }
            Check(Resources.FindObjectsOfTypeAll<Material>().Length == materialCount, "Repeated hint state changes must not allocate or leak materials.");
            CheckRender(ui.hintButton.targetGraphic, disabled, "Button");
            foreach (var name in new[] { "Bulb", "Badge", "Count" })
                CheckRender(ui.hintButton.transform.Find(name).GetComponent<Graphic>(), disabled, name);
            Check(!ShaderUtil.GetShaderMessages(disabled.shader).Any(message => message.severity.ToString() == "Error"),
                "Rendering exposed a disabled hint shader compilation error.");
            File.WriteAllText(report, "PASS: all five authored hint graphics preserve their original colors at half brightness and restore original materials/colors/text; dynamic HintHand unchanged; repeated toggles and StopTutorial restore interaction without material allocations. Button ColorTint does not apply a second dim; actual button background, Bulb/Badge sprites and Count font retain half of each RGB channel with unchanged alpha, then recover the enabled pixels. Enabled/disabled PNGs are in Temp. Prefab and player settings untouched.");
            Debug.Log(File.ReadAllText(report));
        }
        catch (Exception exception) { File.WriteAllText(report, "FAIL " + exception); Debug.LogException(exception); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CheckRender(Graphic source, Material disabled, string name)
    {
        var preview = new GameObject("Hint disabled preview", typeof(RectTransform), typeof(Canvas));
        var cameraNode = new GameObject("Hint disabled preview camera", typeof(Camera));
        var target = new RenderTexture(256, 256, 24);
        var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            preview.hideFlags = cameraNode.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.position = new Vector3(10000, 10000, 0);
            var canvas = preview.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)preview.transform).sizeDelta = Vector2.one * 256;
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(preview.transform, false);
            var graphic = (Graphic)node.AddComponent(source.GetType());
            EditorUtility.CopySerialized(source, graphic);
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = graphic.rectTransform.pivot = Vector2.one * .5f;
            graphic.rectTransform.anchoredPosition = Vector2.zero;
            graphic.rectTransform.sizeDelta = Vector2.one * 160;
            graphic.enabled = true;
            var camera = cameraNode.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = preview.transform.position + new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 128;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.targetTexture = target; canvas.worldCamera = camera;

            Color32[] Render(Material material, string state)
            {
                graphic.material = material; graphic.SetMaterialDirty(); graphic.SetVerticesDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                if (state != null) File.WriteAllBytes("Temp/FindDifferences-hint-" + name.ToLowerInvariant() + "-" + state + ".png", pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }

            var enabled = Render(source.material, "enabled");
            var dimmed = Render(disabled, "disabled");
            var restored = Render(source.material, null);
            var visible = Enumerable.Range(0, enabled.Length).Where(i => enabled[i].a > 230 &&
                Math.Max(enabled[i].r, Math.Max(enabled[i].g, enabled[i].b)) > 40).ToArray();
            Check(visible.Length > 20, name + " must produce visible sprite or font pixels.");
            Check(visible.All(i => Math.Abs(dimmed[i].r - enabled[i].r * .5f) <= 2 &&
                Math.Abs(dimmed[i].g - enabled[i].g * .5f) <= 2 && Math.Abs(dimmed[i].b - enabled[i].b * .5f) <= 2),
                name + " must halve each RGB channel without converting its original color to grayscale.");
            Check(Enumerable.Range(0, enabled.Length).All(i => Math.Abs(dimmed[i].a - enabled[i].a) <= 1),
                name + " must preserve its original transparency and shape.");
            var enabledLuma = visible.Average(i => Luma(enabled[i]));
            var disabledLuma = visible.Average(i => Luma(dimmed[i]));
            Check(disabledLuma < enabledLuma * .55f && disabledLuma > enabledLuma * .45f,
                name + " must retain about half its enabled luminance instead of becoming black.");
            Check(Enumerable.Range(0, enabled.Length).All(i => Distance(enabled[i], restored[i]) <= 2),
                name + " must recover the original enabled render after changing its material back.");
            if (name == "Count")
                Check(visible.Any(i => enabled[i].r > 220 && enabled[i].g > 220 && enabled[i].b > 220 && dimmed[i].r < 220),
                    "The white Count font must become visibly gray.");
            else
                Check(visible.Any(i => Math.Max(enabled[i].r, Math.Max(enabled[i].g, enabled[i].b)) -
                    Math.Min(enabled[i].r, Math.Min(enabled[i].g, enabled[i].b)) > 20 &&
                    Math.Max(dimmed[i].r, Math.Max(dimmed[i].g, dimmed[i].b)) -
                    Math.Min(dimmed[i].r, Math.Min(dimmed[i].g, dimmed[i].b)) > 8), name + " must retain its authored colors while dimmed.");
        }
        finally
        {
            RenderTexture.active = previous;
            foreach (var item in new UnityEngine.Object[] { cameraNode, preview, target, pixels })
                UnityEngine.Object.DestroyImmediate(item);
        }
    }

    static float Luma(Color32 color)
    {
        var value = (Color)color;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) value = value.linear;
        return (.2126f * value.r + .7152f * value.g + .0722f * value.b) * 255;
    }
    static int Distance(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Max(Math.Abs(a.b - b.b), Math.Abs(a.a - b.a))));
    static void Call(UIDifferences ui, string name, params object[] args) => typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, args);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
