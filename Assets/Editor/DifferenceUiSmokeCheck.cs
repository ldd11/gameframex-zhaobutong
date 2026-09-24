#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Create Temp/FindDifferences-ui-smoke.request to run from the open Editor.
[InitializeOnLoad]
public static class DifferenceUiSmokeCheck
{
    const string Pending = "FindDifferences.UiSmoke.Pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-ui-smoke"));
    static readonly string Request = Folder + ".request";
    static readonly string[] IntKeys = { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate", "RoundContent" };
    static bool running;
    static bool launcherCaptured;
    static double deadline;

    static DifferenceUiSmokeCheck() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Find Differences/Run UI Smoke Checks")]
    public static void Start()
    {
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Pending, true);
        deadline = EditorApplication.timeSinceStartup + 90;
        File.WriteAllText(Path.Combine(Folder, "report.txt"), "RUNNING: waiting for Launcher\n");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Find Differences/Check Loading Motion Only")]
    public static async void CheckLoadingMotionOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || !Active(ui, "homeDesign"))
        {
            Debug.LogWarning("Enter Play and wait for the home page before checking loading motion.");
            return;
        }
        var layer = (GameObject)Field(ui, "loadingDesign");
        var magnifier = (RectTransform)((GameObject)Field(ui, "loadingMagnifier")).transform;
        var fill = ((Image)Field(ui, "loadingFill")).rectTransform;
        var center = (Vector2)Field(ui, "loadingMagnifierCenter");
        var oldPosition = magnifier.anchoredPosition;
        var oldRotation = magnifier.localRotation;
        var oldFillSize = fill.sizeDelta;
        var oldStarted = Field(ui, "loadingStarted");
        var oldActive = layer.activeSelf;
        var oldSibling = layer.transform.GetSiblingIndex();
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-loading-motion.txt");
        running = true;
        try
        {
            File.WriteAllText(report, "RUNNING: loading motion only; original save and API configuration untouched.\n");
            typeof(UIDifferences).GetField("loadingStarted", Private).SetValue(ui, Time.unscaledTime);
            layer.SetActive(true); layer.transform.SetAsLastSibling();
            await Task.Delay(60);
            var first = magnifier.anchoredPosition;
            var previous = first - center;
            var radius = previous.magnitude;
            var angle = 0f; var travel = 0f; var samples = 0;
            var end = Time.realtimeSinceStartup + 2.05f;
            if (radius < 1) throw new InvalidOperationException("Loading orbit radius must be nonzero.");
            while (Time.realtimeSinceStartup < end)
            {
                await Task.Delay(100);
                if (!ui || !EditorApplication.isPlaying) throw new InvalidOperationException("Play stopped during loading motion check.");
                var offset = magnifier.anchoredPosition - center;
                if (Mathf.Abs(offset.magnitude - radius) > .1f)
                    throw new InvalidOperationException("Loading magnifier did not keep a constant orbit radius.");
                if (Quaternion.Angle(oldRotation, magnifier.localRotation) > .1f)
                    throw new InvalidOperationException("Loading magnifier changed its image orientation.");
                if (previous.x * offset.y - previous.y * offset.x >= 0)
                    throw new InvalidOperationException("Loading magnifier did not move clockwise.");
                angle += Vector2.SignedAngle(previous, offset);
                travel = Mathf.Max(travel, Vector2.Distance(first, magnifier.anchoredPosition));
                previous = offset; samples++;
            }
            if (travel < radius || angle > -300)
                throw new InvalidOperationException("Loading magnifier did not complete the expected circular movement.");
            var result = $"PASS: {samples} samples; constant radius={radius:F2}, clockwise angle={angle:F1}, displacement={travel:F2}; image orientation unchanged.";
            File.AppendAllText(report, result + "\n"); Debug.Log(result);
        }
        catch (Exception error) { File.AppendAllText(report, "FAIL: " + error + "\n"); Debug.LogException(error); }
        finally
        {
            if (ui)
            {
                layer.SetActive(oldActive); layer.transform.SetSiblingIndex(oldSibling);
                magnifier.anchoredPosition = oldPosition; magnifier.localRotation = oldRotation;
                fill.sizeDelta = oldFillSize;
                typeof(UIDifferences).GetField("loadingStarted", Private).SetValue(ui, oldStarted);
                typeof(UIDifferences).GetField("loadingMagnifierCenter", Private).SetValue(ui, center);
            }
            running = false;
            File.AppendAllText(report, "Original loading state restored; save and API configuration untouched.\n");
        }
    }

    [MenuItem("Tools/Find Differences/Check Progress Row Only")]
    public static void CheckProgressRowOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || ui.Round == null || !ui.play.activeInHierarchy)
        {
            Debug.LogWarning("Enter Play and load a level before checking the progress row.");
            return;
        }
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-progress-row.txt");
        try
        {
            var row = (RectTransform)ui.progressDots[0].transform.parent;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            var previousRight = row.rect.xMin;
            var centerY = ui.progressDots[0].rectTransform.localPosition.y;
            for (var i = 0; i < ui.Round.Total; i++)
            {
                var dot = ui.progressDots[i].rectTransform;
                var left = row.InverseTransformPoint(dot.TransformPoint(dot.rect.min));
                var right = row.InverseTransformPoint(dot.TransformPoint(dot.rect.max));
                if (!dot.gameObject.activeInHierarchy || dot.parent != row || Mathf.Abs(dot.localPosition.y - centerY) > .1f ||
                    left.x < previousRight - .1f || right.x > row.rect.xMax + .1f ||
                    left.y < row.rect.yMin - .1f || right.y > row.rect.yMax + .1f)
                    throw new InvalidOperationException("Progress dot is hidden, wrapped, overlapping or outside the row: " + i);
                previousRight = right.x;
            }
            var result = $"PASS: {ui.Round.Total} dots in one row; size={ui.progressDots[0].rectTransform.sizeDelta}, row width={row.rect.width:F2}; no overlap or overflow.";
            File.WriteAllText(report, result + "\n"); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error + "\n"); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Check Gameplay Layout Only")]
    public static async void CheckGameplayLayoutOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-gameplay-layout.txt");
        running = true;
        try
        {
            ui.StartLevel();
            await Wait(() => ui.play.activeInHierarchy && ui.Round != null && !ui.IsLoadingLevel, "real level opens");
            Canvas.ForceUpdateCanvases();
            var progress = (RectTransform)ui.progressDots[0].transform.parent;
            var board = (RectTransform)ui.upperImage.transform.parent;
            var progressBottom = ui.stage.InverseTransformPoint(progress.TransformPoint(progress.rect.min)).y;
            var boardTop = ui.stage.InverseTransformPoint(board.TransformPoint(board.rect.max)).y;
            VerifyPrefabLayout(ui);
            if (boardTop >= progressBottom)
                throw new InvalidOperationException("The prefab places the board over the progress row.");
            var lower = (RectTransform)ui.lowerImage.transform.parent;
            var upperBottom = ui.stage.InverseTransformPoint(board.TransformPoint(board.rect.min)).y;
            var lowerTop = ui.stage.InverseTransformPoint(lower.TransformPoint(lower.rect.max)).y;
            var lowerBottom = ui.stage.InverseTransformPoint(lower.TransformPoint(lower.rect.min)).y;
            var hint = (RectTransform)ui.hintButton.transform;
            var hintTop = ui.stage.InverseTransformPoint(hint.TransformPoint(hint.rect.max)).y;
            if (!ui.upperImage.gameObject.activeInHierarchy || !ui.lowerImage.gameObject.activeInHierarchy ||
                !ui.upperImage.sprite || !ui.lowerImage.sprite || lowerTop > upperBottom + .1f ||
                hintTop > lowerBottom || lowerBottom < ui.stage.rect.yMin)
                throw new InvalidOperationException("Boards are missing, overlapping, outside the stage or covered by the hint button.");
            typeof(UIDifferences).GetMethod("ShowCross", Private).Invoke(ui, new object[] { .5f, .5f });
            if (!ui.crossTop.gameObject.activeSelf || !ui.crossBottom.gameObject.activeSelf)
                throw new InvalidOperationException("Bound miss feedback did not appear.");
            await Task.Delay(750);
            if (ui.crossTop.gameObject.activeSelf || ui.crossBottom.gameObject.activeSelf)
                throw new InvalidOperationException("Bound miss feedback did not clear.");
            var result = $"PASS: real level {ui.SelectedLevel + 1}, {ui.Round.Total} dots; board below progress by {progressBottom - boardTop:F2}; authored prefab layout and UI sprites preserved.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { running = false; }
    }

    [MenuItem("Tools/Find Differences/Check UI Initialization Only")]
    public static void CheckInitializationOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-initialization.txt");
        try
        {
            if (!(bool)Field(ui, "interactionsBound") || !(Field(ui, "hintHand") is DifferenceHintHand) ||
                !(Field(ui, "hintSpotlight") is DifferenceHintSpotlight) || !(Field(ui, "homeDesign") is GameObject))
                throw new InvalidOperationException("UI initialization did not finish.");
            var stage = ui.stage;
            var min = stage.anchorMin; var max = stage.anchorMax; var size = stage.sizeDelta;
            var scale = stage.localScale; var position = stage.anchoredPosition;
            var update = typeof(UIDifferences).GetMethod("LateUpdate", Private);
            try
            {
                stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f); stage.sizeDelta = Vector2.zero;
                stage.ForceUpdateRectTransforms(); update.Invoke(ui, null);
                if (stage.localScale != scale) throw new InvalidOperationException("Zero-sized stage changed scale.");
                typeof(UIDifferences).GetField("interactionsBound", Private).SetValue(ui, false);
                update.Invoke(ui, null);
            }
            finally
            {
                stage.anchorMin = min; stage.anchorMax = max; stage.sizeDelta = size;
                stage.localScale = scale; stage.anchoredPosition = position;
                typeof(UIDifferences).GetField("interactionsBound", Private).SetValue(ui, true);
            }
            update.Invoke(ui, null);
            if (stage.rect.width <= 0 || stage.rect.height <= 0 || float.IsNaN(stage.localScale.x) || float.IsInfinity(stage.localScale.x))
                throw new InvalidOperationException("Invalid runtime stage dimensions or scale.");
            var result = "PASS: generated bindings and UI initialization succeeded; hint effects created; zero-size layout and pre-initialization updates are safe; authored stage restored.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Check Board Coordinates Only")]
    public static void CheckBoardCoordinatesOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || ui.Round == null) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-board-coordinates.txt");
        try
        {
            var data = (DifferenceLevel)typeof(UIDifferences).GetProperty("CurrentLevel", Private).GetValue(ui);
            var originals = (Image[])Field(ui, "originalPatchImages");
            var flashes = (Image[])Field(ui, "changedFlashImages");
            foreach (var board in new[] { ui.upperImage, ui.lowerImage })
            {
                var rect = board.rectTransform;
                var size = rect.sizeDelta; var pivot = rect.pivot; var position = rect.anchoredPosition; var scale = rect.localScale;
                try
                {
                    for (var variant = 0; variant < 3; variant++)
                    {
                        if (variant > 0)
                        {
                            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, variant == 1 ? 800 : 360);
                            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, variant == 1 ? 350 : 640);
                            rect.pivot = variant == 1 ? new Vector2(.31f, .73f) : new Vector2(.5f, .5f);
                            rect.localScale = variant == 1 ? new Vector3(1.7f, .8f, 1) : Vector3.one;
                        }
                        rect.ForceUpdateRectTransforms();
                        for (var i = 0; i < data.regions.Length; i++)
                        {
                            var spot = ui.spots[i];
                            var marker = (board == ui.upperImage ? ui.topRings : ui.bottomRings)[i].rectTransform;
                            AssertBoardPoint(rect, marker.TransformPoint(marker.rect.center), new Vector2(spot.x, spot.y), "found ring");
                            foreach (var pictures in new[] { ui.patchImages, originals, flashes })
                            {
                                var picture = pictures[i].rectTransform;
                                var crop = (RectTransform)picture.parent;
                                if (crop.parent != rect) continue;
                                var r = data.regions[i];
                                AssertBoardPoint(rect, crop.TransformPoint(new Vector2(crop.rect.xMin, crop.rect.yMax)),
                                    new Vector2(r.x - r.z / 2, r.y - r.w / 2), "crop top-left");
                                AssertBoardPoint(rect, crop.TransformPoint(new Vector2(crop.rect.xMax, crop.rect.yMin)),
                                    new Vector2(r.x + r.z / 2, r.y + r.w / 2), "crop bottom-right");
                                var cropped = pictures != originals && data.croppedPatches != null;
                                AssertBoardPoint(rect, picture.TransformPoint(new Vector2(picture.rect.xMin, picture.rect.yMax)),
                                    cropped ? new Vector2(r.x - r.z / 2, r.y - r.w / 2) : Vector2.zero, "picture top-left");
                                AssertBoardPoint(rect, picture.TransformPoint(new Vector2(picture.rect.xMax, picture.rect.yMin)),
                                    cropped ? new Vector2(r.x + r.z / 2, r.y + r.w / 2) : Vector2.one, "picture bottom-right");
                            }
                            var canvas = board.GetComponentInParent<Canvas>();
                            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                            var screen = RectTransformUtility.WorldToScreenPoint(camera, marker.TransformPoint(marker.rect.center));
                            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out var local))
                                throw new InvalidOperationException("Screen click conversion failed.");
                            var point = new Vector2((local.x - rect.rect.xMin) / rect.rect.width, 1 - (local.y - rect.rect.yMin) / rect.rect.height);
                            var round = new Hotfix.Manager.DifferenceRound(new[] {spot}, UIDifferences.ImageAspect);
                            if (round.Click(point.x, point.y) != 0) throw new InvalidOperationException("Visual marker does not match hit coordinates.");
                        }
                        typeof(UIDifferences).GetMethod("ShowCross", Private).Invoke(ui, new object[] {.73f, .29f});
                        var cross = board == ui.upperImage ? ui.crossTop.rectTransform : ui.crossBottom.rectTransform;
                        AssertBoardPoint(rect, cross.TransformPoint(cross.rect.center), new Vector2(.73f, .29f), "miss feedback");
                    }
                }
                finally { rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; rect.localScale = scale; }
            }
            var result = $"PASS: {data.regions.Length} spots on both boards; authored size, 800x350 and 360x640; changed pivots and zoom; crops, flash pictures, rings, screen hit coordinates and miss feedback aligned. Prefab unchanged.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
    }

    static void AssertBoardPoint(RectTransform board, Vector3 world, Vector2 expected, string label)
    {
        var point = board.InverseTransformPoint(world);
        var normalized = new Vector2((point.x - board.rect.xMin) / board.rect.width, (board.rect.yMax - point.y) / board.rect.height);
        if (Vector2.Distance(normalized, expected) > .001f)
            throw new InvalidOperationException(label + " mismatch: " + normalized + " expected " + expected);
    }

    static void VerifyPrefabLayout(UIDifferences ui)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab").GetComponent<UIDifferences>();
        if (ui.stage.sizeDelta != prefab.stage.sizeDelta) throw new InvalidOperationException("Stage size overwritten at runtime.");
        foreach (var path in new[] { "Play", "Play/Background", "Play/Back", "Play/Zoom", "Play/Hint", "Play/Hint/Bulb", "Play/Frame", "Play/Progress", "Play/UpperViewport", "Play/LowerViewport" })
        {
            var actual = (RectTransform)ui.stage.Find(path);
            var expected = (RectTransform)prefab.stage.Find(path);
            // Layout/AspectRatio fitters legitimately drive runtime RectTransform values.
            if (!actual.drivenByObject && (actual.anchorMin != expected.anchorMin || actual.anchorMax != expected.anchorMax ||
                actual.pivot != expected.pivot || actual.anchoredPosition != expected.anchoredPosition ||
                actual.sizeDelta != expected.sizeDelta || actual.localScale != expected.localScale))
                throw new InvalidOperationException("Prefab layout overwritten: " + path);
            var image = actual.GetComponent<Image>();
            var source = expected.GetComponent<Image>();
            if (image && source && image.sprite != source.sprite)
                throw new InvalidOperationException("Prefab sprite overwritten: " + path);
        }
    }

    static void Tick()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request)) { File.Delete(Request); Start(); }
        if (!SessionState.GetBool(Pending, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (EditorApplication.isPlaying && !ui && !launcherCaptured &&
            (GameObject.Find("UILauncher(Clone)") || GameObject.Find("UILauncher")))
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "00-launcher.png"));
            launcherCaptured = true;
        }
        if (EditorApplication.isPlaying && ui) { SessionState.SetBool(Pending, false); Run(ui); }
        else if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool(Pending, false);
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "FAIL: Launcher did not create UIDifferences in 90 seconds.\n");
            EditorApplication.isPlaying = false;
        }
    }

    static async void Run(UIDifferences ui)
    {
        running = true;
        var saved = new Dictionary<string, object>();
        var settings = GameApp.Setting;
        var oldUrl = ui.levelApiUrl;
        var oldTesting = (bool)Field(ui, "testing");
        var errors = new List<string>();
        Application.LogCallback collect = (message, trace, type) =>
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); };
        Application.logMessageReceived += collect;
        try
        {
            foreach (var key in IntKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetInt(UIDifferences.Key + key);
            foreach (var key in BoolKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetBool(UIDifferences.Key + key);
            foreach (var key in StringKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetString(UIDifferences.Key + key);
            Hotfix.Manager.DifferenceRound.SelfCheck();
            ui.SetTesting(true); ui.ShowHome();
            foreach (var key in IntKeys) settings.RemoveSetting(UIDifferences.Key + key);
            settings.RemoveSetting(UIDifferences.Key + "RoundContent");
            settings.SetInt(UIDifferences.Key + "Coins", 300);
            ui.levelApiUrl = ""; ui.OnOpen(null);
            await Capture("01-home");
            Check(Active(ui, "homeDesign"), "new home visible");
            VerifyPrefabLayout(ui);
            Check(!ui.shopButton.gameObject.activeInHierarchy && !ui.trophyButton.gameObject.activeInHierarchy,
                "shop and ranking navigation hidden");
            foreach (var oldButton in ui.home.GetComponentsInChildren<Button>(true))
                Check(!oldButton.gameObject.activeInHierarchy, "old home button excluded from pointer and keyboard navigation: " + oldButton.name);

            await Click(ui.stage.Find("FigmaHome/Settings").GetComponent<Button>());
            await Task.Delay(350); await Capture("02-settings");
            Check(Active(ui, "settingsDesign"), "new settings visible");
            var oldSound = settings.GetBool(UIDifferences.Key + "Sound", true);
            var soundButton = ui.stage.Find("FigmaSettings/Card/Sound");
            Check(soundButton && soundButton.GetComponent<Button>(), "settings sound button exists");
            await Click(soundButton.GetComponent<Button>());
            Check(settings.GetBool(UIDifferences.Key + "Sound", true) != oldSound, "sound toggle saves new state");
            await Capture("03-settings-toggle");
            await Click(ui.stage.Find("FigmaSettings/Close").GetComponent<Button>());

            ui.SetTesting(false);
            await Click(ui.stage.Find("FigmaHome/Start").GetComponent<Button>());
            Check(Active(ui, "loadingDesign"), "local load shows loading page");
            var spinner = (RectTransform)((GameObject)Field(ui, "loadingMagnifier")).transform;
            await Task.Delay(40);
            var rotation = spinner.localRotation;
            var center = (Vector2)Field(ui, "loadingMagnifierCenter");
            var offset = spinner.anchoredPosition - center;
            await Task.Delay(120);
            var nextOffset = spinner.anchoredPosition - center;
            Check(Vector2.Distance(offset, nextOffset) > 1 && offset.magnitude > 1 &&
                Mathf.Abs(offset.magnitude - nextOffset.magnitude) < .1f &&
                offset.x * nextOffset.y - offset.y * nextOffset.x < 0, "loading magnifier moves clockwise on a circular path");
            Check(Quaternion.Angle(rotation, spinner.localRotation) < .1f, "loading magnifier keeps its image orientation");
            await Capture("04-loading");
            ui.ShowHome(); await Task.Delay(1000);
            Check(ui.home.activeSelf && ui.Round == null && !Active(ui, "loadingDesign"), "cancelled load cannot reopen gameplay");
            await Click(ui.stage.Find("FigmaHome/Start").GetComponent<Button>());
            await Wait(() => ui.play.activeSelf && ui.Round != null && !Active(ui, "loadingDesign"), "local level opens");
            await Capture("05-gameplay");
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y, true);
            Check(ui.Round.Count == 1 && ui.topRings[0].isActiveAndEnabled && ui.bottomRings[0].isActiveAndEnabled, "correct click adds both rings");
            var flight = ui.play.GetComponentInChildren<DifferenceFoundFlight>();
            Check(flight, "correct click launches progress flight");
            Check(flight.material.shader.isSupported, "flight shader supports the current graphics device");
            var patch = ((Image[])Field(ui, "originalPatchImages"))[0];
            await Task.Delay(200);
            Check(patch.isActiveAndEnabled && patch.color.a > 0, "difference crop visibly flashes after correct click");
            await Capture("06-found-effect");
            await Task.Delay(2200);
            Check(ui.progressLabel.text.StartsWith("1 /"), "flight updates progress");
            Check(!patch.isActiveAndEnabled && ui.topRings[0].isActiveAndEnabled && ui.bottomRings[0].isActiveAndEnabled,
                "crop flash clears while both found rings remain");
            for (var i = 1; i < ui.Round.Total; i++) ui.ClickImage(ui.spots[i].x, ui.spots[i].y);
            await Wait(() => (bool)Field(ui, "figmaResultActive"), "victory appears after last effect");
            Check((bool)Field(ui, "figmaResultWon") && Active(ui, "victoryDesign"), "victory visual active");
            await Capture("07-victory");
            var previousLevel = ui.SelectedLevel;
            await Click(ui.stage.Find("FigmaVictory/Next").GetComponent<Button>());
            await Wait(() => ui.Round != null && !ui.Round.Finished && !Active(ui, "loadingDesign"), "next level opens");
            Check(ui.SelectedLevel == previousLevel + 1 && !(bool)Field(ui, "figmaResultActive"), "victory next level advances once");
            for (var i = 0; i < 3; i++) { ui.ClickImage(.99f, .02f); await Task.Delay(400); }
            await Wait(() => (bool)Field(ui, "figmaResultActive"), "failure appears");
            Check(ui.Round.Lives == 0 && !(bool)Field(ui, "figmaResultWon") && Active(ui, "failDesign"), "failure visual active");
            await Task.Delay(350); await Capture("08-failure");
            var failedLevel = ui.SelectedLevel;
            await Click(ui.stage.Find("FigmaFail/Card/Retry").GetComponent<Button>());
            await Wait(() => ui.Round != null && !ui.Round.Finished && !Active(ui, "loadingDesign"), "retry opens");
            Check(ui.SelectedLevel == failedLevel && ui.Round.Count == 0 && ui.Round.Lives == 3, "failure retry resets the same level");
            await Capture("09-retry");
            ui.ShowHome();
            settings.SetInt(UIDifferences.Key + "Hints", 0);
            settings.SetString(UIDifferences.Key + "GiftDate", "");
            ui.OnOpen(null);
            await Click(ui.stage.Find("FigmaHome/Start").GetComponent<Button>());
            await Wait(() => ui.Round != null && !Active(ui, "loadingDesign"), "hint test level opens");
            await Click(ui.hintButton);
            Check(Active(ui, "hintDesign"), "empty hints open new hint popup");
            await Task.Delay(350); await Capture("10-hint");
            await Click(ui.stage.Find("FigmaHint/Card/Free").GetComponent<Button>());
            Check(!Active(ui, "hintDesign") && ui.HintActive && ui.Hints == 0, "free hint opens spotlight and consumes exactly one hint");
            await Task.Delay(450); await Capture("11-hint-spotlight");
            var spotlight = ui.play.GetComponentInChildren<DifferenceHintSpotlight>();
            var rendered = spotlight.canvasRenderer.GetMaterial(0);
            File.AppendAllText(Path.Combine(Folder, "report.txt"),
                "Spotlight diagnostic: color=" + spotlight.color + " rect=" + spotlight.rectTransform.rect +
                " rendererCull=" + spotlight.canvasRenderer.cull + " depth=" + spotlight.depth + " shader=" + rendered.shader.name +
                " supported=" + rendered.shader.isSupported + " keywords=" + string.Join(",", rendered.shaderKeywords) +
                " _Size=" + rendered.GetVector("_Size") + " _HoleA=" + rendered.GetVector("_HoleA") +
                " _HoleB=" + rendered.GetVector("_HoleB") +
                " baseSize=" + spotlight.material.GetVector("_Size") + "\n");
            foreach (var shaderName in new[] { "HintSpotlight", "FoundGlow" })
            {
                var local = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Bundles/UI/UIDifferences/Art/" + shaderName + ".shader");
                var loaded = shaderName == "HintSpotlight" ? ui.hintSpotlightMaterial.shader : ui.foundFlightMaterial.shader;
                File.AppendAllText(Path.Combine(Folder, "report.txt"), shaderName + ": runtime=" + loaded.GetInstanceID() +
                    ", runtimePath=" + AssetDatabase.GetAssetPath(loaded) + ", runtimeSupported=" + loaded.isSupported +
                    ", local=" + local.GetInstanceID() + ", localSupported=" + local.isSupported + "\n");
                foreach (var message in ShaderUtil.GetShaderMessages(local))
                    File.AppendAllText(Path.Combine(Folder, "report.txt"), "Shader compiler: " + message.severity + " " + message.message + "\n");
            }
            Check(rendered.shader.isSupported, "spotlight shader supports the current graphics device");
            ui.ShowHome();
            settings.SetInt(UIDifferences.Key + "Completed", ui.levels.Length);
            settings.SetInt(UIDifferences.Key + "RoundLevel", -1);
            ui.OnOpen(null);
            await Click(ui.stage.Find("FigmaHome/Start").GetComponent<Button>());
            await Wait(() => ui.Round != null && !Active(ui, "loadingDesign"), "completed game can restart");
            Check(ui.SelectedLevel == 0 && ui.Round.Count == 0 && !ui.album.activeSelf,
                "Start after all local levels replays level zero without opening the hidden album");
            await Capture("12-completed-replay");
            Check(errors.Count == 0, "no runtime errors: " + string.Join(" | ", errors));
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "PASS: all UI smoke checks completed.\n");
        }
        catch (Exception error)
        {
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "FAIL: " + error + "\n");
            Debug.LogException(error);
            await Capture("failure-state");
        }
        finally
        {
            Application.logMessageReceived -= collect;
            if (ui) ui.ShowHome();
            foreach (var key in IntKeys) { if (saved.TryGetValue(key, out var value)) settings.SetInt(UIDifferences.Key + key, (int)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            foreach (var key in BoolKeys) { if (saved.TryGetValue(key, out var value)) settings.SetBool(UIDifferences.Key + key, (bool)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            foreach (var key in StringKeys) { if (saved.TryGetValue(key, out var value)) settings.SetString(UIDifferences.Key + key, (string)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            settings.Save();
            if (ui) { ui.levelApiUrl = oldUrl; ui.SetTesting(oldTesting); ui.OnOpen(null); }
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "Original save restored.\n");
            running = false; EditorApplication.isPlaying = false;
        }
    }

    static async Task Click(Button button)
    {
        await Task.Delay(100); Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "actual raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    static async Task Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var path = Path.Combine(Folder, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await Task.Delay(250);
        if (!File.Exists(path)) throw new InvalidOperationException("Game View did not produce screenshot: " + name);
    }

    static async Task Wait(Func<bool> condition, string message)
    {
        var end = DateTime.UtcNow.AddSeconds(15);
        while (EditorApplication.isPlaying && !condition() && DateTime.UtcNow < end) await Task.Delay(50);
        Check(condition(), message);
    }
    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static bool Active(UIDifferences ui, string name) => Field(ui, name) is GameObject go && go.activeInHierarchy;
    static void Check(bool success, string message)
    {
        File.AppendAllText(Path.Combine(Folder, "report.txt"), (success ? "OK: " : "FAILED: ") + message + "\n");
        if (!success) throw new InvalidOperationException(message);
    }
}
#endif
