#if ENABLE_UI_UGUI
using System;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed class DifferenceFoundFlight : UnityEngine.UI.MaskableGraphic
    {
        const float FlightTime = .52f, FlashTime = .12f, SettleTime = .20f;
        public const float Duration = 1.6f;
        Vector2 origin;
        RectTransform destination;
        Transform effect, arrivalEffect;
        GameObject arrivalPrefab;
        ParticleSystem[] particles;
        float elapsed, cleanupTime;
        bool revealed, stopped;
        Action onReveal;


        public static void Launch(RectTransform parent, Vector3 worldOrigin, RectTransform target, Action onReveal = null, GameObject effectPrefab = null)
        {
            if (!effectPrefab) throw new InvalidOperationException("请绑定 UIDifferences 的 Found Flight Prefab。");
            var canvas = parent.GetComponentInParent<Canvas>();
            if (!canvas || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                throw new InvalidOperationException("星星粒子需要使用 Screen Space Camera 或 World Space Canvas。");
            var node = new GameObject("FoundFlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(DifferenceFoundFlight));
            node.layer = parent.gameObject.layer;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var flight = node.GetComponent<DifferenceFoundFlight>();
            flight.raycastTarget = false;
            var ui = parent.GetComponentInParent<UIDifferences>();
            flight.arrivalPrefab = ui.foundArrivalPrefab;
            flight.origin = rect.InverseTransformPoint(worldOrigin);
            flight.destination = target;
            flight.onReveal = onReveal;
            flight.effect = Instantiate(effectPrefab, rect, false).transform;
            flight.effect.localPosition = flight.origin;
            foreach (var child in flight.effect.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = node.layer;
            flight.particles = flight.effect.GetComponentsInChildren<ParticleSystem>(true);
            flight.cleanupTime = Duration;
            foreach (var particle in flight.particles)
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.useUnscaledTime = true;
                flight.cleanupTime = Mathf.Max(flight.cleanupTime, FlightTime + main.startLifetime.constantMax + .1f);
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = canvas.sortingLayerID;
                renderer.sortingOrder = canvas.sortingOrder + 1;
                particle.Play(false);
            }
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
            if (!destination)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            effect.localPosition = Point(Mathf.Clamp01(elapsed / FlightTime));
            if (!stopped && elapsed >= FlightTime)
            {
                stopped = true;
                if (arrivalPrefab)
                {
                    arrivalEffect = Instantiate(arrivalPrefab, rectTransform, false).transform;
                    arrivalEffect.localPosition = Point(1);
                    var canvas = GetComponentInParent<Canvas>();
                    foreach (var child in arrivalEffect.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
                    foreach (var particle in arrivalEffect.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = particle.main;
                        main.loop = false;
                        main.useUnscaledTime = true;
                        cleanupTime = Mathf.Max(cleanupTime, elapsed + main.startDelay.constantMax + main.duration + main.startLifetime.constantMax + .1f);
                        var renderer = particle.GetComponent<ParticleSystemRenderer>();
                        renderer.sortingLayerID = canvas.sortingLayerID;
                        renderer.sortingOrder = canvas.sortingOrder + 2;
                        particle.Play(false);
                    }
                }
                foreach (var particle in particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!revealed && elapsed >= FlightTime + FlashTime)
            {
                revealed = true;
                var reveal = onReveal; onReveal = null;
                reveal?.Invoke();
            }
            if (revealed)
                destination.localScale = Vector3.one * (1 + .22f * Mathf.Sin(Mathf.Clamp01((elapsed - FlightTime - FlashTime) / SettleTime) * Mathf.PI));
            if (arrivalEffect) arrivalEffect.localPosition = Point(1);
            if (elapsed >= cleanupTime)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        protected override void OnDisable()
        {
            if (destination) destination.localScale = Vector3.one;
            onReveal = null;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh) { mesh.Clear(); }
    }
}
#endif
