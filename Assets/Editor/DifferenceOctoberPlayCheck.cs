#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class DifferenceOctoberPlayCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-october-preview"));
    static readonly string[] IntKeys = { "Completed", "JourneyCompleted", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "TutorialStep", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "Journey.RoundLevel", "Journey.RoundMask", "Journey.RoundLives", "Journey.RoundMistakes", "Journey.RoundHint" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration", "TutorialComplete" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate", "RoundContent", "AnalyticsRound", "Journey.RoundContent", "Journey.AnalyticsRound" };
    static bool running, pauseReturnOnly, journeyLoadFailureOnly;

    [MenuItem("Tools/Find Differences/Check Pause Return Only")]
    public static void CheckPauseReturnOnly()
    {
        if (running) return;
        pauseReturnOnly = true;
        Run();
    }

    [MenuItem("Tools/Find Differences/Check Journey Load Failure Only")]
    public static void CheckJourneyLoadFailureOnly()
    {
        if (running) return;
        journeyLoadFailureOnly = true;
        Run();
    }

    [MenuItem("Tools/Find Differences/Run October Play Checks")]
    public static async void Run()
    {
        if (running) return;
        var checkPauseReturn = pauseReturnOnly;
        var checkJourneyLoadFailure = journeyLoadFailureOnly;
        pauseReturnOnly = journeyLoadFailureOnly = false;
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("Start Launcher before running October play checks.");
        if (ui.AdInputBlocked) throw new InvalidOperationException("Wait for the active ad operation before checking.");
        Directory.CreateDirectory(Folder);
        var report = Path.Combine(Folder, "report.txt");
        File.WriteAllText(report, "RUNNING: October UI, Journey HTTP and save-isolation checks.\n");
        Call(ui, "SaveRound");
        var settings = GameApp.Setting;
        var saved = new Dictionary<string, object>();
        foreach (var name in IntKeys) if (settings.HasSetting(UIDifferences.Key + name)) saved[name] = settings.GetInt(UIDifferences.Key + name);
        foreach (var name in BoolKeys) if (settings.HasSetting(UIDifferences.Key + name)) saved[name] = settings.GetBool(UIDifferences.Key + name);
        foreach (var name in StringKeys) if (settings.HasSetting(UIDifferences.Key + name)) saved[name] = settings.GetString(UIDifferences.Key + name);
        var previousTesting = (bool)Field(ui, "testing");
        var previousEnabled = ui.enabled;
        var analytics = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("GameFrameX.Startup.Application.DifferenceAnalytics")).First(type => type != null);
        var analyticsFields = new Dictionary<FieldInfo, object>();
        foreach (var name in new[] { "pending", "initialized", "draining", "firebaseSender", "adjustSender", "activeSeconds", "unsavedSeconds" })
        {
            var field = analytics.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            analyticsFields[field] = field.GetValue(null);
        }
        var pendingKey = "DifferenceAnalytics.Pending";
        var hadPending = PlayerPrefs.HasKey(pendingKey);
        var previousPending = PlayerPrefs.GetString(pendingKey, "");
        var errors = new List<string>();
        Application.LogCallback collect = (message, trace, type) =>
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message + "\n" + trace); };
        Application.logMessageReceived += collect;
        running = true;
        try
        {
            analytics.GetField("pending", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
                Activator.CreateInstance(analytics.GetField("pending", BindingFlags.Static | BindingFlags.NonPublic).FieldType, true));
            analytics.GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
            analytics.GetField("firebaseSender", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (Action<string, Dictionary<string, object>>)((_, __) => { }));
            analytics.GetField("adjustSender", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (Func<string, bool>)(_ => true));
            if (checkJourneyLoadFailure)
            {
                CheckJourneyLoadFailures(ui);
                Check(errors.Count == 0, "Journey load failure checks have no runtime errors: " + string.Join(" | ", errors));
                File.AppendAllText(report, "PASS: Journey JSON/image/empty-level failures return to an interactive map; main failures return home; URL numbering and both saves remain intact. Transport was never advanced.\n");
                return;
            }
            if (checkPauseReturn)
            {
                ui.SetTesting(true); ui.ShowHome();
                settings.SetBool(UIDifferences.Key + "TutorialComplete", true);
                settings.SetInt(UIDifferences.Key + "Completed", 20);
                settings.SetInt(UIDifferences.Key + "JourneyCompleted", 0);
                settings.SetInt(UIDifferences.Key + "RoundLevel", -1);
                settings.SetInt(UIDifferences.Key + "Journey.RoundLevel", -1);
                ui.OnOpen(null); Call(ui, "CloseAds");
                Set(ui, "completed", UIDifferences.JourneyUnlockLevel);
                void Leave()
                {
                    ui.backButton.onClick.Invoke();
                    ((UnityEngine.UI.Button)Field(ui, "pauseHomeButton")).onClick.Invoke();
                    ((UnityEngine.UI.Button)Field(ui, "leaveConfirmButton")).onClick.Invoke();
                }
                ui.SelectLevel(0);
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
                Leave();
                Check(ui.home.activeInHierarchy && !ui.JourneyVisible && !ui.JourneyPlaying && !ui.PauseInputBlocked && ui.Round == null &&
                    settings.GetInt(UIDifferences.Key + "RoundMask") == 1, "Main pause exit returns home and saves the unfinished round.");
                ui.ShowJourney();
                ((UnityEngine.UI.Button)Field(ui, "journeyPlayButton")).onClick.Invoke();
                Check(ui.JourneyPlaying && ui.Round.Count == 0, "Journey opens its own local test round.");
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
                Leave();
                Check(ui.JourneyVisible && ((GameObject)Field(ui, "journeyDesign")).activeInHierarchy && !ui.JourneyPlaying &&
                    !ui.PauseInputBlocked && ui.Round == null && !ui.play.activeSelf &&
                    settings.GetInt(UIDifferences.Key + "Journey.RoundMask") == 1 && settings.GetInt(UIDifferences.Key + "RoundMask") == 1,
                    "Journey pause exit returns to its map, closes overlays and saves Journey without changing the main round.");
                ((UnityEngine.UI.Button)Field(ui, "journeyPlayButton")).onClick.Invoke();
                Check(ui.JourneyPlaying && ui.Round.FoundMask == 1 && ui.Round.Lives == 3,
                    "Reopening Journey restores the saved difference and lives.");
                Check(errors.Count == 0, "Pause return checks have no runtime errors: " + string.Join(" | ", errors));
                File.AppendAllText(report, "PASS: main pause exit returns home; Journey pause exit returns to the map; independent progress restores. Local test levels only.\n");
                return;
            }
            ui.SetTesting(true); ui.ShowHome(); ui.SetTesting(false);
            settings.SetBool(UIDifferences.Key + "TutorialComplete", true);
            settings.SetInt(UIDifferences.Key + "Completed", 0); settings.SetInt(UIDifferences.Key + "RoundLevel", -1);
            ui.OnOpen(null); Call(ui, "CloseAds");
            var homeStartLabel = ((UnityEngine.UI.Button)Field(ui, "designStartButton")).GetComponentInChildren<UnityEngine.UI.Text>(true);
            Check(homeStartLabel.text == "Level 1" && ui.SelectedLevel == 0, "A new player's home button shows Level 1.");
            settings.SetInt(UIDifferences.Key + "Completed", 7);
            ui.OnOpen(null); Call(ui, "CloseAds");
            Check(homeStartLabel.text == "Level 8" && ui.SelectedLevel == 7, "Seven main clears display the next challenge as Level 8.");
            await Capture(ui, "home-current-level");
            settings.SetInt(UIDifferences.Key + "RoundLevel", 3); settings.SetInt(UIDifferences.Key + "RoundMask", 1);
            settings.SetInt(UIDifferences.Key + "RoundLives", 2);
            ui.OnOpen(null); Call(ui, "CloseAds");
            Check(homeStartLabel.text == "Level 4" && ui.SelectedLevel == 3, "An unfinished earlier round takes priority and displays Level 4.");
            settings.SetInt(UIDifferences.Key + "Completed", 19); settings.SetInt(UIDifferences.Key + "JourneyCompleted", 0);
            settings.SetInt(UIDifferences.Key + "RoundLevel", 20); settings.SetInt(UIDifferences.Key + "RoundMask", 1);
            settings.SetInt(UIDifferences.Key + "RoundLives", 2); settings.SetString(UIDifferences.Key + "RoundContent", "october-main-round");
            settings.SetInt(UIDifferences.Key + "Journey.RoundLevel", -1);
            settings.SetInt(UIDifferences.Key + "Coins", 10000); settings.SetInt(UIDifferences.Key + "Hints", 5);
            settings.SetInt(UIDifferences.Key + "OwnedAvatars", 249); settings.SetInt(UIDifferences.Key + "OwnedFrames", 15);
            settings.SetBool(UIDifferences.Key + "Sound", false); settings.SetBool(UIDifferences.Key + "Music", false);
            settings.SetBool(UIDifferences.Key + "TutorialComplete", true); settings.Save();
            ui.OnOpen(null); Call(ui, "CloseAds");
            var entry = (UnityEngine.UI.Button)Field(ui, "journeyEntryButton");
            var lockOverlay = (GameObject)Field(ui, "journeyLockOverlay");
            var progress = (UnityEngine.UI.Text)Field(ui, "journeyUnlockProgress");
            var bar = (UnityEngine.UI.Scrollbar)Field(ui, "journeyUnlockBar");
            var lockedBannerText = ui.stage.Find("FigmaHome/Journey/LevelTips").GetComponent<UnityEngine.UI.Text>();
            Check(lockedBannerText.text == "Complete 20 levels to unlock Christmas Carnival!" && !lockedBannerText.text.Contains("111111"),
                "Locked Journey banner uses the 20-level English unlock instruction without placeholder text.");
            Check(!entry.interactable && lockOverlay.activeSelf && progress.text == "19/20" && Mathf.Abs(bar.size - .95f) < .001f,
                "19 completed main levels keep the authored banner locked with real progress.");
            entry.onClick.Invoke(); Check(!ui.JourneyVisible, "Locked entry callback cannot bypass unlocking.");
            await Capture(ui, "home-locked");
            Set(ui, "completed", 20); Call(ui, "RefreshWallet");
            Check(entry.interactable && !lockOverlay.activeSelf && !progress.gameObject.activeSelf && bar.size == 1 &&
                ((UnityEngine.UI.Text)Field(ui, "journeyUnlockedLabel")).gameObject.activeSelf, "20 completed levels reveal the unlocked banner.");
            await Capture(ui, "home-unlocked");
            await Click(entry);
            var nodes = (UnityEngine.UI.Button[])Field(ui, "journeyLevelButtons");
            var playLabel = (UnityEngine.UI.Text)Field(ui, "journeyPlayLabel");
            Check(ui.JourneyVisible && nodes.Length == 200 && nodes[0].interactable && !nodes[1].interactable &&
                nodes[199].GetComponentInChildren<UnityEngine.UI.Text>(true).text == "200", "Journey displays 200 real node numbers and locks future levels.");
            nodes[1].onClick.Invoke(); Check(playLabel.text == "Play Level 1", "A locked node cannot change the selected level.");
            await Capture(ui, "journey");
            await Click((UnityEngine.UI.Button)Field(ui, "journeyPlayButton"));
            await Wait(() => ui && !ui.IsLoadingLevel && ui.play.activeSelf && ui.Round != null, "Christmas level 1 must load through the real HTTP endpoint.", 90);
            Check(ui.JourneyPlaying && ui.LevelUrl(0) == "https://diffapi.joystar.pro/api/christmas/levels/1" &&
                (string)Field(ui, "loadedApiUrl") == ui.LevelUrl(0) && ui.Round.Total == 15 && ui.upperImage.sprite && ui.lowerImage.sprite,
                "The real Christmas JSON and both image assets are loaded.");
            Canvas.ForceUpdateCanvases();
            Check(ui.levelLabel.isActiveAndEnabled && ui.levelLabel.text == "Level 1" && ui.levelLabel.fontSize == 60 &&
                ui.levelLabel.color == (Color)new Color32(218, 234, 255, 255) && ui.levelLabel.alignment == TextAnchor.MiddleCenter,
                "Gameplay displays the current Level in the Figma font size, color and alignment.");
            var headerCenter = ui.play.transform.InverseTransformPoint(ui.levelLabel.rectTransform.TransformPoint(ui.levelLabel.rectTransform.rect.center));
            var pauseCenter = ui.play.transform.InverseTransformPoint(ui.backButton.image.rectTransform.TransformPoint(ui.backButton.image.rectTransform.rect.center));
            Check(Mathf.Abs(headerCenter.x - ((RectTransform)ui.play.transform).rect.center.x) < .01f && Mathf.Abs(headerCenter.y - pauseCenter.y) < .01f,
                "The gameplay Level label stays horizontally centered and vertically aligned with Pause.");
            ui.noticeLabel.transform.parent.gameObject.SetActive(false);
            await Capture(ui, "gameplay-level");
            var spot = ui.spots[0]; ui.ClickImage(spot.x, spot.y);
            Check(ui.Round.FoundMask == 1, "A found Journey difference saves normal gameplay progress.");
            var round = ui.Round; var coins = ui.Coins; var hints = ui.Hints;
            await Click(ui.backButton); await Capture(ui, "pause");
            Check(ui.PauseInputBlocked && ui.Round == round, "The former back button opens Pause without discarding the round.");
            ui.ClickImage(ui.spots[1].x, ui.spots[1].y); ui.UseHint();
            Check(ui.Round.FoundMask == 1 && ui.Coins == coins && ui.Hints == hints, "Pause blocks board and hint input.");
            await Click((UnityEngine.UI.Button)Field(ui, "pauseContinueButton"));
            Check(!ui.PauseInputBlocked && ui.Round == round, "Continue returns to the same round.");
            await Click(ui.backButton); await Task.Delay(450);
            await Click((UnityEngine.UI.Button)Field(ui, "pauseHomeButton")); await Capture(ui, "leave");
            Check(((GameObject)Field(ui, "leaveDesign")).activeSelf && ui.PauseInputBlocked, "Home requests confirmation before leaving.");
            await Click((UnityEngine.UI.Button)Field(ui, "leaveKeepPlayingButton"));
            Check(!ui.PauseInputBlocked && ui.Round == round, "Keep Playing preserves the round.");
            await Click(ui.backButton); await Task.Delay(450);
            await Click((UnityEngine.UI.Button)Field(ui, "pauseHomeButton")); await Task.Delay(450);
            await Click((UnityEngine.UI.Button)Field(ui, "leaveConfirmButton"));
            Check(!ui.JourneyPlaying && ui.JourneyVisible && !ui.PauseInputBlocked && ui.Round == null && settings.GetInt(UIDifferences.Key + "Journey.RoundMask") == 1,
                "Leaving saves Journey, clears the paused round and returns to the Journey map.");
            Check(homeStartLabel.text == "Level 21" && ui.SelectedLevel == 20,
                "Returning to the Journey map preserves the saved main challenge selection.");
            CheckMainRound(settings.GetInt(UIDifferences.Key + "RoundLevel"), settings.GetInt(UIDifferences.Key + "RoundMask"),
                settings.GetInt(UIDifferences.Key + "RoundLives"), settings.GetString(UIDifferences.Key + "RoundContent"));
            await Click((UnityEngine.UI.Button)Field(ui, "journeyPlayButton"));
            await Wait(() => ui && !ui.IsLoadingLevel && ui.play.activeSelf && ui.Round != null, "Reopening Journey restores its unfinished round.", 90);
            Check(ui.Round.FoundMask == 1 && ui.Completed == 20 && ui.JourneyCompleted == 0, "Journey restoration keeps both progression counts separate.");
            DifferenceJourneyCheck.Run();
            ui.SetTesting(true); Call(ui, "CheckPauseFlow"); ui.SetTesting(false);
            var beforeJourneyWin = ui.Coins;
            for (var i = 1; i < ui.Round.Total; i++) ui.ClickImage(ui.spots[i].x, ui.spots[i].y);
            await Wait(() => (bool)Field(ui, "figmaResultActive"), "Completing the real Journey round opens Victory.", 25);
            Check(ui.JourneyPlaying && ui.JourneyCompleted == 1 && ui.Completed == 20 && ui.Coins == beforeJourneyWin + 10,
                "Journey completion unlocks its next node and grants its reward without advancing the main game.");
            await Click((UnityEngine.UI.Button)Field(ui, "designNextButton"));
            await Wait(() => ui && !ui.IsLoadingLevel && ui.play.activeSelf && ui.Round != null && ui.SelectedLevel == 1,
                "Journey Victory Next opens Christmas level 2.", 90);
            Check((string)Field(ui, "loadedApiUrl") == "https://diffapi.joystar.pro/api/christmas/levels/2" && ui.JourneyPlaying,
                "The next level stays on the Christmas endpoint.");
            Check(ui.levelLabel.text == "Level 2", "The gameplay Level label updates after advancing to the next level.");
            ui.ShowHome(); await Click(entry);
            Check(nodes[1].interactable && !nodes[2].interactable && playLabel.text == "Play Level 2",
                "Returning to the map shows the newly unlocked frontier.");
            ui.ShowHome();
            CheckMainRound(settings.GetInt(UIDifferences.Key + "RoundLevel"), settings.GetInt(UIDifferences.Key + "RoundMask"),
                settings.GetInt(UIDifferences.Key + "RoundLives"), settings.GetString(UIDifferences.Key + "RoundContent"));
            await CheckProfileRules(ui);
            var toast = ui.noticeLabel.transform.parent.GetComponent<CommonToast>();
            toast.Show("UI check");
            var toastCanvas = toast.GetComponent<Canvas>();
            var modalCanvas = ui.modal.GetComponent<Canvas>();
            var toastOwner = toast.GetComponentInParent<UIDifferences>(true);
            var toastParentCanvas = toast.transform.parent.GetComponentInParent<Canvas>(true);
            Check(toastCanvas.sortingOrder > ui.profile.GetComponent<Canvas>().sortingOrder &&
                (!modalCanvas || toastCanvas.sortingOrder > modalCanvas.sortingOrder),
                "Toast remains visible above the new popup canvases. Actual toast=" + toastCanvas.sortingOrder +
                ", modal=" + (modalCanvas ? modalCanvas.sortingOrder.ToString() : "inactive") + ", parent=" + (toastParentCanvas ? toastParentCanvas.sortingOrder.ToString() : "missing") +
                ", override=" + toastCanvas.overrideSorting + ", active=" + toast.gameObject.activeInHierarchy +
                ", owner=" + (toastOwner ? toastOwner.name : "missing") + ", sameOwner=" + (toastOwner == ui) +
                ", mappedToast=" + (toastOwner && toastOwner.noticeLabel && toastOwner.noticeLabel.transform.parent == toast.transform));
            toast.gameObject.SetActive(false);
            ui.profile.SetActive(false); ui.SetTesting(true); ui.ShowHome();
            Call(ui, "CheckTutorialPauseFlow");
            Check(!ui.PauseInputBlocked && ui.Round != null && ui.Round.Count == 0,
                "Tutorial Pause, Continue, confirmed Home, saved-step restoration and Escape all work.");
            Check(ui.levelLabel.isActiveAndEnabled && ui.levelLabel.text == "Level 1", "The main game's tutorial also displays Level 1.");
            await Click(ui.backButton); await Capture(ui, "tutorial-pause");
            await Click((UnityEngine.UI.Button)Field(ui, "pauseContinueButton"));
            Check(errors.Count == 0, "No Unity error, exception or assertion was logged: " + string.Join("\n", errors));
            File.AppendAllText(report, "PASS: all October play checks completed; screenshots are 1179x2556.\n");
        }
        catch (Exception error)
        {
            File.AppendAllText(report, "FAIL: " + error + "\n");
            Debug.LogException(error);
        }
        finally
        {
            Application.logMessageReceived -= collect;
            try
            {
                if (ui) { ui.SetTesting(true); Call(ui, "CancelRemoteLoad"); ui.ShowHome(); }
                foreach (var name in IntKeys) { if (saved.TryGetValue(name, out var value)) settings.SetInt(UIDifferences.Key + name, (int)value); else settings.RemoveSetting(UIDifferences.Key + name); }
                foreach (var name in BoolKeys) { if (saved.TryGetValue(name, out var value)) settings.SetBool(UIDifferences.Key + name, (bool)value); else settings.RemoveSetting(UIDifferences.Key + name); }
                foreach (var name in StringKeys) { if (saved.TryGetValue(name, out var value)) settings.SetString(UIDifferences.Key + name, (string)value); else settings.RemoveSetting(UIDifferences.Key + name); }
                settings.Save();
                foreach (var item in analyticsFields) item.Key.SetValue(null, item.Value);
                if (hadPending) PlayerPrefs.SetString(pendingKey, previousPending); else PlayerPrefs.DeleteKey(pendingKey);
                PlayerPrefs.Save();
                if (ui) { ui.SetTesting(previousTesting); ui.enabled = previousEnabled; ui.OnOpen(null); }
                File.AppendAllText(report, "Original settings and analytics queue restored.\n");
            }
            catch (Exception error) { File.AppendAllText(report, "FAIL restoring original state: " + error + "\n"); Debug.LogException(error); }
            finally { running = false; }
        }
    }

    static void CheckJourneyLoadFailures(UIDifferences ui)
    {
        ui.SetTesting(true); ui.ShowHome();
        var settings = GameApp.Setting;
        settings.SetBool(UIDifferences.Key + "TutorialComplete", true);
        var progress = new Dictionary<string, int>
        {
            { "Completed", 20 }, { "JourneyCompleted", 2 },
            { "RoundLevel", 7 }, { "RoundMask", 5 }, { "RoundLives", 2 }, { "RoundMistakes", 1 }, { "RoundHint", -1 },
            { "Journey.RoundLevel", 1 }, { "Journey.RoundMask", 3 }, { "Journey.RoundLives", 1 }, { "Journey.RoundMistakes", 2 }, { "Journey.RoundHint", 4 }
        };
        foreach (var item in progress) settings.SetInt(UIDifferences.Key + item.Key, item.Value);
        settings.SetString(UIDifferences.Key + "RoundContent", "load-failure-main-save");
        settings.SetString(UIDifferences.Key + "Journey.RoundContent", "load-failure-journey-save");
        ui.SetTesting(false); ui.OnOpen(null); Call(ui, "CloseAds");
        var map = (GameObject)Field(ui, "journeyDesign");
        var nodes = (UnityEngine.UI.Button[])Field(ui, "journeyLevelButtons");
        var play = (UnityEngine.UI.Button)Field(ui, "journeyPlayButton");
        var label = (UnityEngine.UI.Text)Field(ui, "journeyPlayLabel");
        ui.ShowJourney();
        Check(ui.JourneyVisible && map.activeInHierarchy, "The unlocked Journey map opens before failure checks.");
        Set(ui, "journeyPlaying", true);
        Check(ui.LevelUrl(0) == "https://diffapi.joystar.pro/api/christmas/levels/1" &&
            ui.LevelUrl(1) == "https://diffapi.joystar.pro/api/christmas/levels/2" &&
            ui.LevelUrl(199) == "https://diffapi.joystar.pro/api/christmas/levels/200", "Journey URLs use the requested level number, including levels 1, 2 and 200.");

        void Failure(bool journey, string error, string name)
        {
            Set(ui, "journeyPlaying", journey); Set(ui, "journeyVisible", false);
            Set(ui, "localLoading", false);
            var request = new DifferenceRemoteLevel { Error = error };
            Set(ui, "loadingLevel", request); Set(ui, "loadingStarted", Time.unscaledTime - 1);
            ui.startButton.interactable = false;
            var routine = (System.Collections.IEnumerator)typeof(UIDifferences).GetMethod("LoadRemoteLevel", Private)
                .Invoke(ui, new object[] { request, 1, true, false });
            try
            {
                // Skip this lazy transport iterator: neither JSON nor image HTTP is started.
                Check(routine.MoveNext() && routine.Current is System.Collections.IEnumerator && ui.IsLoadingLevel,
                    name + " reaches the transport boundary while loading.");
                Check(request.Error == error && request.Level == null, name + " keeps the injected transport result without advancing it.");
                Check(!routine.MoveNext(), name + " finishes the failure branch on its second step.");
                Check(!ui.IsLoadingLevel && Field(ui, "loadingLevel") == null && ui.startButton.interactable && ui.Round == null,
                    name + " clears loading and restores input without opening a round.");
                if (journey)
                {
                    Check(ui.JourneyVisible && !ui.JourneyPlaying && map.activeInHierarchy && !ui.play.activeSelf && play.interactable,
                        name + " returns to the Journey map.");
                    nodes[0].onClick.Invoke();
                    Check((int)Field(ui, "journeySelected") == 0 && label.text == "Play Level 1" && play.interactable,
                        name + " leaves map selection and Play usable.");
                }
                else Check(ui.home.activeInHierarchy && !ui.JourneyVisible && !ui.JourneyPlaying && !map.activeSelf,
                    name + " keeps the main-mode failure destination at Home.");
                Check(progress.All(item => settings.GetInt(UIDifferences.Key + item.Key) == item.Value) &&
                    settings.GetString(UIDifferences.Key + "RoundContent") == "load-failure-main-save" &&
                    settings.GetString(UIDifferences.Key + "Journey.RoundContent") == "load-failure-journey-save" &&
                    ui.Completed == 20 && ui.JourneyCompleted == 2, name + " preserves existing main and Journey progress.");
            }
            finally { (routine as IDisposable)?.Dispose(); request.Dispose(); }
        }
        Failure(true, "Injected JSON download failure", "Journey JSON failure");
        Failure(true, "Injected image download failure", "Journey image failure");
        Failure(true, null, "Journey empty level without Error");
        Failure(false, "Injected JSON download failure", "Main load failure");
    }

    static async Task CheckProfileRules(UIDifferences ui)
    {
        const string path = "Card/ChoicesScroll/Viewport/Choices/";
        var settings = GameApp.Setting;
        var toast = ui.noticeLabel.transform.parent.GetComponent<CommonToast>();
        await Click(ui.stage.Find("FigmaHome/Avatar").GetComponent<UnityEngine.UI.Button>());
        async Task EditNickname(string value)
        {
            var savedName = settings.GetString(UIDifferences.Key + "Nickname");
            await Click(ui.profile.transform.Find("Card/User/ConfirmName").GetComponent<UnityEngine.UI.Button>());
            await Wait(() => ui.profile.activeSelf && ui.nicknameInput.isFocused, "The pencil focuses the nickname field without closing Profile.", 3);
            Check(settings.GetString(UIDifferences.Key + "Nickname") == savedName, "The pencil starts editing without saving a nickname.");
            ui.nicknameInput.text = value;
            ui.nicknameInput.onEndEdit.Invoke(ui.nicknameInput.text);
        }
        var wallet = ui.profile.transform.Find("Wallet/Coins").GetComponent<UnityEngine.UI.Text>();
        Check(wallet.text == ui.Coins.ToString(), "Opening Profile shows the current wallet balance.");
        var originalCoins = ui.Coins;
        Check(ui.avatarSprites.Length == 38, "Profile contains the complete 38-avatar Figma catalogue.");
        for (var i = 0; i < ui.avatarSprites.Length; i++)
        {
            var choice = ui.profile.transform.Find(path + "Choice" + i);
            Check(choice.gameObject.activeSelf && !choice.Find("Lock").gameObject.activeSelf && !choice.Find("Price").gameObject.activeSelf,
                "Avatar " + i + " is available for free without a lock or price.");
        }
        await Capture(ui, "profile");
        Call(ui, "SelectProfileChoice", 31); Call(ui, "SelectProfileChoice", 32);
        ui.profileChoicesScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
        await Click(ui.profile.transform.Find(path + "Choice37").GetComponent<UnityEngine.UI.Button>());
        Check(!ui.modal.activeSelf && ui.Coins == originalCoins && settings.GetInt(UIDifferences.Key + "Avatar") == 37,
            "Avatars beyond index 31 are free, selectable and persisted without bit-mask limits.");
        await Capture(ui, "profile-bottom");
        var originalNickname = ui.nicknameInput.text;
        await EditNickname(" ");
        Check(ui.profile.activeSelf && !ui.modal.activeSelf && toast.gameObject.activeSelf && ui.noticeLabel.text.Contains("nickname"),
            "An empty nickname uses the common toast and preserves Profile.");
        Check(ui.nicknameInput.text == originalNickname, "An empty nickname restores the previous name.");
        Set(ui, "completed", 0); Set(ui, "journeyCompleted", 0); Call(ui, "RefreshWallet");
        await Click(ui.profile.transform.Find("Card/FrameTab").GetComponent<UnityEngine.UI.Button>());
        for (var i = 0; i < 4; i++)
        {
            var before = ui.Coins; Call(ui, "SelectProfileChoice", i);
            Check(ui.Coins == before && !ui.profile.transform.Find(path + "Choice" + i + "/Lock").gameObject.activeSelf,
                "First-row frame " + i + " is free.");
        }
        Call(ui, "SelectProfileChoice", 0); toast.gameObject.SetActive(false);
        await Capture(ui, "frames");
        ui.profileChoicesScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
        await Capture(ui, "frames-levels");
        ui.profileChoicesScroll.verticalNormalizedPosition = 1;
        var prices = new[] { 288, 888, 1588, 2588 };
        for (var index = 4; index <= 7; index++)
        {
            var price = prices[index - 4];
            Set(ui, "coins", price - 1); Call(ui, "RefreshWallet"); Call(ui, "SelectProfileChoice", index);
            Check(!ui.modal.activeSelf && ui.Coins == price - 1 && toast.gameObject.activeSelf && ui.noticeLabel.text.Contains(price.ToString()),
                "Frame " + index + " explains its " + price + "-coin requirement through toast without spending.");
            Check(wallet.text == (price - 1).ToString(), "An unaffordable frame preserves the displayed wallet balance.");
            if (index == 4) await Capture(ui, "frame-coins-toast");
            Set(ui, "coins", price); Call(ui, "SelectProfileChoice", index);
            Check(!ui.modal.activeSelf && ui.Coins == 0 && settings.GetInt(UIDifferences.Key + "Frame") == index &&
                (settings.GetInt(UIDifferences.Key + "OwnedFrames") & (1 << index)) != 0,
                "An affordable frame deducts exactly " + price + " coins and saves ownership.");
            Check(wallet.text == "0" && ui.coinsLabel.text == "0", "A frame purchase updates Profile and Home wallets together.");
            Call(ui, "SelectProfileChoice", index);
            Check(ui.Coins == 0 && wallet.text == "0" && !ui.modal.activeSelf, "Owned frame " + index + " never charges twice.");
        }
        var requirements = new[] { 30, 20, 50, 100 };
        for (var index = 8; index <= 11; index++)
        {
            var required = requirements[index - 8];
            var journey = index == 9 || index == 10;
            var progress = journey ? "journeyCompleted" : "completed";
            var label = (journey ? "Journey " : "Level ") + required;
            Set(ui, journey ? "completed" : "journeyCompleted", 200);
            Set(ui, progress, required - 1); Call(ui, "RefreshProfile");
            Check(ui.profile.transform.Find(path + "Choice" + index + "/Price").GetComponent<UnityEngine.UI.Text>().text == label,
                "Frame " + index + " displays the correct " + label + " requirement.");
            ui.profileChoicesScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            await Click(ui.profile.transform.Find(path + "Choice" + index).GetComponent<UnityEngine.UI.Button>());
            Check(!ui.modal.activeSelf && ui.Coins == 0 && toast.gameObject.activeSelf && ui.noticeLabel.text.Contains(label) &&
                settings.GetInt(UIDifferences.Key + "Frame") != index,
                "Frame " + index + " remains locked before " + label + " despite 200 clears in the other mode and explains why through toast.");
            if (index == 8) await Capture(ui, "frame-level-toast");
            if (index == 9) await Capture(ui, "frame-journey-toast");
            Set(ui, progress, required); Call(ui, "SelectProfileChoice", index);
            Check(!ui.modal.activeSelf && ui.Coins == 0 && settings.GetInt(UIDifferences.Key + "Frame") == index &&
                !ui.profile.transform.Find(path + "Choice" + index + "/Lock").gameObject.activeSelf,
                "Exactly " + label + " unlocks frame " + index + " for free.");
        }
        var ownedFramesBeforeName = settings.GetInt(UIDifferences.Key + "OwnedFrames");
        await EditNickname("旅程玩家");
        Check(ui.profile.activeSelf && settings.GetString(UIDifferences.Key + "Nickname") == "旅程玩家" && ui.Coins == 0 &&
            settings.GetInt(UIDifferences.Key + "OwnedFrames") == ownedFramesBeforeName && ui.homeAvatar.sprite == ui.avatarSprites[37] &&
            ui.homeFrame.sprite == ui.profileFrameSprites[11], "Ending nickname editing saves the name, keeps Profile open and preserves avatar, frame, ownership and coins.");
        await Click(ui.profile.transform.Find("Close").GetComponent<UnityEngine.UI.Button>());
        ui.OnOpen(null); Call(ui, "CloseAds");
        Check(ui.homeAvatar.sprite == ui.avatarSprites[37] && ui.Coins == 0, "Reopening restores the high-index free avatar and exact balance.");
        await Click(ui.stage.Find("FigmaHome/Avatar").GetComponent<UnityEngine.UI.Button>());
        Check(ui.nicknameInput.text == "旅程玩家", "Profile restores the saved nickname.");
        Check(wallet.text == ui.Coins.ToString(), "Reopening Profile restores its wallet balance.");
        Set(ui, "completed", 0); Set(ui, "journeyCompleted", 0);
        await Click(ui.profile.transform.Find("Card/FrameTab").GetComponent<UnityEngine.UI.Button>());
        Call(ui, "SelectProfileChoice", 11);
        Check(settings.GetInt(UIDifferences.Key + "Frame") == 11 && !ui.modal.activeSelf && ui.Coins == 0,
            "An already owned frame remains usable with legacy saved ownership.");
        Set(ui, "completed", 100); Set(ui, "journeyCompleted", 50);
    }

    static void CheckMainRound(int level, int mask, int lives, string content) =>
        Check(level == 20 && mask == 1 && lives == 2 && content == "october-main-round", "The main unfinished round remains unchanged after Journey saves.");
    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static void Set(UIDifferences ui, string name, object value) => typeof(UIDifferences).GetField(name, Private).SetValue(ui, value);
    static void Call(UIDifferences ui, string name, params object[] args) => typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, args);
    static void Check(bool valid, string message)
    {
        File.AppendAllText(Path.Combine(Folder, "report.txt"), (valid ? "OK: " : "FAILED: ") + message + "\n");
        if (!valid) throw new InvalidOperationException(message);
    }
    static async Task Wait(Func<bool> ready, string message, int seconds)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (EditorApplication.isPlaying && !ready() && DateTime.UtcNow < end) await Task.Delay(50);
        Check(ready(), message);
    }
    static async Task Click(UnityEngine.UI.Button button)
    {
        await Task.Delay(100);
        var readyUntil = DateTime.UtcNow.AddSeconds(3);
        while (button && button.IsActive() && !button.IsInteractable() && DateTime.UtcNow < readyUntil) await Task.Delay(50);
        Canvas.ForceUpdateCanvases();
        Check(button && button.IsActive() && button.IsInteractable(), "Button is available: " + (button ? button.name : "missing"));
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    static async Task Capture(UIDifferences ui, string name)
    {
        await Task.Delay(450);
        var canvas = ui.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.worldCamera;
        if (!camera || canvas.renderMode == RenderMode.ScreenSpaceOverlay) throw new InvalidOperationException("A canvas camera is required for deterministic October captures.");
        var target = RenderTexture.GetTemporary(1179, 2556, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active; var previousAspect = camera.aspect;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.aspect = 1179f / 2556;
            Canvas.ForceUpdateCanvases(); Call(ui, "LateUpdate"); Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1179, 2556, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1179, 2556), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); if (image) UnityEngine.Object.DestroyImmediate(image);
            Canvas.ForceUpdateCanvases(); Call(ui, "LateUpdate");
        }
    }
}
#endif
