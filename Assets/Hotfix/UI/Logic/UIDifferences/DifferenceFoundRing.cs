#if ENABLE_UI_UGUI
using UnityEngine;
using System.Collections.Generic;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceFoundRing : UnityEngine.UI.MaskableGraphic
    {
        [System.Serializable]
        public sealed class Style
        {
            [Header("大小与描边")]
            [Min(.01f), Tooltip("在差异区域直径基础上调整大小，不缩放线宽。")]
            public float sizeMultiplier = 1;
            public float radiusOffset;
            [Min(1), Tooltip("圆圈最终直径的下限（UI 单位）；出现动画仍保留缩放效果。")]
            public float minDiameter = 100;
            [Min(1), Tooltip("圆圈最终直径的上限（UI 单位），不包含外侧光晕。")]
            public float maxDiameter = 220;
            [Min(.1f), Tooltip("作者圆圈按源图像素计算描边；其他关卡按 UI 单位。")]
            public float lineWidth = 16;
            [Range(0, 1)] public float opacity = 1;
            [Range(32, 256)] public int segments = 128;
            [Tooltip("从外缘（左端）到内缘（右端）的颜色与透明度。")]
            public Gradient outlineColors = new Gradient
            {
                colorKeys = new[] {
                    new GradientColorKey(new Color32(20, 88, 3, 255), 0),
                    new GradientColorKey(new Color32(20, 88, 3, 255), .7f / 16),
                    new GradientColorKey(new Color32(219, 255, 172, 255), 1.4f / 16),
                    new GradientColorKey(new Color32(108, 247, 16, 255), 2.4f / 16),
                    new GradientColorKey(new Color32(80, 237, 5, 255), 5f / 16),
                    new GradientColorKey(new Color32(49, 201, 0, 255), 11.8f / 16),
                    new GradientColorKey(new Color32(22, 117, 0, 255), 14.2f / 16),
                    new GradientColorKey(new Color32(223, 255, 179, 255), 15.3f / 16)
                },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.9f, .7f / 16),
                    new GradientAlphaKey(1, 1.4f / 16), new GradientAlphaKey(1, 15.3f / 16), new GradientAlphaKey(0, 1) }
            };
            [Header("局部高光")]
            public Color highlightColor = new Color(.92f, 1, .8f);
            [Range(0, 1)] public float highlightIntensity = .85f;
            [Range(0, 360), Tooltip("0 为右侧，90 为上方。")]
            public float highlightAngle = 126.87f;
            [Min(1), Tooltip("越大，高光的弧段越短。")]
            public float highlightSharpness = 18;
            [Tooltip("高光覆盖的描边区间：0 外缘，1 内缘。")]
            public Vector2 highlightRange = new Vector2(.15f, .3125f);
            [Header("柔光（透明渐变光晕）")]
            public Color glowColor = new Color(.35f, 1, .05f);
            [Range(0, 1), Tooltip("0 关闭光晕。")]
            public float glowIntensity;
            [Min(0)] public float glowWidth = 8;
            [Header("出现动画")]
            [Min(0), Tooltip("0 立即显示。")]
            public float animationDuration = .4f;
            [Min(0)] public float fadeDuration = .09f;
            [Range(.05f, 1)] public float initialSize = .8f;
            [Min(0), Tooltip("0 无回弹，越大回弹越明显。")]
            public float overshoot = 1.70158f;
        }

        public Style style = new Style();
        [System.NonSerialized] public float sourceImageWidth;
        readonly List<float> bands = new List<float>();
        float elapsed = float.MaxValue;

        public void Play(bool animate)
        {
            elapsed = animate ? 0 : Mathf.Max(0, style.animationDuration);
            SetVerticesDirty();
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            var duration = Mathf.Max(0, style.animationDuration);
            if (elapsed >= duration) return;
            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0, deltaTime));
            SetVerticesDirty();
        }

        float AnimationProgress => style.animationDuration <= 0 ? 1 : Mathf.Clamp01(elapsed / style.animationDuration);

        float LayoutUnit
        {
            get
            {
                var board = GetComponentInParent<DifferenceBoard>(true);
                if (!board || !board.owner || !board.owner.play) return 1;
                // Viewports can use different authored scales; board zoom still scales the whole ring.
                var scale = board.owner.play.transform.InverseTransformVector(board.transform.parent.TransformVector(Vector3.right)).magnitude;
                return 1 / Mathf.Max(.001f, scale);
            }
        }

        float StrokeUnit => sourceImageWidth > 0
            ? ((RectTransform)GetComponentInParent<DifferenceBoard>(true).transform).rect.width / sourceImageWidth
            : LayoutUnit;

        public float Radius
        {
            get
            {
                var bounds = rectTransform.rect;
                var unit = LayoutUnit;
                var radius = Mathf.Min(bounds.width, bounds.height) * .5f * Mathf.Max(.01f, style.sizeMultiplier) + style.radiusOffset * unit;
                var minimum = Mathf.Max(1, style.minDiameter) * .5f * unit;
                radius = Mathf.Clamp(radius, minimum, Mathf.Max(minimum, style.maxDiameter * .5f * unit));
                var t = AnimationProgress - 1;
                var overshoot = Mathf.Max(0, style.overshoot);
                var bounce = 1 + (overshoot + 1) * t * t * t + overshoot * t * t;
                return radius * Mathf.LerpUnclamped(Mathf.Clamp(style.initialSize, .05f, 1), 1, bounce);
            }
        }

        public float InnerRadius
        {
            get
            {
                var radius = Radius;
                var unit = StrokeUnit;
                return radius <= .5f * unit ? 0 : Mathf.Max(.5f * unit, radius - Mathf.Max(.1f, style.lineWidth) * unit);
            }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            var radius = Radius;
            var unit = StrokeUnit;
            var progress = AnimationProgress;
            if (radius <= .5f * unit) return;
            // Small circles retain a transparent center even if the requested width exceeds the radius.
            var width = Mathf.Clamp(style.lineWidth * unit, .1f * unit, radius - .5f * unit);
            var segments = Mathf.Clamp(style.segments, 32, 256);
            var alpha = progress >= 1 || style.fadeDuration <= 0 ? 1 : Mathf.Clamp01(elapsed / style.fadeDuration);
            alpha *= Mathf.Clamp01(style.opacity);
            var glow = style.glowColor * color;
            glow.a *= Mathf.Clamp01(style.glowIntensity) * alpha;
            var glowWidth = Mathf.Max(0, style.glowWidth) * unit;
            if (glow.a > 0 && glowWidth > 0)
            {
                var clear = glow; clear.a = 0;
                AddBand(mesh, bounds.center, radius + glowWidth, radius, clear, glow, segments);
                AddBand(mesh, bounds.center, radius, radius - width, glow, glow, segments);
                AddBand(mesh, bounds.center, radius - width, Mathf.Max(.5f * unit, radius - width - glowWidth), glow, clear, segments);
            }
            bands.Clear(); bands.Add(0); bands.Add(1);
            foreach (var key in style.outlineColors.colorKeys) if (!bands.Contains(key.time)) bands.Add(key.time);
            foreach (var key in style.outlineColors.alphaKeys) if (!bands.Contains(key.time)) bands.Add(key.time);
            bands.Sort();
            var start = mesh.currentVertCount;
            var highlightAngle = style.highlightAngle * Mathf.Deg2Rad;
            var highlightDirection = new Vector2(Mathf.Cos(highlightAngle), Mathf.Sin(highlightAngle));
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var shine = Mathf.Pow(Mathf.Max(0, Vector2.Dot(direction, highlightDirection)), Mathf.Max(1, style.highlightSharpness));
                for (var band = 0; band < bands.Count; band++)
                {
                    var tint = style.outlineColors.Evaluate(bands[band]);
                    if (bands[band] >= Mathf.Clamp01(Mathf.Min(style.highlightRange.x, style.highlightRange.y)) &&
                        bands[band] <= Mathf.Clamp01(Mathf.Max(style.highlightRange.x, style.highlightRange.y)))
                        tint = Color.Lerp(tint, style.highlightColor, shine * Mathf.Clamp01(style.highlightIntensity));
                    tint *= color; tint.a *= alpha;
                    mesh.AddVert(bounds.center + direction * (radius - bands[band] * width), tint, Vector2.zero);
                }
                if (i == 0) continue;
                for (var band = 0; band < bands.Count - 1; band++)
                {
                    var a = start + (i - 1) * bands.Count + band;
                    var b = start + i * bands.Count + band;
                    mesh.AddTriangle(a, b, b + 1);
                    mesh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }

        static void AddBand(UnityEngine.UI.VertexHelper mesh, Vector2 center, float outer, float inner,
            Color outside, Color inside, int segments)
        {
            var start = mesh.currentVertCount;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * outer, outside, Vector2.zero);
                mesh.AddVert(center + direction * inner, inside, Vector2.zero);
                if (i == 0) continue;
                var b = start + i * 2;
                mesh.AddTriangle(b - 2, b, b + 1); mesh.AddTriangle(b - 2, b + 1, b - 1);
            }
        }
    }
}
#endif
