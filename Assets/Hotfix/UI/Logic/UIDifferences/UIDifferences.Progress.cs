#if ENABLE_UI_UGUI
using Spine;
using Spine.Unity;

namespace Hotfix.UI
{
    public sealed partial class UIDifferences
    {
        void ResetProgressDots()
        {
            for (var i = 0; i < progressDots.Length; i++)
            {
                var dot = progressDots[i];
                dot.Initialize(false);
                dot.UnscaledTime = true; dot.timeScale = 1; dot.freeze = false;
                dot.raycastTarget = false;
                dot.layoutScaleMode = SkeletonGraphic.LayoutMode.WidthControlsHeight;
                dot.AnimationState.ClearTracks();
                dot.Skeleton.SetToSetupPose();
                PlayProgressAnimation(dot, Round != null && i < Round.Count ? "a3" : "a1");
                dot.Update(0);
                dot.gameObject.SetActive(Round != null && i < Round.Total);
            }
        }

        static TrackEntry PlayProgressAnimation(SkeletonGraphic dot, string animation)
        {
            var entry = dot.AnimationState.SetAnimation(0, animation, false);
            entry.MixDuration = 0;
            return entry;
        }

        static TrackEntry PlayProgressTransition(SkeletonGraphic dot, string animation)
        {
            var entry = PlayProgressAnimation(dot, animation);
            var idle = dot.AnimationState.AddAnimation(0, "a3", false, entry.Animation.Duration);
            idle.MixDuration = 0;
            return entry;
        }

        static void SyncProgressDot(SkeletonGraphic dot, bool found)
        {
            var current = dot.AnimationState.GetCurrent(0);
            if (!found)
            {
                if (current == null || current.Animation.Name != "a1") PlayProgressAnimation(dot, "a1");
            }
            else if (current == null) PlayProgressAnimation(dot, "a3");
            else if (current.Animation.Name == "a1") PlayProgressTransition(dot, "a2");
        }
    }
}
#endif
