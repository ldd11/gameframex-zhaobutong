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
        [SerializeField] Button designContactButton, designTermsButton, designPrivacyButton;
        [Header("Settings Links")]
        [SerializeField] string contactUrl = "mailto:contact@joystar.pro";
        [SerializeField] string termsOfServiceUrl = "https://joystar.pro/terms";
        [SerializeField] string privacyPolicyUrl = "https://joystar.pro/privacy.html";
        [Header("Vibration")]
        [SerializeField, Min(1)] int foundVibrationMilliseconds = 30, missVibrationMilliseconds = 60;
        [SerializeField, Range(1, 255)] int foundVibrationAmplitude = 100, missVibrationAmplitude = 200;
        [SerializeField] RectTransform rewardWallet, rewardCoinOrigin;
        [SerializeField] Text rewardCoinsLabel;
        [SerializeField] Image rewardCoinIcon;
        Coroutine rewardRoutine;
        GameObject rewardFlights;
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
            if (designContactButton) designContactButton.onClick.AddListener(() => OpenSettingsLink(contactUrl));
            if (designTermsButton) designTermsButton.onClick.AddListener(() => OpenSettingsLink(termsOfServiceUrl));
            if (designPrivacyButton) designPrivacyButton.onClick.AddListener(() => OpenSettingsLink(privacyPolicyUrl));
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
            if (won) ReplayVictoryAnimation();
            PlayTone(won ? winSound : failSound);
        }

        void ReplayVictoryAnimation()
        {
            foreach (var animation in victoryDesign.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>())
            {
                animation.Initialize(false);
                animation.UnscaledTime = true;
                animation.freeze = false;
                animation.AnimationState.ClearTracks();
                animation.Skeleton.SetToSetupPose();
                animation.AnimationState.SetAnimation(0, animation.startingAnimation, false);
                animation.Update(0);
            }
        }

        void CloseFigmaResult() { CancelRewardCoins(); figmaResultActive = false; UpdateFigmaDesign(currentPage); }
        void OnFigmaResultPrimary()
        {
            if (figmaResultWon) { CloseFigmaResult(); if (level + 1 < LevelLimit) BeginLevel(level + 1, false); else ShowHome(); }
            else Revive();
        }
        void OnFigmaResultSecondary() { CloseFigmaResult(); BeginLevel(level, false); }

        IEnumerator ReplayCompletedProgress()
        {
            var count = Mathf.Min(Round.Total, progressDots.Length);
            var ringOrder = new int[Mathf.Min(Round.Total, Mathf.Min(topRings.Length, bottomRings.Length))];
            for (var i = 0; i < ringOrder.Length; i++) ringOrder[i] = i;
            Array.Sort(ringOrder, (a, b) =>
            {
                var horizontal = spots[a].x.CompareTo(spots[b].x);
                return horizontal != 0 ? horizontal : spots[a].y.CompareTo(spots[b].y);
            });
            // One root allows cancellation to remove all replay particles immediately.
            var root = new GameObject("CompletionSweep", typeof(RectTransform));
            root.transform.SetParent(play.transform, false);
            try
            {
                for (var i = 0; i < count; i++)
                {
                    if (i < ringOrder.Length)
                    {
                        var index = ringOrder[i];
                        ClearRingParticlesNow(topRings[index]);
                        ClearRingParticlesNow(bottomRings[index]);
                        ShowFoundRing(topRings[index], true);
                        ShowFoundRing(bottomRings[index], true);
                    }
                    var target = progressDots[i].rectTransform;
                    StartCoroutine(Pulse(target));
                    PlayTone(foundSound);
                    if (foundArrivalPrefab)
                    {
                        var effect = Instantiate(foundArrivalPrefab, root.transform, false);
                        effect.transform.position = target.TransformPoint(target.rect.center);
                        var canvas = play.GetComponentInParent<Canvas>();
                        foreach (var child in effect.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = play.layer;
                        foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                            var main = particle.main;
                            main.loop = false; main.useUnscaledTime = true;
                            var renderer = particle.GetComponent<ParticleSystemRenderer>();
                            renderer.sortingLayerID = canvas.sortingLayerID;
                            renderer.sortingOrder = canvas.sortingOrder + 2;
                            particle.Play(false);
                        }
                    }
                    yield return new WaitForSecondsRealtime(.065f);
                }
                var ringAnimation = foundRingSpine ? foundRingSpine.GetSkeletonData(false).FindAnimation("a2") : null;
                yield return new WaitForSecondsRealtime(Mathf.Max(.4f, ringAnimation == null ? 0 : ringAnimation.Duration));
            }
            finally { if (root) Destroy(root); }
        }

        void StartRewardCoins(int reward)
        {
            CancelRewardCoins();
            if (reward <= 0 || !rewardWallet || !rewardCoinsLabel || !rewardCoinIcon) return;
            rewardRoutine = StartCoroutine(AnimateRewardCoins(reward));
        }

        IEnumerator AnimateRewardCoins(int reward)
        {
            var finalBalance = coins;
            rewardWallet.gameObject.SetActive(true);
            rewardWallet.SetAsLastSibling();
            rewardCoinsLabel.text = (finalBalance - reward).ToString();
            rewardFlights = new GameObject("RewardCoinFlights", typeof(RectTransform));
            var parent = (RectTransform)rewardFlights.transform;
            parent.SetParent(victoryDesign.transform, false);
            parent.anchorMin = Vector2.zero; parent.anchorMax = Vector2.one;
            parent.offsetMin = parent.offsetMax = Vector2.zero;
            var count = Mathf.Min(reward, 20);
            parent.gameObject.layer = rewardWallet.gameObject.layer;
            Canvas.ForceUpdateCanvases();
            var originRect = rewardCoinOrigin ? rewardCoinOrigin : victoryPreview.rectTransform;
            var originPoint = rewardCoinOrigin ? originRect.rect.center : new Vector2(originRect.rect.center.x, originRect.rect.yMin + originRect.rect.height * .08f);
            var origin = parent.InverseTransformPoint(originRect.TransformPoint(originPoint));
            var preview = victoryPreview.rectTransform;
            var corners = new Vector3[4];
            preview.GetWorldCorners(corners);
            var width = Vector3.Distance(parent.InverseTransformPoint(corners[0]), parent.InverseTransformPoint(corners[3]));
            var spread = width * .18f;
            var icons = new DifferenceRewardCoin[count];
            var arrived = 0;
            for (var i = 0; i < count; i++)
            {
                icons[i] = DifferenceRewardCoin.Create(parent, rewardCoinIcon.sprite, width * .075f);
                icons[i].gameObject.SetActive(false);
            }
            for (var time = 0f; arrived < count; time += Time.unscaledDeltaTime)
            {
                var target = parent.InverseTransformPoint(rewardCoinIcon.rectTransform.TransformPoint(rewardCoinIcon.rectTransform.rect.center));
                for (var i = arrived; i < count; i++)
                {
                    var age = time - DifferenceRewardCoin.SpawnDelay - i * DifferenceRewardCoin.Stagger;
                    if (age < 0) continue;
                    var coin = icons[i];
                    coin.gameObject.SetActive(true);
                    coin.transform.localPosition = DifferenceRewardCoin.Position(origin, target, spread, age, i, out var flight);
                    coin.SetAppearance(age, i, flight);
                    if (flight >= 1)
                    {
                        coin.gameObject.SetActive(false);
                        arrived++;
                        rewardCoinsLabel.text = (finalBalance - reward + reward * arrived / count).ToString();
                    }
                }
                yield return null;
            }
            rewardCoinsLabel.text = finalBalance.ToString();
            yield return new WaitForSecondsRealtime(.3f);
            rewardWallet.gameObject.SetActive(false);
            Destroy(rewardFlights); rewardFlights = null; rewardRoutine = null;
        }

        void CancelRewardCoins()
        {
            if (rewardRoutine != null) { StopCoroutine(rewardRoutine); rewardRoutine = null; }
            if (rewardFlights) { Destroy(rewardFlights); rewardFlights = null; }
            if (rewardWallet) rewardWallet.gameObject.SetActive(false);
        }

        void ToggleVibration()
        {
            vibration = !vibration; Save(); RefreshSettings();
            PlayResultVibration(0);
        }

        Vector2Int GetVibrationFeedback(int result)
        {
            if (!vibration || (result < 0 && result != Hotfix.Manager.DifferenceRound.Miss)) return Vector2Int.zero;
            return result >= 0
                ? new Vector2Int(Mathf.Max(1, foundVibrationMilliseconds), Mathf.Clamp(foundVibrationAmplitude, 1, 255))
                : new Vector2Int(Mathf.Max(1, missVibrationMilliseconds), Mathf.Clamp(missVibrationAmplitude, 1, 255));
        }

        void PlayResultVibration(int result)
        {
            if (testing) return;
            var feedback = GetVibrationFeedback(result);
            if (feedback.x > 0) GameFrameX.Startup.Application.DifferenceHaptics.Play(feedback.x, feedback.y);
        }

        async void OpenSettingsLink(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address) ||
                (address.Scheme != "https" && address.Scheme != "http" && address.Scheme != "mailto"))
            {
                Notice("链接暂时不可用，请稍后重试。", 2);
                return;
            }
            if (UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            await System.Threading.Tasks.Task.Delay(500);
            Application.OpenURL(address.AbsoluteUri);
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
