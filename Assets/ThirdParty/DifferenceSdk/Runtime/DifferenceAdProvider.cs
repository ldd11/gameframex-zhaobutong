#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using System;
using GameFrameX.Startup.Application;
using UnityEngine;

// MAX transport only. Level rules, the fullscreen lock and reward ownership live in DifferenceAds.
[DisallowMultipleComponent]
public sealed class DifferenceAdProvider : MonoBehaviour
{
    string interstitialId, rewardedId, bannerId, appOpenId;
    string interstitialPlacement, rewardedPlacement;
    bool initialized, applicationPaused, interstitialLoading, interstitialReady, rewardedLoading, rewardedReady;
    bool bannerCreated, bannerLoading, bannerLoaded, bannerVisible, bannerRequestObserved, displayed, rewardDelivered;
    int interstitialFailures, rewardedFailures, bannerFailures, fullscreen;
    double interstitialRetryAt, rewardedRetryAt, bannerRetryAt, refreshAt, showRequestedAt;
    double Now => Time.realtimeSinceStartupAsDouble;

    public void Initialize(string interstitial, string rewarded, string banner, string appOpen = null)
    {
        if (initialized) return;
        interstitialId = interstitial?.Trim();
        rewardedId = rewarded?.Trim();
        bannerId = banner?.Trim();
        appOpenId = appOpen?.Trim();
        initialized = true;
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += InterstitialLoaded;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += InterstitialLoadFailed;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += InterstitialDisplayed;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += InterstitialDisplayFailed;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += InterstitialHidden;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent += InterstitialClicked;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += InterstitialRevenue;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += RewardedLoaded;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += RewardedLoadFailed;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += RewardedDisplayed;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += RewardedDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += RewardedEarned;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += RewardedHidden;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += RewardedClicked;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += RewardedRevenue;
        MaxSdkCallbacks.Banner.OnAdLoadedEvent += BannerLoaded;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += BannerLoadFailed;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += BannerRevenue;
        // Observe real callbacks only; configuring this ID does not request or show app-open ads.
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += AppOpenLoaded;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += AppOpenLoadFailed;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += AppOpenDisplayed;
        MaxSdkCallbacks.AppOpen.OnAdClickedEvent += AppOpenClicked;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += AppOpenHidden;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += AppOpenRevenue;
        DifferenceAds.InterstitialReady = IsInterstitialReady;
        DifferenceAds.RewardedReady = IsRewardedReady;
        DifferenceAds.ShowInterstitial = ShowInterstitial;
        DifferenceAds.ShowRewarded = ShowRewarded;
        DifferenceAds.RefreshRequested += Refresh;
        Refresh();
    }

    void Update()
    {
        if (!initialized) return;
        // MAX can return without a callback if native readiness changes between check and show.
        if (fullscreen != 0 && !displayed && Application.isFocused && !applicationPaused && Now - showRequestedAt >= 15)
            CloseFullscreen(true);
        if (Now < refreshAt) return;
        refreshAt = Now + 0.5;
        Refresh();
    }

    void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        if (!paused && fullscreen != 0 && !displayed) showRequestedAt = Now;
    }

    void OnApplicationFocus(bool focused)
    {
        // Give native callbacks time to reach Unity after dismissing an ad or returning to the app.
        if (focused && fullscreen != 0 && !displayed) showRequestedAt = Now;
    }

    bool IsInterstitialReady()
    {
        if (!initialized || string.IsNullOrEmpty(interstitialId) || DifferenceAds.NoAds ||
            !DifferenceAds.Config.interstitialEnabled || fullscreen != 0 || !interstitialReady) return false;
        try { return interstitialReady = MaxSdk.IsInterstitialReady(interstitialId); }
        catch (Exception exception) { Report("Interstitial readiness", exception); return interstitialReady = false; }
    }

    bool IsRewardedReady()
    {
        if (!initialized || string.IsNullOrEmpty(rewardedId) || !DifferenceAds.Config.rewardedEnabled ||
            fullscreen != 0 || !rewardedReady) return false;
        try { return rewardedReady = MaxSdk.IsRewardedAdReady(rewardedId); }
        catch (Exception exception) { Report("Rewarded readiness", exception); return rewardedReady = false; }
    }

    void ShowInterstitial(string placement)
    {
        if (!IsInterstitialReady()) { DifferenceAds.FullscreenClosed(true); return; }
        fullscreen = 1;
        interstitialPlacement = AnalyticsPlacement(placement);
        displayed = false;
        interstitialReady = false;
        showRequestedAt = Now;
        try { MaxSdk.ShowInterstitial(interstitialId, placement); }
        catch (Exception exception) { Report("Interstitial show", exception); CloseFullscreen(true); }
    }

    void ShowRewarded(string placement)
    {
        if (!IsRewardedReady()) { DifferenceAds.FullscreenClosed(true); return; }
        fullscreen = 2;
        rewardedPlacement = AnalyticsPlacement(placement);
        displayed = rewardDelivered = false;
        rewardedReady = false;
        showRequestedAt = Now;
        try { MaxSdk.ShowRewardedAd(rewardedId, placement); }
        catch (Exception exception) { Report("Rewarded show", exception); CloseFullscreen(true); }
    }

    void Refresh()
    {
        if (!initialized) return;
        var config = DifferenceAds.Config;
        SyncBanner(config);
        if (fullscreen != 0 || DifferenceAds.Busy) return;
        if (!DifferenceAds.NoAds && config.interstitialEnabled &&
            DifferenceAds.HighestLevel >= config.interstitialFirstLevel && !string.IsNullOrEmpty(interstitialId) &&
            !interstitialLoading && !interstitialReady && Now >= interstitialRetryAt)
        {
            interstitialLoading = true;
            try { MaxSdk.LoadInterstitial(interstitialId); }
            catch (Exception exception) { Report("Interstitial load", exception); InterstitialLoadFailed(interstitialId, null); }
        }
        if (config.rewardedEnabled && DifferenceAds.HighestLevel >= config.rewardedPreloadLevel &&
            !string.IsNullOrEmpty(rewardedId) && !rewardedLoading && !rewardedReady && Now >= rewardedRetryAt)
        {
            rewardedLoading = true;
            try { MaxSdk.LoadRewardedAd(rewardedId); }
            catch (Exception exception) { Report("Rewarded load", exception); RewardedLoadFailed(rewardedId, null); }
        }
    }

    void SyncBanner(DifferenceAdConfig config)
    {
        var allowed = config.bannerEnabled && !DifferenceAds.NoAds &&
            DifferenceAds.HighestLevel >= 1 && !string.IsNullOrEmpty(bannerId);
        if (!allowed)
        {
            DestroyBanner();
            return;
        }
        try
        {
            if (!bannerCreated)
            {
                if (Now < bannerRetryAt) return;
                bannerRequestObserved = false;
                bannerCreated = bannerLoading = true;
#if UNITY_ANDROID
                // The helper installs the request listener before the first native load, on Android's UI thread.
                using (var bridge = new AndroidJavaClass("com.difference.sdk.DifferenceBannerAnalytics"))
                    bridge.CallStatic("createBanner", bannerId, gameObject.name);
#else
                MaxSdk.CreateBanner(bannerId, new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter));
#endif
                // Create starts loading; hide immediately so level 1 never flashes a banner.
                MaxSdk.HideBanner(bannerId);
                MaxSdk.StopBannerAutoRefresh(bannerId);
            }
            else if (!bannerLoaded && !bannerLoading && Now >= bannerRetryAt)
            {
                bannerLoading = true;
                MaxSdk.LoadBanner(bannerId);
            }
            var visible = bannerLoaded && DifferenceAds.GamePageVisible && !DifferenceAds.Busy &&
                DifferenceAds.CurrentLevel >= config.bannerFirstVisibleLevel;
            if (visible == bannerVisible) return;
            bannerVisible = visible;
            if (visible)
            {
                MaxSdk.ShowBanner(bannerId);
                MaxSdk.StartBannerAutoRefresh(bannerId);
            }
            else
            {
                MaxSdk.HideBanner(bannerId);
                MaxSdk.StopBannerAutoRefresh(bannerId);
            }
        }
        catch (Exception exception)
        {
            Report("Banner", exception);
            bannerRetryAt = Now + RetryDelay(++bannerFailures);
            DestroyBanner();
        }
    }

    void DestroyBanner()
    {
        if (!bannerCreated) return;
        bannerCreated = bannerLoading = bannerLoaded = bannerVisible = bannerRequestObserved = false;
        try { MaxSdk.HideBanner(bannerId); MaxSdk.DestroyBanner(bannerId); }
        catch (Exception exception) { Report("Banner cleanup", exception); }
    }

    void InterstitialLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (id != interstitialId || !initialized) return;
        interstitialLoading = false;
        interstitialReady = true;
        interstitialFailures = 0;
        TrackPreload("interstitial", true);
    }
    void InterstitialLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (id != interstitialId || !initialized) return;
        interstitialLoading = interstitialReady = false;
        interstitialRetryAt = Now + RetryDelay(++interstitialFailures);
        if (error != null) TrackPreload("interstitial", false);
    }
    void RewardedLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedId || !initialized) return;
        rewardedLoading = false;
        rewardedReady = true;
        rewardedFailures = 0;
        TrackPreload("reward", true);
    }
    void RewardedLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (id != rewardedId || !initialized) return;
        rewardedLoading = rewardedReady = false;
        rewardedRetryAt = Now + RetryDelay(++rewardedFailures);
        if (error != null) TrackPreload("reward", false);
    }
    void BannerLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (id != bannerId || !bannerCreated) return;
        bannerLoading = false;
        bannerLoaded = true;
        bannerFailures = 0;
        // Loaded is not an impression. RevenuePaid is the observable MAX banner impression callback.
        Refresh();
    }
    void BannerLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (id != bannerId || !bannerCreated) return;
        bannerLoading = bannerLoaded = false;
        bannerRetryAt = Now + RetryDelay(++bannerFailures);
        if (error != null && bannerRequestObserved) DifferenceAnalytics.BannerLoadFailed();
        Refresh();
    }
    void InterstitialDisplayed(string id, MaxSdkBase.AdInfo info)
    {
        if (id != interstitialId || fullscreen != 1 || displayed) return;
        displayed = true;
        TrackAd("interstitial_ad_show", interstitialPlacement);
        DifferenceAds.InterstitialDisplayed();
    }
    void RewardedDisplayed(string id, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedId || fullscreen != 2 || displayed) return;
        displayed = true;
        TrackAd("reward_ad_show", rewardedPlacement);
        DifferenceAds.RewardedDisplayed();
    }
    void RewardedEarned(string id, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo info)
    {
        if (id != rewardedId || fullscreen != 2 || rewardDelivered) return;
        rewardDelivered = true;
        TrackAd("reward_ad_rewarded", rewardedPlacement);
        DifferenceAds.RewardedEarned();
    }
    void InterstitialDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
    {
        if (id == interstitialId && fullscreen == 1) CloseFullscreen(true);
    }
    void RewardedDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
    {
        if (id == rewardedId && fullscreen == 2) CloseFullscreen(true);
    }
    void InterstitialHidden(string id, MaxSdkBase.AdInfo info)
    {
        if (id != interstitialId || fullscreen != 1) return;
        TrackAd("interstitial_ad_close", interstitialPlacement);
        CloseFullscreen(false);
    }
    void RewardedHidden(string id, MaxSdkBase.AdInfo info)
    {
        if (id == rewardedId && fullscreen == 2) CloseFullscreen(false);
    }
    void CloseFullscreen(bool failed)
    {
        if (fullscreen == 0) return;
        if (fullscreen == 1) interstitialRetryAt = Now + (failed ? 2 : 0);
        else rewardedRetryAt = Now + (failed ? 2 : 0);
        fullscreen = 0;
        displayed = false;
        DifferenceAds.FullscreenClosed(failed);
    }

    void InterstitialClicked(string id, MaxSdkBase.AdInfo info)
    {
        if (id == interstitialId && fullscreen == 1) TrackAd("interstitial_ad_click", interstitialPlacement);
    }
    void RewardedClicked(string id, MaxSdkBase.AdInfo info)
    {
        if (id == rewardedId && fullscreen == 2) TrackAd("reward_ad_click", rewardedPlacement);
    }
    void InterstitialRevenue(string id, MaxSdkBase.AdInfo info)
    {
        if (id == interstitialId) RecordRevenue(info);
    }
    void RewardedRevenue(string id, MaxSdkBase.AdInfo info)
    {
        if (id == rewardedId) RecordRevenue(info);
    }
    void BannerRevenue(string id, MaxSdkBase.AdInfo info)
    {
        if (id != bannerId || !initialized) return;
        RecordRevenue(info);
        // If the native request hook is unavailable, don't report a batch with fabricated zero requests.
        // Impression revenue is still valid and is reported independently above.
        if (info != null && bannerCreated && bannerRequestObserved) DifferenceAnalytics.BannerShown();
    }
    void AppOpenLoaded(string id, MaxSdkBase.AdInfo info)
    {
        if (IsAppOpen(id)) TrackPreload("appopen", true);
    }
    void AppOpenLoadFailed(string id, MaxSdkBase.ErrorInfo error)
    {
        if (IsAppOpen(id)) TrackPreload("appopen", false);
    }
    void AppOpenDisplayed(string id, MaxSdkBase.AdInfo info)
    {
        if (IsAppOpen(id)) TrackAd("appopen_ad_show", AnalyticsPlacement(info?.Placement));
    }
    void AppOpenClicked(string id, MaxSdkBase.AdInfo info)
    {
        if (IsAppOpen(id)) TrackAd("appopen_ad_click", AnalyticsPlacement(info?.Placement));
    }
    void AppOpenHidden(string id, MaxSdkBase.AdInfo info)
    {
        if (IsAppOpen(id)) TrackAd("appopen_ad_close", AnalyticsPlacement(info?.Placement));
    }
    void AppOpenRevenue(string id, MaxSdkBase.AdInfo info)
    {
        if (IsAppOpen(id)) RecordRevenue(info);
    }
    bool IsAppOpen(string id) => initialized && !string.IsNullOrEmpty(appOpenId) && id == appOpenId;

    static void TrackPreload(string format, bool success) =>
        DifferenceAnalytics.Track(format + "_ad_preload", "load_status", success ? "true" : "false");
    static void TrackAd(string name, string placement)
    {
        if (string.IsNullOrEmpty(placement)) DifferenceAnalytics.Track(name);
        else DifferenceAnalytics.Track(name, "trigger_timing", placement);
    }
    static string AnalyticsPlacement(string placement)
    {
        switch (placement)
        {
            case "hint_reward": return "hint_1";
            case "revive_reward": return "revive";
            case "level_retry": return "level_enter";
            default: return placement;
        }
    }
    void RecordRevenue(MaxSdkBase.AdInfo info)
    {
        if (!initialized || info == null) return;
        DifferenceAnalytics.RecordAdRevenue(info.Revenue, info.AdFormat, info.NetworkName, info.AdUnitIdentifier,
            AnalyticsPlacement(info.Placement));
    }

    // Called by the native request listener, including MAX's native banner auto-refresh requests.
    public void OnBannerRequestStarted(string id)
    {
        if (!initialized || !bannerCreated || id != bannerId) return;
        bannerRequestObserved = true;
        DifferenceAnalytics.BannerRequest();
    }

    static double RetryDelay(int failures) => Math.Min(60, Math.Pow(2, Math.Min(failures, 6)));
    static void Report(string operation, Exception exception) =>
        Debug.LogWarning("[Difference SDK] " + operation + " failed (" + exception.GetType().Name + ").");

    void OnDestroy()
    {
        if (!initialized) return;
        initialized = false;
        DifferenceAds.RefreshRequested -= Refresh;
        if (DifferenceAds.InterstitialReady == IsInterstitialReady) DifferenceAds.InterstitialReady = null;
        if (DifferenceAds.RewardedReady == IsRewardedReady) DifferenceAds.RewardedReady = null;
        if (DifferenceAds.ShowInterstitial == ShowInterstitial) DifferenceAds.ShowInterstitial = null;
        if (DifferenceAds.ShowRewarded == ShowRewarded) DifferenceAds.ShowRewarded = null;
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= InterstitialLoaded;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= InterstitialLoadFailed;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= InterstitialDisplayed;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= InterstitialDisplayFailed;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= InterstitialHidden;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= InterstitialClicked;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= InterstitialRevenue;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= RewardedLoaded;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= RewardedLoadFailed;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= RewardedDisplayed;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= RewardedDisplayFailed;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= RewardedEarned;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= RewardedHidden;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= RewardedClicked;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= RewardedRevenue;
        MaxSdkCallbacks.Banner.OnAdLoadedEvent -= BannerLoaded;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= BannerLoadFailed;
        MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= BannerRevenue;
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= AppOpenLoaded;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= AppOpenLoadFailed;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent -= AppOpenDisplayed;
        MaxSdkCallbacks.AppOpen.OnAdClickedEvent -= AppOpenClicked;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= AppOpenHidden;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent -= AppOpenRevenue;
        DestroyBanner();
        CloseFullscreen(true);
    }
}
#endif
