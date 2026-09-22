#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    public sealed class DifferencePopupMotion : MonoBehaviour
    {
        const float Duration = .32f;
        RectTransform card, close;
        CanvasGroup cardFade, closeFade;
        UnityEngine.UI.Image scrim;
        Color scrimColor;
        Vector3 closePosition;
        float elapsed;

        void Awake()
        {
            card = (RectTransform)transform.Find("Card");
            close = (RectTransform)transform.Find("Close");
            scrim = transform.Find("Scrim").GetComponent<UnityEngine.UI.Image>();
            scrimColor = scrim.color;
            CenterPivot(card); CenterPivot(close);
            closePosition = close.localPosition;
            cardFade = card.gameObject.AddComponent<CanvasGroup>();
            closeFade = close.gameObject.AddComponent<CanvasGroup>();
        }

        static void CenterPivot(RectTransform rect)
        {
            var pivot = new Vector2(.5f, .5f);
            var offset = Vector2.Scale(pivot - rect.pivot, rect.rect.size);
            rect.pivot = pivot;
            rect.anchoredPosition += offset;
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
            var t = elapsed / Duration;
            var p = t - 1;
            var eased = 1 + 2.70158f * p * p * p + 1.70158f * p * p;
            var scale = Mathf.LerpUnclamped(.78f, 1, eased);
            card.localScale = close.localScale = Vector3.one * scale;
            close.localPosition = card.localPosition + (closePosition - card.localPosition) * scale;
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
