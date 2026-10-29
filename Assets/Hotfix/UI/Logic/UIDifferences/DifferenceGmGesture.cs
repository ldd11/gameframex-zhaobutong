#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    internal sealed class DifferenceGmGesture
    {
        bool tracking, triggered;
        double startedAt;
        int firstId, secondId, thirdId;
        Vector2 firstPosition, secondPosition, thirdPosition;

        public bool Advance(int count, Touch first, Touch second, Touch third, double now, float tolerance)
        {
            if (count != 3 || !Active(first) || !Active(second) || !Active(third) ||
                first.fingerId == second.fingerId || first.fingerId == third.fingerId || second.fingerId == third.fingerId)
            {
                Reset();
                return false;
            }
            if (triggered) return false;
            var squaredTolerance = tolerance * tolerance;
            if (!tracking || first.phase == TouchPhase.Began || second.phase == TouchPhase.Began || third.phase == TouchPhase.Began ||
                !Still(first, squaredTolerance) || !Still(second, squaredTolerance) || !Still(third, squaredTolerance))
            {
                tracking = true;
                startedAt = now;
                firstId = first.fingerId; firstPosition = first.position;
                secondId = second.fingerId; secondPosition = second.position;
                thirdId = third.fingerId; thirdPosition = third.position;
                return false;
            }
            if (now - startedAt < 10) return false;
            triggered = true;
            return true;
        }

        public void Reset() { tracking = triggered = false; }

        static bool Active(Touch touch) => touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;

        bool Still(Touch touch, float squaredTolerance)
        {
            Vector2 origin;
            if (touch.fingerId == firstId) origin = firstPosition;
            else if (touch.fingerId == secondId) origin = secondPosition;
            else if (touch.fingerId == thirdId) origin = thirdPosition;
            else return false;
            return (touch.position - origin).sqrMagnitude <= squaredTolerance;
        }
    }
}
#endif
