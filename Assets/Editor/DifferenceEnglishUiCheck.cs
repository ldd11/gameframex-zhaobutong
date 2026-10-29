#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceEnglishUiCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Find Differences/Check English UI Only")]
    public static void Run()
    {
        const string reportPath = "Temp/FindDifferences-english-ui.txt";
        var paths = new[] { "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab",
            "Assets/Resources/UI/UILauncher/UGUI/UILauncher/UILauncher.prefab" };
        var roots = new GameObject[paths.Length];
        try
        {
            var report = new StringBuilder();
            DifferenceRemoteLevel.SelfCheck();
            report.AppendLine("PASS translated level data validation and fallback level titles.");
            for (var i = 0; i < roots.Length; i++)
            {
                roots[i] = PrefabUtility.LoadPrefabContents(paths[i]);
                report.AppendLine("PASS " + paths[i] + ": " + CheckTexts(roots[i]) + " Text/TMP labels contain no Chinese.");
            }
            var ui = roots[0].GetComponent<UIDifferences>();
            Check(ui && ui.levels != null && ui.levels.Length == 3, "Expected three authored local levels.");
            foreach (var level in ui.levels)
            {
                Check(!string.IsNullOrWhiteSpace(level.title), "Local level titles must not be empty.");
                CheckEnglish(level.title, "Local level title");
            }
            var root = (RectTransform)roots[0].transform;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
            ui.play.SetActive(true);
            var layout = ui.play.GetComponent<DifferencePlayLayout>();
            foreach (var field in typeof(DifferencePlayLayout).GetFields().Where(field => field.Name.EndsWith("Offset", StringComparison.Ordinal)))
                field.SetValue(layout, Vector2.zero);
            var fitSafe = typeof(UIDifferences).GetMethod("FitSafeArea", Private);
            var ensure = typeof(UIDifferences).GetMethod("EnsureTutorialVisuals", Private);
            var fitMessage = typeof(UIDifferences).GetMethod("LayoutTutorialMessage", Private);
            Check(fitSafe != null && ensure != null && fitMessage != null, "Safe area and tutorial layout methods must exist.");
            ensure.Invoke(ui, null);
            var message = (Text)typeof(UIDifferences).GetField("tutorialMessage", Private).GetValue(ui);
            Check(message && message.font && message.fontSize >= 46 && !message.resizeTextForBestFit,
                "Tutorial text must use a readable fixed font size of at least 46.");
            var panel = (RectTransform)message.transform.parent;
            var screens = new[] { new Vector2(1080, 1920), new Vector2(1080, 2340), new Vector2(1179, 2556), new Vector2(2340, 1080) };
            var areas = new[] { new Rect(0, 0, 1080, 1920), new Rect(0, 0, 1080, 2261), new Rect(0, 102, 1179, 2277), new Rect(79, 0, 2182, 1080) };
            var messages = new[] {
                "Tap the highlighted spot\nin either picture.",
                "Pinch out to zoom in.\nOn a computer, scroll to zoom.",
                "Tap Hint to find a difference.\nUnlimited hints in this level.",
                "Tap the highlighted spot\nto find this difference."
            };
            for (var variant = 0; variant < screens.Length; variant++)
            {
                var screen = screens[variant]; var area = areas[variant];
                root.sizeDelta = new Vector2(screen.x * 2556 / screen.y, 2556);
                root.ForceUpdateRectTransforms();
                var normalized = new Rect(area.x / screen.x, area.y / screen.y, area.width / screen.x, area.height / screen.y);
                fitSafe.Invoke(ui, new object[] { normalized });
                var safe = new Rect(root.rect.xMin + normalized.xMin * root.rect.width, root.rect.yMin + normalized.yMin * root.rect.height,
                    normalized.width * root.rect.width, normalized.height * root.rect.height);
                var play = (RectTransform)layout.transform;
                var min = play.InverseTransformPoint(root.TransformPoint(safe.min));
                var max = play.InverseTransformPoint(root.TransformPoint(safe.max));
                layout.ApplyLayout(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
                foreach (var text in messages)
                {
                    message.text = text;
                    fitMessage.Invoke(ui, null);
                    CheckMessage(root, safe, layout.frame, panel, message);
                    var size = panel.rect.size;
                    fitMessage.Invoke(ui, null);
                    Check(panel.rect.size == size, "Repeated tutorial layout must not drift.");
                }
                report.AppendLine($"PASS {screen.x}x{screen.y} safe={area}: tutorial font {message.fontSize}, panel {panel.rect.size}, all four messages fit without overflow.");
                if (variant == 0)
                {
                    var width = layout.frame.rect.width;
                    try
                    {
                        layout.frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 640);
                        message.text = messages[1];
                        fitMessage.Invoke(ui, null);
                        CheckMessage(root, safe, layout.frame, panel, message);
                        report.AppendLine("PASS narrow 640-unit picture frame: panel shrinks and wrapped text grows without overflow.");
                    }
                    finally { layout.frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width); }
                }
            }
            CheckTexts(roots[0]);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, report + "Three local level titles checked. Prefabs and player settings unchanged.");
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, "FAIL " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            foreach (var root in roots)
            {
                if (!root) continue;
                // The isolated tutorial owns this material; its runtime OnDestroy uses delayed destruction.
                foreach (var shade in root.GetComponentsInChildren<DifferenceHintSpotlight>(true))
                {
                    var field = typeof(DifferenceHintSpotlight).GetField("spotlightMaterial", Private);
                    var material = field.GetValue(shade) as Material;
                    field.SetValue(shade, null);
                    if (material) UnityEngine.Object.DestroyImmediate(material);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    static int CheckTexts(GameObject root)
    {
        var count = 0;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            CheckEnglish(text.text, AnimationUtility.CalculateTransformPath(text.transform, root.transform)); count++;
        }
        // TMP is optional for this Editor assembly; inspect its public text property without adding a dependency.
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (!component) continue;
            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                if (type.FullName != "TMPro.TMP_Text") continue;
                CheckEnglish((string)type.GetProperty("text").GetValue(component), AnimationUtility.CalculateTransformPath(component.transform, root.transform));
                count++; break;
            }
        }
        Check(count > 0, root.name + " must contain player-facing labels.");
        return count;
    }

    static void CheckMessage(RectTransform root, Rect safe, RectTransform frame, RectTransform panel, Text text)
    {
        CheckEnglish(text.text, "Tutorial instruction");
        Check(text.fontSize >= 46 && !text.resizeTextForBestFit && text.horizontalOverflow == HorizontalWrapMode.Wrap,
            "Tutorial text must remain large and wrap instead of shrinking.");
        Check(panel.rect.width <= Mathf.Min(900, frame.rect.width - 48) + .1f && panel.rect.height >= 144,
            "Tutorial panel must fit the current picture width and retain a readable height.");
        Check(text.preferredHeight <= text.rectTransform.rect.height + .1f, "Tutorial instruction is vertically clipped.");
        var panelCorners = new Vector3[4]; panel.GetWorldCorners(panelCorners);
        foreach (var corner in panelCorners)
        {
            var rootPoint = root.InverseTransformPoint(corner);
            var framePoint = frame.InverseTransformPoint(corner);
            Check(rootPoint.x >= safe.xMin - .1f && rootPoint.x <= safe.xMax + .1f &&
                framePoint.x >= frame.rect.xMin - .1f && framePoint.x <= frame.rect.xMax + .1f,
                "Tutorial instruction exceeds the safe or picture width.");
        }
        using (var generator = new TextGenerator())
        {
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.generateOutOfBounds = true;
            Check(generator.Populate(text.text, settings) && generator.lineCount >= 2, "Tutorial instruction must generate readable wrapped text.");
            foreach (var vertex in generator.verts.Take(Mathf.Max(0, generator.verts.Count - 4)))
                Check(vertex.position.x >= text.rectTransform.rect.xMin - 1 && vertex.position.x <= text.rectTransform.rect.xMax + 1 &&
                    vertex.position.y >= text.rectTransform.rect.yMin - 1 && vertex.position.y <= text.rectTransform.rect.yMax + 1,
                    "Tutorial glyphs overflow the text rectangle.");
        }
    }

    static void CheckEnglish(string value, string context)
    {
        if (value == null) return;
        Check(!value.Any(character => character >= '\u3400' && character <= '\u9fff' || character >= '\uf900' && character <= '\ufaff'),
            "Chinese player text remains at " + context + ": " + value);
    }

    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
