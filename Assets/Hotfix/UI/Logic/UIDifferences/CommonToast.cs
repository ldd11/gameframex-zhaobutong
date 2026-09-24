#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.UI
{
    // Reusable on any toast root containing a Text. The prefab defines its resting position.
    public sealed class CommonToast : MonoBehaviour
    {
        public float riseDistance = 90f;
        public float fadeDuration = .65f;
        RectTransform rect;
        CanvasGroup group;
        Text label;
        Vector2 startPosition;
        float elapsed, hold;

        void Initialize()
        {
            if (rect) return;
            rect = (RectTransform)transform;
            startPosition = rect.anchoredPosition;
            label = GetComponentInChildren<Text>(true);
            group = GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Show(string message, float seconds = 2f)
        {
            Initialize();
            label.text = message;
            elapsed = 0;
            hold = Mathf.Max(0, seconds - Mathf.Max(.01f, fadeDuration));
            rect.anchoredPosition = startPosition;
            group.alpha = 1;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        void Update() { Advance(Time.unscaledDeltaTime); }

        void Advance(float delta)
        {
            elapsed += delta;
            var t = Mathf.Clamp01((elapsed - hold) / Mathf.Max(.01f, fadeDuration));
            rect.anchoredPosition = startPosition + Vector2.up * (riseDistance * t);
            group.alpha = 1 - t;
            if (t >= 1) gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (!rect) return;
            rect.anchoredPosition = startPosition;
            group.alpha = 1;
        }
    }
}
#endif
