#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    // Layout only: all artwork, controls, anchors and click bindings stay in the prefab.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DifferencePlayLayout : MonoBehaviour
    {
        [Header("游戏页适配（Canvas 单位）")]
        [Tooltip("X：左右最小留白；Y：顶部和底部最小留白。")]
        public Vector2 edgePadding = new Vector2(5, 40);
        [Min(1), Tooltip("顶部栏最大宽度（Canvas 单位）。宽屏居中收窄，手机保持安全区内的原有宽度。")]
        public float headerMaxWidth = 1600;
        [Min(0), Tooltip("底部广告预留高度，使用 Canvas 设计单位（不是设备像素）。设为 0 取消预留。")]
        public float bottomAdHeight = 120;
        [Min(0), Tooltip("按钮、进度条、图片之间的最小距离。")]
        public float sectionGap = 20;
        [Min(0), Tooltip("上下两张图片之间的距离。")]
        public float pictureGap = 4;
        [Min(0), Tooltip("边框在图片外侧的宽度。")]
        public float framePadding = 4;
        [Header("现有预制体节点")]
        public RectTransform upper, lower, frame, progress, back, life, hint, zoom, level;
        [Header("单独位置偏移（Canvas 单位，X 向右，Y 向上）")]
        public Vector2 upperOffset;
        public Vector2 lowerOffset, frameOffset, progressOffset, backOffset, lifeOffset, hintOffset, zoomOffset, levelOffset;
        DrivenRectTransformTracker tracker;
        bool tracked;
        float progressReferenceHeight;

        void LateUpdate() { if (!Application.isPlaying) ApplyLayout(); }
        void OnDisable() { tracker.Clear(); tracked = false; }
        void OnValidate() { tracker.Clear(); tracked = false; progressReferenceHeight = 0; }

        public bool ApplyLayout()
        {
            var area = ((RectTransform)transform).rect;
            if (Application.isPlaying)
            {
                var ui = GetComponentInParent<UIDifferences>();
                if (ui)
                {
                    var root = (RectTransform)ui.transform;
                    var safe = Screen.safeArea;
                    if (root.rect.width <= 0 || root.rect.height <= 0 || safe.width <= 0 || safe.height <= 0) return false;
                    // Only the content uses the safe area; Stage remains full-screen at scale one.
                    var min = root.rect.min + Vector2.Scale(root.rect.size,
                        new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height)));
                    var max = root.rect.min + Vector2.Scale(root.rect.size,
                        new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height)));
                    var localMin = transform.InverseTransformPoint(root.TransformPoint(min));
                    var localMax = transform.InverseTransformPoint(root.TransformPoint(max));
                    area = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
                }
            }
            return ApplyLayout(area);
        }

        public bool ApplyLayout(Rect area)
        {
            if (!enabled || !upper || !lower || !frame || !progress || !back || !life || !hint || !zoom) return false;
            if (area.width <= 0 || area.height <= 0) return false;
            if (!tracked)
            {
                foreach (var rect in new[] { upper, lower, frame, progress })
                    tracker.Add(this, rect, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
                foreach (var rect in new[] { back, life, hint, zoom })
                    if (rect) tracker.Add(this, rect, DrivenTransformProperties.AnchoredPosition);
                if (level) tracker.Add(this, level, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
                tracked = true;
            }
            var padding = Vector2.Max(Vector2.zero, edgePadding);
            var headerWidth = Mathf.Min(area.width - padding.x * 2, Mathf.Max(1, headerMaxWidth));
            var gap = Mathf.Max(0, sectionGap);
            var middle = Mathf.Max(0, pictureGap) + Mathf.Max(0, lowerOffset.y - upperOffset.y);
            var border = Mathf.Max(0, framePadding);
            if (level)
            {
                var titleReserve = Mathf.Max(Size(back).x + backOffset.x - levelOffset.x,
                    Size(life).x - lifeOffset.x + levelOffset.x);
                var titleWidth = Mathf.Max(1, headerWidth - 2 * (titleReserve + gap));
                level.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, titleWidth / Mathf.Max(.001f, Mathf.Abs(level.localScale.x)));
                level.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(Size(back).y, Size(life).y) / Mathf.Max(.001f, Mathf.Abs(level.localScale.y)));
            }
            var headerTop = Mathf.Max(Size(back).y * .5f + backOffset.y, Size(life).y * .5f + lifeOffset.y);
            var headerHalfHeight = Mathf.Max(Size(back).y, Size(life).y) * .5f;
            if (level && level.gameObject.activeSelf)
            {
                headerTop = Mathf.Max(headerTop, Size(level).y * .5f + levelOffset.y);
                headerHalfHeight = Mathf.Max(headerHalfHeight, Size(level).y * .5f);
            }
            // A shared baseline stays below the safe-area top even with individual offsets.
            var headerCenter = area.yMax - padding.y - Mathf.Max(headerHalfHeight, headerTop);
            var headerBottom = Mathf.Min(headerCenter + backOffset.y - Size(back).y * .5f,
                headerCenter + lifeOffset.y - Size(life).y * .5f);
            if (level && level.gameObject.activeSelf)
                headerBottom = Mathf.Min(headerBottom, headerCenter + levelOffset.y - Size(level).y * .5f);
            if (progressReferenceHeight <= 0)
            {
                progressReferenceHeight = Size(progress).y;
                foreach (Transform child in progress)
                    if (child.gameObject.activeSelf && child is RectTransform dot)
                        progressReferenceHeight = Mathf.Max(progressReferenceHeight, Size(dot).y * Mathf.Abs(progress.localScale.y));
            }
            // A stable row budget prevents Spine dot resizing from feeding back into picture size.
            var progressHeight = progressReferenceHeight;
            var progressCenter = headerBottom - gap - progressHeight * .5f - Mathf.Max(0, progressOffset.y) + progressOffset.y;
            // Reserve the hand's full motion even while hidden, so idle guidance never moves the board.
            var handBottom = Mathf.Max(Size(hint).y * .5f, DifferenceHintHand.BottomExtent * Mathf.Abs(hint.localScale.y));
            // Downward offsets need extra space below the pictures, not a compensating upward baseline.
            var footerTop = Mathf.Max(Size(hint).y * .5f + Mathf.Max(0, hintOffset.y),
                Size(zoom).y * .5f + Mathf.Max(0, zoomOffset.y));
            var footerBottom = Mathf.Max(handBottom - hintOffset.y, Size(zoom).y * .5f - zoomOffset.y);
            var topShift = Mathf.Max(0, Mathf.Max(upperOffset.y, Mathf.Max(lowerOffset.y, frameOffset.y)));
            var bottomShift = Mathf.Min(0, Mathf.Min(upperOffset.y, Mathf.Min(lowerOffset.y, frameOffset.y)));
            var top = progressCenter - progressHeight * .5f - gap - border - topShift;
            var bottom = area.yMin + Mathf.Max(0, bottomAdHeight) + padding.y + footerTop + footerBottom + gap + border - bottomShift;
            var width = Mathf.Min(area.width - padding.x * 2 - border * 2,
                (top - bottom - middle) * UIDifferences.ImageAspect * .5f);
            if (width <= 0 || float.IsNaN(width) || float.IsInfinity(width)) return false;
            var height = width / UIDifferences.ImageAspect;
            var totalHeight = height * 2 + middle;
            // Center the whole stack, including progress, tools and the hand, in its remaining space.
            var centerDrop = Mathf.Max(0, top - bottom - totalHeight) * .5f;
            progressCenter -= centerDrop;
            var center = new Vector2(area.center.x, top - totalHeight * .5f - centerDrop);
            var board = new Rect(center.x - width * .5f, center.y - totalHeight * .5f, width, totalHeight);
            var resized = (Size(upper) - new Vector2(width, height)).sqrMagnitude > .01f;
            Fit(upper, new Rect(board.x, board.y + height + middle, width, height), upperOffset);
            Fit(lower, new Rect(board.x, board.y, width, height), lowerOffset);
            Fit(frame, new Rect(board.x - border, board.y - border, width + border * 2, totalHeight + border * 2), frameOffset);
            Fit(progress, new Rect(board.x + progressOffset.x, progressCenter - progressHeight * .5f, width, progressHeight), Vector2.zero);
            Place(back, new Vector2(area.center.x - headerWidth * .5f + Size(back).x * .5f, headerCenter) + backOffset);
            Place(life, new Vector2(area.center.x + headerWidth * .5f - Size(life).x * .5f, headerCenter) + lifeOffset);
            if (level)
            {
                Place(level, new Vector2(center.x, headerCenter) + levelOffset);
            }
            var footerCenter = board.yMin - border + bottomShift - gap - footerTop;
            var toolsWidth = Mathf.Min(area.width - padding.x * 2,
                Mathf.Max(width + border * 2, Size(hint).x + Size(zoom).x * 2 + gap * 2));
            Place(hint, new Vector2(center.x, footerCenter) + hintOffset);
            Place(zoom, new Vector2(center.x + toolsWidth * .5f - Size(zoom).x * .5f, footerCenter) + zoomOffset);
            return resized;
        }

        static Vector2 Size(RectTransform rect) => Vector2.Scale(rect.rect.size,
            new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));

        static void Fit(RectTransform rect, Rect bounds, Vector2 offset)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bounds.width / Mathf.Max(.001f, Mathf.Abs(rect.localScale.x)));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bounds.height / Mathf.Max(.001f, Mathf.Abs(rect.localScale.y)));
            Place(rect, bounds.center + offset);
        }

        static void Place(RectTransform rect, Vector2 center)
        {
            // localPosition accounts for the existing anchors; neither anchors nor pivots need changing.
            var pivot = center + Vector2.Scale(rect.pivot - Vector2.one * .5f, Size(rect));
            rect.localPosition = new Vector3(pivot.x, pivot.y, rect.localPosition.z);
        }
    }
}
#endif
