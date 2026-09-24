#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    // Layered coin faces retain a visible rim while turning edge-on, without a second camera.
    public sealed class DifferenceRewardCoin : MonoBehaviour
    {
        const int RimLayers = 6;
        readonly Image[] faces = new Image[RimLayers + 1];
        float diameter;
        public const float SpawnDelay = .12f, Stagger = .065f, PopTime = .48f, FlightTime = .78f;
        public static float EndTime(int count) => SpawnDelay + Mathf.Max(0, count - 1) * Stagger + PopTime + FlightTime;

        public static DifferenceRewardCoin Create(RectTransform parent, Sprite sprite, float size)
        {
            var node = new GameObject("RewardCoin", typeof(RectTransform), typeof(DifferenceRewardCoin));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            var coin = node.GetComponent<DifferenceRewardCoin>();
            coin.diameter = size;
            for (var i = 0; i < coin.faces.Length; i++)
            {
                var face = new GameObject(i == RimLayers ? "Face" : "Rim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                face.layer = node.layer;
                face.transform.SetParent(node.transform, false);
                var image = coin.faces[i] = face.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                // Stretch the face while turning: preserveAspect would turn it into a tiny circle.
                image.preserveAspect = false;
            }
            return coin;
        }

        public void SetAppearance(float age, int index, float flight)
        {
            var angle = index * 1.71f + age * 10.5f;
            var facing = Mathf.Cos(angle);
            var width = Mathf.Max(.035f, Mathf.Abs(facing));
            var edge = Mathf.Sin(angle) * diameter * .16f;
            var faceLight = .82f + .18f * Mathf.Abs(facing);
            for (var i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                face.rectTransform.sizeDelta = new Vector2(diameter * width, diameter);
                face.rectTransform.anchoredPosition = new Vector2(edge * (i / (float)RimLayers - .5f), 0);
                face.color = i == RimLayers ? new Color(faceLight, faceLight, faceLight, 1)
                    : Color.Lerp(new Color(.48f, .29f, .055f), new Color(1, .83f, .32f), i / (float)RimLayers);
            }
            var pop = Mathf.Lerp(.25f, 1, Mathf.Clamp01(age / .12f));
            var depth = .86f + .14f * Mathf.Sin(index * 2.4f);
            transform.localScale = Vector3.one * (pop * Mathf.Lerp(depth, .78f, flight));
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(angle * .4f) * 18);
        }

        public static Vector3 Position(Vector3 origin, Vector3 target, float spread, float age, int index, out float flight)
        {
            var sideways = Mathf.Sin(index * 2.39996f) * spread;
            var launch = origin + new Vector3(sideways, spread * (.12f + .14f * Mathf.Cos(index * 1.7f)), 0);
            flight = Mathf.Clamp01((age - PopTime) / FlightTime);
            if (age < PopTime)
            {
                var t = Mathf.Clamp01(age / PopTime);
                // Fan out just above the bottom edge, lift, then settle before takeoff.
                return Vector3.Lerp(origin, launch, 1 - (1 - t) * (1 - t))
                    + Vector3.up * (Mathf.Sin(t * Mathf.PI) * spread * (.55f + .2f * Mathf.Cos(index)));
            }
            var eased = flight * flight;
            var control = launch + new Vector3((target.x - launch.x) * .08f, (target.y - launch.y) * .68f, 0);
            return (1 - eased) * (1 - eased) * launch + 2 * (1 - eased) * eased * control + eased * eased * target;
        }
    }
}
#endif
