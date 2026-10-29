#if ENABLE_UI_UGUI
using Hotfix.Manager;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        readonly DifferenceGmGesture gmGesture = new DifferenceGmGesture();
        bool gmUnlimitedHints, gmJumpJourney;
        GameObject gmWindow;
        UnityEngine.UI.Text gmStatus;
        UnityEngine.UI.Button gmCompleteButton, gmUnlimitedButton, gmAddHintButton, gmJumpButton;
        UnityEngine.UI.InputField gmLevelInput;
        string gmMessage;
        public bool GmVisible => gmWindow && gmWindow.activeSelf;
        public bool GmUnlimitedHints => gmUnlimitedHints;
        public bool CanCompleteGmLevel => !AdInputBlocked && !IsLoadingLevel && play.activeInHierarchy &&
            Round != null && !Round.Finished && !settled && !figmaResultActive && !settings.activeSelf &&
            !PauseInputBlocked && !modal.activeSelf && !(hintDesign && hintDesign.activeSelf);

        public bool GmContainsScreenPoint(Vector2 point)
        {
            if (!GmVisible) return false;
            var canvas = gmWindow.GetComponent<Canvas>().rootCanvas;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)gmWindow.transform, point,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
        }

        void PollGmGesture()
        {
            if (GmVisible) return;
            if (AdInputBlocked || IsLoadingLevel || !Application.isFocused || PauseInputBlocked || figmaResultActive || modal.activeSelf)
            { gmGesture.Reset(); return; }
            var count = Input.touchCount;
            var first = default(Touch); var second = default(Touch); var third = default(Touch);
            if (count == 3) { first = Input.GetTouch(0); second = Input.GetTouch(1); third = Input.GetTouch(2); }
            if (gmGesture.Advance(count, first, second, third, Time.realtimeSinceStartupAsDouble, Mathf.Max(12, Screen.dpi * .08f)))
                ShowGmPanel();
        }

        public void ShowGmPanel()
        {
            if (!interactionsBound || AdInputBlocked) return;
            if (!GmVisible)
            {
                CancelTabDrag();
                if (hintSpotlight) hintSpotlight.Cancel();
                hintIdle = 0; if (hintHand) hintHand.Show(false);
                HideTutorialVisuals(); tutorialShown = -1;
            }
            if (!gmWindow) CreateGmWindow();
            gmWindow.SetActive(true);
            gmWindow.transform.SetAsLastSibling();
            RefreshGmWindow();
        }

        void CreateGmWindow()
        {
            gmWindow = new GameObject("GmWindow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            gmWindow.layer = stage.gameObject.layer;
            gmWindow.transform.SetParent(stage, false);
            var window = (RectTransform)gmWindow.transform;
            var width = Mathf.Min(380, Mathf.Max(240, stage.rect.width - 24));
            Place(window, 12, 100, width, 420);
            var background = gmWindow.GetComponent<UnityEngine.UI.Image>();
            var original = modal.transform.Find("Card").GetComponent<UnityEngine.UI.Image>();
            background.sprite = original.sprite; background.type = original.type; background.color = original.color;
            DifferencePopupMotion.SortAboveGameParticles(gmWindow);
            gmWindow.GetComponent<Canvas>().sortingOrder += 10;

            var title = Label(modalTitle, "Title", "GM - Drag to move", 12, 4, width - 64, 42);
            title.fontSize = 23; title.raycastTarget = true;
            title.gameObject.AddComponent<DifferenceGmDrag>().window = window;
            var close = Instantiate(modalClose, window);
            close.name = "Close";
            Place((RectTransform)close.transform, width - 46, 5, 38, 38);
            close.onClick.RemoveAllListeners(); close.onClick.AddListener(CloseGmPanel);
            close.gameObject.SetActive(true);
            gmStatus = Label(modalBody, "Status", "", 12, 50, width - 24, 72);
            gmStatus.fontSize = 21;
            gmCompleteButton = Button(modalAction, "Complete", "Complete Level", 12, 128, width - 24, 52, CompleteGmLevel);
            gmUnlimitedButton = Button(modalSecondary, "Unlimited", "", 12, 188, width - 24, 46, ToggleGmUnlimitedHints);
            gmAddHintButton = Button(modalTertiary, "AddHint", "Add 1 Hint", 12, 242, width - 24, 46, AddGmHint);
            Label(modalBody, "LevelLabel", "Level", 12, 304, 62, 44).fontSize = 22;
            gmLevelInput = Instantiate(nicknameInput, window);
            gmLevelInput.name = "LevelInput";
            Place((RectTransform)gmLevelInput.transform, 78, 302, width - 206, 48);
            gmLevelInput.onValueChanged.RemoveAllListeners(); gmLevelInput.onEndEdit.RemoveAllListeners();
            gmLevelInput.enabled = true; gmLevelInput.interactable = true; gmLevelInput.readOnly = false;
            gmLevelInput.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
            gmLevelInput.characterLimit = 10;
            gmLevelInput.textComponent.fontSize = 24;
            Fill(gmLevelInput.textComponent.rectTransform, 8);
            gmLevelInput.text = (JourneyVisible ? journeySelected + 1 : level + 1).ToString();
            gmJumpButton = Button(modalSecondary, "Jump", "Jump", width - 118, 302, 106, 48,
                () => { if (int.TryParse(gmLevelInput.text, out var target)) JumpGmLevel(target); else gmMessage = "Enter a valid level number."; });
            Label(modalBody, "Help", "Only this window blocks clicks.\nDrag the title to move it aside.", 12, 364, width - 24, 48).fontSize = 18;
            foreach (var child in gmWindow.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gmWindow.layer;
            foreach (var group in gmWindow.GetComponentsInChildren<CanvasGroup>(true))
            { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }

            void Place(RectTransform rect, float x, float y, float w, float h)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
                rect.localScale = Vector3.one;
            }
            void Fill(RectTransform rect, float padding)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.one * padding; rect.offsetMax = -Vector2.one * padding;
            }
            UnityEngine.UI.Text Label(UnityEngine.UI.Text template, string name, string text, float x, float y, float w, float h)
            {
                var label = Instantiate(template, window); label.name = name; label.text = text;
                Place(label.rectTransform, x, y, w, h);
                label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
                return label;
            }
            UnityEngine.UI.Button Button(UnityEngine.UI.Button template, string name, string text,
                float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
            {
                var button = Instantiate(template, window); button.name = name;
                Place((RectTransform)button.transform, x, y, w, h);
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(PlayButtonSound); button.onClick.AddListener(action);
                var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
                label.text = text; label.fontSize = 23; Fill(label.rectTransform, 0);
                button.gameObject.SetActive(true);
                return button;
            }
        }

        void RefreshGmWindow()
        {
            if (!GmVisible) return;
            var journey = JourneyPlaying || JourneyVisible;
            if (gmJumpJourney != journey)
            {
                gmJumpJourney = journey; gmMessage = "";
                gmLevelInput.SetTextWithoutNotify((JourneyVisible ? journeySelected + 1 : level + 1).ToString());
            }
            gmStatus.text = (IsLoadingLevel ? (journey ? "Journey" : "Main") + " loading level " + (level + 1) : play.activeSelf && Round != null
                ? (JourneyPlaying ? "Journey" : "Main") + " Level " + (level + 1) + "  |  Found " + Round.Count + "/" + Round.Total
                : JourneyVisible ? "Journey map | Level " + (journeySelected + 1) : "Main | No active level") + "\nHints: " + hints +
                (string.IsNullOrEmpty(gmMessage) ? "" : "\n" + gmMessage);
            gmCompleteButton.interactable = CanCompleteGmLevel;
            gmUnlimitedButton.interactable = !AdInputBlocked;
            gmUnlimitedButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Unlimited Hints: " + (gmUnlimitedHints ? "ON" : "OFF");
            gmAddHintButton.interactable = !AdInputBlocked && hints < 10000;
            gmJumpButton.interactable = !AdInputBlocked && !IsLoadingLevel;
        }

        public void CloseGmPanel()
        {
            if (gmWindow) gmWindow.SetActive(false);
        }

        public void CompleteGmLevel()
        {
            if (!GmVisible || !CanCompleteGmLevel) return;
            ClearFoundFeedback();
            while (!Round.Finished) ApplyResult(Round.RevealNext());
            RefreshGmWindow();
        }

        public void ToggleGmUnlimitedHints()
        {
            if (!GmVisible || AdInputBlocked) return;
            gmUnlimitedHints = !gmUnlimitedHints;
            GameApp.Setting.SetBool(Key + "GmUnlimitedHints", gmUnlimitedHints);
            Save(); UpdateHUD(); RefreshGmWindow();
        }

        public void AddGmHint()
        {
            if (!GmVisible || AdInputBlocked || hints >= 10000) return;
            hints++; Save(); UpdateHUD(); RefreshGmWindow();
        }

        public bool JumpGmLevel(int oneBasedLevel)
        {
            if (!GmVisible || AdInputBlocked || IsLoadingLevel) return false;
            var limit = JourneyVisible ? JourneyLevelCount : LevelLimit;
            if (testing) limit = Mathf.Min(limit, levels.Length);
            if (oneBasedLevel < 1 || oneBasedLevel > limit)
            { gmMessage = "Level must be between 1 and " + limit + "."; RefreshGmWindow(); return false; }
            StopTutorial();
            if (JourneyVisible && !journeyPlaying)
            {
                SaveRound(); Round = null; journeyPlaying = true;
            }
            journeyVisible = false;
            ClosePauseOverlays();
            gmMessage = "";
            BeginLevel(oneBasedLevel - 1, false);
            RefreshGmWindow();
            return true;
        }
    }
}
#endif
