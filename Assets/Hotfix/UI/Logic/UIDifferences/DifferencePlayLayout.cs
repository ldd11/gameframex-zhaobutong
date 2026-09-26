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
        public Vector2 edgePadding = new Vector2(32, 40);
        [Min(0), Tooltip("底部广告预留高度，使用 Canvas 设计单位（不是设备像素）。设为 0 取消预留。")]
        public float bottomAdHeight = 120;
        [Min(0), Tooltip("按钮、进度条、图片之间的最小距离。")]
        public float sectionGap = 12;
        [Min(0), Tooltip("上下两张图片之间的距离。")]
        public float pictureGap = 4;
        [Min(0), Tooltip("边框在图片外侧的宽度。")]
        public float framePadding = 4;
        [Tooltip("图片组相对页面中心的上下偏移；正数向上，空间不足时自动限制。")]
        public float boardCenterY = 50;
        [Header("现有预制体节点")]
        public RectTransform upper, lower, frame, progress, back, life, hint, zoom, level;
        DrivenRectTransformTracker tracker;
        bool tracked;

        void LateUpdate() { if (!Application.isPlaying) ApplyLayout(); }
        void OnDisable() { tracker.Clear(); tracked = false; }
        void OnValidate() { tracker.Clear(); tracked = false; }

        public bool ApplyLayout()
        {
            if (!enabled || !upper || !lower || !frame || !progress || !back || !life || !hint || !zoom) return false;
            var area = ((RectTransform)transform).rect;
            if (area.width <= 0 || area.height <= 0) return false;
            var padding = Vector2.Max(Vector2.zero, edgePadding);
            var gap = Mathf.Max(0, sectionGap);
            var middle = Mathf.Max(0, pictureGap);
            var border = Mathf.Max(0, framePadding);
            var headerHeight = Mathf.Max(Size(back).y, Size(life).y);
            if (level && level.gameObject.activeSelf) headerHeight = Mathf.Max(headerHeight, Size(level).y);
            var footerHeight = Mathf.Max(Size(hint).y, Size(zoom).y);
            // Reserve the hand's full motion even while hidden, so idle guidance never moves the board.
            var footerBottom = Mathf.Max(footerHeight * .5f, DifferenceHintHand.BottomExtent * Mathf.Abs(hint.localScale.y));
            var top = area.yMax - padding.y - headerHeight - Size(progress).y - gap * 2 - border;
            var bottom = area.yMin + Mathf.Max(0, bottomAdHeight) + padding.y + footerHeight * .5f + footerBottom + gap + border;
            var width = Mathf.Min(area.width - padding.x * 2 - border * 2,
                (top - bottom - middle) * UIDifferences.ImageAspect * .5f);
            if (width <= 0 || float.IsNaN(width) || float.IsInfinity(width)) return false;
            var height = width / UIDifferences.ImageAspect;
            var totalHeight = height * 2 + middle;
            var center = new Vector2(area.center.x,
                Mathf.Clamp(area.center.y + boardCenterY, bottom + totalHeight * .5f, top - totalHeight * .5f));
            var board = new Rect(center.x - width * .5f, center.y - totalHeight * .5f, width, totalHeight);
            var resized = (Size(upper) - new Vector2(width, height)).sqrMagnitude > .01f;
            if (!tracked)
            {
                foreach (var rect in new[] { upper, lower, frame, progress })
                    tracker.Add(this, rect, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
                foreach (var rect in new[] { back, life, hint, zoom, level })
                    if (rect) tracker.Add(this, rect, DrivenTransformProperties.AnchoredPosition);
                tracked = true;
            }
            Fit(upper, new Rect(board.x, board.y + height + middle, width, height));
            Fit(lower, new Rect(board.x, board.y, width, height));
            Fit(frame, new Rect(board.x - border, board.y - border, width + border * 2, totalHeight + border * 2));
            Fit(progress, new Rect(board.x, board.yMax + border + gap, width, Size(progress).y));
            var headerTop = area.yMax - padding.y;
            Place(back, new Vector2(board.xMin + Size(back).x * .5f, headerTop - headerHeight * .5f));
            Place(life, new Vector2(board.xMax - Size(life).x * .5f, headerTop - headerHeight * .5f));
            if (level) Place(level, new Vector2(center.x, headerTop - headerHeight * .5f));
            var footerCenter = board.yMin - border - gap - footerHeight * .5f;
            Place(hint, new Vector2(center.x, footerCenter));
            Place(zoom, new Vector2(board.xMax - Size(zoom).x * .5f, footerCenter));
            return resized;
        }

        static Vector2 Size(RectTransform rect) => Vector2.Scale(rect.rect.size,
            new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));

        static void Fit(RectTransform rect, Rect bounds)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bounds.width / Mathf.Max(.001f, Mathf.Abs(rect.localScale.x)));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bounds.height / Mathf.Max(.001f, Mathf.Abs(rect.localScale.y)));
            Place(rect, bounds.center);
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
