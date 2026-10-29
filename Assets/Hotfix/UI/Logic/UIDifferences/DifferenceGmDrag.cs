#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.UI
{
    public sealed class DifferenceGmDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform window;
        Vector2 previous;
        bool dragging;

        public void OnBeginDrag(PointerEventData data) => dragging = LocalPoint(data, out previous);

        public void OnDrag(PointerEventData data)
        {
            if (!dragging || !LocalPoint(data, out var point)) return;
            window.anchoredPosition += point - previous;
            previous = point;
            ClampToParent();
        }

        bool LocalPoint(PointerEventData data, out Vector2 point)
        {
            point = default;
            return window && window.parent is RectTransform parent &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out point);
        }

        void OnRectTransformDimensionsChange() => ClampToParent();

        void ClampToParent()
        {
            if (!window || !(window.parent is RectTransform parent)) return;
            var position = window.anchoredPosition;
            position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, parent.rect.width - window.rect.width));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0, parent.rect.height - window.rect.height), 0);
            window.anchoredPosition = position;
        }
    }
}
#endif
