#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceFoundMask : UnityEngine.UI.MaskableGraphic
    {
        public DifferenceFoundRing ring;

        void LateUpdate() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (!ring) return;
            var source = ring.rectTransform;
            var center = source.rect.center;
            var radius = ring.InnerRadius;
            if (radius <= 0) return;
            var segments = Mathf.Clamp(ring.style.segments, 32, 256);
            mesh.AddVert(rectTransform.InverseTransformPoint(source.TransformPoint(center)), Color.white, Vector2.zero);
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                mesh.AddVert(rectTransform.InverseTransformPoint(source.TransformPoint(point)), Color.white, Vector2.zero);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
#endif
