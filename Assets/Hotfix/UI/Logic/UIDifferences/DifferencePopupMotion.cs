#if ENABLE_UI_UGUI
using System.Linq;
using UnityEngine;

namespace Hotfix.UI
{
    public sealed class DifferencePopupMotion : MonoBehaviour
    {
        const float Duration = .32f;
        [SerializeField] RectTransform card, close;
        CanvasGroup cardFade, closeFade;
        [SerializeField] UnityEngine.UI.Image scrim;
        bool initialized, closeInsideCard;
        Color scrimColor;
        Vector2 closePosition;
        Vector3 cardScale, closeScale;
        float elapsed;

        void Awake()
        {
            initialized = false;
            // Legacy dialogs add this component at runtime; prefab dialogs retain direct references.
            if (!card) card = (RectTransform)transform.Find("Card");
            if (!close) close = GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == "Close");
            if (!scrim) scrim = GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(item => item.name == "Scrim");
            if (!card || !close || !scrim) { enabled = false; return; }
            closeInsideCard = close.IsChildOf(card);
            scrimColor = scrim.color;
            closePosition = close.anchoredPosition;
            cardScale = card.localScale; closeScale = close.localScale;
            initialized = true;
        }

        void OnEnable()
        {
            if (!initialized || !card || !close || !scrim) Awake();
            if (!initialized) return;
            SortAboveGameParticles(gameObject);
            // Prefab refreshes can remove runtime-added groups while the popup retains its cached references.
            cardFade = card.GetComponent<CanvasGroup>();
            if (!cardFade) cardFade = card.gameObject.AddComponent<CanvasGroup>();
            closeFade = close.GetComponent<CanvasGroup>();
            if (!closeFade) closeFade = close.gameObject.AddComponent<CanvasGroup>();
            elapsed = 0; Apply();
        }

        public static void SortAboveGameParticles(GameObject popup)
        {
            var parentCanvas = popup.transform.parent ? popup.transform.parent.GetComponentInParent<Canvas>() : null;
            if (!parentCanvas) return;
            var canvas = popup.GetComponent<Canvas>();
            if (!canvas) canvas = popup.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerID = parentCanvas.sortingLayerID;
            // Gameplay particles use parent + 1/+2; dialogs occupy the next layer, with purchases above Profile.
            var owner = popup.GetComponentInParent<UIDifferences>();
            var order = 3;
            if (owner && owner.modal == popup) order = 4;
            if (owner && owner.noticeLabel && owner.noticeLabel.transform.parent.gameObject == popup) order = 5;
            canvas.sortingOrder = parentCanvas.sortingOrder + order;
            canvas.worldCamera = parentCanvas.rootCanvas.worldCamera;
            canvas.additionalShaderChannels = parentCanvas.additionalShaderChannels;
            if (!popup.GetComponent<UnityEngine.UI.GraphicRaycaster>()) popup.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            if (elapsed >= Duration) return;
            elapsed = Mathf.Min(Duration, elapsed + deltaTime);
            Apply();
        }

        void Apply()
        {
            if (!initialized || !card || !close || !scrim || !cardFade || !closeFade) return;
            var t = elapsed / Duration;
            var p = t - 1;
            var eased = 1 + 2.70158f * p * p * p + 1.70158f * p * p;
            var scale = Mathf.LerpUnclamped(.78f, 1, eased);
            card.localScale = cardScale * scale;
            if (!closeInsideCard)
            {
                close.localScale = closeScale * scale;
                var center = close.parent.InverseTransformPoint(card.position);
                var resting = close.localPosition + (Vector3)(closePosition - close.anchoredPosition);
                close.localPosition = center + (resting - center) * scale;
            }
            cardFade.alpha = closeFade.alpha = Mathf.Clamp01(elapsed / .12f);
            cardFade.interactable = closeFade.interactable = t >= 1;
            cardFade.blocksRaycasts = closeFade.blocksRaycasts = t >= 1;
            var tint = scrimColor; tint.a *= Mathf.Clamp01(elapsed / .18f);
            scrim.color = tint;
        }

        void OnDisable()
        {
            if (!card) return;
            elapsed = Duration;
            Apply();
        }
    }
}
#endif
