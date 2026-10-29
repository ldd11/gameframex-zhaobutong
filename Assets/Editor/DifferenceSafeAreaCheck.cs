#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceSafeAreaCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Find Differences/Check Safe Area Only")]
    public static void Run()
    {
        const string reportPath = "Temp/FindDifferences-safe-area.txt";
        var rootObject = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = rootObject.GetComponent<UIDifferences>();
            var root = (RectTransform)rootObject.transform;
            var layout = ui.play.GetComponent<DifferencePlayLayout>();
            var background = ui.play.transform.Find("Background").GetComponent<Image>();
            var backgroundAspect = background.GetComponent<AspectRatioFitter>().aspectRatio;
            var fit = typeof(UIDifferences).GetMethod("FitScreenBackgrounds", Private);
            var fitSafe = typeof(UIDifferences).GetMethod("FitSafeArea", Private);
            Check(fit != null && fitSafe != null && background.sprite, "全屏背景、安全区适配方法和图片必须存在");
            typeof(UIDifferences).GetField("Stage_Play_Background", Private).SetValue(ui, background);
            ui.play.SetActive(true);
            var guide = DifferenceHintHand.Create(layout.hint, ui.hintHandSprite);
            guide.Show(true);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
            var nodes = new[] { ui.stage, layout.upper, layout.lower, layout.frame, layout.progress, layout.back, layout.life, layout.hint, layout.zoom, layout.level };
            var offsetFields = typeof(DifferencePlayLayout).GetFields().Where(field => field.Name.EndsWith("Offset", StringComparison.Ordinal)).ToArray();
            // Check automatic layout independently of authored manual offsets; the offset check covers those separately.
            foreach (var field in offsetFields) field.SetValue(layout, Vector2.zero);
            var offsets = offsetFields.Select(field => field.GetValue(layout)).ToArray();
            var backgroundParent = background.transform.parent;
            var backgroundOrder = background.transform.GetSiblingIndex();
            var backgroundPaths = new[]
            {
                "Play/Background", "FigmaHome/Artwork", "FigmaLoading/Background", "FigmaJourney/Background", "Home/Beach",
                "Shop/Background", "Ranking/Background", "Settings/Background", "FigmaVictory/Bg",
                "Profile/Scrim", "MusicPanel/Scrim", "Achievements/Scrim", "Album/Scrim", "Modal/Scrim",
                "FigmaSettings/Scrim", "FigmaFail/Scrim", "FigmaHint/Scrim", "FigmaPause/Scrim", "FigmaLeave/Scrim"
            };
            var backgrounds = backgroundPaths.Select(path => new BackgroundState(ui, path)).ToArray();
            var safePages = new SafePagesState(ui.stage);
            var protectedRects = ui.stage.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == "Card" || rect.name == "Toast").ToArray();
            Check(protectedRects.Any(rect => rect.name == "Card") && protectedRects.Any(rect => rect.name == "Toast"), "必须检查弹窗和 Toast 尺寸");
            var report = new StringBuilder();
            var screens = new[] { new Vector2(1080, 1920), new Vector2(1080, 2340), new Vector2(1179, 2556), new Vector2(2340, 1080) };
            var areas = new[] { new Rect(0, 0, 1080, 1920), new Rect(0, 0, 1080, 2261), new Rect(0, 102, 1179, 2277), new Rect(79, 0, 2182, 1080) };
            var widthChanged = false;
            for (var variant = 0; variant < screens.Length; variant++)
            {
                var screen = screens[variant];
                var area = areas[variant];
                root.sizeDelta = new Vector2(screen.x * 2556 / screen.y, 2556);
                root.ForceUpdateRectTransforms();
                var normalizedArea = new Rect(area.x / screen.x, area.y / screen.y, area.width / screen.x, area.height / screen.y);
                fitSafe.Invoke(ui, new object[] { normalizedArea });
                CheckStage(root, ui.stage);
                var safe = new Rect(root.rect.xMin + area.x / screen.x * root.rect.width,
                    root.rect.yMin + area.y / screen.y * root.rect.height,
                    area.width / screen.x * root.rect.width, area.height / screen.y * root.rect.height);
                safePages.Check(root, safe, normalizedArea);
                var toastPosition = safePages.Toast.anchoredPosition;
                safePages.Toast.anchoredPosition += new Vector2(17, -63);
                var animatedPosition = safePages.Toast.anchoredPosition;
                fitSafe.Invoke(ui, new object[] { normalizedArea });
                Check(safePages.Toast.anchoredPosition == animatedPosition, "安全区适配不能覆盖 Toast 正在使用的动画位移");
                safePages.Toast.anchoredPosition = toastPosition;
                var play = (RectTransform)layout.transform;
                var localSafe = Bounds(play, new[] { new Vector3(safe.xMin, safe.yMin, 0), new Vector3(safe.xMin, safe.yMax, 0),
                    new Vector3(safe.xMax, safe.yMax, 0), new Vector3(safe.xMax, safe.yMin, 0) }.Select(root.TransformPoint).ToArray());
                Rect[] headers = null;
                var firstWidth = 0f;
                foreach (var adHeight in new[] { 0f, 120f, 240f })
                {
                    layout.bottomAdHeight = adHeight;
                    layout.ApplyLayout(localSafe);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layout.progress);
                    CheckLayout(layout, localSafe, guide);
                    var currentHeaders = new[] { layout.back, layout.life, layout.level }.Select(rect => Bounds(play, Corners(rect))).ToArray();
                    var pictureWidth = Bounds(play, Corners(layout.upper)).width;
                    if (headers == null) { headers = currentHeaders; firstWidth = pictureWidth; }
                    else
                    {
                        Check(currentHeaders.SequenceEqual(headers), "仅图片可用高度改变时顶部三个控件跟随图片横移");
                        widthChanged |= Mathf.Abs(pictureWidth - firstWidth) > .1f;
                    }
                    var saved = nodes.Select(Snapshot).ToArray();
                    var protectedSizes = protectedRects.Select(rect => rect.rect.size).ToArray();
                    for (var pass = 0; pass < 3; pass++)
                    {
                        fitSafe.Invoke(ui, new object[] { normalizedArea });
                        CheckStage(root, ui.stage);
                        safePages.Check(root, safe, normalizedArea);
                        fit.Invoke(ui, new object[] { root });
                        CheckBackground(root, background, backgroundAspect);
                        foreach (var state in backgrounds) state.Check(root, ui.stage);
                        for (var i = 0; i < protectedRects.Length; i++)
                            Check(protectedRects[i].rect.size == protectedSizes[i], protectedRects[i].name + " 尺寸被背景适配改变");
                        for (var i = 0; i < nodes.Length; i++)
                            Check(Snapshot(nodes[i]) == saved[i], nodes[i].name + " 重复背景或内容适配后漂移");
                        for (var i = 0; i < offsetFields.Length; i++)
                            Check(Equals(offsetFields[i].GetValue(layout), offsets[i]), offsetFields[i].Name + " 被改变");
                        Check(background.transform.parent == backgroundParent && background.transform.GetSiblingIndex() == backgroundOrder && !background.raycastTarget,
                            "背景必须保持原层级且不拦截点击");
                        foreach (var node in nodes.Skip(1))
                            foreach (var corner in Corners(node))
                                Check(Contains(safe, root.InverseTransformPoint(corner)), node.name + " 超出安全区域");
                        foreach (var board in new[] { ui.upperImage.rectTransform, ui.lowerImage.rectTransform }) CheckCoordinates(board);
                        layout.ApplyLayout(localSafe);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(layout.progress);
                        CheckLayout(layout, localSafe, guide);
                    }
                    report.AppendLine($"PASS {screen.x}x{screen.y} safe={area} ad={adHeight}: Stage scale 1/full root; safe pages with authored 720px sizes and scales; Nav safe bottom/Toast animation preserved; centeredStack/maxHeader HUD/content/hand; 19 backgrounds; normalized masked clicks.");
                }
                foreach (var state in backgrounds.Where(state => state.Sliding))
                {
                    var page = (RectTransform)state.Parent;
                    var position = page.anchoredPosition;
                    var localCenter = state.LocalCenter;
                    var fitted = Snapshot(state.Image.rectTransform);
                    try
                    {
                        foreach (var shift in new[] { -ui.stage.rect.width, ui.stage.rect.width * .5f, ui.stage.rect.width })
                        {
                            page.anchoredPosition = position + Vector2.right * shift;
                            var slideX = page.anchoredPosition.x;
                            fitSafe.Invoke(ui, new object[] { normalizedArea });
                            Check(page.anchoredPosition.x == slideX && page.anchoredPosition.y == 0,
                                page.name + " 安全区适配不能覆盖纯 X 滑页偏移");
                            CheckStage(root, ui.stage);
                            safePages.Check(root, safe, normalizedArea);
                            fit.Invoke(ui, new object[] { root });
                            state.Check(root, ui.stage);
                            Check(Vector3.Distance(state.LocalCenter, localCenter) < .01f, state.Image.name + " 随父页滑动时本地中心漂移");
                        }
                    }
                    finally { page.anchoredPosition = position; }
                    fit.Invoke(ui, new object[] { root });
                    state.Check(root, ui.stage);
                    Check(Snapshot(state.Image.rectTransform) == fitted, state.Image.name + " 滑页复位后背景不幂等");
                    fit.Invoke(ui, new object[] { root });
                    Check(Snapshot(state.Image.rectTransform) == fitted, state.Image.name + " 重复适配后背景漂移");
                }
            }
            Check(widthChanged, "矩阵必须覆盖广告预留导致图片变窄、顶部控件保持原位的场景");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, report + "Prefab and player settings unchanged.");
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(reportPath, "FAIL " + exception);
            Debug.LogException(exception);
        }
        finally { PrefabUtility.UnloadPrefabContents(rootObject); }
    }

    static void CheckStage(RectTransform root, RectTransform stage)
    {
        Check(stage.localScale == Vector3.one && stage.localRotation == Quaternion.identity &&
            stage.anchorMin == Vector2.zero && stage.anchorMax == Vector2.one &&
            stage.offsetMin == Vector2.zero && stage.offsetMax == Vector2.zero && stage.anchoredPosition == Vector2.zero,
            "Stage 必须保持 scale 1、满屏锚点和零位移，不能随安全区整体缩放");
        var rendered = Bounds(root, Corners(stage));
        Check(Mathf.Abs(rendered.xMin - root.rect.xMin) < .1f && Mathf.Abs(rendered.xMax - root.rect.xMax) < .1f &&
            Mathf.Abs(rendered.yMin - root.rect.yMin) < .1f && Mathf.Abs(rendered.yMax - root.rect.yMax) < .1f,
            "Stage 必须覆盖完整 root，而不是缩到安全区内");
    }

    static void CheckBackground(RectTransform root, Image image, float expectedAspect, Vector3 offset = default)
    {
        using (var mesh = new VertexHelper())
        {
            typeof(Image).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(image, new object[] { mesh });
            Check(mesh.currentVertCount >= 4, "背景没有生成可见图形");
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var vertex = new UIVertex();
            for (var i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position); max = Vector2.Max(max, vertex.position);
            }
            var rendered = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            foreach (var corner in Corners(root))
                Check(Contains(rendered, image.rectTransform.InverseTransformPoint(corner + offset)), image.name + " 背景实际图形没有覆盖对应全屏角落");
            var aspect = rendered.width * Mathf.Abs(image.transform.lossyScale.x) / (rendered.height * Mathf.Abs(image.transform.lossyScale.y));
            if (expectedAspect > 0) Check(Mathf.Abs(aspect - expectedAspect) < .001f, image.name + " 背景原有比例被改变");
            else
            {
                var expected = Corners(root).Select(corner => image.rectTransform.InverseTransformPoint(corner + offset)).ToArray();
                Check(Mathf.Abs(rendered.xMin - expected.Min(point => point.x)) < .1f &&
                    Mathf.Abs(rendered.xMax - expected.Max(point => point.x)) < .1f &&
                    Mathf.Abs(rendered.yMin - expected.Min(point => point.y)) < .1f &&
                    Mathf.Abs(rendered.yMax - expected.Max(point => point.y)) < .1f, image.name + " 原有 Stretch 背景或纯色遮罩必须恰好铺满屏幕");
            }
        }
    }

    static void CheckCoordinates(RectTransform board)
    {
        var mask = board.parent.GetComponent<RectMask2D>();
        Check(mask && mask.isActiveAndEnabled, "图片窗口必须保留活动的 RectMask2D");
        var viewport = (RectTransform)board.parent;
        var inside = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center));
        var outside = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(new Vector2(viewport.rect.xMin - viewport.rect.width, viewport.rect.center.y)));
        Check(mask.IsRaycastLocationValid(inside, null) && !mask.IsRaycastLocationValid(outside, null), "图片窗口点击裁切改变");
        var scale = board.localScale;
        try
        {
            foreach (var zoom in new[] { 1f, 2f })
            {
                board.localScale = new Vector3(scale.x * zoom, scale.y * zoom, scale.z);
                foreach (var expected in new[] { new Vector2(.23f, .37f), new Vector2(.5f, .5f), new Vector2(.81f, .72f) })
                {
                    var local = new Vector2(board.rect.xMin + expected.x * board.rect.width, board.rect.yMax - expected.y * board.rect.height);
                    var screen = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(local));
                    Check(RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, null, out var point), "点击坐标转换失败");
                    var normalized = new Vector2((point.x - board.rect.xMin) / board.rect.width, (board.rect.yMax - point.y) / board.rect.height);
                    Check(Vector2.Distance(normalized, expected) < .001f, "背景适配改变了图片归一化坐标");
                    var round = new DifferenceRound(new[] { new DifferenceSpot(expected.x, expected.y, .02f) }, UIDifferences.ImageAspect);
                    Check(round.Click(normalized.x, normalized.y) == 0, "归一化点击未命中独立测试差异");
                }
            }
        }
        finally { board.localScale = scale; }
    }

    static void CheckLayout(DifferencePlayLayout layout, Rect safe, DifferenceHintHand guide)
    {
        var play = (RectTransform)layout.transform;
        var controls = new[] { layout.back, layout.life, layout.level };
        var bounds = controls.Select(control => Bounds(play, Corners(control))).ToArray();
        Check(bounds.All(rect => Mathf.Abs(rect.center.y - bounds[0].center.y) < .1f), "顶部暂停、生命值和关卡标题没有对齐基线");
        var topGap = safe.yMax - bounds.Max(rect => rect.yMax);
        Check(Mathf.Abs(topGap - layout.edgePadding.y) < .1f, "顶部存在留白与单独偏移叠加");
        var headerWidth = Mathf.Min(safe.width - Mathf.Max(0, layout.edgePadding.x) * 2, Mathf.Max(1, layout.headerMaxWidth));
        Check(Mathf.Abs(bounds[0].xMin - (safe.center.x - headerWidth * .5f)) < .1f &&
            Mathf.Abs(bounds[1].xMax - (safe.center.x + headerWidth * .5f)) < .1f &&
            Mathf.Abs(bounds[2].center.x - safe.center.x) < .1f, "顶部控件必须在安全区内按限宽范围居中定位");

        var gap = Mathf.Max(0, layout.sectionGap);
        var border = Mathf.Max(0, layout.framePadding);
        var middle = Mathf.Max(0, layout.pictureGap);
        var progress = Bounds(play, Corners(layout.progress));
        var upper = Bounds(play, Corners(layout.upper));
        var lower = Bounds(play, Corners(layout.lower));
        var frame = Bounds(play, Corners(layout.frame));
        var hint = Bounds(play, Corners(layout.hint));
        var zoom = Bounds(play, Corners(layout.zoom));
        Check(bounds.Min(rect => rect.yMin) - progress.yMax >= gap - .1f, "顶部控件与进度条间距不足");
        Check(progress.yMin - frame.yMax >= gap - .1f, "进度条与图片边框重叠");
        Check(Mathf.Abs(upper.width / upper.height - UIDifferences.ImageAspect) < .001f &&
            Mathf.Abs(lower.width / lower.height - UIDifferences.ImageAspect) < .001f &&
            Mathf.Abs(upper.width - lower.width) < .1f && Mathf.Abs(upper.center.x - lower.center.x) < .1f,
            "上下图片必须保持同宽、同轴和 3:2 比例");
        Check(upper.yMin - lower.yMax >= middle - .1f, "上下图片互相重叠或图片间距不足");
        Check(Mathf.Abs(hint.center.y - zoom.center.y) < .1f &&
            frame.yMin - Mathf.Max(hint.yMax, zoom.yMax) >= gap - .1f, "Hint 与 Zoom 必须同一基线且位于图片边框下方");
        var bottom = safe.yMin + Mathf.Max(0, layout.bottomAdHeight);
        foreach (var rect in new[] { progress, upper, lower, frame, hint, zoom })
            Check(rect.yMin >= bottom - .1f, "内容或按钮进入底部广告预留区域");
        var footerHeight = Mathf.Max(hint.height, zoom.height);
        var footerBottom = Mathf.Max(footerHeight * .5f, DifferenceHintHand.BottomExtent * Mathf.Abs(layout.hint.localScale.y));
        Check(hint.center.y - footerBottom >= bottom + layout.edgePadding.y - .1f, "按钮或手势完整运动范围超出安全区底部和广告留白");
        var topSlack = bounds.Min(rect => rect.yMin) - gap - progress.yMax;
        var bottomSlack = hint.center.y - footerBottom - bottom - layout.edgePadding.y;
        Check(Mathf.Abs(topSlack - bottomSlack) < .2f,
            $"进度、双图和底部按钮组未在顶部控件与广告安全底线之间垂直居中：topSlack={topSlack:F2}, bottomSlack={bottomSlack:F2}");
        var hand = (RectTransform)guide.transform.Find("Hand");
        foreach (var sample in new[] { 0f, .18f, .42f, .56f, .82f, 1.4f })
        {
            typeof(DifferenceHintHand).GetField("elapsed", Private).SetValue(guide, 0f);
            typeof(DifferenceHintHand).GetMethod("Advance", Private).Invoke(guide, new object[] { sample });
            Check(Bounds(play, Corners(hand)).yMin >= bottom - .1f, "提示手动画越过安全区底部或广告预留区域");
        }

        var availableWidth = safe.width - Mathf.Max(0, layout.edgePadding.x) * 2 - border * 2;
        var availableHeight = safe.height - Mathf.Max(0, layout.edgePadding.y) * 2 - Mathf.Max(0, layout.bottomAdHeight) -
            bounds.Max(rect => rect.height) - progress.height - gap * 3 - border * 2 - middle - footerHeight * .5f - footerBottom;
        var expectedWidth = Mathf.Min(availableWidth, availableHeight * UIDifferences.ImageAspect * .5f);
        Check(Mathf.Abs(upper.width - expectedWidth) < .2f, "图片宽度没有同时受可用宽度和高度限制");
        Check(Mathf.Abs(frame.center.x - safe.center.x) < .1f, "图片组未居中于安全区");
        if (expectedWidth < availableWidth - .1f)
            Check(frame.xMin > safe.xMin + layout.edgePadding.x && frame.xMax < safe.xMax - layout.edgePadding.x,
                "高度受限的宽屏图片两侧必须保留留白");
    }

    sealed class SafePagesState
    {
        readonly RectTransform stage, play, navigation;
        readonly RectTransform[] stretch, fixedPages, critical;
        readonly Vector3[] scales;
        readonly Vector2[] fixedSizes, fixedPivots;
        readonly Vector2 navigationSize, navigationPivot, toastSize, toastPosition;
        public readonly RectTransform Toast;

        public SafePagesState(RectTransform stage)
        {
            this.stage = stage;
            RectTransform At(string path)
            {
                var rect = stage.Find(path) as RectTransform;
                DifferenceSafeAreaCheck.Check(rect, "缺少安全区测试页面：" + path);
                return rect;
            }
            stretch = new[] { "Home", "Profile", "FigmaHome", "FigmaLoading", "FigmaJourney", "FigmaSettings",
                "FigmaFail", "FigmaHint", "FigmaVictory", "FigmaPause", "FigmaLeave" }.Select(At).ToArray();
            fixedPages = new[] { "Shop", "Ranking", "Settings", "Achievements", "Album", "Modal", "MusicPanel" }.Select(At).ToArray();
            play = At("Play"); navigation = At("Navigation"); Toast = At("Toast");
            fixedSizes = fixedPages.Select(page => page.rect.size).ToArray();
            fixedPivots = fixedPages.Select(page => page.pivot).ToArray();
            scales = stretch.Concat(fixedPages).Concat(new[] { play, navigation, Toast }).Select(page => page.localScale).ToArray();
            navigationSize = navigation.rect.size; navigationPivot = navigation.pivot;
            toastSize = Toast.sizeDelta; toastPosition = Toast.anchoredPosition;
            critical = stretch.Where(page => page.name != "Home").SelectMany(page => page.GetComponentsInChildren<RectTransform>(true))
                .Where(rect => rect.name == "Card" || rect.name == "Close").Concat(new[] {
                    At("FigmaHome/Start"), At("FigmaLoading/Track"), At("FigmaVictory/Next"), At("FigmaJourney/Scroll") }).ToArray();
            DifferenceSafeAreaCheck.Check(critical.Any(rect => rect.name == "Card") && critical.Any(rect => rect.name == "Close"),
                "必须检查 Figma 弹窗和关闭按钮");
        }

        public void Check(RectTransform root, Rect safe, Rect normalized)
        {
            var pages = stretch.Concat(fixedPages).Concat(new[] { play, navigation, Toast }).ToArray();
            for (var i = 0; i < pages.Length; i++)
                DifferenceSafeAreaCheck.Check(pages[i].parent == stage && pages[i].localScale == scales[i],
                    pages[i].name + " 页面必须留在 Stage 并保留原设计缩放");
            foreach (var page in stretch)
                DifferenceSafeAreaCheck.Check(page.anchorMin == normalized.min && page.anchorMax == normalized.max &&
                    page.pivot == Vector2.one * .5f && page.sizeDelta == Vector2.zero && page.anchoredPosition.y == 0 &&
                    (page.name == "Home" || page.anchoredPosition.x == 0), page.name + " 未按实际安全区拉伸布局");
            for (var i = 0; i < fixedPages.Length; i++)
            {
                var page = fixedPages[i];
                var bounds = Bounds(root, Corners(page));
                var slide = root.InverseTransformVector(stage.TransformVector(new Vector3(page.anchoredPosition.x, 0, 0)));
                DifferenceSafeAreaCheck.Check(page.anchorMin == page.anchorMax && page.pivot == fixedPivots[i] &&
                    page.rect.size == fixedSizes[i] && page.anchoredPosition.y == 0 &&
                    Vector2.Distance(bounds.center - (Vector2)slide, safe.center) < .1f &&
                    (page.name == "Shop" || page.name == "Ranking" || page.anchoredPosition.x == 0),
                    page.name + " 必须保留原 pivot 和固定设计尺寸，实际边界居中到安全区并保留滑页位移");
            }
            DifferenceSafeAreaCheck.Check(play.anchorMin == Vector2.zero && play.anchorMax == Vector2.one &&
                play.sizeDelta == Vector2.zero && play.anchoredPosition == Vector2.zero, "Play 必须保持全屏且独立使用安全区内容布局");
            var nav = Bounds(root, Corners(navigation));
            DifferenceSafeAreaCheck.Check(navigation.rect.size == navigationSize && navigation.pivot == navigationPivot &&
                navigation.anchorMax == navigation.anchorMin &&
                navigation.anchoredPosition == Vector2.zero && Mathf.Abs(nav.yMin - safe.yMin) < .1f && Mathf.Abs(nav.center.x - safe.center.x) < .1f,
                "Navigation 必须保留设计尺寸并贴安全区底部居中");
            DifferenceSafeAreaCheck.Check(Toast.anchorMin == new Vector2(normalized.center.x, normalized.yMax) && Toast.anchorMax == Toast.anchorMin &&
                Toast.sizeDelta == toastSize && Toast.anchoredPosition == toastPosition, "Toast 只改安全区锚点，不能改变原尺寸或动画偏移");
            foreach (var rect in critical)
                foreach (var corner in Corners(rect))
                    DifferenceSafeAreaCheck.Check(Contains(safe, root.InverseTransformPoint(corner)), rect.parent.name + "/" + rect.name + " 超出安全区域");
        }
    }

    sealed class BackgroundState
    {
        public readonly Image Image;
        public readonly Transform Parent;
        public readonly bool Sliding;
        readonly Sprite sprite;
        readonly Vector2 pivot;
        readonly Color color;
        readonly int sibling;
        readonly bool raycast;
        readonly float aspect;
        public Vector3 LocalCenter => Parent.InverseTransformPoint(Image.rectTransform.TransformPoint(Image.rectTransform.rect.center));

        public BackgroundState(UIDifferences ui, string path)
        {
            Image = ui.stage.Find(path)?.GetComponent<Image>();
            DifferenceSafeAreaCheck.Check(Image, "缺少全屏背景或遮罩：" + path);
            Parent = Image.transform.parent; sibling = Image.transform.GetSiblingIndex();
            sprite = Image.sprite; pivot = Image.rectTransform.pivot; color = Image.color; raycast = Image.raycastTarget;
            var fitter = Image.GetComponent<AspectRatioFitter>();
            aspect = sprite && fitter ? fitter.aspectRatio : 0;
            Sliding = path == "Home/Beach" || path == "Shop/Background" || path == "Ranking/Background";
        }

        public void Check(RectTransform root, RectTransform stage)
        {
            DifferenceSafeAreaCheck.Check(Image.sprite == sprite && Image.rectTransform.pivot == pivot && Image.color == color &&
                Image.raycastTarget == raycast && Image.transform.parent == Parent && Image.transform.GetSiblingIndex() == sibling,
                Image.name + " 的原图、轴心、颜色透明度、点击或层级被改变");
            var offset = Sliding ? stage.TransformVector(new Vector3(((RectTransform)Parent).anchoredPosition.x, 0, 0)) : Vector3.zero;
            CheckBackground(root, Image, aspect, offset);
        }
    }

    static Vector3[] Corners(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners;
    }

    static Rect Bounds(RectTransform parent, Vector3[] worldCorners)
    {
        var corners = worldCorners.Select(parent.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
            corners.Max(point => point.x), corners.Max(point => point.y));
    }

    static string Snapshot(RectTransform rect) => string.Join("|", rect.anchorMin.ToString("R"), rect.anchorMax.ToString("R"), rect.pivot.ToString("R"),
        rect.anchoredPosition.ToString("R"), rect.sizeDelta.ToString("R"), rect.localScale.ToString("R"));

    static bool Contains(Rect rect, Vector2 point) => point.x >= rect.xMin - .1f && point.x <= rect.xMax + .1f && point.y >= rect.yMin - .1f && point.y <= rect.yMax + .1f;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
