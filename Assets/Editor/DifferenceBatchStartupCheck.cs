#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameFrameX.UI.Runtime;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DifferenceBatchStartupCheck
{
    const string Pending = "FindDifferences.BatchStartup.Pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string Report => Path.GetFullPath(Path.Combine(Application.dataPath, transitionOnly ? "../Temp/FindDifferences-cached-transition.txt" : "../Temp/FindDifferences-batch-startup.txt"));
    static readonly PropertyInfo HomeReadyProperty = Type.GetType("GameFrameX.Startup.Application.ApplicationStartupEntry, GameFrameX.Startup.Application", true).GetProperty("HomeReady");
    static readonly FieldInfo LauncherField = Type.GetType("GameFrameX.Startup.Application.LauncherUIHandler, GameFrameX.Startup.Application", true).GetField("_ui", BindingFlags.Static | BindingFlags.NonPublic);
    static double startedAt, warmupAt, warmupSeconds, readyAt;
    static bool sawLogo, sawWarmup, warmupEnded, checkedCache;
    static int number;
    static string startupWarning, runtimeError;
    static readonly string[] ProgressInts = { "Completed", "JourneyCompleted", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "TutorialStep" };
    static readonly string[] ProgressStrings = { "RoundContent", "AnalyticsRound" };
    static Dictionary<string, object> progressBackup;
    static bool transitionOnly, transitionStarted, animationMoved;
    static int transitionLevel, sampledFrame = -1, loadingSamples;
    static double transitionAt, previousFrameAt, maxFrameInterval;
    static Vector2 firstMagnifier;
    static float firstFill;

    static DifferenceBatchStartupCheck()
    {
        if (SessionState.GetBool(Pending, false)) Observe();
    }

    [MenuItem("Tools/Find Differences/Check Main Batch Startup Only")]
    public static void Run() => Start(false);

    [MenuItem("Tools/Find Differences/Check Cached Level Transition Only")]
    public static void CheckCachedLevelTransitionOnly() => Start(true);

    static void Start(bool checkTransition)
    {
        if (SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play before checking an actual Launcher startup.");
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/Launcher.unity")
            throw new InvalidOperationException("Open the Launcher scene before checking startup.");
        sawLogo = sawWarmup = warmupEnded = checkedCache = false;
        transitionOnly = checkTransition;
        transitionStarted = animationMoved = false; progressBackup = null;
        sampledFrame = -1; loadingSamples = 0;
        transitionAt = previousFrameAt = maxFrameInterval = 0;
        warmupAt = warmupSeconds = readyAt = 0; number = 0;
        startupWarning = runtimeError = null;
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, checkTransition ? "RUNNING: observing cached current-level loading; no differences or next-level buttons are clicked; round settings will be restored.\n" :
            "RUNNING: observing one actual startup; no settings, cache or level buttons are changed by the check.\n");
        SessionState.SetBool(Pending + ".Transition", checkTransition);
        SessionState.SetFloat(Pending + ".StartedAt", (float)EditorApplication.timeSinceStartup);
        SessionState.SetBool(Pending, true);
        Observe();
        EditorApplication.isPlaying = true;
    }

    static void Observe()
    {
        transitionOnly = SessionState.GetBool(Pending + ".Transition", false);
        startedAt = SessionState.GetFloat(Pending + ".StartedAt", (float)EditorApplication.timeSinceStartup);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }

    static void Log(string message, string trace, LogType type)
    {
        if (message.StartsWith("Startup level cache failed:", StringComparison.Ordinal)) startupWarning = message;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message + "\n" + trace;
    }

    static void PlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Pending, false))
            Finish("FAIL: Play stopped before startup checks completed.");
    }

    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static void Call(UIDifferences ui, string name) => typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, null);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static string LauncherDetails()
    {
        var details = "";
        foreach (var form in Resources.FindObjectsOfTypeAll<UIForm>())
            if (form.GetType().Name == "UILauncher")
                details += " [name=" + form.gameObject.name + ", visible=" + form.Visible + ", activeSelf=" + form.gameObject.activeSelf +
                    ", activeInHierarchy=" + form.gameObject.activeInHierarchy + ", enabled=" + form.enabled +
                    ", scene=" + form.gameObject.scene.name + ", group=" + (form.UIGroup == null ? "null" : form.UIGroup.Name) + "]";
        return "Handler _ui=" + (LauncherField.GetValue(null) == null ? "null" : LauncherField.GetValue(null).ToString()) + "; all UILauncher forms:" + details;
    }

    static void Tick()
    {
        try
        {
            var now = EditorApplication.timeSinceStartup;
            Check(now - startedAt < 30, "Actual startup did not finish within the 30-second total limit.");
            Check(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (!EditorApplication.isPlaying) return;
            var launcher = LauncherField.GetValue(null) as UIForm;
            var logoVisible = launcher && launcher.Visible && launcher.gameObject.activeInHierarchy;
            var homeReady = (bool)HomeReadyProperty.GetValue(null);
            sawLogo |= logoVisible;
            var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
            if (!ui) return;
            var warming = Field(ui, "startupDataCancellation") != null;
            if (warming)
            {
                if (transitionOnly && progressBackup == null) BackupProgress();
                if (!sawWarmup) { warmupAt = now; number = ui.SelectedLevel + 1; sawWarmup = true; }
                if (!logoVisible || homeReady)
                    Check(false, "Logo closed or HomeReady became true while batch warmup was active. logoVisible=" +
                        logoVisible + ", homeReady=" + homeReady + ", warmupElapsed=" + (now - warmupAt).ToString("F2") + "s. " + LauncherDetails());
                Check(ui.Round == null && Field(ui, "loadingLevel") == null && !ui.play.activeSelf,
                    "Startup marker failed to defer automatic first-level loading until the batch was ready.");
                Check(now - warmupAt < DifferenceBatchCache.DefaultTimeout + 2, "Batch warmup exceeded its 12-second timeout allowance.");
                return;
            }
            if (sawWarmup && !warmupEnded)
            {
                warmupEnded = true; warmupSeconds = now - warmupAt;
                File.AppendAllText(Report, "OK: Logo held and automatic level loading stayed deferred throughout warmup (" + warmupSeconds.ToString("F2") + "s).\n");
            }
            if (!homeReady)
            {
                if (transitionOnly && warmupEnded) ObserveTransition(ui, now, false);
                return;
            }
            Check(sawLogo && sawWarmup && warmupEnded && !logoVisible, "HomeReady must follow the observed warmup and Logo close.");
            Check(warmupSeconds < DifferenceBatchCache.DefaultTimeout + 2, "Completed batch warmup exceeded its timeout allowance.");
            Check(!ui.modal.activeSelf, "Startup left a terminal modal open.");
            if (readyAt == 0) readyAt = now;
            if (startupWarning != null)
            {
                Check(ui.home.activeInHierarchy && ui.Round == null && !ui.IsLoadingLevel && !ui.play.activeSelf &&
                    !(bool)Field(ui, "tutorialPending"), "Failed warmup did not return to an usable Home without an automatic tutorial round.");
                var toastVisible = ui.noticeLabel.transform.parent.gameObject.activeInHierarchy &&
                    ui.noticeLabel.text.Contains("Unable to load levels.");
                Check(toastVisible || now - readyAt < 2, "Failed warmup did not show its toast after HomeReady.");
                if (!toastVisible) return;
                Check(!transitionOnly, "Startup failed; cached transition could not be exercised. " + startupWarning);
                Finish("PASS: first startup failure closed Logo, returned to Home, cleared tutorial blocking and showed a toast without a terminal dialog.\n" + startupWarning);
                return;
            }
            if (!checkedCache)
            {
                using (var cache = new DifferenceBatchCache())
                    Check(cache.TryRead(number, out _, out _, out _, out _), "Successful warmup did not leave the requested level readable from its committed batch.");
                checkedCache = true;
            }
            if (transitionOnly) { ObserveTransition(ui, now, true); return; }
            if (ui.IsLoadingLevel) return;
            Check(ui.Round != null ? ui.play.activeInHierarchy : ui.home.activeInHierarchy,
                "Successful warmup did not reach Home or the automatic first tutorial round.");
            Finish("PASS: Logo waited for the committed batch; requested level " + number + " is readable; HomeReady followed warmup and Logo close; normal startup reached its page without a terminal dialog.");
        }
        catch (Exception error) { Finish("FAIL: " + error); }
    }

    static void BackupProgress()
    {
        progressBackup = new Dictionary<string, object>();
        var settings = GameApp.Setting;
        foreach (var name in ProgressInts)
            if (settings.HasSetting(UIDifferences.Key + name)) progressBackup[name] = settings.GetInt(UIDifferences.Key + name);
        foreach (var name in ProgressStrings)
            if (settings.HasSetting(UIDifferences.Key + name)) progressBackup[name] = settings.GetString(UIDifferences.Key + name);
        if (settings.HasSetting(UIDifferences.Key + "TutorialComplete"))
            progressBackup["TutorialComplete"] = settings.GetBool(UIDifferences.Key + "TutorialComplete");
    }

    static void ObserveTransition(UIDifferences ui, double now, bool homeReady)
    {
        if (!transitionStarted)
        {
            if (!homeReady && !ui.IsLoadingLevel) return;
            Check(progressBackup != null, "Current-round settings were not captured before automatic loading.");
            transitionStarted = true; transitionAt = previousFrameAt = now;
            transitionLevel = ui.SelectedLevel;
            Check(transitionLevel + 1 == number && !ui.JourneyPlaying, "Transition must restore the current main level.");
            if (!ui.IsLoadingLevel && ui.Round == null) ui.StartLevel();
        }
        Check(ui.SelectedLevel == transitionLevel, "Loading changed the selected level number.");
        if (Time.frameCount != sampledFrame)
        {
            maxFrameInterval = Math.Max(maxFrameInterval, now - previousFrameAt);
            previousFrameAt = now; sampledFrame = Time.frameCount;
            if (ui.IsLoadingLevel)
            {
                Check(((GameObject)Field(ui, "loadingDesign")).activeInHierarchy, "Loading UI is hidden while the current level loads.");
                var position = ((RectTransform)((GameObject)Field(ui, "loadingMagnifier")).transform).anchoredPosition;
                var width = ((UnityEngine.UI.Image)Field(ui, "loadingFill")).rectTransform.rect.width;
                if (loadingSamples++ == 0) { firstMagnifier = position; firstFill = width; }
                else animationMoved |= (position - firstMagnifier).sqrMagnitude > .01f || Mathf.Abs(width - firstFill) > .01f;
            }
        }
        if (!homeReady || ui.IsLoadingLevel) return;
        Check(ui.play.activeInHierarchy && ui.Round != null, "The cached current level did not enter gameplay.");
        Check(loadingSamples >= 2 && animationMoved, "Loading animation did not visibly change across actual Play frames.");
        var restored = progressBackup.TryGetValue("RoundLevel", out var savedLevel) && (int)savedLevel == transitionLevel;
        var mask = restored && progressBackup.TryGetValue("RoundMask", out var savedMask) ? (int)savedMask : 0;
        var lives = restored && progressBackup.TryGetValue("RoundLives", out var savedLives) ? (int)savedLives : 3;
        Check(ui.Round.FoundMask == mask && ui.Round.Lives == lives, "Opening the current level changed its saved differences or lives.");
        Finish("PASS: cached level " + number + " entered gameplay without changing level, found mask or lives; loading animated across " +
            loadingSamples + " distinct Play frames. Transition=" + (now - transitionAt).ToString("F3") + "s; maximum sampled frame interval=" +
            (maxFrameInterval * 1000).ToString("F2") + "ms (recorded, not capped).");
    }

    static void Finish(string result)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= PlayState;
        Application.logMessageReceived -= Log;
        if (transitionOnly && progressBackup != null)
        {
            try
            {
                var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
                if (ui) { Call(ui, "CancelStartupData"); Call(ui, "StopTutorial"); Call(ui, "ShowHomeInternal"); }
                var settings = GameApp.Setting;
                foreach (var name in ProgressInts)
                    if (progressBackup.TryGetValue(name, out var integer)) settings.SetInt(UIDifferences.Key + name, (int)integer);
                    else settings.RemoveSetting(UIDifferences.Key + name);
                foreach (var name in ProgressStrings)
                    if (progressBackup.TryGetValue(name, out var text)) settings.SetString(UIDifferences.Key + name, (string)text);
                    else settings.RemoveSetting(UIDifferences.Key + name);
                if (progressBackup.TryGetValue("TutorialComplete", out var tutorial)) settings.SetBool(UIDifferences.Key + "TutorialComplete", (bool)tutorial);
                else settings.RemoveSetting(UIDifferences.Key + "TutorialComplete");
                settings.Save(); progressBackup = null;
                result += "\nOriginal current-round settings restored before stopping Play.";
            }
            catch (Exception error) { result += "\nFAIL restoring original current-round settings: " + error; }
        }
        File.AppendAllText(Report, result + "\nTotal observed time: " + (EditorApplication.timeSinceStartup - startedAt).ToString("F2") + "s. Returning to Edit Mode.\n");
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
    }
}
#endif
