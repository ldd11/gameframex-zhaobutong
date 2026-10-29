#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        [SerializeField] GameObject pauseDesign, leaveDesign;
        [SerializeField] UnityEngine.UI.Button pauseCloseButton, pauseMusicButton, pauseSoundButton, pauseVibrationButton;
        [SerializeField] UnityEngine.UI.Button pauseContinueButton, pauseHomeButton, leaveCloseButton, leaveKeepPlayingButton, leaveConfirmButton;
        [SerializeField] UnityEngine.UI.Image pauseMusicToggle, pauseSoundToggle, pauseVibrationToggle;

        public bool PauseInputBlocked => (pauseDesign && pauseDesign.activeSelf) || (leaveDesign && leaveDesign.activeSelf);

        void BindPauseDesign()
        {
            if (!pauseDesign || !leaveDesign) return;
            foreach (var popup in new[] { pauseDesign, leaveDesign })
                if (!popup.GetComponent<DifferencePopupMotion>()) popup.AddComponent<DifferencePopupMotion>();
            pauseCloseButton.onClick.AddListener(ClosePause);
            pauseContinueButton.onClick.AddListener(ClosePause);
            pauseHomeButton.onClick.AddListener(ShowLeaveConfirmation);
            leaveCloseButton.onClick.AddListener(ClosePause);
            leaveKeepPlayingButton.onClick.AddListener(ClosePause);
            leaveConfirmButton.onClick.AddListener(LeavePausedLevel);
            pauseMusicButton.onClick.AddListener(() =>
            {
                if (AdInputBlocked) return;
                musicEnabled = !musicEnabled; Save(); PlayMusic(); RefreshSettings(); RefreshPauseSettings();
            });
            pauseSoundButton.onClick.AddListener(() =>
            {
                if (AdInputBlocked) return;
                sound = !sound; Save(); RefreshSettings(); RefreshPauseSettings();
            });
            pauseVibrationButton.onClick.AddListener(() => { ToggleVibration(); RefreshPauseSettings(); });
            ClosePauseOverlays();
        }

        public void ShowPause()
        {
            if (AdInputBlocked || PauseInputBlocked || !pauseDesign || !play.activeSelf || IsLoadingLevel ||
                Round == null || Round.Finished || settled || modal.activeSelf || settings.activeSelf ||
                (hintDesign && hintDesign.activeSelf) || figmaResultActive) return;
            SampleAnalytics(); SaveRound();
            if (hintSpotlight) hintSpotlight.Cancel();
            HideTutorialVisuals(); tutorialShown = -1;
            hintIdle = 0; if (hintHand) hintHand.Show(false);
            pauseDesign.SetActive(true); pauseDesign.transform.SetAsLastSibling();
            RefreshPauseSettings(); UpdateAdsVisibility(); SampleAnalytics();
        }

        void RefreshPauseSettings()
        {
            pauseMusicToggle.sprite = musicEnabled ? toggleOn : toggleOff;
            pauseSoundToggle.sprite = sound ? toggleOn : toggleOff;
            pauseVibrationToggle.sprite = vibration ? toggleOn : toggleOff;
        }

        void ShowLeaveConfirmation()
        {
            if (AdInputBlocked || !pauseDesign.activeSelf) return;
            SampleAnalytics();
            leaveDesign.SetActive(true); leaveDesign.transform.SetAsLastSibling();
            pauseDesign.SetActive(false);
            UpdateAdsVisibility();
        }

        void ClosePause()
        {
            if (AdInputBlocked || !PauseInputBlocked) return;
            ClosePauseOverlays();
            hintIdle = 0;
            RestoreHint(); UpdateAdsVisibility();
        }

        void LeavePausedLevel()
        {
            if (AdInputBlocked || !leaveDesign.activeSelf) return;
            if (JourneyPlaying) ShowJourney();
            else ShowHomeInternal();
        }

        void ClosePauseOverlays()
        {
            SampleAnalytics();
            if (pauseDesign) pauseDesign.SetActive(false);
            if (leaveDesign) leaveDesign.SetActive(false);
            SampleAnalytics();
        }

        bool HandlePauseEscape()
        {
            if (GmVisible) { CloseGmPanel(); return true; }
            if (!PauseInputBlocked) return false;
            ClosePause();
            return true;
        }

#if UNITY_EDITOR
        void CheckTutorialPauseFlow()
        {
            if (!testing || JourneyPlaying || !home.activeSelf)
                throw new System.InvalidOperationException("Tutorial pause check requires the main test home with settings backed up.");
            BeginLevel(0, false);
            tutorialPending = true; OpenTutorial(false); AdvanceTutorial();
            var initialStep = tutorialStep;
            var target = spots[tutorialTarget];
            backButton.onClick.Invoke();
            UnityEngine.Assertions.Assert.IsTrue(PauseInputBlocked && !TutorialAllowsView, "The tutorial must allow pause and block gestures.");
            ClickImage(target.x, target.y); UseHint(); AdvanceTutorial();
            UnityEngine.Assertions.Assert.IsTrue(Round.Count == 0 && tutorialStep == initialStep, "Pausing must preserve the tutorial step and ignore clicks.");
            pauseContinueButton.onClick.Invoke(); AdvanceTutorial();
            UnityEngine.Assertions.Assert.IsTrue(TutorialRunning && tutorialStep == initialStep && backButton.gameObject.activeSelf, "Continue must restore the same tutorial step and pause entry.");
            backButton.onClick.Invoke(); pauseHomeButton.onClick.Invoke(); leaveConfirmButton.onClick.Invoke();
            UnityEngine.Assertions.Assert.IsTrue(home.activeSelf && Round == null && tutorialPending && GetInt("RoundLevel", -1) == 0,
                "Confirmed tutorial exit must reach Home and preserve its unfinished round.");
            StartLevel();
            UnityEngine.Assertions.Assert.IsTrue(TutorialRunning && Round.Count == 0 && tutorialStep == initialStep, "Starting again must restore the saved tutorial step.");
            backButton.onClick.Invoke();
            UnityEngine.Assertions.Assert.IsTrue(HandlePauseEscape() && !PauseInputBlocked && tutorialStep == initialStep, "Escape must resume the tutorial without advancing it.");
        }

        void CheckPauseFlow()
        {
            if (!testing || Round == null || Round.Finished)
                throw new System.InvalidOperationException("Pause check requires a live test round.");
            var round = Round;
            var mask = round.FoundMask; var lives = round.Lives; var savedCoins = coins; var savedHints = hints;
            var timeScale = Time.timeScale;
            try
            {
                backButton.onClick.Invoke();
                UnityEngine.Assertions.Assert.IsTrue(PauseInputBlocked && pauseDesign.activeSelf && !leaveDesign.activeSelf, "Back must open Pause.");
                var target = spots[round.Hint()];
                ClickImage(target.x, target.y); UseHint();
                UnityEngine.Assertions.Assert.IsTrue(round.FoundMask == mask && round.Lives == lives && coins == savedCoins && hints == savedHints, "Pause must block board clicks and hints.");
                var initialMusic = musicEnabled; var initialSound = sound; var initialVibration = vibration;
                pauseMusicButton.onClick.Invoke(); pauseSoundButton.onClick.Invoke(); pauseVibrationButton.onClick.Invoke();
                UnityEngine.Assertions.Assert.IsTrue(musicEnabled != initialMusic && sound != initialSound && vibration != initialVibration &&
                    pauseMusicToggle.sprite == (musicEnabled ? toggleOn : toggleOff) && pauseSoundToggle.sprite == (sound ? toggleOn : toggleOff) &&
                    pauseVibrationToggle.sprite == (vibration ? toggleOn : toggleOff), "Pause switches must update their settings and visible states.");
                pauseMusicButton.onClick.Invoke(); pauseSoundButton.onClick.Invoke(); pauseVibrationButton.onClick.Invoke();
                pauseHomeButton.onClick.Invoke();
                UnityEngine.Assertions.Assert.IsTrue(PauseInputBlocked && !pauseDesign.activeSelf && leaveDesign.activeSelf, "Home must request confirmation.");
                leaveKeepPlayingButton.onClick.Invoke();
                UnityEngine.Assertions.Assert.IsTrue(!PauseInputBlocked && play.activeSelf && Round == round && Time.timeScale == timeScale, "Keep Playing must resume the same round.");
                ShowPause();
                UnityEngine.Assertions.Assert.IsTrue(HandlePauseEscape() && !PauseInputBlocked, "Escape must close Pause.");
            }
            finally { ClosePauseOverlays(); }
        }
#endif
    }
}
#endif
