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
        Vector3 closePosition;
        Vector3 cardScale, closeScale;
        float elapsed;

        void Awake()
        {
            // Legacy dialogs add this component at runtime; prefab dialogs retain direct references.
            if (!card) card = (RectTransform)transform.Find("Card");
            if (!close) close = GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == "Close");
            if (!scrim) scrim = GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(item => item.name == "Scrim");
            if (!card || !close || !scrim) { enabled = false; return; }
            closeInsideCard = close.IsChildOf(card);
            scrimColor = scrim.color;
            closePosition = close.localPosition;
            cardScale = card.localScale; closeScale = close.localScale;
            cardFade = card.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();
            closeFade = close.GetComponent<CanvasGroup>() ?? close.gameObject.AddComponent<CanvasGroup>();
            initialized = true;
        }

        void OnEnable() { elapsed = 0; Apply(); }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float deltaTime)
        {
            if (elapsed >= Duration) return;
            elapsed = Mathf.Min(Duration, elapsed + deltaTime);
            Apply();
        }

        void Apply()
        {
            if (!initialized) return;
            var t = elapsed / Duration;
            var p = t - 1;
            var eased = 1 + 2.70158f * p * p * p + 1.70158f * p * p;
            var scale = Mathf.LerpUnclamped(.78f, 1, eased);
            card.localScale = cardScale * scale;
            if (!closeInsideCard)
            {
                close.localScale = closeScale * scale;
                var center = close.parent.InverseTransformPoint(card.position);
                close.localPosition = center + (closePosition - center) * scale;
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
