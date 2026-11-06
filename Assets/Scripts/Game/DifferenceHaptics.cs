using UnityEngine;
using UnityEngine.Scripting;

namespace GameFrameX.Startup.Application
{
    // Keep the native entry point in the AOT assembly for HybridCLR players.
    [Preserve]
    public static class DifferenceHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void DifferencePlayHaptic(int style);
#endif

        [Preserve] public static void PlayLight() { PlayPredefined(2, 15, 0); }
        [Preserve] public static void PlayMedium() { PlayPredefined(0, 20, 1); }
        [Preserve] public static void PlayHeavy() { PlayPredefined(5, 30, 2); }

        static void PlayPredefined(int effectId, int fallbackMilliseconds, int iosStyle)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
                    var sdk = version.GetStatic<int>("SDK_INT");
                    var hasAmplitudeControl = false;
                    if (sdk >= 26)
                    {
                        try { hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl"); }
                        catch (AndroidJavaException) { }
                    }
                    if (sdk >= 29)
                    {
                        try
                        {
                            using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (var effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", effectId))
                                vibrator.Call("vibrate", effect);
#if DEVELOPMENT_BUILD
                            LogAndroidVibration(effectId, fallbackMilliseconds, 120, sdk, hasAmplitudeControl, "predefined");
#endif
                            return;
                        }
                        catch (AndroidJavaException) { }
                    }
                    if (sdk >= 26)
                    {
                        try
                        {
                            if (hasAmplitudeControl)
                            {
                                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)fallbackMilliseconds, 120))
                                    vibrator.Call("vibrate", effect);
#if DEVELOPMENT_BUILD
                                LogAndroidVibration(effectId, fallbackMilliseconds, 120, sdk, hasAmplitudeControl, "one-shot");
#endif
                                return;
                            }
                        }
                        catch (AndroidJavaException) { }
                    }
                    vibrator.Call("vibrate", (long)fallbackMilliseconds);
#if DEVELOPMENT_BUILD
                    LogAndroidVibration(effectId, fallbackMilliseconds, 120, sdk, hasAmplitudeControl, "duration-only");
#endif
                }
            }
            catch (AndroidJavaException error) { Debug.LogWarning("Vibration unavailable: " + error.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            DifferencePlayHaptic(iosStyle);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && DEVELOPMENT_BUILD
        static void LogAndroidVibration(int effectId, int fallbackMilliseconds, int amplitude, int sdk, bool hasAmplitudeControl, string mode)
        {
            Debug.Log($"[Haptics] effect={effectId} mode={mode} fallback={fallbackMilliseconds}ms/{amplitude} sdk={sdk} amplitudeControl={hasAmplitudeControl}");
        }
#endif

        [Preserve]
        public static void Play(int milliseconds, int amplitude)
        {
            if (milliseconds <= 0 || amplitude <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
                    var sdk = version.GetStatic<int>("SDK_INT");
#if DEVELOPMENT_BUILD
                    var hasAmplitudeControl = false;
                    if (sdk >= 26)
                    {
                        try { hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl"); }
                        catch (AndroidJavaException) { }
                    }
#endif
                    if (sdk >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                            vibrator.Call("vibrate", effect);
#if DEVELOPMENT_BUILD
                        LogAndroidVibration(-1, milliseconds, Mathf.Clamp(amplitude, 1, 255), sdk, hasAmplitudeControl, "one-shot");
#endif
                    }
                    else
                    {
                        vibrator.Call("vibrate", (long)milliseconds);
#if DEVELOPMENT_BUILD
                        LogAndroidVibration(-1, milliseconds, amplitude, sdk, hasAmplitudeControl, "duration-only");
#endif
                    }
                }
            }
            catch (AndroidJavaException error) { Debug.LogWarning("Vibration unavailable: " + error.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            // Match the puzzle game's amplitude-to-style mapping (100 = Light).
            DifferencePlayHaptic(amplitude >= 150 ? 2 : amplitude > 100 ? 1 : 0);
#endif
        }
    }
}
