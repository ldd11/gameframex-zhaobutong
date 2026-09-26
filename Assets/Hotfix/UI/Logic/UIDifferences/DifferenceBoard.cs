#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.UI
{
    public sealed class DifferenceBoard : MonoBehaviour, IPointerClickHandler, IScrollHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public UIDifferences owner;
        public DifferenceBoard peer;
        readonly Vector3[] corners = new Vector3[4];
        bool dragging, pinching;
        float blockedUntil, pinchDistance;
        Vector2 pinchMidpoint;
        bool resetting;
        float resetStarted, resetZoom;
        public bool IsResetting => resetting || (peer && peer.resetting);
        RectTransform Content => (RectTransform)transform;
        RectTransform Viewport => transform.parent as RectTransform;
        float Zoom => Content.localScale.x;
        Camera EventCamera
        {
            get
            {
                var canvas = GetComponentInParent<Canvas>();
                return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            }
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || IsResetting || dragging || pinching ||
                Time.unscaledTime < blockedUntil || !Inside(data.position, data.pressEventCamera)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, data.position, data.pressEventCamera, out var local)) return;
            var rect = Content.rect;
            if (owner) owner.ClickImage((local.x - rect.xMin) / rect.width,
                1 - (local.y - rect.yMin) / rect.height, Content == owner.lowerImage.rectTransform);
        }

        public void ResetView()
        {
            dragging = pinching = false;
            blockedUntil = 0;
            if (peer) { peer.dragging = peer.pinching = false; peer.blockedUntil = 0; }
            SetZoom(1);
        }

        public void RefreshViewport() { if (Viewport) ClampAndSync(); }

        public void SetZoom(float zoom)
        {
            CancelReset();
            if (!Viewport || float.IsNaN(zoom) || float.IsInfinity(zoom)) return;
            ZoomAround(zoom, Content.InverseTransformPoint(Viewport.TransformPoint(Viewport.rect.center)));
        }

        public void FocusSpot(Vector2 point, float zoom)
        {
            CancelReset();
            Content.localScale = new Vector3(Mathf.Clamp(zoom, 1, 2.5f), Mathf.Clamp(zoom, 1, 2.5f), 1);
            var local = new Vector2(Content.rect.xMin + point.x * Content.rect.width, Content.rect.yMax - point.y * Content.rect.height);
            Content.anchoredPosition += Viewport.rect.center - (Vector2)Viewport.InverseTransformPoint(Content.TransformPoint(local));
            ClampAndSync();
        }

        public void SetView(float zoom, Vector2 position)
        {
            CancelReset();
            Content.localScale = new Vector3(Mathf.Clamp(zoom, 1, 2.5f), Mathf.Clamp(zoom, 1, 2.5f), 1);
            Content.anchoredPosition = position;
            ClampAndSync();
        }

        public void OnScroll(PointerEventData data)
        {
            if (IsResetting || (owner && owner.HintActive)) return;
            if (!Inside(data.position, data.enterEventCamera)) return;
            BlockClick();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, data.position, data.enterEventCamera, out var local))
                ZoomAround(Zoom + data.scrollDelta.y * .2f, local);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (IsResetting || (owner && owner.HintActive)) return;
            if (data.button != PointerEventData.InputButton.Left || !Inside(data.pressPosition, data.pressEventCamera)) return;
            dragging = true;
            BlockClick();
        }

        public void OnDrag(PointerEventData data)
        {
            if (IsResetting || (owner && owner.HintActive)) return;
            if (!dragging || pinching || Input.touchCount > 1 || Zoom <= 1) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, data.position, data.pressEventCamera, out var current) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, data.position - data.delta, data.pressEventCamera, out var previous))
            {
                Content.anchoredPosition += current - previous;
                ClampAndSync();
            }
        }

        public void OnEndDrag(PointerEventData data)
        {
            dragging = false;
            BlockClick();
        }

        void Update()
        {
            if (owner && owner.HintActive) { dragging = pinching = false; return; }
            if (resetting)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - resetStarted) / .45f);
                ZoomAround(Mathf.Lerp(resetZoom, 1, t), Content.InverseTransformPoint(Viewport.TransformPoint(Viewport.rect.center)));
                BlockClick();
                if (t >= 1) resetting = false;
                return;
            }
            if (IsResetting) return;
            if (Input.touchCount < 2)
            {
                if (pinching) { pinching = false; BlockClick(); }
                return;
            }
            var first = Input.GetTouch(0).position;
            var second = Input.GetTouch(1).position;
            // Only the board containing the first finger drives the shared view.
            if (!pinching && ((peer && peer.pinching) || !Inside(first, EventCamera))) return;
            var midpoint = (first + second) * .5f;
            var distance = Vector2.Distance(first, second);
            BlockClick();
            if (pinching && pinchDistance > 1 && distance > 1)
            {
                var camera = EventCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, midpoint, camera, out var current) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(Viewport, pinchMidpoint, camera, out var previous))
                    Content.anchoredPosition += current - previous;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Content, midpoint, camera, out var local))
                    ZoomAround(Zoom * distance / pinchDistance, local);
            }
            pinching = true;
            pinchMidpoint = midpoint;
            pinchDistance = distance;
        }

        bool Inside(Vector2 point, Camera camera)
        { return Viewport && RectTransformUtility.RectangleContainsScreenPoint(Viewport, point, camera); }

        public void ResetViewAnimated()
        {
            if (!Viewport || IsResetting || Zoom <= 1 || (owner && owner.HintActive)) return;
            dragging = pinching = false;
            if (peer) peer.dragging = peer.pinching = false;
            resetZoom = Zoom;
            resetStarted = Time.unscaledTime;
            resetting = true;
            BlockClick();
        }

        void CancelReset()
        {
            resetting = false;
            if (peer) peer.resetting = false;
        }

        void ZoomAround(float zoom, Vector2 localPoint)
        {
            var before = (Vector2)Viewport.InverseTransformPoint(Content.TransformPoint(localPoint));
            Content.localScale = new Vector3(Mathf.Clamp(zoom, 1, 2.5f), Mathf.Clamp(zoom, 1, 2.5f), 1);
            var after = (Vector2)Viewport.InverseTransformPoint(Content.TransformPoint(localPoint));
            Content.anchoredPosition += before - after;
            ClampAndSync();
        }

        void ClampAndSync()
        {
            ClampPosition();
            if (!peer || peer == this || !peer.Viewport) return;
            Bounds(out var min, out var max);
            var overflow = (max - min - Viewport.rect.size) * .5f;
            var offset = (min + max) * .5f - Viewport.rect.center;
            var pan = new Vector2(overflow.x > 0 ? offset.x / overflow.x : 0, overflow.y > 0 ? offset.y / overflow.y : 0);
            peer.Content.localScale = Content.localScale;
            peer.Bounds(out min, out max);
            overflow = Vector2.Max(Vector2.zero, (max - min - peer.Viewport.rect.size) * .5f);
            peer.Content.anchoredPosition += peer.Viewport.rect.center + Vector2.Scale(pan, overflow) - (min + max) * .5f;
            peer.ClampPosition();
        }

        void ClampPosition()
        {
            Bounds(out var min, out var max);
            var view = Viewport.rect;
            var shift = Vector2.zero;
            shift.x = max.x - min.x <= view.width ? view.center.x - (min.x + max.x) * .5f :
                min.x > view.xMin ? view.xMin - min.x : max.x < view.xMax ? view.xMax - max.x : 0;
            shift.y = max.y - min.y <= view.height ? view.center.y - (min.y + max.y) * .5f :
                min.y > view.yMin ? view.yMin - min.y : max.y < view.yMax ? view.yMax - max.y : 0;
            Content.anchoredPosition += shift;
        }

        void Bounds(out Vector2 min, out Vector2 max)
        {
            Content.GetWorldCorners(corners);
            min = max = Viewport.InverseTransformPoint(corners[0]);
            for (var i = 1; i < corners.Length; i++)
            {
                var point = (Vector2)Viewport.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
        }

        void BlockClick()
        {
            blockedUntil = Time.unscaledTime + .2f;
            if (peer) peer.blockedUntil = blockedUntil;
        }

        void OnDisable()
        {
            CancelReset();
            dragging = pinching = false;
        }
    }
}
#endif
