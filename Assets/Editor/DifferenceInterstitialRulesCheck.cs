#if DIFFERENCE_AD_RULES_ISOLATED
// Compiled with the production DifferenceAds.cs by RunDifferenceInterstitialRulesCheck.ps1.
// These substitutes exist only in that separate process; Unity and real PlayerPrefs are never used.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GameFrameX.Startup.Application;

static class DifferenceInterstitialRulesCheck
{
    const string Prefs = "DifferenceAds.";
    const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
    static int assertions, shows, continuations;
    static readonly MethodInfo Gate = typeof(DifferenceAds).GetMethod("InterstitialBlockReason", Private);

    static int Main()
    {
        try
        {
            Check(Gate != null, "The production interstitial gate exposes the injected monotonic clock.");
            StartupProtection();
            JourneyRules();
            MainRules();
            PageContextAndColdStart();
            LegacyMainSignatures();
            CommonRules();
            if (DifferenceAds.SdkEnabled)
            {
                OperationsAndNoCatchUp();
                JourneyUpdatesGlobalCooldownOnly();
                MainHistoryAndRewarded();
                RewardedInterstitialSeparation();
                SharedIntervalAndPause();
            }
            else SdkFreeEntryPoints();
            Console.WriteLine("PASS: " + assertions + " assertions; SDK=" + DifferenceAds.SdkEnabled +
                "; injected clock, in-memory preferences, no Unity or device state used.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    static void StartupProtection()
    {
        Reset();
        UnityEngine.Time.realtimeSinceStartupAsDouble = 5000;
        foreach (var journey in new[] { false, true })
        {
            foreach (var now in new[] { 0d, 1d, 60d, 179d, 179.999999d })
                Allows(false, journey ? 3 : 6, journey, now, "Cold-start protection remains active before 180 seconds.");
            Allows(true, journey ? 3 : 6, journey, 180, "The protection boundary is inclusive at 180 seconds.");
            Allows(true, journey ? 3 : 6, journey, 180.001, "Eligible attempts work immediately after protection.");
        }
        Check(DifferenceAds.ApplyConfig(new DifferenceAdConfig
        {
            interstitialFirstLevel = 1, interstitialDenseLevel = 1,
            interstitialEarlyStep = 1, interstitialCooldownSeconds = 0, version = "remote-no-wait"
        }), "A valid permissive remote configuration is accepted.");
        Allows(false, 1, false, 179.999, "Remote first-level/CD rules cannot remove main startup protection.");
        Allows(false, 3, true, 179.999, "Remote first-level/CD rules cannot remove Journey startup protection.");
        Allows(true, 1, false, 180, "Remote main rules still take effect after protection.");
    }

    static void JourneyRules()
    {
        Reset();
        Check(DifferenceAds.ApplyConfig(new DifferenceAdConfig
        {
            interstitialFirstLevel = 50, interstitialDenseLevel = 70,
            interstitialEarlyStep = 9, interstitialCooldownSeconds = 999999, version = "strict-main-rules"
        }), "Strict main rules are valid.");
        foreach (var level in new[] { -1, 0, 1, 2 })
            Allows(false, level, true, 1000, "Journey levels below 3 never show an interstitial.");
        foreach (var level in new[] { 3, 4, 5, 6, 8, 15, 50, 100 })
        {
            UnityEngine.PlayerPrefs.SetInt(Prefs + "InterstitialShown." + level, 1);
            Allows(true, level, true, 1000, "Journey ignores main level spacing and shown history when the shared interval has expired.");
        }
    }

    static void MainRules()
    {
        Reset();
        foreach (var level in new[] { 1, 2, 3, 4, 5, 7, 9, 11, 13 })
            Allows(false, level, false, 1000, "Existing main first-level/early spacing is preserved.");
        foreach (var level in new[] { 6, 8, 10, 12, 14, 15, 16, 17 })
            Allows(true, level, false, 1000, "Existing main candidate levels remain eligible.");
        UnityEngine.PlayerPrefs.SetInt(Prefs + "InterstitialShown.6", 1);
        Allows(false, 6, false, 1000, "A main level's persistent shown record still blocks replay.");
        Allows(true, 6, true, 1000, "The same record does not block a Journey win.");
        UnityEngine.PlayerPrefs.DeleteKey(Prefs + "InterstitialShown.6");
        Set("lastInterstitialTime", 600d);
        Allows(false, 6, false, 719.999, "Main cooldown remains active below 120 seconds.");
        Allows(true, 6, false, 720, "Main cooldown expires exactly at 120 seconds.");
        Allows(false, 6, true, 600, "Journey shares the main cooldown.");
        Allows(true, 6, true, 720, "Journey is eligible after the shared cooldown.");
    }

    static void CommonRules()
    {
        foreach (var journey in new[] { false, true })
        {
            Reset();
            var level = journey ? 3 : 6;
            var config = DifferenceAds.Config;
            config.interstitialEnabled = false;
            DifferenceAds.ApplyConfig(config);
            Allows(false, level, journey, 1000, "The global interstitial switch applies to both modes.");
            Check(Reason(level, journey, 0) == "disabled", "The global switch takes priority over startup protection.");
            config.interstitialEnabled = true;
            DifferenceAds.ApplyConfig(config);
            Set("noAds", true);
            Allows(false, level, journey, 1000, "No-ads entitlement applies to both modes.");
            Check(Reason(level, journey, 0) == "no-ads", "No-ads entitlement takes priority over startup protection.");
            Set("noAds", false);
            var pending = typeof(DifferenceAds).GetField("pending", Private);
            pending.SetValue(null, Enum.ToObject(pending.FieldType, 1));
            Allows(false, level, journey, 1000, "An existing fullscreen ad blocks both modes.");
            Check(Reason(level, journey, 0) == "fullscreen-busy", "The fullscreen lock takes priority over startup protection.");
            pending.SetValue(null, Enum.ToObject(pending.FieldType, 0));
            DifferenceAds.ShowInterstitial = null;
            Allows(false, level, journey, 1000, "A missing provider never starts an ad.");
            DifferenceAds.ShowInterstitial = _ => { };
            DifferenceAds.InterstitialReady = () => false;
            Allows(false, level, journey, 1000, "An ad that is not ready is skipped.");
            DifferenceAds.InterstitialReady = () => throw new InvalidOperationException("Injected readiness failure");
            Allows(false, level, journey, 1000, "A provider readiness exception is treated as not ready.");
        }
    }

    static void PageContextAndColdStart()
    {
        Reset();
        Check(!DifferenceAds.CurrentJourney, "A cold runtime starts outside Journey mode.");
        var refreshes = 0;
        DifferenceAds.RefreshRequested += () => refreshes++;
        DifferenceAds.SetGamePage(3, true, true);
        Check(DifferenceAds.CurrentJourney && DifferenceAds.CurrentLevel == 3 && DifferenceAds.GamePageVisible,
            "The page context exposes the current Journey level for preloading.");
        DifferenceAds.SetGamePage(3, true);
        Check(!DifferenceAds.CurrentJourney && refreshes == 2, "Switching mode at the same level and visibility refreshes the provider.");
        UnityEngine.Time.realtimeSinceStartupAsDouble = 179;
        DifferenceAds.SetGamePage(6, true);
        Check(Reason(6, false, 179) == "startup-protection", "Opening or switching pages cannot bypass process startup protection.");
        UnityEngine.Time.realtimeSinceStartupAsDouble = 180;
        DifferenceAds.SetGamePage(3, true, true);
        Allows(true, 3, true, 180, "Switching into Journey does not restart the 180-second process clock.");
        typeof(DifferenceAds).GetMethod("ResetRuntime", Private).Invoke(null, null);
        Check(!DifferenceAds.CurrentJourney, "A new cold runtime clears the Journey page context.");
    }

    static void OperationsAndNoCatchUp()
    {
        foreach (var journey in new[] { false, true })
        foreach (var placement in new[] { "level_complete", "level_enter" })
        {
            Reset();
            var level = journey ? 3 : 6;
            UnityEngine.Time.realtimeSinceStartupAsDouble = 179.999;
            Attempt(level, "protected", journey, placement);
            Check(shows == 0 && continuations == 1 && !DifferenceAds.Busy, "A protected win/retry attempt continues gameplay immediately: " + placement);
            UnityEngine.Time.realtimeSinceStartupAsDouble = 180;
            Check(shows == 0, "Ending protection does not queue an earlier skipped ad.");
            Attempt(level, "protected", journey, placement);
            Check(shows == 0 && continuations == 1, "Repeating the protected operation cannot produce a catch-up ad or second continuation.");
            Attempt(level, "next-operation", journey, placement);
            Check(shows == 1 && continuations == 2 && !DifferenceAds.Busy, "A later independent eligible operation can show once.");
            Attempt(level, "next-operation", journey, placement);
            Check(shows == 1 && continuations == 2, "A repeated callback cannot show or continue a second time.");
        }
        Reset();
        foreach (var level in new[] { 1, 2 })
        foreach (var placement in new[] { "level_complete", "level_enter" })
            Attempt(level, "journey-protected-" + level + "-" + placement, true, placement);
        Check(shows == 0 && continuations == 4, "Journey levels 1/2 never show on either completion or failure retry.");
        Attempt(3, "journey-first-win", true);
        Attempt(3, "journey-first-retry", true, "level_enter");
        Attempt(3, "journey-second-retry", true, "level_enter");
        Attempt(3, "journey-replay-win", true);
        Attempt(4, "journey-next-retry", true, "level_enter");
        Check(shows == 1 && continuations == 9 && !DifferenceAds.Busy,
            "Journey retries also obey the shared cooldown and continue immediately.");
        Attempt(4, "journey-next-retry", true, "level_enter");
        Check(shows == 1 && continuations == 9, "The same Journey retry callback still executes only once.");
        UnityEngine.Time.realtimeSinceStartupAsDouble += 120;
        Attempt(4, "journey-after-cooldown", true);
        Check(shows == 2 && continuations == 10, "A new Journey operation can show after 120 seconds.");
    }

    static void LegacyMainSignatures()
    {
        var ads = typeof(DifferenceAds);
        Check(ads.GetMethod("SetGamePage", new[] { typeof(int), typeof(bool) }) != null,
            "The original two-argument SetGamePage signature remains available to old hotfix assemblies.");
        Check(ads.GetMethod("TryInterstitial", new[] { typeof(int), typeof(string), typeof(string), typeof(Action) }) != null,
            "The original four-argument TryInterstitial signature remains available to old hotfix assemblies.");
        Check(!ads.GetMethod("SetGamePage", new[] { typeof(int), typeof(bool), typeof(bool) }).GetParameters()[2].IsOptional &&
            !ads.GetMethod("TryInterstitial", new[] { typeof(int), typeof(string), typeof(string), typeof(Action), typeof(bool) }).GetParameters()[4].IsOptional,
            "The Journey-aware overloads use explicit mode arguments.");
        Reset();
        DifferenceAds.SetGamePage(6, true, true);
        DifferenceAds.SetGamePage(6, true);
        Check(!DifferenceAds.CurrentJourney && DifferenceAds.GamePageVisible, "The legacy page overload always forwards to main mode.");
        DifferenceAds.SetGamePage(6, true, true);
        UnityEngine.PlayerPrefs.SetInt(Prefs + "InterstitialShown.6", 1);
        DifferenceAds.TryInterstitial(6, "legacy-shown", "level_complete", () => continuations++);
        DifferenceAds.TryInterstitial(3, "legacy-early", "level_enter", () => continuations++);
        Check(shows == 0 && continuations == 2, "The legacy ad overload preserves main shown/first-level rules even when the page context is Journey.");
        UnityEngine.PlayerPrefs.DeleteKey(Prefs + "InterstitialShown.6");
        DifferenceAds.TryInterstitial(6, "legacy-main-win", "level_complete", () => continuations++);
        Check(shows == (DifferenceAds.SdkEnabled ? 1 : 0) && continuations == 3,
            "A fresh eligible legacy call uses the main SDK-on/off behavior.");
        if (!DifferenceAds.SdkEnabled) return;
        Check(UnityEngine.PlayerPrefs.GetInt(Prefs + "InterstitialShown.6", 0) == 1,
            "The legacy overload persists a main-level shown record.");
        DifferenceAds.TryInterstitial(8, "legacy-cooldown", "level_enter", () => continuations++);
        Check(shows == 1 && continuations == 4, "Legacy main retries preserve the 120-second cooldown.");
        UnityEngine.Time.realtimeSinceStartupAsDouble += 120;
        DifferenceAds.TryInterstitial(8, "legacy-main-retry", "level_enter", () => continuations++);
        Check(shows == 2 && continuations == 5 && UnityEngine.PlayerPrefs.GetInt(Prefs + "InterstitialShown.8", 0) == 1,
            "The legacy retry entry works after cooldown and remains main mode.");
    }

    static void JourneyUpdatesGlobalCooldownOnly()
    {
        Reset();
        Attempt(6, "journey-same-number-as-main", true);
        Check(shows == 1 && continuations == 1, "The Journey ad is shown.");
        Check(!UnityEngine.PlayerPrefs.HasKey(Prefs + "InterstitialShown.6"), "Journey must not write the main shown record.");
        Allows(false, 6, false, 319.999, "A Journey ad starts the shared cooldown for main mode.");
        Allows(true, 6, false, 320, "Main mode may show after 120 seconds.");
        Allows(false, 6, true, 319.999, "Journey also respects the shared cooldown.");
        Allows(true, 6, true, 320, "Journey may show after 120 seconds.");
    }

    static void MainHistoryAndRewarded()
    {
        Reset();
        Attempt(6, "main-first", false);
        Check(shows == 1 && UnityEngine.PlayerPrefs.GetInt(Prefs + "InterstitialShown.6", 0) == 1 &&
            !UnityEngine.PlayerPrefs.HasKey(Prefs + "LastInterstitialUtc"), "Only the main-level shown record is persisted.");
        Attempt(6, "main-replay", false);
        Attempt(8, "main-too-soon", false);
        Check(shows == 1 && continuations == 3, "Main replay history and cooldown continue without duplicate ads.");
        UnityEngine.Time.realtimeSinceStartupAsDouble += 120;
        Attempt(8, "main-after-cooldown", false, "level_enter");
        Check(shows == 2 && continuations == 4, "Main failure retry remains eligible after its normal cooldown.");

        Reset();
        UnityEngine.Time.realtimeSinceStartupAsDouble = 1;
        var rewardedShows = 0; var rewards = 0; var finishes = 0;
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ =>
        {
            rewardedShows++;
            DifferenceAds.RewardedDisplayed();
            DifferenceAds.RewardedEarned();
            DifferenceAds.RewardedEarned();
            DifferenceAds.FullscreenClosed(false);
        };
        Check(DifferenceAds.TryRewarded(1, "hint_reward", () => rewards++, () => finishes++), "A voluntary rewarded ad is accepted during startup protection.");
        Check(rewardedShows == 1 && rewards == 1 && finishes == 1 && !DifferenceAds.Busy && !UnityEngine.AudioListener.pause,
            "Reward ownership remains once-only and fullscreen input/audio release completes.");
    }

    static void SdkFreeEntryPoints()
    {
        Reset();
        Attempt(6, "sdk-free-main", false);
        Attempt(3, "sdk-free-journey", true);
        Check(shows == 0 && continuations == 2 && !DifferenceAds.Busy, "SDK-free builds skip both modes and continue immediately.");
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ => throw new Exception("SDK-free rewarded provider must not be invoked.");
        Check(!DifferenceAds.TryRewarded(1, "hint_reward", () => { }, () => { }), "SDK-free builds do not request rewarded ads.");
        Check(double.IsNegativeInfinity((double)Get("lastInterstitialTime")), "SDK-free attempts do not start the interval.");
    }

    static void RewardedInterstitialSeparation()
    {
        foreach (var journey in new[] { false, true })
        {
            Reset();
            var level = journey ? 3 : 6;
            var rewards = 0; var finishes = 0;
            DifferenceAds.RewardedReady = () => true;
            DifferenceAds.ShowRewarded = _ =>
            {
                DifferenceAds.RewardedDisplayed();
                UnityEngine.Time.realtimeSinceStartupAsDouble = 250;
                DifferenceAds.RewardedEarned();
                DifferenceAds.FullscreenClosed(false);
            };
            Check(DifferenceAds.TryRewarded(level, "hint_reward", () => rewards++, () =>
            {
                finishes++;
                Attempt(level, "after-reward", journey);
            }), "Rewarded ad was accepted.");
            Check(rewards == 1 && finishes == 1 && shows == 0 && continuations == 1 && !DifferenceAds.Busy,
                "Reward and continuation complete without an immediate interstitial in either mode.");
            Check(Reason(level, journey, 369.999) == "cooldown", "Cooldown starts when the displayed rewarded ad closes.");
            Allows(true, level, journey, 370, "A new eligible interstitial may show after 120 seconds.");
            UnityEngine.Time.realtimeSinceStartupAsDouble = 370;
            Attempt(level, "after-reward-expired", journey);
            Check(shows == 1 && continuations == 2, "The next eligible operation can display after the cooldown.");
        }

        Reset();
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ => DifferenceAds.FullscreenClosed(true);
        Check(DifferenceAds.TryRewarded(6, "hint_reward", null, null), "A failed request still completes its callbacks.");
        Allows(true, 6, false, 200, "A rewarded ad that never displayed must not start a cooldown.");

        Reset();
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ =>
        {
            DifferenceAds.RewardedDisplayed();
            DifferenceAds.RewardedEarned();
            UnityEngine.Time.realtimeSinceStartupAsDouble = 240;
            DifferenceAds.FullscreenClosed(true);
        };
        Check(DifferenceAds.TryRewarded(6, "hint_reward", null, null), "A rewarded display may fail after earlier SDK callbacks.");
        Check(double.IsNegativeInfinity((double)Get("lastInterstitialTime")), "A failed callback never starts a reward interval, even after display or reward.");
        Allows(true, 6, false, 240, "A failed rewarded callback leaves the existing interval unchanged.");

        Reset();
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ => DifferenceAds.FullscreenClosed(false);
        Check(DifferenceAds.TryRewarded(6, "hint_reward", null, null), "A hidden callback may arrive without a displayed callback.");
        Check(Reason(6, false, 319.999) == "cooldown", "Rewarded hidden starts cooldown even when earlier SDK callbacks are missing.");
        Allows(true, 6, false, 320, "The hidden callback cooldown expires after 120 seconds.");

        Reset();
        var config = DifferenceAds.Config;
        config.interstitialCooldownSeconds = 0;
        Check(DifferenceAds.ApplyConfig(config) && DifferenceAds.Config.interstitialCooldownSeconds == 120,
            "A remote interval cannot override the fixed 120-second policy.");
        DifferenceAds.RewardedReady = () => true;
        DifferenceAds.ShowRewarded = _ => { DifferenceAds.RewardedDisplayed(); DifferenceAds.FullscreenClosed(false); };
        DifferenceAds.TryRewarded(3, "hint_reward", null, null);
        Check(Reason(3, true, 319.999) == "cooldown", "The reward starts the fixed 120-second interval.");
        Allows(true, 3, true, 320, "The shared cooldown expires after 120 seconds.");
    }

    static void SharedIntervalAndPause()
    {
        Reset();
        DifferenceAds.ShowInterstitial = _ =>
        {
            shows++;
            DifferenceAds.InterstitialDisplayed();
            UnityEngine.Time.realtimeSinceStartupAsDouble = 240;
            DifferenceAds.FullscreenClosed(false);
        };
        Attempt(6, "long-interstitial", false);
        Check(shows == 1 && continuations == 1, "A displayed interstitial closes and continues.");
        Check(Reason(3, true, 359.999) == "cooldown", "An interstitial's interval restarts when it closes, not only when requested.");
        Allows(true, 3, true, 360, "An interstitial permits the next candidate 120 game seconds after close.");

        Reset();
        DifferenceAds.ShowInterstitial = _ =>
        {
            shows++;
            DifferenceAds.InterstitialDisplayed();
            UnityEngine.Time.realtimeSinceStartupAsDouble = 240;
            DifferenceAds.FullscreenClosed(true);
        };
        Attempt(6, "failed-after-display", false);
        Check((double)Get("lastInterstitialTime") == 200, "A failed interstitial retains the timer started by its eligible attempt.");
        Allows(true, 3, true, 320, "A failed interstitial does not restart the timer at the failure callback.");

        Reset();
        DifferenceAds.InterstitialReady = () => false;
        Attempt(6, "not-ready", false);
        Check(shows == 0 && continuations == 1, "An unready interstitial continues gameplay.");
        Check((double)Get("lastInterstitialTime") == 200, "An eligible but unready attempt resets the shared interval.");
        DifferenceAds.InterstitialReady = () => true;
        Attempt(3, "after-unready", true);
        Check(shows == 0 && continuations == 2, "The attempt prevents an immediate Journey interstitial.");
        UnityEngine.Time.realtimeSinceStartupAsDouble = 5000;
        UnityEngine.Time.timeAsDouble = 200;
        Check((string)Gate.Invoke(null, new object[] { 3, true, 5000d }) == "cooldown", "Wall time while paused does not advance the gameplay interval.");
        UnityEngine.Time.timeAsDouble = 320;
        Check((string)Gate.Invoke(null, new object[] { 3, true, 5000d }) == null, "120 seconds of game time expire the interval.");
        typeof(DifferenceAds).GetMethod("ResetRuntime", Private).Invoke(null, null);
        Set("initialized", true);
        DifferenceAds.InterstitialReady = () => true;
        DifferenceAds.ShowInterstitial = _ => { };
        Check((string)Gate.Invoke(null, new object[] { 3, true, 5000d }) == null, "A restart does not restore a prior cooldown.");
    }

    static void Reset()
    {
        typeof(DifferenceAds).GetMethod("ResetRuntime", Private).Invoke(null, null);
        UnityEngine.PlayerPrefs.Clear();
        UnityEngine.Time.realtimeSinceStartupAsDouble = 200;
        UnityEngine.AudioListener.pause = false;
        Set("initialized", true);
        Set("noAds", false);
        Set("highestLevel", 0);
        Set("lastInterstitialTime", double.NegativeInfinity);
        DifferenceAds.InterstitialReady = () => true;
        DifferenceAds.ShowInterstitial = _ =>
        {
            shows++;
            DifferenceAds.InterstitialDisplayed();
            DifferenceAds.FullscreenClosed(false);
        };
        shows = continuations = 0;
    }

    static void Attempt(int level, string operation, bool journey, string placement = "level_complete") =>
        DifferenceAds.TryInterstitial(level, operation, placement, () => continuations++, journey);
    static void Allows(bool expected, int level, bool journey, double now, string message)
    {
        var reason = Reason(level, journey, now);
        Check((reason == null) == expected, message + " [level=" + level + ", journey=" + journey + ", now=" + now.ToString(CultureInfo.InvariantCulture) + ", reason=" + (reason ?? "allowed") + "]");
    }
    static string Reason(int level, bool journey, double now)
    {
        UnityEngine.Time.timeAsDouble = now;
        return (string)Gate.Invoke(null, new object[] { level, journey, now });
    }
    static object Get(string name) => typeof(DifferenceAds).GetField(name, Private).GetValue(null);
    static void Set(string name, object value) => typeof(DifferenceAds).GetField(name, Private).SetValue(null, value);
    static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        assertions++;
    }
}

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { } }
    public static class Time { public static double timeAsDouble; static double real;
        public static double realtimeSinceStartupAsDouble { get => real; set { real = value; timeAsDouble = value; } } }
    public static class AudioListener { public static bool pause; }
    public static class Debug { public static void Log(object message) { } public static void LogWarning(object message) { } }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static int GetInt(string key, int fallback = 0) => Values.TryGetValue(key, out var value) ? (int)value : fallback;
        public static string GetString(string key, string fallback = "") => Values.TryGetValue(key, out var value) ? (string)value : fallback;
        public static void SetInt(string key, int value) => Values[key] = value;
        public static void SetString(string key, string value) => Values[key] = value;
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void Save() { }
        public static void Clear() => Values.Clear();
    }
}
namespace GameFrameX.Startup.Application
{
    public static class DifferenceAnalytics { public static void Track(string name, params object[] values) { } }
}
#endif
