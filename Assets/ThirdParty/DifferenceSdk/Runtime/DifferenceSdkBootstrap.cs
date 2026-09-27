using UnityEngine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdjustSdk;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.RemoteConfig;
using GameFrameX.Startup.Application;
using GoogleMobileAds.Ump.Api;
#endif

// Added to the first built scene only when ENABLE_DIFFERENCE_SDK is enabled.
[DisallowMultipleComponent]
public sealed class DifferenceSdkBootstrap : MonoBehaviour
{
    public string maxSdkKey;
    public string adjustAppToken;
    public bool adjustSandbox;
    public string interstitialAdUnitId;
    public string rewardedAdUnitId;
    public string bannerAdUnitId;
    public string appOpenAdUnitId; // Reserved configuration; app-open ads are not requested.
    [System.Serializable]
    public sealed class AdjustEventToken
    {
        public string eventName;
        public string token;
    }
    public AdjustEventToken[] adjustEventTokens = new AdjustEventToken[0];

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private static DifferenceSdkBootstrap instance;
    private FirebaseApp firebaseApp;
    private const string LastAdConfigKey = "Difference.Sdk.LastAdConfig";
    private readonly HashSet<string> missingAdjustTokens = new HashSet<string>();

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        try
        {
            var cached = PlayerPrefs.GetString(LastAdConfigKey, "");
            if (!string.IsNullOrEmpty(cached))
                DifferenceAds.ApplyConfig(JsonUtility.FromJson<DifferenceAdConfig>(cached));
        }
        catch (Exception exception) { LogFailure("Cached ad configuration", exception); }
    }

    private void Start()
    {
        if (instance != this) return;
        // Each task handles its own failures; gameplay never waits for SDK/network startup.
        _ = InitializeFirebaseAsync();
        _ = InitializeAdvertisingAsync();
    }

    private async Task InitializeFirebaseAsync()
    {
        try
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (!this) return;
            if (status != DependencyStatus.Available)
            {
                Debug.LogWarning("[Difference SDK] Firebase dependencies unavailable: " + status);
                return;
            }
            firebaseApp = FirebaseApp.DefaultInstance;
            DifferenceAnalytics.SetFirebaseSender(SendFirebaseEvent);
            Crashlytics.IsCrashlyticsCollectionEnabled = true;
            Crashlytics.ReportUncaughtExceptionsAsFatal = true;
            Debug.Log("[Difference SDK] Firebase initialized.");
            await InitializeRemoteConfigAsync();
        }
        catch (Exception exception)
        {
            LogFailure("Firebase initialization", exception);
        }
    }

    private async Task InitializeRemoteConfigAsync()
    {
        try
        {
            var defaults = new DifferenceAdConfig();
            var parameters = new Dictionary<string, object>
            {
                { "interstitial_enabled", defaults.interstitialEnabled },
                { "interstitial_first_level", defaults.interstitialFirstLevel },
                { "interstitial_dense_level", defaults.interstitialDenseLevel },
                { "interstitial_early_step", defaults.interstitialEarlyStep },
                { "interstitial_cooldown_seconds", defaults.interstitialCooldownSeconds },
                { "banner_enabled", defaults.bannerEnabled },
                { "banner_first_visible_level", defaults.bannerFirstVisibleLevel },
                { "rewarded_enabled", defaults.rewardedEnabled },
                { "rewarded_preload_level", defaults.rewardedPreloadLevel }
            };
            var remote = FirebaseRemoteConfig.DefaultInstance;
            await remote.SetDefaultsAsync(parameters);
            if (!this) return;
            try { ApplyRemoteConfig(remote, parameters.Keys); }
            catch (Exception exception) { LogFailure("Cached Remote Config", exception); }
            await remote.FetchAsync(TimeSpan.FromHours(1));
            if (!this) return;
            await remote.ActivateAsync();
            if (this) ApplyRemoteConfig(remote, parameters.Keys);
        }
        catch (Exception exception)
        {
            // The defaults or previously accepted configuration stay active on failure.
            LogFailure("Remote Config", exception);
        }
    }

    private static void ApplyRemoteConfig(FirebaseRemoteConfig remote, IEnumerable<string> keys)
    {
        var hasRemoteValue = false;
        foreach (var key in keys)
            if (remote.GetValue(key).Source == ValueSource.RemoteValue) hasRemoteValue = true;
        if (!hasRemoteValue) return;

        var config = new DifferenceAdConfig
        {
            interstitialEnabled = remote.GetValue("interstitial_enabled").BooleanValue,
            interstitialFirstLevel = checked((int)remote.GetValue("interstitial_first_level").LongValue),
            interstitialDenseLevel = checked((int)remote.GetValue("interstitial_dense_level").LongValue),
            interstitialEarlyStep = checked((int)remote.GetValue("interstitial_early_step").LongValue),
            interstitialCooldownSeconds = remote.GetValue("interstitial_cooldown_seconds").DoubleValue,
            bannerEnabled = remote.GetValue("banner_enabled").BooleanValue,
            bannerFirstVisibleLevel = checked((int)remote.GetValue("banner_first_visible_level").LongValue),
            rewardedEnabled = remote.GetValue("rewarded_enabled").BooleanValue,
            rewardedPreloadLevel = checked((int)remote.GetValue("rewarded_preload_level").LongValue),
            version = ""
        };
        var explicitVersion = remote.GetValue("ad_config_version").StringValue;
        config.version = string.IsNullOrWhiteSpace(explicitVersion)
            ? "rc-" + Hash128.Compute(JsonUtility.ToJson(config))
            : explicitVersion.Trim();
        if (!DifferenceAds.ApplyConfig(config))
        {
            Debug.LogWarning("[Difference SDK] Invalid Remote Config ad rules ignored; previous rules remain active.");
            return;
        }
        PlayerPrefs.SetString(LastAdConfigKey, JsonUtility.ToJson(config));
        PlayerPrefs.Save();
        Debug.Log("[Difference SDK] Ad configuration applied: " + config.version);
    }

    private async Task InitializeAdvertisingAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(maxSdkKey) || string.IsNullOrWhiteSpace(adjustAppToken))
            {
                Debug.LogWarning("[Difference SDK] MAX/Adjust configuration is missing; advertising startup skipped.");
                return;
            }

            var consentUpdated = new TaskCompletionSource<FormError>();
            ConsentInformation.Update(new ConsentRequestParameters(), error => consentUpdated.TrySetResult(error));
            var updateError = await consentUpdated.Task;
            if (!this) return;
            if (updateError == null)
            {
                var formClosed = new TaskCompletionSource<FormError>();
                ConsentForm.LoadAndShowConsentFormIfRequired(error => formClosed.TrySetResult(error));
                var formError = await formClosed.Task;
                if (!this) return;
                if (formError != null)
                    Debug.LogWarning("[Difference SDK] Consent form failed, code: " + formError.ErrorCode);
            }
            else
            {
                Debug.LogWarning("[Difference SDK] Consent update failed, code: " + updateError.ErrorCode);
            }

            // On a network failure UMP may still have valid consent from an earlier launch.
            // MAX reads the IAB consent strings; 'Obtained' alone does not mean personalized consent.
            if (!ConsentInformation.CanRequestAds())
            {
                Debug.Log("[Difference SDK] Consent does not allow advertising startup.");
                return;
            }

#if UNITY_IOS
            try
            {
                // UMP can briefly leave the app inactive while dismissing its native form.
                await Task.Yield();
                while (this && !Application.isFocused) await Task.Yield();
                if (!this) return;
                if (Adjust.GetAppTrackingAuthorizationStatus() == 0)
                {
                    var trackingAuthorization = new TaskCompletionSource<int>();
                    Adjust.RequestAppTrackingAuthorization(status => trackingAuthorization.TrySetResult(status));
                    await trackingAuthorization.Task;
                    if (!this) return;
                }
            }
            catch (Exception exception)
            {
                LogFailure("ATT authorization", exception);
            }
#endif
            InitializeAdjust();
            InitializeMax();
        }
        catch (Exception exception)
        {
            LogFailure("Advertising consent", exception);
        }
    }

    private void InitializeAdjust()
    {
        try
        {
            var config = new AdjustConfig(adjustAppToken,
                adjustSandbox ? AdjustEnvironment.Sandbox : AdjustEnvironment.Production)
            {
                LogLevel = AdjustLogLevel.Error
            };
#if UNITY_IOS
            config.IsIdfaReadingEnabled = Adjust.GetAppTrackingAuthorizationStatus() == 3;
#endif
            Adjust.InitSdk(config);
            DifferenceAnalytics.SetAdjustSender(SendAdjustEvent);
#if UNITY_IOS
            if (Adjust.GetAppTrackingAuthorizationStatus() == 3)
                DifferenceAnalytics.TrackAdjustOnce("allow_att_adjust_ios");
#endif
            Debug.Log("[Difference SDK] Adjust initialized.");
        }
        catch (Exception exception)
        {
            LogFailure("Adjust initialization", exception);
        }
    }

    private void InitializeMax()
    {
        try
        {
            MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxInitialized;
            MaxSdk.SetSdkKey(maxSdkKey);
            MaxSdk.InitializeSdk();
        }
        catch (Exception exception)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxInitialized;
            LogFailure("MAX initialization", exception);
        }
    }

    private void OnMaxInitialized(MaxSdkBase.SdkConfiguration configuration)
    {
        MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxInitialized;
        if (!this) return;
        if (configuration != null && configuration.IsSuccessfullyInitialized)
        {
            var provider = gameObject.GetComponent<DifferenceAdProvider>();
            if (!provider) provider = gameObject.AddComponent<DifferenceAdProvider>();
            provider.Initialize(interstitialAdUnitId, rewardedAdUnitId, bannerAdUnitId, appOpenAdUnitId);
            Debug.Log("[Difference SDK] MAX advertising provider initialized.");
        }
        else
            Debug.LogWarning("[Difference SDK] MAX initialization failed; gameplay continues.");
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxInitialized;
        DifferenceAnalytics.Flush();
        DifferenceAnalytics.SetFirebaseSender(null);
        DifferenceAnalytics.SetAdjustSender(null);
        instance = null;
        // Firebase's default app belongs to the process and may be used by other game code.
        firebaseApp = null;
    }

    private static void SendFirebaseEvent(string name, Dictionary<string, object> values)
    {
        var parameters = new List<Parameter>(values.Count);
        foreach (var item in values)
        {
            // Keep the spreadsheet's Firebase parameter types: numbers must not become strings.
            if (item.Value is string text) parameters.Add(new Parameter(item.Key, text));
            else if (item.Value is long integer) parameters.Add(new Parameter(item.Key, integer));
            else if (item.Value is int intValue) parameters.Add(new Parameter(item.Key, (long)intValue));
            else if (item.Value is double number) parameters.Add(new Parameter(item.Key, number));
            else if (item.Value is float floatValue) parameters.Add(new Parameter(item.Key, (double)floatValue));
            else throw new ArgumentException("Unsupported analytics parameter type: " + item.Key);
        }
        FirebaseAnalytics.LogEvent(name, parameters.ToArray());
    }

    private bool SendAdjustEvent(string eventName)
    {
        foreach (var entry in adjustEventTokens ?? new AdjustEventToken[0])
        {
            if (entry == null || entry.eventName != eventName) continue;
            var token = entry.token?.Trim();
            if (string.IsNullOrEmpty(token) || !System.Text.RegularExpressions.Regex.IsMatch(token, "^[a-zA-Z0-9]{6}$")) break;
            Adjust.TrackEvent(new AdjustEvent(token));
            return true;
        }
        if (missingAdjustTokens.Add(eventName))
            Debug.LogWarning("[Difference SDK] Adjust event token missing: " + eventName + "; event kept pending.");
        return false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (instance == this) DifferenceAnalytics.Flush();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (instance == this) DifferenceAnalytics.Flush();
    }

    private void OnApplicationQuit()
    {
        if (instance == this) DifferenceAnalytics.Flush();
    }

    private static void LogFailure(string operation, Exception exception)
    {
        // Vendor exception messages can contain application keys or URLs.
        Debug.LogWarning("[Difference SDK] " + operation + " failed (" + exception.GetType().Name + "); gameplay continues.");
    }
#endif
}
