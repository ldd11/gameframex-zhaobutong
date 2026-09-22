#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceButtonShine : UnityEngine.UI.MaskableGraphic
    {
        const float SweepDuration = 1.05f;
        const float Period = 3.6f;
        float elapsed;

        public static void Create(UnityEngine.UI.Button button)
        {
            var source = button.GetComponent<UnityEngine.UI.Image>();
            var clip = new GameObject("ShineClip", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            clip.layer = button.gameObject.layer;
            var bounds = (RectTransform)clip.transform;
            bounds.SetParent(button.transform, false);
            bounds.SetAsFirstSibling();
            bounds.anchorMin = Vector2.zero; bounds.anchorMax = Vector2.one;
            bounds.offsetMin = Vector2.one * 6; bounds.offsetMax = Vector2.one * -6;
            var maskImage = clip.GetComponent<UnityEngine.UI.Image>();
            maskImage.sprite = source.sprite; maskImage.type = source.type;
            maskImage.pixelsPerUnitMultiplier = 1.3f;
            maskImage.raycastTarget = false;
            clip.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

            var band = new GameObject("Shine", typeof(RectTransform), typeof(DifferenceButtonShine));
            band.layer = clip.layer;
            var rect = (RectTransform)band.transform;
            rect.SetParent(bounds, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var shine = band.GetComponent<DifferenceButtonShine>();
            shine.raycastTarget = false;
            shine.color = new Color(1, 1, .88f, .48f);
        }

        protected override void OnEnable()
        {
            elapsed = Period - .7f;
            base.OnEnable();
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            var wasSweeping = elapsed < SweepDuration;
            elapsed = Mathf.Repeat(elapsed + deltaTime, Period);
            if (wasSweeping || elapsed < SweepDuration) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (elapsed >= SweepDuration) return;
            var rect = rectTransform.rect;
            var halfWidth = rect.width * .12f;
            var slant = rect.height * .5f;
            var center = Mathf.Lerp(rect.xMin - halfWidth - slant, rect.xMax + halfWidth, elapsed / SweepDuration);
            for (var i = 0; i < 3; i++)
            {
                var tint = color; tint.a = i == 1 ? color.a : 0;
                var x = center + (i - 1) * halfWidth;
                mesh.AddVert(new Vector3(x, rect.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x + slant, rect.yMax), tint, Vector2.zero);
                if (i == 0) continue;
                var v = i * 2;
                mesh.AddTriangle(v - 2, v - 1, v);
                mesh.AddTriangle(v, v - 1, v + 1);
            }
        }
    }
}
#endif
