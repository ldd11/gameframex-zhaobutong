#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.UI
{
    public sealed class DifferencePageSwipe : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public UIDifferences owner;
        public UnityEngine.UI.ScrollRect scroll;
        PointerEventData dragging;
        Vector2 origin;
        bool horizontal;

        public void OnInitializePotentialDrag(PointerEventData data)
        {
            if (scroll && dragging == null) scroll.OnInitializePotentialDrag(data);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (dragging != null || data.button != PointerEventData.InputButton.Left || !owner || !owner.CanSwipeTabs) return;
            var delta = data.position - data.pressPosition;
            horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            if (horizontal)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.stage, data.pressPosition, data.pressEventCamera, out origin) ||
                    !owner.BeginTabDrag()) return;
                if (scroll) scroll.StopMovement();
            }
            else if (scroll) scroll.OnBeginDrag(data);
            else return;
            dragging = data;
            data.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData data)
        {
            if (dragging == null || dragging.pointerId != data.pointerId) return;
            if (horizontal)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.stage, data.position, data.pressEventCamera, out var point))
                    owner.DragTabs(point.x - origin.x);
            }
            else if (scroll) scroll.OnDrag(data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (dragging == null || dragging.pointerId != data.pointerId) return;
            OnDrag(data);
            dragging = null;
            if (horizontal) owner.EndTabDrag();
            else if (scroll) scroll.OnEndDrag(data);
        }

        void OnDisable()
        {
            if (dragging == null) return;
            var data = dragging; dragging = null;
            if (horizontal) { if (owner) owner.CancelTabDrag(); }
            else if (scroll) scroll.OnEndDrag(data);
        }
    }
}
#endif
