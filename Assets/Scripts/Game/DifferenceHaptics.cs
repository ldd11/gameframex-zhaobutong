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
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                            vibrator.Call("vibrate", effect);
                    }
                    else vibrator.Call("vibrate", (long)milliseconds);
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
