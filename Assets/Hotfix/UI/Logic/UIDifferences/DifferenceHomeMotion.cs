#if ENABLE_UI_UGUI
using Spine.Unity;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed class DifferenceHomeMotion : MonoBehaviour
    {
        SkeletonGraphic logo;
        Transform start;
        [SerializeField, Min(0)] float initialScale = 0;
        [SerializeField, Min(1)] float peakScale = 1.2f;
        [SerializeField, Min(.01f)] float growDuration = .18f;
        [SerializeField, Min(.01f)] float settleDuration = .32f;
        float elapsed;
        bool waitingForHome;

        void OnEnable()
        {
            if (!logo) logo = transform.Find("Logo/Spine")?.GetComponent<SkeletonGraphic>();
            if (!start) start = transform.Find("Start");
            if (!logo || !start) return;
            logo.Initialize(false);
            if (logo.AnimationState == null) return;
            logo.UnscaledTime = true;
            logo.freeze = false;
            logo.AnimationState.ClearTracks();
            logo.Skeleton.SetToSetupPose();
            logo.AnimationState.SetAnimation(0, logo.startingAnimation, false);
            logo.Update(0);
            // The first home is initialized behind UILauncher. Hold its first pose until startup finishes.
            logo.freeze = true;
            waitingForHome = true;
            elapsed = 0;
            start.localScale = Vector3.one * initialScale;
        }

        void Update()
        {
            Advance(Time.unscaledDeltaTime, GameFrameX.Startup.Application.ApplicationStartupEntry.HomeReady);
        }

        void Advance(float deltaTime, bool homeReady)
        {
            if (!start || !logo || logo.AnimationState == null) return;
            if (waitingForHome)
            {
                if (!homeReady) return;
                waitingForHome = false;
                logo.freeze = false;
                return;
            }
            var grow = Mathf.Max(.01f, growDuration);
            var settle = Mathf.Max(.01f, settleDuration);
            elapsed = Mathf.Min(grow + settle, elapsed + deltaTime);
            var t = Mathf.Clamp01(elapsed / grow);
            var scale = elapsed < grow
                ? Mathf.Lerp(initialScale, peakScale, 1 - (1 - t) * (1 - t))
                : Mathf.Lerp(peakScale, 1, Mathf.SmoothStep(0, 1, (elapsed - grow) / settle));
            start.localScale = Vector3.one * scale;
        }

        void OnDisable()
        {
            if (start) start.localScale = Vector3.one;
            if (logo) logo.freeze = false;
        }
    }
}
#endif
