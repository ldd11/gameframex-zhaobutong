#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceFoundRing : UnityEngine.UI.MaskableGraphic
    {
        // Insets are in board units, so larger differences do not get thicker outlines.
        static readonly float[] Insets = { 0, .6f, 1.5f, 2.2f, 5.2f, 6.1f, 7.3f, 8f };
        static readonly Color32[] Colors = {
            new Color32(62, 102, 24, 0), new Color32(62, 102, 24, 150),
            new Color32(255, 255, 239, 255), new Color32(241, 255, 185, 255),
            new Color32(138, 225, 24, 255), new Color32(75, 154, 12, 255),
            new Color32(255, 255, 245, 255), new Color32(255, 255, 245, 0)
        };

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            var radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (radius <= 8) return;
            var segments = Mathf.Clamp(Mathf.CeilToInt(radius * 2), 64, 192);
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (var band = 0; band < Insets.Length; band++)
                    mesh.AddVert(bounds.center + direction * (radius - Insets[band]), Colors[band], Vector2.zero);
                if (i == 0) continue;
                for (var band = 0; band < Insets.Length - 1; band++)
                {
                    var a = (i - 1) * Insets.Length + band;
                    var b = i * Insets.Length + band;
                    mesh.AddTriangle(a, b, b + 1);
                    mesh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }
    }
}
#endif
