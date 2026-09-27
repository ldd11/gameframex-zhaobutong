#if ENABLE_UI_UGUI
using System;
using GameFrameX.Startup.Application;
using Hotfix.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        CanvasGroup adInputGroup;
        bool adUiOpen, adInputLocked, adWasInteractable, adWasBlocking, adTransitionPending;
        bool lastAdPageVisible;
        int adSession, lastAdPageLevel = -1;
        string roundAdOperation, freeHintLabel;
        DifferenceRound winAdCheckedRound;
        public bool AdInputBlocked => DifferenceAds.Busy || adInputLocked || adTransitionPending;

        void OpenAds()
        {
            adUiOpen = true; adSession++; lastAdPageLevel = -1;
            DifferenceAds.FullscreenChanged -= OnAdFullscreenChanged;
            DifferenceAds.FullscreenChanged += OnAdFullscreenChanged;
            RefreshHintRewardLabel();
            OnAdFullscreenChanged(DifferenceAds.Busy);
        }

        void CloseAds()
        {
            if (!adUiOpen && !adInputLocked) return;
            adUiOpen = false; adSession++; adTransitionPending = false;
            DifferenceAds.FullscreenChanged -= OnAdFullscreenChanged;
            RestoreAdInput();
            DifferenceAds.SetGamePage(level + 1, false);
            lastAdPageVisible = false; lastAdPageLevel = -1;
        }

        protected override void OnEventUnSubscribe()
        {
            CloseAds();
            base.OnEventUnSubscribe();
        }

        void OnAdFullscreenChanged(bool shown)
        {
            if (!this) return;
            SampleAnalytics();
            SampleAnalyticsLoadAd(shown);
            if (shown && !adInputLocked)
            {
                SaveRound();
                adInputGroup = GetComponent<CanvasGroup>();
                if (!adInputGroup) adInputGroup = gameObject.AddComponent<CanvasGroup>();
                adWasInteractable = adInputGroup.interactable; adWasBlocking = adInputGroup.blocksRaycasts;
                adInputGroup.interactable = false;
                adInputGroup.blocksRaycasts = true;
                adInputLocked = true;
                CancelTabDrag();
                if (UnityEngine.EventSystems.EventSystem.current)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
            else if (!shown) RestoreAdInput();
            UpdateAdsVisibility();
        }

        void RestoreAdInput()
        {
            if (adInputLocked && adInputGroup)
            {
                adInputGroup.interactable = adWasInteractable;
                adInputGroup.blocksRaycasts = adWasBlocking;
            }
            adInputLocked = false;
        }

        void UpdateAdsVisibility()
        {
            if (!adUiOpen) return;
            var visible = !testing && isActiveAndEnabled && currentPage == play && play.activeInHierarchy &&
                Round != null && !Round.Finished && !IsLoadingLevel && !figmaResultActive && !AdInputBlocked &&
                !modal.activeSelf && !settings.activeSelf && !profile.activeSelf && !musicPanel.activeSelf &&
                !achievements.activeSelf && !album.activeSelf && !(hintDesign && hintDesign.activeSelf);
            var visibleLevel = level + 1;
            if (lastAdPageLevel == visibleLevel && lastAdPageVisible == visible) return;
            lastAdPageLevel = visibleLevel; lastAdPageVisible = visible;
            DifferenceAds.SetGamePage(visibleLevel, visible);
        }

        void CheckWinInterstitial(DifferenceRound finishedRound, int visibleLevel)
        {
            if (testing || !adUiOpen || Round != finishedRound || winAdCheckedRound == finishedRound ||
                !figmaResultActive || !figmaResultWon) return;
            winAdCheckedRound = finishedRound;
            DifferenceAds.TryInterstitial(visibleLevel, roundAdOperation + ":win", "level_complete", () => { });
        }

        void RetryWithInterstitial()
        {
            if (AdInputBlocked || !figmaResultActive || figmaResultWon || Round == null) return;
            var failedRound = Round; var retryLevel = level; var session = adSession;
            adTransitionPending = true;
            Action resume = () =>
            {
                if (!this || !adUiOpen || adSession != session) return;
                adTransitionPending = false;
                if (Round != failedRound || currentPage != play || !figmaResultActive || figmaResultWon) return;
                CloseFigmaResult(); BeginLevel(retryLevel, false, true);
            };
            if (testing) resume();
            else DifferenceAds.TryInterstitial(retryLevel + 1, Guid.NewGuid().ToString("N") + ":retry", "level_enter", resume);
        }

        void RefreshHintRewardLabel()
        {
            var label = designFreeButton ? designFreeButton.GetComponentInChildren<Text>(true) : null;
            if (!label) return;
            if (freeHintLabel == null) freeHintLabel = label.text;
            //label.text = DifferenceAds.SdkEnabled ? "看视频领提示" : freeHintLabel;
        }

        void RequestRewardRevive()
        {
            if (!adUiOpen || !figmaResultActive || figmaResultWon) return;
            var failedRound = Round; var session = adSession;
            var earned = false;
            TrackRewardIntent("revive");
            var accepted = DifferenceAds.TryRewarded(level + 1, "revive_reward", () => earned = true, () =>
            {
                // Resume only after the ad closes and the shared input/audio lock is released.
                if (!this || !adUiOpen || adSession != session || !earned || Round != failedRound ||
                    currentPage != play || !play.activeInHierarchy || IsLoadingLevel ||
                    !figmaResultActive || figmaResultWon) return;
                CompleteRevive(false);
            });
            if (!accepted && this && adUiOpen) Notice("Video not ready. Please try again later.", 2);
        }

        void RequestRewardHint()
        {
            var rewardedRound = Round; var session = adSession;
            var settingsStore = GameApp.Setting;
            var earned = false;
            TrackRewardIntent("hint_1");
            var accepted = DifferenceAds.TryRewarded(level + 1, "hint_reward", () =>
            {
                if (earned) return;
                earned = true;
                // The reward belongs to the player even if this pooled form closed during the video.
                if (this && adUiOpen && adSession == session)
                {
                    hints++; Save(); UpdateHUD();
                }
                else
                {
                    var earnedHints = settingsStore.GetInt(Key + "Hints", 5) + 1;
                    settingsStore.SetInt(Key + "Hints", earnedHints);
                    settingsStore.Save();
                    var current = UnityEngine.Object.FindObjectOfType<UIDifferences>();
                    if (current && current.adUiOpen) { current.hints = earnedHints; current.UpdateHUD(); }
                }
            }, () =>
            {
                if (!this || !adUiOpen || adSession != session || !earned || Round != rewardedRound ||
                    currentPage != play || !play.activeInHierarchy || IsLoadingLevel || figmaResultActive ||
                    modal.activeSelf || settings.activeSelf || !hintDesign.activeSelf) return;
                CloseDesignHint(); UpdateHUD(); UseHint();
            });
            if (!accepted && this && adUiOpen) Notice("Video not ready. Please try again later.", 2);
        }
    }
}
#endif
