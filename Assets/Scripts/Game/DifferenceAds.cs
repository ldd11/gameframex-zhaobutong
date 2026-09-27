using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GameFrameX.Startup.Application
{
    [Serializable]
    public sealed class DifferenceAdConfig
    {
        public bool interstitialEnabled = true;
        public int interstitialFirstLevel = 6;
        public int interstitialDenseLevel = 15;
        public int interstitialEarlyStep = 2;
        public double interstitialCooldownSeconds = 60;
        public bool bannerEnabled = true;
        public int bannerFirstVisibleLevel = 2;
        public bool rewardedEnabled = true;
        public int rewardedPreloadLevel = 2;
        public string version = "defaults-v1";

        internal DifferenceAdConfig Copy() => (DifferenceAdConfig)MemberwiseClone();
    }

    // Game rules stay available in ordinary builds; the optional SDK supplies these delegates.
    public static class DifferenceAds
    {
        const string Key = "DifferenceAds.";
        enum FullscreenKind { None, Interstitial, Rewarded }
        static readonly HashSet<string> checkedOperations = new HashSet<string>();
        static DifferenceAdConfig config = new DifferenceAdConfig();
        static FullscreenKind pending;
        static bool initialized, noAds, displayed, rewardGranted, previousAudioPause;
        static int highestLevel, currentLevel, pendingLevel;
        static bool gamePageVisible;
        static double lastInterstitialTime = double.NegativeInfinity;
        static string pendingPlacement, pendingConfigVersion;
        static Action pendingFinished, pendingReward;

        public static Func<bool> InterstitialReady, RewardedReady;
        public static Action<string> ShowInterstitial, ShowRewarded;
        public static event Action<bool> FullscreenChanged;
        public static event Action RefreshRequested;

        public static bool SdkEnabled
        {
            get
            {
#if ENABLE_DIFFERENCE_SDK
                return true;
#else
                return false;
#endif
            }
        }
        public static bool Busy => pending != FullscreenKind.None;
        public static bool NoAds { get { EnsureInitialized(); return noAds; } }
        public static int HighestLevel { get { EnsureInitialized(); return highestLevel; } }
        public static int CurrentLevel => currentLevel;
        public static bool GamePageVisible => gamePageVisible;
        public static DifferenceAdConfig Config => config.Copy();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            if (Busy) AudioListener.pause = previousAudioPause;
            initialized = false;
            pending = FullscreenKind.None;
            currentLevel = 0;
            gamePageVisible = false;
            pendingFinished = pendingReward = null;
            InterstitialReady = RewardedReady = null;
            ShowInterstitial = ShowRewarded = null;
            FullscreenChanged = null;
            RefreshRequested = null;
            checkedOperations.Clear();
            config = new DifferenceAdConfig();
        }

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            noAds = PlayerPrefs.GetInt(Key + "NoAds", 0) == 1;
            highestLevel = Math.Max(0, PlayerPrefs.GetInt(Key + "HighestLevel", 0));
            lastInterstitialTime = double.NegativeInfinity;
            if (double.TryParse(PlayerPrefs.GetString(Key + "LastInterstitialUtc", ""),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var utc) &&
                !double.IsNaN(utc) && !double.IsInfinity(utc) && utc > 0)
            {
                // Convert persisted wall time once; in-session cooldown uses only the monotonic clock.
                // A device clock moving backwards starts a fresh cooldown, never an indefinite wait.
                var elapsed = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d - utc);
                lastInterstitialTime = Time.realtimeSinceStartupAsDouble - elapsed;
            }
        }

        public static bool ApplyConfig(DifferenceAdConfig value)
        {
            if (value == null || value.interstitialFirstLevel < 1 ||
                value.interstitialDenseLevel < value.interstitialFirstLevel || value.interstitialEarlyStep < 1 ||
                value.interstitialCooldownSeconds < 0 || double.IsNaN(value.interstitialCooldownSeconds) ||
                double.IsInfinity(value.interstitialCooldownSeconds) || value.bannerFirstVisibleLevel < 1 ||
                value.rewardedPreloadLevel < 1 || string.IsNullOrWhiteSpace(value.version) || value.version.Length > 64 ||
                value.version.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
            {
                Log("config", 0, "", "invalid-kept-current");
                return false;
            }
            config = value.Copy();
            Log("config", 0, "", "applied");
            NotifyRefresh();
            return true;
        }

        public static void SetNoAds(bool value)
        {
            EnsureInitialized();
            if (noAds == value) return;
            noAds = value;
            PlayerPrefs.SetInt(Key + "NoAds", value ? 1 : 0);
            PlayerPrefs.Save();
            Log("no-ads", currentLevel, "", value ? "enabled" : "disabled");
            // Changing policy never closes an ad already on screen or discards its reward callback.
            NotifyRefresh();
        }

        public static void SetGamePage(int level, bool visible)
        {
            EnsureInitialized();
            visible &= level > 0;
            level = Math.Max(0, level);
            if (currentLevel == level && gamePageVisible == visible) return;
            currentLevel = level;
            gamePageVisible = visible;
            if (visible && level > highestLevel)
            {
                highestLevel = level;
                PlayerPrefs.SetInt(Key + "HighestLevel", highestLevel);
                PlayerPrefs.Save();
            }
            NotifyRefresh();
        }

        public static void TryInterstitial(int level, string operationId, string placement, Action continuation)
        {
            EnsureInitialized();
            var rules = config;
            if (!string.IsNullOrEmpty(operationId) && !checkedOperations.Add(operationId))
            {
                Log("interstitial", level, placement, "duplicate-operation", rules.version);
                return; // A repeated UI callback must not advance the same operation a second time.
            }
            string reason = !SdkEnabled ? "sdk-off" : string.IsNullOrWhiteSpace(operationId) ? "invalid-operation" :
                !rules.interstitialEnabled ? "disabled" : noAds ? "no-ads" : Busy ? "fullscreen-busy" :
                level < rules.interstitialFirstLevel ? "before-first-level" :
                level < rules.interstitialDenseLevel && (level - rules.interstitialFirstLevel) % rules.interstitialEarlyStep != 0 ? "not-candidate" :
                PlayerPrefs.GetInt(Key + "InterstitialShown." + level, 0) == 1 ? "level-already-shown" :
                Time.realtimeSinceStartupAsDouble - lastInterstitialTime < rules.interstitialCooldownSeconds ? "cooldown" :
                ShowInterstitial == null || !IsReady(InterstitialReady) ? "not-ready" : null;
            if (reason == null || reason == "not-ready")
                DifferenceAnalytics.Track("interstitial_ad_trigger", "trigger_timing", placement == "level_retry" ? "level_enter" : placement);
            if (reason != null)
            {
                Log("interstitial", level, placement, reason, rules.version);
                InvokeSafely(continuation);
                return;
            }
            BeginFullscreen(FullscreenKind.Interstitial, level, placement, null, continuation, rules.version);
            try { ShowInterstitial(placement); }
            catch (Exception) { FullscreenClosed(true); }
        }

        public static bool TryRewarded(int level, string placement, Action reward, Action finished)
        {
            EnsureInitialized();
            var rules = config;
            var reason = !SdkEnabled ? "sdk-off" : !rules.rewardedEnabled ? "disabled" :
                level < 1 ? "invalid-level" : Busy ? "fullscreen-busy" :
                ShowRewarded == null || !IsReady(RewardedReady) ? "not-ready" : null;
            if (reason != null)
            {
                Log("rewarded", level, placement, reason, rules.version);
                return false;
            }
            BeginFullscreen(FullscreenKind.Rewarded, level, placement, reward, finished, rules.version);
            try { ShowRewarded(placement); }
            catch (Exception) { FullscreenClosed(true); }
            return true;
        }

        static bool IsReady(Func<bool> ready)
        {
            try { return ready != null && ready(); }
            catch (Exception) { return false; }
        }

        static void BeginFullscreen(FullscreenKind kind, int level, string placement, Action reward, Action finished, string configVersion)
        {
            pending = kind;
            pendingLevel = level;
            pendingPlacement = placement;
            pendingConfigVersion = configVersion;
            pendingReward = reward;
            pendingFinished = finished;
            displayed = rewardGranted = false;
            previousAudioPause = AudioListener.pause;
            AudioListener.pause = true;
            Log(kind.ToString(), level, placement, "requested", pendingConfigVersion);
            NotifyFullscreen(true);
            NotifyRefresh();
        }

        public static void InterstitialDisplayed()
        {
            if (pending != FullscreenKind.Interstitial || displayed) return;
            displayed = true;
            lastInterstitialTime = Time.realtimeSinceStartupAsDouble;
            PlayerPrefs.SetInt(Key + "InterstitialShown." + pendingLevel, 1);
            PlayerPrefs.SetString(Key + "LastInterstitialUtc",
                (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d).ToString("R", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            Log("interstitial", pendingLevel, pendingPlacement, "displayed", pendingConfigVersion);
        }

        public static void RewardedDisplayed()
        {
            if (pending != FullscreenKind.Rewarded || displayed) return;
            displayed = true;
            Log("rewarded", pendingLevel, pendingPlacement, "displayed", pendingConfigVersion);
        }

        public static void RewardedEarned()
        {
            if (pending != FullscreenKind.Rewarded || rewardGranted) return;
            rewardGranted = true;
            Log("rewarded", pendingLevel, pendingPlacement, "earned", pendingConfigVersion);
            InvokeSafely(pendingReward);
        }

        public static void FullscreenClosed(bool failed)
        {
            if (!Busy) return;
            Log(pending.ToString(), pendingLevel, pendingPlacement, failed ? "failed" : "closed", pendingConfigVersion);
            var finished = pendingFinished;
            pending = FullscreenKind.None;
            pendingFinished = pendingReward = null;
            AudioListener.pause = previousAudioPause;
            NotifyFullscreen(false);
            NotifyRefresh();
            InvokeSafely(finished);
        }

        static void NotifyFullscreen(bool value)
        {
            if (FullscreenChanged == null) return;
            foreach (Action<bool> handler in FullscreenChanged.GetInvocationList())
                try { handler(value); }
                catch (Exception exception) { Debug.LogWarning("[Difference Ads] fullscreen callback failed: " + exception.GetType().Name); }
        }

        static void NotifyRefresh()
        {
            if (RefreshRequested == null) return;
            foreach (Action handler in RefreshRequested.GetInvocationList()) InvokeSafely(handler);
        }

        static void InvokeSafely(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception exception) { Debug.LogWarning("[Difference Ads] callback failed: " + exception.GetType().Name); }
        }

        static void Log(string action, int level, string placement, string reason, string version = null) =>
            Debug.Log("[Difference Ads] version=" + (version ?? config.version) + " action=" + action + " level=" + level +
                " placement=" + placement + " reason=" + reason);
    }
}
