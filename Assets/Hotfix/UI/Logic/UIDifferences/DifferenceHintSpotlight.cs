#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    public sealed class DifferenceHintSpotlight : Image
    {
        public const float OpenTime = .38f, CloseTime = .28f, Radius = 75;
        public int TargetIndex { get; private set; } = -1;
        public bool Ready => isActiveAndEnabled && !closing && elapsed >= OpenTime;
        DifferenceBoard board;
        RectTransform upper, lower;
        Material spotlightMaterial;
        Vector2 target, startPan, endPan;
        float elapsed, startZoom;
        bool closing;

        public static DifferenceHintSpotlight Create(RectTransform parent, Material material)
        {
            var go = new GameObject("HintSpotlight", typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var effect = go.AddComponent<DifferenceHintSpotlight>();
            effect.spotlightMaterial = new Material(material);
            effect.material = effect.spotlightMaterial;
            effect.color = new Color(0, 0, 0, .8f);
            go.SetActive(false);
            return effect;
        }

        public void Show(DifferenceBoard source, RectTransform first, RectTransform second, Vector2 point, int index)
        {
            board = source; upper = first; lower = second; target = point; TargetIndex = index;
            elapsed = 0; closing = false;
            startZoom = upper.localScale.x; startPan = upper.anchoredPosition;
            board.FocusSpot(point, 2);
            endPan = upper.anchoredPosition;
            board.SetView(startZoom, startPan);
            gameObject.SetActive(true); transform.SetAsLastSibling();
            Advance(0);
        }

        public void Dismiss()
        {
            if (!isActiveAndEnabled || closing) return;
            closing = true; elapsed = 0;
            startZoom = upper.localScale.x; startPan = upper.anchoredPosition;
            board.ResetView(); endPan = upper.anchoredPosition;
            board.SetView(startZoom, startPan);
        }

        public void Cancel()
        {
            if (!gameObject.activeSelf) return;
            board.ResetView(); TargetIndex = -1; gameObject.SetActive(false);
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            var t = Mathf.Clamp01(elapsed / (closing ? CloseTime : OpenTime));
            var eased = 1 - Mathf.Pow(1 - t, 3);
            board.SetView(Mathf.Lerp(startZoom, closing ? 1 : 2, eased), Vector2.Lerp(startPan, endPan, eased));
            var radius = closing ? Mathf.Lerp(Radius, 135, eased) : Mathf.Lerp(280, Radius, eased);
            spotlightMaterial.SetVector("_Size", rectTransform.rect.size);
            spotlightMaterial.SetVector("_HoleA", Hole(upper, radius));
            spotlightMaterial.SetVector("_HoleB", Hole(lower, radius));
            color = new Color(0, 0, 0, .8f * (closing ? 1 - eased : Mathf.Clamp01(elapsed / .08f)));
            if (closing && t >= 1) { TargetIndex = -1; gameObject.SetActive(false); }
        }

        Vector4 Hole(RectTransform image, float radius)
        {
            var local = new Vector2(image.rect.xMin + target.x * image.rect.width, image.rect.yMax - target.y * image.rect.height);
            var center = (Vector2)rectTransform.InverseTransformPoint(image.TransformPoint(local)) - rectTransform.rect.min;
            return new Vector4(center.x, center.y, radius, 22);
        }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!Ready) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return true;
            local -= rectTransform.rect.min;
            foreach (var image in new[] { upper, lower })
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)image.parent, screenPoint, eventCamera)) continue;
                var hole = Hole(image, Radius);
                if ((local - new Vector2(hole.x, hole.y)).sqrMagnitude <= Radius * Radius) return false;
            }
            return true;
        }

        protected override void OnDestroy()
        {
            if (spotlightMaterial) Destroy(spotlightMaterial);
            base.OnDestroy();
        }
    }
}
#endif
