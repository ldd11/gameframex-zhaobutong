#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceHintHand : UnityEngine.UI.MaskableGraphic
    {
        const float Period = 1.5f;
        static readonly Vector2 Lift = new Vector2(12, -14);
        UnityEngine.UI.Image hand;
        float elapsed;

        public bool Visible => isActiveAndEnabled;

        public static DifferenceHintHand Create(UnityEngine.UI.Button button, Sprite sprite)
        {
            var root = new GameObject("HintGuide", typeof(RectTransform), typeof(DifferenceHintHand));
            root.layer = button.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(10, 6.3f);
            rect.sizeDelta = ((RectTransform)button.transform).rect.size;
            var guide = root.GetComponent<DifferenceHintHand>();
            guide.raycastTarget = false;

            var visual = new GameObject("Hand", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            visual.layer = root.layer;
            var handRect = (RectTransform)visual.transform;
            handRect.SetParent(rect, false);
            handRect.anchorMin = handRect.anchorMax = new Vector2(.5f, .5f);
            // The pivot sits on the fingertip, so a press stays on the bulb at any Canvas scale.
            handRect.pivot = new Vector2(.145f, .875f);
            handRect.sizeDelta = new Vector2(220,234); //Vector2.one * 132;
            guide.hand = visual.GetComponent<UnityEngine.UI.Image>();
            guide.hand.sprite = sprite;
            guide.hand.preserveAspect = true;
            guide.hand.raycastTarget = false;
            guide.Show(false);
            return guide;
        }

        public void Show(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0;
            Apply();
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            elapsed = Mathf.Repeat(elapsed + deltaTime, Period);
            Apply();
        }

        void Apply()
        {
            if (!hand) return;
            var press = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .18f) / .24f));
            var release = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .56f) / .26f));
            var weight = press * (1 - release);
            hand.rectTransform.anchoredPosition = Lift * (1 - weight);
            hand.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, .92f, weight);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var t = (elapsed - .40f) / .55f;
            if (t < 0 || t >= 1) return;
            var radius = Mathf.Lerp(8, 36, t);
            for (var i = 0; i <= 64; i++)
            {
                var angle = i * Mathf.PI * 2 / 64;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (var band = 0; band < 3; band++)
                {
                    var tint = Color.white; tint.a = band == 1 ? .7f * (1 - t) : 0;
                    mesh.AddVert(direction * (radius + (band - 1) * 1.8f), tint, Vector2.zero);
                    if (i == 0 || band == 2) continue;
                    var a = (i - 1) * 3 + band;
                    mesh.AddTriangle(a, a + 3, a + 4);
                    mesh.AddTriangle(a, a + 4, a + 1);
                }
            }
        }
    }
}
#endif
