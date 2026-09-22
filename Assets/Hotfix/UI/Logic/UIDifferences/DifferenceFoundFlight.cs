#if ENABLE_UI_UGUI
using System;
using UnityEngine;

namespace Hotfix.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DifferenceFoundFlight : UnityEngine.UI.MaskableGraphic
    {
        const float FlightTime = .52f, FlashTime = .12f, SettleTime = .20f;
        const int EmissionCount = 48;
        const float MaxParticleLife = .38f;
        public const float Duration = FlightTime + MaxParticleLife;
        readonly Vector2[] emissionPositions = new Vector2[EmissionCount];
        int emitted;
        Vector2 origin;
        RectTransform destination;
        float elapsed;
        bool revealed;
        Action onReveal;

        public static void Launch(RectTransform parent, Vector3 worldOrigin, RectTransform target, Action onReveal = null, Material glowMaterial = null)
        {
            var node = new GameObject("FoundFlight", typeof(RectTransform), typeof(DifferenceFoundFlight));
            node.layer = parent.gameObject.layer;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var flight = node.GetComponent<DifferenceFoundFlight>();
            flight.raycastTarget = false;
            flight.material = glowMaterial;
            flight.origin = rect.InverseTransformPoint(worldOrigin);
            flight.destination = target;
            flight.onReveal = onReveal;
        }

        public static void Clear(GameObject page)
        {
            foreach (var flight in page.GetComponentsInChildren<DifferenceFoundFlight>(true))
            {
                flight.gameObject.SetActive(false);
                Destroy(flight.gameObject);
            }
        }

        Vector2 Point(float t)
        {
            Vector2 end = rectTransform.InverseTransformPoint(destination.TransformPoint(destination.rect.center));
            var direction = end - origin;
            var sideways = new Vector2(-direction.y, direction.x).normalized;
            var wave = Mathf.Sin(t * Mathf.PI * 4) * Mathf.Sin(t * Mathf.PI);
            return Vector2.Lerp(origin, end, t) + sideways * (Mathf.Min(56, direction.magnitude * .12f) * wave);
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            elapsed += deltaTime;
            // A star keeps its birthplace instead of sliding forward with the head.
            while (destination && emitted < EmissionCount && emitted * FlightTime / (EmissionCount - 1) <= elapsed)
            {
                emissionPositions[emitted] = Point(emitted / (EmissionCount - 1f));
                emitted++;
            }
            if (destination && !revealed && elapsed >= FlightTime + FlashTime)
            {
                revealed = true;
                var reveal = onReveal; onReveal = null;
                reveal?.Invoke();
            }
            if (!destination || elapsed >= Duration)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            if (revealed)
                destination.localScale = Vector3.one * (1 + .22f * Mathf.Sin(Mathf.Clamp01((elapsed - FlightTime - FlashTime) / SettleTime) * Mathf.PI));
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            if (destination) destination.localScale = Vector3.one;
            onReveal = null;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (!destination) return;
            if (elapsed < .2f) Impact(mesh, origin, elapsed / .2f);
            Ribbon(mesh);
            Trail(mesh);
            if (elapsed < FlightTime)
            {
                var t = elapsed / FlightTime;
                var center = Point(t);
                Glow(mesh, center, 32, new Color(.65f, 1, .12f, .42f));
                Glow(mesh, center, 16, new Color(1, .95f, .48f, .95f));
                Glow(mesh, center, 7, Color.white);
                Star(mesh, center, 9, Color.white, -.3f);
                Star(mesh, center + new Vector2(7, 5), 5, new Color(1, 1, .75f), .2f);
            }
            else
            {
                var arrival = elapsed - FlightTime;
                var fade = 1 - Mathf.Clamp01((arrival - FlashTime) / SettleTime);
                var center = Point(1);
                Glow(mesh, center, 37, new Color(.65f, 1, .12f, .6f * fade));
                Glow(mesh, center, 20, new Color(1, 1, .78f, fade));
                Star(mesh, center, 19, new Color(1, 1, 1, fade));
                Impact(mesh, center, Mathf.Clamp01(arrival / (FlashTime + SettleTime)));
            }
        }

        void Ribbon(UnityEngine.UI.VertexHelper mesh)
        {
            var fade = 1 - Mathf.Clamp01((elapsed - FlightTime) / .08f);
            if (fade <= 0 || elapsed <= 0) return;
            const int segments = 20;
            var head = Mathf.Min(elapsed, FlightTime);
            var tail = Mathf.Max(0, head - .11f);
            var start = mesh.currentVertCount;
            for (var i = 0; i <= segments; i++)
            {
                var along = i / (float)segments;
                var t = Mathf.Lerp(tail, head, along) / FlightTime;
                var center = Point(t);
                var tangent = (Point(Mathf.Min(1, t + .003f)) - Point(Mathf.Max(0, t - .003f))).normalized;
                var normal = new Vector2(-tangent.y, tangent.x);
                var width = Mathf.Lerp(1, 12, along);
                var alpha = along * along * fade;
                mesh.AddVert(center - normal * width, new Color(1, .8f, .05f, 0), Vector2.zero);
                mesh.AddVert(center - normal * (width * .28f), new Color(1, .82f, .1f, .5f * alpha), Vector2.zero);
                mesh.AddVert(center, new Color(1, 1, .8f, .95f * alpha), Vector2.zero);
                mesh.AddVert(center + normal * (width * .28f), new Color(1, .82f, .1f, .5f * alpha), Vector2.zero);
                mesh.AddVert(center + normal * width, new Color(1, .8f, .05f, 0), Vector2.zero);
                if (i == 0) continue;
                for (var band = 0; band < 4; band++)
                {
                    var a = start + (i - 1) * 5 + band; var b = a + 5;
                    mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }

        void Trail(UnityEngine.UI.VertexHelper mesh)
        {
            // Draw soft light behind the crisp glints, so neighboring halos do not wash them out.
            for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < emitted; i++)
            {
                var age = elapsed - i * FlightTime / (EmissionCount - 1);
                var life = Mathf.Lerp(.24f, MaxParticleLife, Seed(i + 19));
                if (age < 0 || age >= life) continue;
                var phase = age / life;
                var fade = Mathf.Clamp01(age / .025f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1, phase)));
                var twinkle = Mathf.Pow(.5f + .5f * Mathf.Sin(age * (25 + Seed(i) * 22) + i * 2.4f), 4);
                var scatter = new Vector2(Seed(i + 3) - .5f, Seed(i + 7) - .5f);
                var point = emissionPositions[i] + scatter * (18 + age * 48) + Vector2.down * (age * age * 14);
                var tint = i % 3 == 0 ? new Color(.6f, 1, .06f) : new Color(.85f, 1, .82f);
                tint.a = fade * (.68f + .32f * twinkle);
                var size = (3 + Seed(i + 11) * 4) * Mathf.Lerp(1, .25f, phase * phase) * (.85f + .35f * twinkle);
                if (pass == 0)
                {
                    Glow(mesh, point, size * 2.1f, new Color(.6f, 1, .08f, fade * .24f));
                    Glow(mesh, emissionPositions[i] - scatter * (22 + age * 42), 2.5f * fade, new Color(1, 1, .65f, fade * .85f));
                }
                else
                {
                    Star(mesh, point, size, tint, i * 1.7f + age * .7f);
                    if (i % 3 == 0) Glow(mesh, point, 2.1f, new Color(1, 1, 1, fade));
                }
            }
        }

        static float Seed(int index) { return Mathf.Repeat(Mathf.Sin(index * 127.1f + 17.7f) * 43758.5453f, 1); }

        static void Impact(UnityEngine.UI.VertexHelper mesh, Vector2 center, float t)
        {
            var fade = 1 - t;
            Glow(mesh, center, 43, new Color(.75f, 1, .12f, .7f * fade * fade));
            Glow(mesh, center, 24, new Color(1, .95f, .55f, fade * fade));
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6 + .2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var point = center + direction * (10 + t * (35 + Seed(i) * 30));
                var side = new Vector2(-direction.y, direction.x) * .7f;
                var start = mesh.currentVertCount;
                var tint = new Color(1, 1, .7f, fade);
                var tip = point + direction * (5 + fade * 13);
                mesh.AddVert(point - side, tint, Vector2.zero); mesh.AddVert(point + side, tint, Vector2.zero);
                mesh.AddVert(tip, new Color(1, 1, .7f, 0), Vector2.zero); mesh.AddTriangle(start, start + 1, start + 2);
                if (i % 2 == 0) Star(mesh, point, 5 * fade, tint, angle);
            }
        }

        static void Star(UnityEngine.UI.VertexHelper mesh, Vector2 center, float size, Color tint, float rotation = 0)
        {
            var start = mesh.currentVertCount;
            var core = Color.Lerp(tint, Color.white, .7f); core.a = tint.a;
            var clear = tint; clear.a = 0;
            mesh.AddVert(center, core, Vector2.zero);
            const int sides = 8;
            for (var i = 0; i < sides; i++)
            {
                var angle = i * Mathf.PI / 4 + rotation;
                var radius = i % 2 == 0 ? size : size * .18f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, tint, Vector2.zero);
                mesh.AddVert(center + direction * (radius + .7f), clear, Vector2.zero);
                var a = start + 1 + i * 2; var b = start + 1 + (i + 1) % sides * 2;
                mesh.AddTriangle(start, a, b);
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        static void Glow(UnityEngine.UI.VertexHelper mesh, Vector2 center, float size, Color tint)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size, tint, Vector2.zero);
                mesh.AddTriangle(start, start + i + 1, start + (i + 1) % 12 + 1);
            }
        }
    }
}
#endif
