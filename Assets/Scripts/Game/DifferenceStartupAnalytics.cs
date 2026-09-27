using UnityEngine;
using GameFrameX.UI.Runtime;

namespace GameFrameX.Startup.Application
{
    // "splash" is the game's UILauncher, not the OS/Unity native splash screen.
    public static class DifferenceStartupAnalytics
    {
        const string PendingKey = "DifferenceAnalytics.PendingStartup";
        static bool started, finished, failed, launcherClosed, initSkipped;
        static double launchAt, splashAt = -1;
        static UIForm home;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            started = finished = failed = launcherClosed = initSkipped = false;
            splashAt = -1;
            home = null;
            UnityEngine.Application.quitting -= OnQuitting;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void CaptureLaunch()
        {
            if (!DifferenceAnalytics.Enabled || started) return;
            started = true;
            launchAt = Time.realtimeSinceStartupAsDouble;
            DifferenceAnalytics.Initialize();
            UnityEngine.Application.quitting += OnQuitting;
            DifferenceAnalytics.Track("app_launch_trigger", "network", DifferenceAnalytics.Network);
            var previous = PlayerPrefs.GetInt(PendingKey, 0);
            if (previous != 0) ReportFailure("previous_launch_incomplete", previous == 2, "unknown");
            SetPending(1);
        }

        public static void Begin(bool skippedRemoteInitialization)
        {
            CaptureLaunch();
            initSkipped = skippedRemoteInitialization;
        }

        public static void SplashVisible()
        {
            if (!DifferenceAnalytics.Enabled || !started || finished || failed || splashAt >= 0) return;
            splashAt = Time.realtimeSinceStartupAsDouble;
            SetPending(2);
        }

        public static void PrepareHome(UIForm form) { home = form; }
        public static void LauncherClosed() { launcherClosed = true; }

        // Called after startup succeeded, the launcher closed, and the next layout frame ran.
        public static void HomeVisible()
        {
            if (!DifferenceAnalytics.Enabled || !started || finished || failed || !launcherClosed ||
                !home || !home.Visible || !home.gameObject.activeInHierarchy) return;
            finished = true;
            SetPending(0);
            var now = Time.realtimeSinceStartupAsDouble;
            var fields = new System.Collections.Generic.List<object> {
                "launch_to_home", Milliseconds(now - launchAt)
            };
            if (splashAt >= 0)
            {
                fields.Add("launch_to_splash"); fields.Add(Milliseconds(splashAt - launchAt));
                fields.Add("splash_to_home"); fields.Add(Milliseconds(now - splashAt));
            }
            // Includes the installation's first cold start; do not sum this with the first-only event.
            DifferenceAnalytics.Track("loading_home_enter", fields.ToArray());
            // No startup data request exists in local mode. Never substitute Firebase or asset loading time.
            if (initSkipped) { fields.Add("init_time"); fields.Add(0d); }
            DifferenceAnalytics.TrackOnce("first_loading_home_enter", fields.ToArray());
            home = null;
        }

        public static void Fail(string cause)
        {
            if (!DifferenceAnalytics.Enabled || !started || finished || failed) return;
            failed = true;
            SetPending(0);
            ReportFailure(cause, splashAt >= 0);
            home = null;
        }

        static void ReportFailure(string cause, bool afterSplash, string network = null)
        {
            network = network ?? DifferenceAnalytics.Network;
            DifferenceAnalytics.Track("app_init_fail_trigger", "cause", cause, "network", network);
            if (afterSplash)
                DifferenceAnalytics.Track("splash_to_home_fail", "cause", cause, "network", network);
        }

        static double Milliseconds(double seconds) => System.Math.Max(0, seconds * 1000);
        static void SetPending(int value) { PlayerPrefs.SetInt(PendingKey, value); PlayerPrefs.Save(); }
        static void OnQuitting()
        {
            if (!DifferenceAnalytics.Enabled) return;
            DifferenceAnalytics.Flush();
            SetPending(0);
        }
    }
}
