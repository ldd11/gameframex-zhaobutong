#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DifferenceNicknameEditCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] IntKeys = { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "JourneyCompleted" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate" };
    static bool running;

    [MenuItem("Tools/Find Differences/Check Nickname Editing Only")]
    public static void Run()
    {
        if (running) return;
        var live = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !live) throw new InvalidOperationException("Start Launcher before checking nickname editing.");
        if (live.AdInputBlocked) throw new InvalidOperationException("Wait for the active ad operation before checking.");
        const string report = "Temp/FindDifferences-nickname-edit.txt";
        Directory.CreateDirectory("Temp");
        File.WriteAllText(report, "RUNNING: isolated Profile button, focus and EndEdit checks.");
        var settings = GameApp.Setting;
        var saved = new Dictionary<string, object>();
        foreach (var key in IntKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetInt(UIDifferences.Key + key);
        foreach (var key in BoolKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetBool(UIDifferences.Key + key);
        foreach (var key in StringKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetString(UIDifferences.Key + key);
        var previousSystem = EventSystem.current;
        var previousSelected = previousSystem ? previousSystem.currentSelectedGameObject : null;
        var previousInput = previousSelected ? previousSelected.GetComponent<InputField>() : null;
        var previousFocused = previousInput && previousInput.isFocused;
        var previousCaret = previousInput ? previousInput.caretPosition : 0;
        var previousAnchor = previousInput ? previousInput.selectionAnchorPosition : 0;
        var previousFocus = previousInput ? previousInput.selectionFocusPosition : 0;
        var previousText = previousInput ? previousInput.text : null;
        GameObject clone = null, eventObject = null;
        UIDifferences ui = null;
        Exception failure = null;
        running = true;
        try
        {
            // A live isolated clone exercises InputField focus without selecting or editing the player's real UI.
            clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab"));
            clone.name = "NicknameEditingCheck";
            ui = clone.GetComponent<UIDifferences>();
            ui.enabled = false;
            foreach (var name in new[] { "completed", "journeyCompleted", "coins", "hints", "avatar", "frame", "music", "ownedAvatars", "ownedFrames", "totalFound", "perfect", "sound", "musicEnabled", "vibration", "nickname", "giftDate" })
                Set(ui, name, Field(live, name));
            Set(ui, "nickname", "Original Pilot");
            Set(ui, "coins", 500); Set(ui, "avatar", 0); Set(ui, "frame", 0); Set(ui, "ownedFrames", 15);
            Set(ui, "draftAvatar", 1); Set(ui, "draftFrame", 4); Set(ui, "frameTab", true); Set(ui, "sound", false);
            ui.modal.SetActive(false);
            ui.profile.SetActive(true);
            var motion = ui.profile.GetComponent<DifferencePopupMotion>();
            if (motion) motion.enabled = false;
            var input = ui.nicknameInput;
            Check(input.characterLimit == 16, "The real nickname input must retain its 16 UTF-16 unit limit.");
            input.SetTextWithoutNotify("Original Pilot");
            var toast = ui.noticeLabel.transform.parent.GetComponent<CommonToast>();
            if (!toast) toast = ui.noticeLabel.transform.parent.gameObject.AddComponent<CommonToast>();
            Set(ui, "toast", toast);
            Call(ui, "BindProfileDesign");
            eventObject = new GameObject("NicknameCheckEventSystem", typeof(EventSystem));
            var events = eventObject.GetComponent<EventSystem>();
            EventSystem.current = events;
            var pencil = ui.profile.transform.Find("Card/User/ConfirmName").GetComponent<Button>();
            settings.SetString(UIDifferences.Key + "Nickname", "Original Pilot");
            settings.SetInt(UIDifferences.Key + "Coins", 500);
            settings.SetInt(UIDifferences.Key + "Frame", 0);
            settings.SetInt(UIDifferences.Key + "OwnedFrames", 15);

            void UnchangedChoices()
            {
                Check(ui.profile.activeSelf && ui.Coins == 500 && (int)Field(ui, "avatar") == 0 && (int)Field(ui, "frame") == 0 &&
                    (int)Field(ui, "ownedFrames") == 15 && (int)Field(ui, "draftAvatar") == 1 && (int)Field(ui, "draftFrame") == 4 &&
                    (bool)Field(ui, "frameTab") && !(bool)Call(ui, "ProfileFrameUnlocked", 4),
                    "Editing a nickname must preserve Profile, coins, ownership and the uncommitted locked frame choice.");
                Check(settings.GetInt(UIDifferences.Key + "Coins") == 500 && settings.GetInt(UIDifferences.Key + "Frame") == 0 &&
                    settings.GetInt(UIDifferences.Key + "OwnedFrames") == 15, "Nickname editing must not purchase or save a draft frame.");
            }

            void Focus()
            {
                var name = (string)Field(ui, "nickname");
                var savedName = settings.GetString(UIDifferences.Key + "Nickname");
                Check(ExecuteEvents.Execute(pencil.gameObject, new PointerEventData(events) { button = PointerEventData.InputButton.Left },
                    ExecuteEvents.pointerClickHandler), "The authored pencil must handle an actual Button pointer click.");
                // InputField intentionally defers activation until LateUpdate; advance that normal Unity lifecycle once.
                typeof(InputField).GetMethod("LateUpdate", Private).Invoke(input, null);
                Check(input.isFocused && events.currentSelectedGameObject == input.gameObject, "The pencil must focus the nickname field.");
                Check((string)Field(ui, "nickname") == name && settings.GetString(UIDifferences.Key + "Nickname") == savedName,
                    "The pencil starts editing without saving the nickname.");
                UnchangedChoices();
            }

            void Edit(string value, string expected)
            {
                Focus();
                input.text = value;
                input.DeactivateInputField();
                Check(!input.isFocused && input.text == expected && (string)Field(ui, "nickname") == expected &&
                    settings.GetString(UIDifferences.Key + "Nickname") == expected,
                    "Ending input editing must save the trimmed nickname: " + expected);
                Check(ValidUtf16(expected) && expected.Length <= 16, "Saved nickname must contain complete UTF-16 characters within 16 units.");
                UnchangedChoices();
            }

            Edit("  Sky Pilot  ", "Sky Pilot");
            Focus(); input.text = "   "; input.DeactivateInputField();
            Check(input.text == "Sky Pilot" && (string)Field(ui, "nickname") == "Sky Pilot" &&
                settings.GetString(UIDifferences.Key + "Nickname") == "Sky Pilot" && toast.gameObject.activeSelf &&
                ui.noticeLabel.text == "Your nickname cannot be empty.", "An empty nickname restores the previous name and shows an English toast.");
            UnchangedChoices();
            Edit("玩家\ud83d\ude00", "玩家\ud83d\ude00");
            Edit("1234567890123456XYZ", "1234567890123456");
            Edit("12345678901234\ud83d\ude00Z", "12345678901234\ud83d\ude00");
            Edit("123456789012345\ud83d\ude00Z", "123456789012345");
            var originalName = input.text;
            Focus(); input.text = "Cancelled";
            input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            input.DeactivateInputField();
            Check(input.wasCanceled && input.text == originalName && (string)Field(ui, "nickname") == originalName &&
                settings.GetString(UIDifferences.Key + "Nickname") == originalName, "Escape must cancel the edit without saving.");
            UnchangedChoices();
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            try
            {
                if (ui && ui.nicknameInput)
                {
                    ui.nicknameInput.onEndEdit.RemoveAllListeners();
                    ui.nicknameInput.DeactivateInputField();
                }
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
                if (eventObject) UnityEngine.Object.DestroyImmediate(eventObject);
                if (previousSystem) EventSystem.current = previousSystem;
                Check(!previousSystem || previousSystem.currentSelectedGameObject == previousSelected, "The original EventSystem selection must remain untouched.");
                Check(!previousInput || previousInput.isFocused == previousFocused && previousInput.text == previousText &&
                    previousInput.caretPosition == previousCaret && previousInput.selectionAnchorPosition == previousAnchor &&
                    previousInput.selectionFocusPosition == previousFocus, "The player's original input text, focus and caret must remain untouched.");
            }
            catch (Exception exception) { if (failure == null) failure = exception; }
            finally
            {
                foreach (var key in IntKeys.Concat(BoolKeys).Concat(StringKeys)) settings.RemoveSetting(UIDifferences.Key + key);
                foreach (var pair in saved)
                {
                    if (pair.Value is int number) settings.SetInt(UIDifferences.Key + pair.Key, number);
                    else if (pair.Value is bool flag) settings.SetBool(UIDifferences.Key + pair.Key, flag);
                    else settings.SetString(UIDifferences.Key + pair.Key, (string)pair.Value);
                }
                settings.Save();
                running = false;
            }
        }
        if (failure != null) { File.WriteAllText(report, "FAIL " + failure); Debug.LogException(failure); return; }
        File.WriteAllText(report, "PASS: authored pencil Button click focuses InputField without saving; real DeactivateInputField/EndEdit saves a trimmed nickname; empty input restores name with English toast; Unicode/16-unit boundaries preserve surrogate pairs; Escape cancels; locked draft frame, coins and Profile remain unchanged. All Save settings restored; live UI, EventSystem selection and original input state untouched.");
        Debug.Log("Nickname editing checks passed; all Save settings and the original UI/input state preserved.");
    }

    static bool ValidUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsLowSurrogate(value[i])) return false;
            if (!char.IsHighSurrogate(value[i])) continue;
            if (++i >= value.Length || !char.IsLowSurrogate(value[i])) return false;
        }
        return true;
    }

    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static void Set(UIDifferences ui, string name, object value) => typeof(UIDifferences).GetField(name, Private).SetValue(ui, value);
    static object Call(UIDifferences ui, string name, params object[] args) => typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, args);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
