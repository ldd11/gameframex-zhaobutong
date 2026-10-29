#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DifferenceRuntimeGmCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] IntKeys = { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "JourneyCompleted", "TutorialStep", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "Journey.RoundLevel", "Journey.RoundMask", "Journey.RoundLives", "Journey.RoundMistakes", "Journey.RoundHint" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration", "TutorialComplete", "GmUnlimitedHints" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate", "RoundContent", "AnalyticsRound", "Journey.RoundContent", "Journey.AnalyticsRound" };
    static bool running;

    [MenuItem("Tools/Find Differences/Check Editor GM Entry Only")]
    public static void CheckEditorEntry()
    {
        const string report = "Temp/FindDifferences-editor-gm.txt";
        var live = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        var wasVisible = live && live.GmVisible;
        try
        {
            if (!EditorApplication.isPlaying)
            {
                Check(EditorApplication.ExecuteMenuItem("Tools/GM 工具") &&
                    Resources.FindObjectsOfTypeAll<DifferenceGMWindow>().Length > 0,
                    "Tools/GM 工具 opens its existing startup window outside Play mode.");
                File.WriteAllText(report, "PASS: Tools/GM 工具 opens the existing Editor window outside Play mode; no player data modified.");
                return;
            }
            Check(live && !live.AdInputBlocked && !live.IsLoadingLevel,
                "Start Launcher and wait for loading or ads before checking the Editor GM entry.");
            var round = live.Round;
            var mask = round?.FoundMask; var lives = round?.Lives;
            var coins = live.Coins; var hints = live.Hints;
            var completed = live.Completed; var journeyCompleted = live.JourneyCompleted;
            var unlimited = live.GmUnlimitedHints;
            Check(EditorApplication.ExecuteMenuItem("Tools/GM 工具") && live.GmVisible &&
                ((GameObject)Field(live, "gmWindow")).activeInHierarchy,
                "Tools/GM 工具 opens the same runtime GM panel used on mobile.");
            Check(ReferenceEquals(round, live.Round) && mask == live.Round?.FoundMask && lives == live.Round?.Lives &&
                live.Coins == coins && live.Hints == hints && live.Completed == completed &&
                live.JourneyCompleted == journeyCompleted && live.GmUnlimitedHints == unlimited,
                "Opening the Editor GM entry must preserve the active round, progress, rewards and hints.");
            File.WriteAllText(report, "PASS: real Tools/GM 工具 menu opens the mobile GM panel in Play mode; active round, progress, coins and hints unchanged; no data-changing buttons invoked.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally { if (live && !wasVisible && live.GmVisible) live.CloseGmPanel(); }
    }

    [MenuItem("Tools/Find Differences/Check Runtime GM Only")]
    public static void Run()
    {
        if (running) return;
        var live = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !live) throw new InvalidOperationException("Start Launcher before checking the runtime GM panel.");
        if (live.AdInputBlocked) throw new InvalidOperationException("Wait for the active ad operation before checking GM.");
        var reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-runtime-gm.txt"));
        var report = new StringBuilder("RUNNING: isolated runtime GM buttons and normal gameplay effects.\n");
        var settings = GameApp.Setting;
        var saved = new Dictionary<string, object>();
        foreach (var key in IntKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetInt(UIDifferences.Key + key);
        foreach (var key in BoolKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetBool(UIDifferences.Key + key);
        foreach (var key in StringKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetString(UIDifferences.Key + key);
        var liveRound = live.Round;
        var liveCoins = live.Coins; var liveHints = live.Hints; var liveCompleted = live.Completed;
        var liveJourneyCompleted = live.JourneyCompleted;
        var livePage = Field(live, "currentPage");
        var liveGm = live.GmVisible; var liveUnlimited = live.GmUnlimitedHints;
        var liveSource = live.musicSource; var liveClip = liveSource ? liveSource.clip : null;
        var listenerPaused = AudioListener.pause;
        GameObject clone = null;
        UIDifferences ui = null;
        Exception failure = null;
        running = true;
        try
        {
            clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab"));
            clone.name = "RuntimeGmCheck";
            ui = clone.GetComponent<UIDifferences>();
            ui.enabled = false;
            var canvas = clone.GetComponent<Canvas>();
            if (!canvas) canvas = clone.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)clone.transform).sizeDelta = new Vector2(1179, 1179f * 1100 / 600);
            foreach (var name in new[] { "coins", "hints", "avatar", "frame", "music", "ownedAvatars", "ownedFrames", "totalFound", "perfect", "nickname", "giftDate" })
                Set(ui, name, Field(live, name));
            Set(ui, "testing", true); Set(ui, "completed", 1); Set(ui, "journeyCompleted", 1);
            Set(ui, "sound", false); Set(ui, "musicEnabled", false); Set(ui, "vibration", false);
            ui.audioSource.mute = ui.musicSource.mute = true;
            ui.OnInit(); // No OnOpen, ad session, startup requests or tutorial initialization.
            var source = ui.levels[1];
            var smallLevel = new DifferenceLevel
            {
                title = "GM Check", original = source.original, changed = source.changed,
                regions = new[] { new Vector4(.25f, .3f, .12f, .12f), new Vector4(.75f, .7f, .12f, .12f) }
            };
            ui.levels = new[] { smallLevel, smallLevel, smallLevel };
            Set(ui, "hints", 3); Set(ui, "coins", 200);
            ui.ShowHome();

            void OpenPanel()
            {
                ui.ShowGmPanel();
                Check(ui.GmVisible && ((GameObject)Field(ui, "gmWindow")).activeInHierarchy && !ui.PauseInputBlocked,
                    "GM opens an independent window without blocking the game's input.");
            }
            Button GmButton(string name) => (Button)Field(ui, name);
            void ClosePanel()
            {
                ((GameObject)Field(ui, "gmWindow")).transform.Find("Close").GetComponent<Button>().onClick.Invoke();
                Check(!ui.GmVisible, "X closes only the independent GM window.");
            }
            void CancelHint()
            {
                ((DifferenceHintSpotlight)Field(ui, "hintSpotlight")).Cancel();
                Set(ui, "pendingHint", -1);
            }
            void OpenRound(bool journey)
            {
                Call(ui, "CancelEndAnimation"); Call(ui, "ClearFoundFeedback");
                Call(ui, "CloseFigmaResult");
                CancelHint(); ui.modal.SetActive(false);
                Set(ui, "journeyPlaying", journey); Set(ui, "journeyVisible", false);
                Set(ui, "completed", journey ? 20 : 1); Set(ui, "journeyCompleted", 1);
                Call(ui, "OpenLevel", 1, false, false);
                Canvas.ForceUpdateCanvases();
                Check(ui.Round != null && ui.Round.Total == 2 && ui.Round.Count == 0 && ui.play.activeInHierarchy,
                    "The fixture opens a fresh two-spot local level without network or ads.");
            }
            string RoundState(string prefix)
            {
                var state = new StringBuilder();
                foreach (var key in new[] { "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint" })
                    state.Append(key).Append('=').Append(settings.HasSetting(UIDifferences.Key + prefix + key)).Append(':').Append(settings.GetInt(UIDifferences.Key + prefix + key, -99)).Append(';');
                foreach (var key in new[] { "RoundContent", "AnalyticsRound" })
                    state.Append(key).Append('=').Append(settings.HasSetting(UIDifferences.Key + prefix + key)).Append(':').Append(settings.GetString(UIDifferences.Key + prefix + key, "")).Append(';');
                return state.ToString();
            }

            OpenPanel();
            var window = (GameObject)Field(ui, "gmWindow");
            var windowRect = (RectTransform)window.transform;
            Check(!ui.modal.activeSelf && !window.transform.Find("Scrim") &&
                !GmButton("gmCompleteButton").interactable && GmButton("gmUnlimitedButton").gameObject.activeSelf &&
                GmButton("gmAddHintButton").gameObject.activeSelf && GmButton("gmJumpButton").gameObject.activeSelf,
                "The floating window has all four controls and no modal or full-screen scrim.");
            if (ui.GmUnlimitedHints) GmButton("gmUnlimitedButton").onClick.Invoke();
            var offLabel = GmButton("gmUnlimitedButton").GetComponentInChildren<Text>(true).text;
            Check(!ui.GmUnlimitedHints && !string.IsNullOrEmpty(offLabel), "The unlimited-hint toggle starts disabled with a visible label.");
            GmButton("gmAddHintButton").onClick.Invoke();
            Check(ui.Hints == 4 && settings.GetInt(UIDifferences.Key + "Hints") == 4,
                "The actual Add 1 Hint button increments and saves the hint count.");
            Set(ui, "hints", 10000); settings.SetInt(UIDifferences.Key + "Hints", 10000); OpenPanel();
            Check(!GmButton("gmAddHintButton").interactable, "Add 1 Hint is disabled at the hint cap.");
            GmButton("gmAddHintButton").onClick.Invoke();
            Check(ui.Hints == 10000 && settings.GetInt(UIDifferences.Key + "Hints") == 10000, "GM hints retain the 10000 cap.");
            OpenPanel(); ClosePanel();
            var guardedHints = ui.Hints; var guardedCoins = ui.Coins; var guardedUnlimited = ui.GmUnlimitedHints;
            ui.AddGmHint(); ui.ToggleGmUnlimitedHints(); ui.CompleteGmLevel();
            Check(ui.Hints == guardedHints && ui.Coins == guardedCoins && ui.GmUnlimitedHints == guardedUnlimited && ui.Round == null,
                "Public GM commands have no effect while its panel is closed.");

            OpenPanel();
            var regularActions = 0;
            ui.ShowDialog("Regular Dialog", "Normal dialog after GM", "OK", () => regularActions++);
            Check(ui.GmVisible && ReferenceEquals(window, Field(ui, "gmWindow")) && ui.modal.activeSelf &&
                ui.modalAction.interactable && !ui.modalSecondary.gameObject.activeSelf && !ui.modalTertiary.gameObject.activeSelf,
                "A regular dialog keeps GM visible and retains its own separate controls.");
            ui.modalAction.onClick.Invoke();
            Check(regularActions == 1 && !ui.modal.activeSelf && ui.GmVisible,
                "The normal dialog's primary action closes only that dialog and runs once.");
            report.AppendLine("PASS: four controls, disabled completion, independent X, no scrim or pause gate, separate ordinary dialog, command guards and saved hint increment/cap.");

            Set(ui, "hints", 3); OpenRound(false);
            OpenPanel(); GmButton("gmUnlimitedButton").onClick.Invoke();
            Check(ui.GmUnlimitedHints && settings.GetBool(UIDifferences.Key + "GmUnlimitedHints", false) &&
                GmButton("gmUnlimitedButton").GetComponentInChildren<Text>(true).text != offLabel,
                "The toggle enables, relabels and persists unlimited hints.");
            ClosePanel();
            foreach (var journey in new[] { false, true })
            {
                CancelHint(); Set(ui, "journeyPlaying", journey);
                ui.UseHint();
                Check(ui.HintActive && ui.Hints == 3 && settings.GetInt(UIDifferences.Key + "Hints") == 3 && ui.hintLabel.text == "∞" && ui.Round.Count == 0,
                    "Unlimited hints apply to both main and Journey gameplay without deducting hints or revealing a spot.");
            }
            OpenPanel(); GmButton("gmUnlimitedButton").onClick.Invoke();
            Check(!ui.GmUnlimitedHints && !settings.GetBool(UIDifferences.Key + "GmUnlimitedHints", true), "Disabling unlimited hints is persisted.");
            ClosePanel();
            foreach (var journey in new[] { false, true })
            {
                CancelHint(); Set(ui, "journeyPlaying", journey);
                var before = ui.Hints;
                ui.UseHint();
                Check(ui.HintActive && ui.Hints == before - 1 && settings.GetInt(UIDifferences.Key + "Hints") == before - 1 && ui.hintLabel.text != "∞",
                    "After disabling GM, main level 2 and Journey both consume and save one hint.");
            }
            report.AppendLine("PASS: unlimited ON/OFF is saved; both modes use real UseHint without/with a saved deduction.");

            OpenRound(false);
            var board = ui.upperImage.GetComponent<DifferenceBoard>();
            var boardType = typeof(DifferenceBoard);
            var touchGate = boardType.GetMethod("BlockGmTouches", Private);
            Check(touchGate != null, "The board must expose its private GM touch latch for this focused regression.");
            bool Touches(int count) => (bool)touchGate.Invoke(board, new object[] { count });
            Check(!Touches(2), "An ordinary two-finger gesture must not acquire the GM touch latch.");
            boardType.GetField("dragging", Private).SetValue(board, true);
            boardType.GetField("pinching", Private).SetValue(board, true);
            Check(Touches(3) && !(bool)boardType.GetField("dragging", Private).GetValue(board) &&
                !(bool)boardType.GetField("pinching", Private).GetValue(board), "Three fingers latch the board and cancel its previous drag/pinch.");
            OpenPanel();
            Check(Touches(1) && (float)boardType.GetField("blockedUntil", Private).GetValue(board) > Time.unscaledTime,
                "A remaining original finger keeps the latch and extends click suppression while GM is open.");
            ClosePanel();
            Check(Touches(0), "The final release frame must still be consumed after closing GM.");
            Check(!Touches(0) && !Touches(2), "Only the following no-touch frame releases the latch; normal two-finger input stays available.");
            var missPoint = ui.upperImage.rectTransform.TransformPoint(new Vector3(
                ui.upperImage.rectTransform.rect.xMin + ui.upperImage.rectTransform.rect.width * .05f,
                ui.upperImage.rectTransform.rect.yMin + ui.upperImage.rectTransform.rect.height * .05f));
            var oldRelease = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, missPoint) };
            Check((bool)boardType.GetMethod("Inside", Private).Invoke(board, new object[] { oldRelease.position, null }),
                "The simulated stale release must land inside the real board viewport.");
            var beforeLives = ui.Round.Lives;
            board.OnPointerClick(oldRelease);
            Check(ui.Round.Lives == beforeLives && ui.Round.Count == 0 &&
                (float)boardType.GetField("blockedUntil", Private).GetValue(board) > Time.unscaledTime,
                "The stale release during blockedUntil must neither reveal a difference nor deduct a life.");
            report.AppendLine("PASS: board GM latch survives 3→1, consumes the final 0 frame, releases on the next 0 and suppresses the stale board release; ordinary two-finger gestures do not latch.");

            OpenPanel();
            var title = window.transform.Find("Title").gameObject;
            var drag = new PointerEventData(EventSystem.current) { position =
                RectTransformUtility.WorldToScreenPoint(null, title.transform.position) };
            var initialPosition = windowRect.anchoredPosition;
            Check(ExecuteEvents.Execute(title, drag, ExecuteEvents.beginDragHandler), "The title must handle window dragging.");
            drag.position += new Vector2(35, -25);
            ExecuteEvents.Execute(title, drag, ExecuteEvents.dragHandler);
            Check(windowRect.anchoredPosition != initialPosition, "Dragging the title must move the independent window.");
            var movedPosition = windowRect.anchoredPosition;
            var raycaster = window.GetComponent<GraphicRaycaster>();
            var windowHits = new List<RaycastResult>();
            var outsidePoint = ui.lowerImage.rectTransform.TransformPoint(new Vector3(
                ui.lowerImage.rectTransform.rect.xMax - 5, ui.lowerImage.rectTransform.rect.yMin + 5));
            var raycastCameraNode = new GameObject("GM raycast camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            var raycastCanvases = ui.GetComponentsInChildren<Canvas>(true);
            var raycastCameras = raycastCanvases.Select(item => item.worldCamera).ToArray();
            try
            {
                var camera = raycastCameraNode.GetComponent<Camera>(); camera.enabled = false;
                var root = (RectTransform)ui.transform;
                camera.transform.position = root.TransformPoint(root.rect.center) + new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = root.rect.height * .5f;
                foreach (var item in raycastCanvases) item.worldCamera = camera;
                Canvas.ForceUpdateCanvases(); camera.Render();
                var outsideScreenPoint = RectTransformUtility.WorldToScreenPoint(camera, outsidePoint);
                raycaster.Raycast(new PointerEventData(EventSystem.current) { position = outsideScreenPoint }, windowHits);
                Check(windowHits.Count == 0, "The floating GM window must not intercept a board point outside its bounds.");
                var insideScreenPoint = RectTransformUtility.WorldToScreenPoint(camera,
                    GmButton("gmAddHintButton").transform.TransformPoint(((RectTransform)GmButton("gmAddHintButton").transform).rect.center));
                raycaster.Raycast(new PointerEventData(EventSystem.current) { position = insideScreenPoint }, windowHits);
                Check(windowHits.Count > 0, "Controls inside the floating GM window must still receive raycasts.");
                Check(!ui.GmContainsScreenPoint(outsideScreenPoint) && ui.GmContainsScreenPoint(insideScreenPoint),
                    "The raw-touch GM hit test must agree with its window's actual raycast bounds.");
            }
            finally
            {
                for (var i = 0; i < raycastCanvases.Length; i++) if (raycastCanvases[i]) raycastCanvases[i].worldCamera = raycastCameras[i];
                UnityEngine.Object.DestroyImmediate(raycastCameraNode);
            }
            ClosePanel(); OpenPanel();
            Check(ReferenceEquals(window, Field(ui, "gmWindow")) && windowRect.anchoredPosition == movedPosition,
                "Closing and reopening GM must reuse its window and dragged position.");
            report.AppendLine("PASS: title drag moves the window; raycasts outside its bounds pass through; reopening preserves the same window and position.");

            foreach (var journey in new[] { false, true })
            {
                OpenRound(journey);
                var otherPrefix = journey ? "" : "Journey.";
                var otherRound = RoundState(otherPrefix);
                var beforeCoins = ui.Coins; var beforeFound = (int)Field(ui, "totalFound");
                var beforePerfect = (int)Field(ui, "perfect");
                OpenPanel();
                if (!journey)
                {
                    CapturePanel(ui);
                    report.AppendLine("PREVIEW: Temp/runtime-gm-panel.png (isolated active main level, 600x1100).");
                }
                Check(ui.CanCompleteGmLevel && GmButton("gmCompleteButton").interactable, "An unfinished playable round enables Complete Level.");
                var spot = ui.spots[0];
                ui.ClickImage(spot.x, spot.y);
                Check(ui.Round.Count == 1 && ui.GmVisible, "The open floating GM window allows normal board input.");
                GmButton("gmCompleteButton").onClick.Invoke();
                Call(ui, "CancelEndAnimation"); Call(ui, "ClearFoundFeedback");
                Check(ui.GmVisible && ReferenceEquals(window, Field(ui, "gmWindow")) &&
                    windowRect.anchoredPosition == movedPosition && ui.Round.Complete && ui.Round.Count == 2 && ui.Coins == beforeCoins + 10 &&
                    (int)Field(ui, "totalFound") == beforeFound + 2 && settings.GetInt(UIDifferences.Key + "Coins") == ui.Coins,
                    "Complete Level saves one normal reward and keeps the same GM window visible at its dragged position.");
                Check(ui.Completed == (journey ? 20 : 2) && ui.JourneyCompleted == (journey ? 2 : 1) &&
                    settings.GetInt(UIDifferences.Key + "Completed") == ui.Completed &&
                    settings.GetInt(UIDifferences.Key + "JourneyCompleted") == ui.JourneyCompleted &&
                    (int)Field(ui, "perfect") == beforePerfect + 1 && settings.GetInt(UIDifferences.Key + "Perfect") == beforePerfect + 1 &&
                    settings.GetInt(UIDifferences.Key + (journey ? "Journey." : "") + "RoundLevel") == -1 && RoundState(otherPrefix) == otherRound,
                    "A first perfect completion advances only its own mode and achievement, clears only its own saved round and preserves the other mode's round.");
                Call(ui, "CloseFigmaResult"); OpenPanel();
                Check(!ui.CanCompleteGmLevel && !GmButton("gmCompleteButton").interactable, "A completed round disables Complete Level.");
                GmButton("gmCompleteButton").onClick.Invoke(); ui.CompleteGmLevel();
                Call(ui, "ApplyResult", 0, null);
                Check(ui.Coins == beforeCoins + 10 && (int)Field(ui, "totalFound") == beforeFound + 2 && ui.Round.Count == 2,
                    "Repeated GM or result callbacks cannot duplicate found spots or rewards.");

                var firstCompleted = ui.Completed; var firstJourneyCompleted = ui.JourneyCompleted;
                var jumpInput = (InputField)Field(ui, "gmLevelInput");
                jumpInput.SetTextWithoutNotify("2"); GmButton("gmJumpButton").onClick.Invoke();
                Check(ui.Round.Count == 0 && ui.SelectedLevel == 1 && ui.JourneyPlaying == journey && ui.Coins == beforeCoins + 10,
                    "The real GM jump button reopens the completed level without granting coins.");
                foreach (var replaySpot in ui.spots) ui.ClickImage(replaySpot.x, replaySpot.y);
                Call(ui, "CancelEndAnimation"); Call(ui, "ClearFoundFeedback");
                Check(ui.Round.Complete && ui.Coins == beforeCoins + 20 && (int)Field(ui, "totalFound") == beforeFound + 4 &&
                    settings.GetInt(UIDifferences.Key + "Coins") == ui.Coins && ui.GmVisible,
                    "Normal play after a GM jump back awards and saves another ten coins while keeping GM visible.");
                Check(ui.Completed == firstCompleted && ui.JourneyCompleted == firstJourneyCompleted &&
                    settings.GetInt(UIDifferences.Key + "Completed") == firstCompleted &&
                    settings.GetInt(UIDifferences.Key + "JourneyCompleted") == firstJourneyCompleted &&
                    (int)Field(ui, "perfect") == beforePerfect + 1 && settings.GetInt(UIDifferences.Key + "Perfect") == beforePerfect + 1 &&
                    settings.GetInt(UIDifferences.Key + (journey ? "Journey." : "") + "RoundLevel") == -1 && RoundState(otherPrefix) == otherRound,
                    "A perfect replay preserves both modes' completion counts, the first-perfect achievement and the other mode's saved round.");
                Check(ui.stage.Find("FigmaVictory").gameObject.activeInHierarchy &&
                    ((Text)Field(ui, "rewardSummaryLabel")).text == "x10",
                    "A replay victory displays its ten-coin reward.");
                ui.ShowShop(); Call(ui, "CloseShop");
                Check(ui.stage.Find("FigmaVictory").gameObject.activeInHierarchy &&
                    ((Text)Field(ui, "rewardSummaryLabel")).text == "x10" &&
                    ui.Coins == beforeCoins + 20 && settings.GetInt(UIDifferences.Key + "Coins") == ui.Coins,
                    "Returning from the shop restores the ten-coin reward display without granting coins again.");
                GmButton("gmCompleteButton").onClick.Invoke(); ui.CompleteGmLevel(); Call(ui, "ApplyResult", 0, null);
                Check(ui.Coins == beforeCoins + 20 && (int)Field(ui, "totalFound") == beforeFound + 4 &&
                    ui.Completed == firstCompleted && ui.JourneyCompleted == firstJourneyCompleted && (int)Field(ui, "perfect") == beforePerfect + 1,
                    "Repeated callbacks after replay and shop return cannot grant extra coins or achievements.");
                report.AppendLine("PASS: " + (journey ? "Journey" : "main") + " first clear and GM-jump replay each award +10 once; normal replay clicks, duplicate settlement guards, x10 after shop return, first-clear achievements and separate mode saves verified.");
            }

            foreach (var journey in new[] { false, true })
            {
                OpenRound(journey); OpenPanel();
                var beforeCoins = ui.Coins; var beforeHints = ui.Hints;
                var beforeCompleted = ui.Completed; var beforeJourneyCompleted = ui.JourneyCompleted;
                var beforeFound = (int)Field(ui, "totalFound");
                var otherPrefix = journey ? "" : "Journey.";
                var otherRound = RoundState(otherPrefix);
                var current = ui.Round;
                foreach (var invalid in new[] { -1, 0, 4, int.MaxValue })
                    Check(!ui.JumpGmLevel(invalid) && ReferenceEquals(current, ui.Round) && ui.SelectedLevel == 1,
                        "Invalid or unavailable local levels must leave the current round unchanged: " + invalid);
                var input = (InputField)Field(ui, "gmLevelInput");
                input.SetTextWithoutNotify(""); GmButton("gmJumpButton").onClick.Invoke();
                Check(ReferenceEquals(current, ui.Round), "An empty level input must not jump.");
                input.SetTextWithoutNotify("999999999999999999999999"); GmButton("gmJumpButton").onClick.Invoke();
                Check(ReferenceEquals(current, ui.Round), "An overflowing level input must not jump.");
                foreach (var number in new[] { 1, 2, 3 })
                {
                    input.text = number.ToString(); GmButton("gmJumpButton").onClick.Invoke();
                    Check(ui.GmVisible && !ui.PauseInputBlocked && ui.JourneyPlaying == journey &&
                        ui.SelectedLevel == number - 1 && ui.Round != null && ui.Round.Count == 0 && ui.Round.Lives == 3 &&
                        !ReferenceEquals(current, ui.Round) && ReferenceEquals(window, Field(ui, "gmWindow")) &&
                        windowRect.anchoredPosition == movedPosition,
                        "The real jump button opens the requested local level and keeps the same floating window: " + number);
                    Check(RoundState(otherPrefix) == otherRound, "Jumping must preserve the other mode's entire saved round.");
                    current = ui.Round;
                }
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
                var foundAfterClick = (int)Field(ui, "totalFound");
                current = ui.Round;
                Check(ui.JumpGmLevel(3) && !ReferenceEquals(current, ui.Round) && ui.Round.Count == 0 && ui.Round.Lives == 3,
                    "Jumping to the current level starts a clean round instead of restoring its found mask.");
                Check(ui.Coins == beforeCoins && ui.Hints == beforeHints && ui.Completed == beforeCompleted &&
                    ui.JourneyCompleted == beforeJourneyCompleted && foundAfterClick == beforeFound + 1 &&
                    (int)Field(ui, "totalFound") == foundAfterClick && RoundState(otherPrefix) == otherRound,
                    "Jumping to locked or current levels does not grant rewards or forge either completion counter.");
                report.AppendLine("PASS: " + (journey ? "Journey" : "main") + " real jump button reaches local levels 1/2/3, bypasses locks without completion/rewards, restarts same level; rejects invalid/overflowing input.");
            }
            ui.ShowHome(); Call(ui, "RefreshGmWindow");
            ui.ShowJourney(); Call(ui, "RefreshGmWindow");
            Check(ui.JourneyVisible && !ui.JourneyPlaying && ui.GmVisible &&
                ((Text)Field(ui, "gmStatus")).text.Contains("Journey") &&
                ((InputField)Field(ui, "gmLevelInput")).text == ((int)Field(ui, "journeySelected") + 1).ToString(),
                "Entering the Journey map keeps GM visible and replaces the main-level target with its selected Journey level.");
            var mapCoins = ui.Coins; var mapCompleted = ui.Completed; var mapJourneyCompleted = ui.JourneyCompleted;
            var mapMainRound = RoundState("");
            ((InputField)Field(ui, "gmLevelInput")).text = "3"; GmButton("gmJumpButton").onClick.Invoke();
            Check(ui.JourneyPlaying && !ui.JourneyVisible && ui.SelectedLevel == 2 && ui.GmVisible &&
                ui.Coins == mapCoins && ui.Completed == mapCompleted && ui.JourneyCompleted == mapJourneyCompleted &&
                RoundState("") == mapMainRound && settings.GetInt(UIDifferences.Key + "Journey.RoundLevel", -1) == 2,
                "Jumping from the Journey map writes only Journey level 3 and leaves the entire main saved round unchanged.");
            ui.ShowHome();
            Check(ui.GmVisible && ReferenceEquals(window, Field(ui, "gmWindow")) && windowRect.anchoredPosition == movedPosition,
                "Returning to Home must preserve the floating GM window and its position.");
            ClosePanel();
            var closedRound = ui.Round;
            Check(!ui.JumpGmLevel(1) && ReferenceEquals(closedRound, ui.Round), "A closed GM window cannot authorize a jump.");
            Check(live.Round == liveRound && live.Coins == liveCoins && live.Hints == liveHints && live.Completed == liveCompleted &&
                live.JourneyCompleted == liveJourneyCompleted && Field(live, "currentPage") == livePage && live.GmVisible == liveGm &&
                live.GmUnlimitedHints == liveUnlimited && live.musicSource == liveSource && (!liveSource || liveSource.clip == liveClip) && AudioListener.pause == listenerPaused,
                "The live UI, round, page, GM settings and music must remain untouched.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try
            {
                if (ui)
                {
                    Call(ui, "CancelEndAnimation"); ui.StopAllCoroutines(); Call(ui, "ClearFoundFeedback");
                    ui.nicknameInput.onEndEdit.RemoveAllListeners();
                }
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
            }
            catch (Exception error) { if (failure == null) failure = error; }
            finally
            {
                foreach (var key in IntKeys.Concat(BoolKeys).Concat(StringKeys)) settings.RemoveSetting(UIDifferences.Key + key);
                foreach (var pair in saved)
                {
                    if (pair.Value is int number) settings.SetInt(UIDifferences.Key + pair.Key, number);
                    else if (pair.Value is bool flag) settings.SetBool(UIDifferences.Key + pair.Key, flag);
                    else settings.SetString(UIDifferences.Key + pair.Key, (string)pair.Value);
                }
                settings.Save(); running = false;
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                report.AppendLine(failure == null ? "PASS: all tested settings restored; live UI untouched." : "FAIL: " + failure);
                File.WriteAllText(reportPath, report.ToString());
            }
        }
        if (failure != null) { Debug.LogException(failure); throw new InvalidOperationException("Runtime GM check failed: " + reportPath, failure); }
        Debug.Log("Runtime GM checks passed; all settings restored: " + reportPath);
    }

    static void CapturePanel(UIDifferences ui)
    {
        var cameraNode = new GameObject("GM preview camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
        var target = new RenderTexture(600, 1100, 24);
        var pixels = new Texture2D(600, 1100, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        var root = (RectTransform)ui.transform;
        var oldPosition = root.position; var oldSize = root.sizeDelta;
        var notice = ui.noticeLabel.transform.parent.gameObject;
        var noticeWasActive = notice.activeSelf;
        var canvases = ui.GetComponentsInChildren<Canvas>(true);
        var oldCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        try
        {
            root.position = new Vector3(10000, 10000, 0);
            root.sizeDelta = new Vector2(1179, 1179f * 1100 / 600);
            Canvas.ForceUpdateCanvases();
            Call(ui, "FitSafeArea", new Rect(0, 0, 1, 1));
            Call(ui, "FitScreenBackgrounds", root);
            notice.SetActive(false); // The short level-open toast has expired before the real ten-second gesture.
            Canvas.ForceUpdateCanvases();
            var layout = ui.play.GetComponent<DifferencePlayLayout>();
            if (layout && layout.enabled) layout.ApplyLayout(((RectTransform)ui.play.transform).rect);
            var camera = cameraNode.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = root.TransformPoint(root.rect.center) + new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = root.rect.height * .5f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .15f);
            camera.targetTexture = target;
            foreach (var canvas in canvases) canvas.worldCamera = camera;
            foreach (var dot in ui.progressDots) if (dot.isActiveAndEnabled) { dot.Update(0); dot.UpdateMesh(); }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 600, 1100), 0, 0); pixels.Apply();
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes("Temp/runtime-gm-panel.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            root.position = oldPosition; root.sizeDelta = oldSize;
            notice.SetActive(noticeWasActive);
            for (var i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].worldCamera = oldCameras[i];
            foreach (var item in new UnityEngine.Object[] { cameraNode, target, pixels }) UnityEngine.Object.DestroyImmediate(item);
            Canvas.ForceUpdateCanvases();
        }
    }

    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static void Set(UIDifferences ui, string name, object value) => typeof(UIDifferences).GetField(name, Private).SetValue(ui, value);
    static object Call(UIDifferences ui, string name, params object[] args) => typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, args);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
#endif
