#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hotfix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DifferencePauseDesignCheck
{
    [MenuItem("Tools/Find Differences/Check Popup Safe Area Motion Only")]
    public static void CheckPopupSafeAreaMotionOnly()
    {
        const string reportPath = "Temp/FindDifferences-popup-safe-area-motion.txt";
        var report = new StringBuilder();
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject world = null;
        try
        {
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            RectTransform Rect(string name, Transform parent)
            {
                var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                rect.SetParent(parent, false);
                return rect;
            }
            world = new GameObject("PopupSafeAreaMotionCheck", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(world, scene);
            world.SetActive(false); // Invoke only the animation steps being checked.
            var root = (RectTransform)world.transform;
            root.sizeDelta = new Vector2(1179, 2556);
            var popup = Rect("Popup", root);
            popup.anchorMin = Vector2.zero; popup.anchorMax = Vector2.one; popup.sizeDelta = Vector2.zero;
            var card = Rect("Card", popup);
            card.sizeDelta = new Vector2(970, 1225); card.anchoredPosition = new Vector2(0, 109.5f);
            card.localScale = new Vector3(.91f, 1.04f, 1);
            var close = Rect("Close", popup);
            // A sibling anchored away from its parent's pivot exposes stale local-position caches.
            close.anchorMin = close.anchorMax = Vector2.one;
            close.sizeDelta = new Vector2(140, 140); close.anchoredPosition = new Vector2(-120, -160);
            close.localScale = new Vector3(1.1f, .9f, 1);
            var scrimRect = Rect("Scrim", popup);
            scrimRect.anchorMin = Vector2.zero; scrimRect.anchorMax = Vector2.one; scrimRect.sizeDelta = Vector2.zero;
            var scrim = scrimRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            scrim.color = new Color(.1f, .2f, .3f, .58f);
            var restPosition = close.anchoredPosition;
            var cardScale = card.localScale;
            var closeScale = close.localScale;
            var scrimColor = scrim.color;
            var motion = popup.gameObject.AddComponent<DifferencePopupMotion>();
            void Invoke(string method, params object[] args)
            {
                var action = typeof(DifferencePopupMotion).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
                Check(action != null, "Missing popup animation method: " + method);
                action.Invoke(motion, args);
            }
            void Refresh()
            {
                root.ForceUpdateRectTransforms(); popup.ForceUpdateRectTransforms(); close.ForceUpdateRectTransforms();
            }
            Refresh();
            Invoke("Awake");
            for (var cycle = 0; cycle < 3; cycle++)
            {
                Invoke("OnEnable");
                var cardFade = card.GetComponent<CanvasGroup>();
                var closeFade = close.GetComponent<CanvasGroup>();
                Check(cardFade && closeFade && !cardFade.interactable && !closeFade.blocksRaycasts,
                    "Opening popup must block its animated controls until the motion finishes.");
                Invoke("Advance", .11f);
                Check((close.anchoredPosition - restPosition).sqrMagnitude > .01f,
                    "Fixture must animate the external Close before resizing.");
                var oldAnchorOrigin = close.localPosition - (Vector3)close.anchoredPosition;
                root.sizeDelta = new Vector2(1080 + cycle * 90, 2340 - cycle * 180);
                popup.anchorMin = new Vector2(.01f * cycle, .03f + .01f * cycle);
                popup.anchorMax = new Vector2(1 - .01f * cycle, .94f - .01f * cycle);
                Refresh();
                var newAnchorOrigin = close.localPosition - (Vector3)close.anchoredPosition;
                Check((newAnchorOrigin - oldAnchorOrigin).sqrMagnitude > 1,
                    "Fixture resize must change Close's local anchor origin.");
                Invoke("Advance", .07f);
                Check(!cardFade.interactable && !closeFade.interactable,
                    "Resizing mid-animation must not enable controls early.");
                Invoke("Advance", 1f);
                Check((close.anchoredPosition - restPosition).sqrMagnitude < .0001f,
                    "External Close did not return to its authored anchored position after resize, cycle " + cycle);
                Check((card.localScale - cardScale).sqrMagnitude < .000001f &&
                    (close.localScale - closeScale).sqrMagnitude < .000001f,
                    "Card and Close must recover their original scales.");
                Check(cardFade.alpha == 1 && closeFade.alpha == 1 && cardFade.interactable && closeFade.interactable &&
                    cardFade.blocksRaycasts && closeFade.blocksRaycasts,
                    "Finished popup must be visible and receive input.");
                Check(Mathf.Abs(scrim.color.a - scrimColor.a) < .0001f,
                    "Finished popup must restore the authored scrim alpha.");
                report.AppendLine("Cycle " + (cycle + 1) + ": anchored Close restored after safe-area resize; scales, input and scrim restored.");
                Invoke("OnDisable");
                Check((close.anchoredPosition - restPosition).sqrMagnitude < .0001f,
                    "Disabling a completed popup must not accumulate Close displacement.");
            }
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, "PASS\n" + report);
            Debug.Log("Popup safe-area motion checks passed. " + reportPath);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, "FAIL\n" + report + error);
            throw;
        }
        finally
        {
            if (world) UnityEngine.Object.DestroyImmediate(world);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/Find Differences/Check Pause Design Only")]
    public static void Validate()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var serialized = new SerializedObject(ui);
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            T Reference<T>(string field) where T : UnityEngine.Object
            {
                var value = serialized.FindProperty(field)?.objectReferenceValue as T;
                Check(value, "Missing pause reference: " + field);
                return value;
            }
            var pause = Reference<GameObject>("pauseDesign");
            var leave = Reference<GameObject>("leaveDesign");
            foreach (var popup in new[] { pause, leave })
            {
                Check(!popup.activeSelf && popup.transform.parent == ui.stage, "Pause overlays must start hidden directly under Stage.");
                var scrim = popup.transform.Find("Scrim")?.GetComponent<UnityEngine.UI.Image>();
                Check(scrim && scrim.raycastTarget && scrim.color.a > 0, "Pause overlays need a visible blocking scrim.");
                Check(popup.GetComponentInChildren<UnityEngine.UI.Text>(true), "Pause overlays need editable text.");
            }
            foreach (var field in new[] { "pauseCloseButton", "pauseMusicButton", "pauseSoundButton", "pauseVibrationButton", "pauseContinueButton", "pauseHomeButton",
                "leaveCloseButton", "leaveKeepPlayingButton", "leaveConfirmButton" })
            {
                var button = Reference<UnityEngine.UI.Button>(field);
                var popup = field.StartsWith("leave", StringComparison.Ordinal) ? leave : pause;
                Check(button.transform.IsChildOf(popup.transform) && button.targetGraphic && button.targetGraphic.raycastTarget,
                    "Pause button must belong to its overlay and receive raycasts: " + field);
            }
            foreach (var field in new[] { "pauseMusicToggle", "pauseSoundToggle", "pauseVibrationToggle" })
                Check(Reference<UnityEngine.UI.Image>(field).transform.IsChildOf(pause.transform), "Pause switches must belong to Pause.");
            Reference<Sprite>("toggleOn"); Reference<Sprite>("toggleOff");
            Check(ui.backButton && ui.backButton.transform.IsChildOf(ui.play.transform), "The game must retain its pause button.");
            Debug.Log("Pause prefab structure checks passed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
