#if DIFFERENCE_GM_GESTURE_ISOLATED
// RunDifferenceGmGestureCheck.ps1 compiles this with the real helper in a separate process.
using System;
using Hotfix.UI;
using UnityEngine;

static class DifferenceGmGestureCheck
{
    static int assertions;
    static Touch A => Finger(11, 100, 200);
    static Touch B => Finger(22, 300, 400);
    static Touch C => Finger(33, 500, 600);

    static int Main()
    {
        try
        {
            BoundaryAndLatch();
            ReorderingAndReplacement();
            ReusedFingerIds();
            MovementFromSegmentOrigin();
            InvalidTouchesAndCounts();
            ExplicitReset();
            NoFrameAllocations();
            Console.WriteLine("PASS: " + assertions + " assertions; real GM gesture helper; exact ten-second boundary, finger identity/order, movement tolerance, cancellation/counts, one-shot latch and Reset; zero allocations across 50,000 frames.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    static void BoundaryAndLatch()
    {
        var gesture = new DifferenceGmGesture();
        Check(!Hold(gesture, 0), "The first three-finger frame starts timing.");
        Check(!Hold(gesture, 9.999), "Holding for 9.999 seconds does not trigger.");
        Check(Hold(gesture, 10), "Holding for exactly ten seconds triggers.");
        Check(!Hold(gesture, 10) && !Hold(gesture, 20) && !Hold(gesture, 100), "A continuous three-finger hold only triggers once.");
        Check(!gesture.Advance(3, Finger(44, 200, 300), B, C, 101, 12), "Replacing a finger after triggering cannot rearm the gesture.");
        Check(!gesture.Advance(3, Finger(44, 900, 300), B, C, 120, 12), "Moving after triggering cannot rearm the gesture.");
        Check(!gesture.Advance(2, A, B, default, 121, 12), "Releasing a finger clears the trigger latch.");
        Check(!Hold(gesture, 122) && !Hold(gesture, 131.999) && Hold(gesture, 132), "A new complete hold may trigger only after leaving three fingers.");
    }

    static void ReorderingAndReplacement()
    {
        var gesture = new DifferenceGmGesture();
        Check(!Hold(gesture, 5), "A nonzero clock starts a new hold.");
        Check(!gesture.Advance(3, C, A, B, 9, 12), "Changing touch order preserves the same three fingers.");
        Check(!gesture.Advance(3, B, C, A, 14.999, 12), "Reordering does not prematurely trigger.");
        Check(gesture.Advance(3, C, B, A, 15, 12), "Each finger keeps its own origin despite touch-array reordering.");

        gesture.Reset();
        Hold(gesture, 0);
        var replacement = Finger(44, C.position.x, C.position.y);
        Check(!gesture.Advance(3, A, B, replacement, 9.5, 12), "A different finger ID restarts timing even at the same position.");
        Check(!gesture.Advance(3, replacement, A, B, 10, 12), "The replaced set does not inherit the previous elapsed time.");
        Check(!gesture.Advance(3, B, replacement, A, 19.499, 12), "A replacement must complete its own ten-second hold.");
        Check(gesture.Advance(3, replacement, B, A, 19.5, 12), "The replacement set triggers at its own ten-second boundary.");
    }

    static void MovementFromSegmentOrigin()
    {
        var gesture = new DifferenceGmGesture();
        Hold(gesture, 0);
        var atThreshold = Finger(11, A.position.x + 12, A.position.y, TouchPhase.Moved);
        Check(!gesture.Advance(3, atThreshold, B, C, 9.999, 12), "Moving exactly the tolerance distance does not reset or trigger early.");
        Check(gesture.Advance(3, atThreshold, B, C, 10, 12), "The tolerance boundary is inclusive.");

        gesture.Reset();
        Hold(gesture, 0);
        Check(!gesture.Advance(3, Finger(11, 108, 200), B, C, 5, 12), "Small drift stays within the segment's origin tolerance.");
        var moved = Finger(11, 115, 200);
        Check(!gesture.Advance(3, moved, B, C, 9, 12), "Movement beyond the original position restarts timing even if the last step was small.");
        Check(!gesture.Advance(3, moved, B, C, 10, 12) && !gesture.Advance(3, moved, B, C, 18.999, 12), "Movement starts a fresh ten-second segment.");
        Check(gesture.Advance(3, moved, B, C, 19, 12), "A stationary moved set triggers after a complete new hold.");

        gesture.Reset();
        Hold(gesture, 0);
        var diagonal = Finger(11, 109, 209);
        Check(!gesture.Advance(3, diagonal, B, C, 9, 12) && !gesture.Advance(3, diagonal, B, C, 10, 12), "Tolerance uses distance rather than independent axis bounds.");
        Check(gesture.Advance(3, diagonal, B, C, 19, 12), "A diagonal movement segment has its own timer.");

        gesture.Reset();
        Check(!gesture.Advance(3, A, B, C, 0, 0) && gesture.Advance(3, A, B, C, 10, 0), "Zero tolerance supports an exactly stationary hold.");
    }

    static void ReusedFingerIds()
    {
        for (var index = 0; index < 3; index++)
        {
            var gesture = new DifferenceGmGesture();
            Hold(gesture, 0);
            var first = A; var second = B; var third = C;
            if (index == 0) first.phase = TouchPhase.Began;
            else if (index == 1) second.phase = TouchPhase.Began;
            else third.phase = TouchPhase.Began;
            Check(!gesture.Advance(3, first, second, third, 9.5, 12), "A new contact with a reused ID and unchanged position restarts timing: " + index);
            Check(!Hold(gesture, 10) && !Hold(gesture, 19.499) && Hold(gesture, 19.5), "Subsequent stationary frames must complete a new ten-second hold: " + index);
        }
        var initial = new DifferenceGmGesture();
        Check(!initial.Advance(3, Finger(11, 100, 200, TouchPhase.Began), B, C, 0, 12) && Hold(initial, 10),
            "An initial Began frame starts timing normally, followed by stationary frames.");
        Check(!initial.Advance(3, Finger(11, 100, 200, TouchPhase.Began), B, C, 11, 12) && !Hold(initial, 21),
            "Began cannot clear an already-triggered latch while three touches remain valid.");
    }

    static void InvalidTouchesAndCounts()
    {
        foreach (var phase in new[] { TouchPhase.Ended, TouchPhase.Canceled })
        {
            var gesture = new DifferenceGmGesture();
            Hold(gesture, 0);
            Check(!gesture.Advance(3, Finger(11, 100, 200, phase), B, C, 9, 12), "An ended/canceled touch interrupts a hold: " + phase);
            Check(!Hold(gesture, 10) && !Hold(gesture, 19.999) && Hold(gesture, 20), "A valid set after cancellation restarts from zero: " + phase);
        }
        foreach (var count in new[] { 0, 1, 2, 4, 5 })
        {
            var gesture = new DifferenceGmGesture();
            Hold(gesture, 0);
            Check(!gesture.Advance(count, default, default, default, 9, 12), "Only exactly three touches are accepted: " + count);
            Check(!Hold(gesture, 10) && !Hold(gesture, 19.999) && Hold(gesture, 20), "A changed touch count resets the timer: " + count);
        }
        var duplicates = new DifferenceGmGesture();
        Check(!duplicates.Advance(3, A, A, C, 0, 12) && !duplicates.Advance(3, A, A, C, 20, 12), "Duplicate IDs are not three distinct fingers.");
        Check(!Hold(duplicates, 21) && Hold(duplicates, 31), "Distinct fingers after duplicate IDs start a new timer.");
        Check(!duplicates.Advance(4, A, B, C, 32, 12) && !Hold(duplicates, 33) && Hold(duplicates, 43), "Adding a fourth finger also releases the one-shot latch.");
    }

    static void ExplicitReset()
    {
        var gesture = new DifferenceGmGesture();
        Hold(gesture, 0);
        Check(!Hold(gesture, 8), "A partial hold is still pending before a background reset.");
        gesture.Reset();
        Check(!Hold(gesture, 9) && !Hold(gesture, 18.999) && Hold(gesture, 19), "Reset discards elapsed time, as required for a background interruption.");
        gesture.Reset();
        gesture.Reset();
        Check(!Hold(gesture, 30) && Hold(gesture, 40), "Reset is idempotent and clears a previous trigger.");
    }

    static void NoFrameAllocations()
    {
        var gesture = new DifferenceGmGesture();
        var first = A; var second = B; var third = C;
        for (var i = 0; i < 200; i++) gesture.Advance(3, first, second, third, i * .01, 12);
        gesture.Reset();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 50000; i++) gesture.Advance(3, first, second, third, i * .001, 12);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Advance allocates no managed memory across 50,000 frames; bytes=" + allocated);
    }

    static bool Hold(DifferenceGmGesture gesture, double now) => gesture.Advance(3, A, B, C, now, 12);
    static Touch Finger(int id, float x, float y, TouchPhase phase = TouchPhase.Stationary) =>
        new Touch { fingerId = id, position = new Vector2(x, y), phase = phase };
    static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
        assertions++;
    }
}

namespace UnityEngine
{
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public int fingerId; public Vector2 position; public TouchPhase phase; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator -(Vector2 left, Vector2 right) => new Vector2(left.x - right.x, left.y - right.y);
    }
}
#endif
