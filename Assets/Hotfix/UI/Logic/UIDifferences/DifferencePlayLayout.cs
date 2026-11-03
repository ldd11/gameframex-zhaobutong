#if ENABLE_UI_UGUI
using UnityEngine;

namespace Hotfix.UI
{
    // Layout only: all artwork, controls, anchors and click bindings stay in the prefab.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DifferencePlayLayout : MonoBehaviour
    {
        [Header("游戏页适配（Canvas 单位）")]
        [Tooltip("X：顶部控件与宽屏图片的左右最小留白（手机外框贴屏幕）；Y：顶部和底部最小留白。")]
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
        RectTransform[] scaledControls;
        Vector3[] originalScales;
        float appliedScale = 1;

        void LateUpdate() { if (!Application.isPlaying) ApplyLayout(); }
        void OnDisable() { RestoreScales(); tracker.Clear(); tracked = false; }
        void OnValidate() { RestoreScales(); tracker.Clear(); tracked = false; progressReferenceHeight = 0; }

        void RestoreScales()
        {
            if (scaledControls != null && !Mathf.Approximately(appliedScale, 1))
                for (var i = 0; i < scaledControls.Length; i++)
                    if (scaledControls[i]) scaledControls[i].localScale = originalScales[i];
            scaledControls = null; originalScales = null; appliedScale = 1;
        }

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
                scaledControls = new[] { progress, back, life, hint, zoom, level };
                originalScales = new Vector3[scaledControls.Length];
                for (var i = 0; i < scaledControls.Length; i++)
                    if (scaledControls[i]) originalScales[i] = scaledControls[i].localScale;
                tracked = true;
            }
            var playing = Application.IsPlaying(gameObject);
            if (!playing && Mathf.Approximately(appliedScale, 1))
            {
                if (originalScales[0] != progress.localScale) progressReferenceHeight = 0;
                for (var i = 0; i < scaledControls.Length; i++)
                    if (scaledControls[i]) originalScales[i] = scaledControls[i].localScale;
            }
            if (progressReferenceHeight <= 0)
            {
                progressReferenceHeight = Size(progress).y / appliedScale;
                foreach (Transform child in progress)
                    if (child.gameObject.activeSelf && child is RectTransform dot)
                        progressReferenceHeight = Mathf.Max(progressReferenceHeight, Size(dot).y * Mathf.Abs(progress.localScale.y) / appliedScale);
            }
            var screen = ((RectTransform)transform).rect;
            var phone = screen.width > 0 && screen.height > 0 && screen.width / screen.height <= .6f;
            var border = Mathf.Max(0, framePadding);
            var targetWidth = screen.width - border * 2;
            var scale = 1f;
            var spacingScale = 1f;
            if (phone && targetWidth > 0)
            {
                var required = targetWidth * 2 / UIDifferences.ImageAspect;
                if (AvailablePictureHeight(area, 1, 1) < required)
                {
                    // Preserve control size first by reducing empty space before scaling the controls.
                    const float minSpacing = .1f;
                    if (AvailablePictureHeight(area, minSpacing, 1) >= required)
                    {
                        var low = minSpacing; var high = 1f;
                        for (var i = 0; i < 12; i++)
                        {
                            var middleScale = (low + high) * .5f;
                            if (AvailablePictureHeight(area, middleScale, 1) >= required) low = middleScale;
                            else high = middleScale;
                        }
                        spacingScale = low;
                    }
                    else
                    {
                        spacingScale = minSpacing;
                        if (AvailablePictureHeight(area, spacingScale, .01f) < required)
                            spacingScale = 1;
                        else
                        {
                            var low = .01f; var high = 1f;
                            for (var i = 0; i < 12; i++)
                            {
                                var middleScale = (low + high) * .5f;
                                if (AvailablePictureHeight(area, spacingScale, middleScale) >= required) low = middleScale;
                                else high = middleScale;
                            }
                            scale = low;
                        }
                    }
                }
            }
            // Edit-mode previews must not write temporary device scaling into the prefab.
            if (!playing) scale = 1;
            var controlsResized = !Mathf.Approximately(scale, appliedScale);
            if (controlsResized)
            {
                for (var i = 0; i < scaledControls.Length; i++)
                    if (scaledControls[i]) scaledControls[i].localScale = originalScales[i] * scale;
                appliedScale = scale;
            }
            var padding = Vector2.Max(Vector2.zero, edgePadding);
            padding.y *= spacingScale;
            var headerWidth = Mathf.Min(area.width - padding.x * 2, Mathf.Max(1, headerMaxWidth));
            var gap = Mathf.Max(0, sectionGap) * spacingScale;
            var middle = Mathf.Max(0, pictureGap) + Mathf.Max(0, lowerOffset.y - upperOffset.y) * spacingScale;
            if (level)
            {
                var titleReserve = Mathf.Max(Size(back).x + backOffset.x - levelOffset.x,
                    Size(life).x - lifeOffset.x + levelOffset.x);
                var titleWidth = Mathf.Max(1, headerWidth - 2 * (titleReserve + gap));
                level.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, titleWidth / Mathf.Max(.001f, Mathf.Abs(level.localScale.x)));
                level.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(Size(back).y, Size(life).y) / Mathf.Max(.001f, Mathf.Abs(level.localScale.y)));
            }
            var headerTop = Mathf.Max(Size(back).y * .5f + backOffset.y * spacingScale, Size(life).y * .5f + lifeOffset.y * spacingScale);
            var headerHalfHeight = Mathf.Max(Size(back).y, Size(life).y) * .5f;
            if (level && level.gameObject.activeSelf)
            {
                headerTop = Mathf.Max(headerTop, Size(level).y * .5f + levelOffset.y * spacingScale);
                headerHalfHeight = Mathf.Max(headerHalfHeight, Size(level).y * .5f);
            }
            // A shared baseline stays below the safe-area top even with individual offsets.
            var headerCenter = area.yMax - padding.y - Mathf.Max(headerHalfHeight, headerTop);
            var headerBottom = Mathf.Min(headerCenter + backOffset.y * spacingScale - Size(back).y * .5f,
                headerCenter + lifeOffset.y * spacingScale - Size(life).y * .5f);
            if (level && level.gameObject.activeSelf)
                headerBottom = Mathf.Min(headerBottom, headerCenter + levelOffset.y * spacingScale - Size(level).y * .5f);
            // A stable row budget prevents Spine dot resizing from feeding back into picture size.
            var progressHeight = progressReferenceHeight * scale;
            var progressCenter = headerBottom - gap - progressHeight * .5f - Mathf.Max(0, progressOffset.y) * spacingScale + progressOffset.y * spacingScale;
            // Reserve the hand's full motion even while hidden, so idle guidance never moves the board.
            var handBottom = Mathf.Max(Size(hint).y * .5f, DifferenceHintHand.BottomExtent * Mathf.Abs(hint.localScale.y));
            // Downward offsets need extra space below the pictures, not a compensating upward baseline.
            var footerTop = Mathf.Max(Size(hint).y * .5f + Mathf.Max(0, hintOffset.y) * spacingScale,
                Size(zoom).y * .5f + Mathf.Max(0, zoomOffset.y) * spacingScale);
            var footerBottom = Mathf.Max(handBottom - hintOffset.y * spacingScale, Size(zoom).y * .5f - zoomOffset.y * spacingScale);
            var topShift = Mathf.Max(0, Mathf.Max(upperOffset.y, Mathf.Max(lowerOffset.y, frameOffset.y))) * spacingScale;
            var bottomShift = Mathf.Min(0, Mathf.Min(upperOffset.y, Mathf.Min(lowerOffset.y, frameOffset.y))) * spacingScale;
            var top = progressCenter - progressHeight * .5f - gap - border - topShift;
            var bottom = area.yMin + Mathf.Max(0, bottomAdHeight) + padding.y + footerTop + footerBottom + gap + border - bottomShift;
            var width = Mathf.Min(phone ? targetWidth : area.width - padding.x * 2 - border * 2,
                (top - bottom - middle) * UIDifferences.ImageAspect * .5f);
            if (phone && width > 0 && targetWidth - width < .01f) width = targetWidth;
            if (width <= 0 || float.IsNaN(width) || float.IsInfinity(width)) return false;
            var height = width / UIDifferences.ImageAspect;
            var totalHeight = height * 2 + middle;
            // Center the whole stack, including progress, tools and the hand, in its remaining space.
            var centerDrop = Mathf.Max(0, top - bottom - totalHeight) * .5f;
            progressCenter -= centerDrop;
            var center = new Vector2(phone ? screen.center.x : area.center.x, top - totalHeight * .5f - centerDrop);
            var board = new Rect(center.x - width * .5f, center.y - totalHeight * .5f, width, totalHeight);
            var resized = (Size(upper) - new Vector2(width, height)).sqrMagnitude > .01f;
            Fit(upper, new Rect(board.x, board.y + height + middle, width, height), ScaledOffset(upperOffset, spacingScale));
            Fit(lower, new Rect(board.x, board.y, width, height), ScaledOffset(lowerOffset, spacingScale));
            Fit(frame, new Rect(board.x - border, board.y - border, width + border * 2, totalHeight + border * 2), ScaledOffset(frameOffset, spacingScale));
            var progressWidth = phone ? Mathf.Min(width, area.width - padding.x * 2) : width;
            var progressResized = Mathf.Abs(Size(progress).x - progressWidth) > .01f;
            Fit(progress, new Rect(area.center.x - progressWidth * .5f + progressOffset.x,
                progressCenter - progressHeight * .5f, progressWidth, progressHeight), Vector2.zero);
            Place(back, new Vector2(area.center.x - headerWidth * .5f + Size(back).x * .5f, headerCenter) + ScaledOffset(backOffset, spacingScale));
            Place(life, new Vector2(area.center.x + headerWidth * .5f - Size(life).x * .5f, headerCenter) + ScaledOffset(lifeOffset, spacingScale));
            if (level)
            {
                Place(level, new Vector2(area.center.x, headerCenter) + ScaledOffset(levelOffset, spacingScale));
            }
            var footerCenter = board.yMin - border + bottomShift - gap - footerTop;
            var toolsWidth = Mathf.Min(area.width - padding.x * 2,
                Mathf.Max(width + border * 2, Size(hint).x + Size(zoom).x * 2 + gap * 2));
            Place(hint, new Vector2(area.center.x, footerCenter) + ScaledOffset(hintOffset, spacingScale));
            Place(zoom, new Vector2(area.center.x + toolsWidth * .5f - Size(zoom).x * .5f, footerCenter) + ScaledOffset(zoomOffset, spacingScale));
            return resized || controlsResized || progressResized;
        }

        static Vector2 ScaledOffset(Vector2 offset, float scale) => new Vector2(offset.x, offset.y * scale);

        float AvailablePictureHeight(Rect area, float spacingScale, float controlScale)
        {
            var backHeight = Size(back).y / appliedScale * controlScale;
            var lifeHeight = Size(life).y / appliedScale * controlScale;
            var headerHalf = Mathf.Max(backHeight, lifeHeight) * .5f;
            var headerTop = Mathf.Max(backHeight * .5f + backOffset.y * spacingScale, lifeHeight * .5f + lifeOffset.y * spacingScale);
            if (level && level.gameObject.activeSelf) headerTop = Mathf.Max(headerTop, headerHalf + levelOffset.y * spacingScale);
            var headerCenter = area.yMax - Mathf.Max(0, edgePadding.y) * spacingScale - Mathf.Max(headerHalf, headerTop);
            var headerBottom = Mathf.Min(headerCenter + backOffset.y * spacingScale - backHeight * .5f,
                headerCenter + lifeOffset.y * spacingScale - lifeHeight * .5f);
            if (level && level.gameObject.activeSelf) headerBottom = Mathf.Min(headerBottom, headerCenter + levelOffset.y * spacingScale - headerHalf);
            var gap = Mathf.Max(0, sectionGap) * spacingScale;
            var progressHeight = progressReferenceHeight * controlScale;
            var progressCenter = headerBottom - gap - progressHeight * .5f - Mathf.Max(0, progressOffset.y) * spacingScale + progressOffset.y * spacingScale;
            var hintHeight = Size(hint).y / appliedScale * controlScale;
            var zoomHeight = Size(zoom).y / appliedScale * controlScale;
            var handBottom = Mathf.Max(hintHeight * .5f, DifferenceHintHand.BottomExtent * Mathf.Abs(hint.localScale.y) / appliedScale * controlScale);
            var footerTop = Mathf.Max(hintHeight * .5f + Mathf.Max(0, hintOffset.y) * spacingScale, zoomHeight * .5f + Mathf.Max(0, zoomOffset.y) * spacingScale);
            var footerBottom = Mathf.Max(handBottom - hintOffset.y * spacingScale, zoomHeight * .5f - zoomOffset.y * spacingScale);
            var topShift = Mathf.Max(0, Mathf.Max(upperOffset.y, Mathf.Max(lowerOffset.y, frameOffset.y))) * spacingScale;
            var bottomShift = Mathf.Min(0, Mathf.Min(upperOffset.y, Mathf.Min(lowerOffset.y, frameOffset.y))) * spacingScale;
            var border = Mathf.Max(0, framePadding);
            var top = progressCenter - progressHeight * .5f - gap - border - topShift;
            var bottom = area.yMin + Mathf.Max(0, bottomAdHeight) + Mathf.Max(0, edgePadding.y) * spacingScale + footerTop + footerBottom + gap + border - bottomShift;
            return top - bottom - Mathf.Max(0, pictureGap) - Mathf.Max(0, lowerOffset.y - upperOffset.y) * spacingScale;
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
