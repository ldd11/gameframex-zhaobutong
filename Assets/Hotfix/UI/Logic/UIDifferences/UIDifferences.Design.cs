#if ENABLE_UI_UGUI
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        // Loaded and retained with the UI prefab by GameFrameX/YooAsset.
        public Sprite[] designSprites;
        [SerializeField] GameObject homeDesign, settingsDesign, loadingDesign, loadingMagnifier, victoryDesign, failDesign, hintDesign;
        [SerializeField] Image loadingFill, victoryPreview, musicToggle, soundToggle, vibrationToggle;
        [SerializeField] Text designCoins, failProgress;
        [SerializeField] Sprite designRound, toggleOn, toggleOff;
        [SerializeField] Button designSettingsButton, designStartButton, designSettingsClose, designMusicButton, designSoundButton, designVibrationButton, designFailClose, designContinueButton, designRetryButton, designHintClose, designFreeButton, designBuyButton, designNextButton;
        bool figmaResultActive, figmaResultWon, localLoading;
        Coroutine localLoadRoutine;
        float loadingStarted, loadingFillWidth;
        Vector2 loadingMagnifierCenter;

        // All page objects and graphics are authored in UIDifferences.prefab.
        void BindFigmaDesign()
        {
            loadingMagnifierCenter = ((RectTransform)loadingMagnifier.transform).anchoredPosition;
            loadingFillWidth = loadingFill.rectTransform.rect.width;
            designSettingsButton.onClick.AddListener(ShowSettings);
            designStartButton.onClick.AddListener(StartLevel);
            designSettingsClose.onClick.AddListener(CloseSettings);
            designMusicButton.onClick.AddListener(() => { musicEnabled = !musicEnabled; Save(); PlayMusic(); RefreshSettings(); });
            designSoundButton.onClick.AddListener(() => { sound = !sound; Save(); RefreshSettings(); });
            designVibrationButton.onClick.AddListener(ToggleVibration);
            designFailClose.onClick.AddListener(ShowHome);
            designContinueButton.onClick.AddListener(OnFigmaResultPrimary);
            designRetryButton.onClick.AddListener(OnFigmaResultSecondary);
            designHintClose.onClick.AddListener(CloseDesignHint);
            designFreeButton.onClick.AddListener(() => AcquireDesignHint(true));
            designBuyButton.onClick.AddListener(() => AcquireDesignHint(false));
            designNextButton.onClick.AddListener(OnFigmaResultPrimary);
        }

        void UpdateFigmaDesign(GameObject page)
        {
            if (!homeDesign) return;
            var loading = IsLoadingLevel;
            homeDesign.SetActive(page == home && !loading && !figmaResultActive);
            loadingDesign.SetActive(loading);
            victoryDesign.SetActive(figmaResultActive && figmaResultWon && !loading);
            failDesign.SetActive(figmaResultActive && !figmaResultWon && !loading);
            settingsDesign.SetActive(settings.activeSelf && !loading && !figmaResultActive);
            if (designCoins) designCoins.text = coins.ToString();
            foreach (var layer in new[] { homeDesign, loadingDesign, victoryDesign, failDesign, hintDesign, settingsDesign })
                if (layer.activeSelf) layer.transform.SetAsLastSibling();
            if (modal.activeSelf) modal.transform.SetAsLastSibling();
        }

        void AdvanceDesignLoading()
        {
            if (!loadingDesign || !loadingDesign.activeSelf) return;
            var elapsed = Time.unscaledTime - loadingStarted;
            var angle = elapsed * Mathf.PI;
            var magnifier = (RectTransform)loadingMagnifier.transform;
            magnifier.anchoredPosition = loadingMagnifierCenter + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 24;
            loadingFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                loadingFillWidth * (.18f + .67f * (1 - Mathf.Exp(-elapsed))));
        }

        IEnumerator LoadLocalLevel(int index, bool resume)
        {
            yield return new WaitForSecondsRealtime(.55f);
            localLoading = false; localLoadRoutine = null;
            OpenLevel(index, resume);
        }

        void ShowFigmaResult(bool won)
        {
            figmaResultWon = won; figmaResultActive = true; modal.SetActive(false); CloseDesignHint();
            victoryPreview.sprite = upperImage.sprite;
            failProgress.text = "You completed " + Mathf.RoundToInt(100f * Round.Count / Mathf.Max(1, Round.Total)) + "%.";
            UpdateFigmaDesign(currentPage);
        }

        void CloseFigmaResult() { figmaResultActive = false; UpdateFigmaDesign(currentPage); }
        void OnFigmaResultPrimary()
        {
            if (figmaResultWon) { CloseFigmaResult(); if (level + 1 < LevelLimit) BeginLevel(level + 1, false); else ShowHome(); }
            else Revive();
        }
        void OnFigmaResultSecondary() { CloseFigmaResult(); BeginLevel(level, false); }

        void ToggleVibration()
        {
            vibration = !vibration; Save(); RefreshSettings();
            if (vibration && !Application.isEditor) Handheld.Vibrate();
        }
        void CloseDesignHint() { if (hintDesign) hintDesign.SetActive(false); }
        void AcquireDesignHint(bool free)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (free && GiftClaimed(today)) { Notice("今日免费提示已领取", 2); return; }
            if (!free && coins < 100) { Notice("金币不足，需要 100 金币", 2); return; }
            if (free) giftDate = today; else coins -= 100;
            hints++; Save(); CloseDesignHint(); UpdateHUD(); UseHint();
        }
    }
}
#endif
