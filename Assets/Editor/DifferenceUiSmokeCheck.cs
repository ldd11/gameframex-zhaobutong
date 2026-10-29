#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Create Temp/FindDifferences-ui-smoke.request to run from the open Editor.
[InitializeOnLoad]
public static class DifferenceUiSmokeCheck
{
    const string Pending = "FindDifferences.UiSmoke.Pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-ui-smoke"));
    static readonly string Request = Folder + ".request";
    static readonly string[] IntKeys = { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "TutorialStep" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration", "TutorialComplete" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate", "RoundContent" };
    static bool running;
    static bool launcherCaptured;
    static double deadline;

    static DifferenceUiSmokeCheck() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Find Differences/Connect Home Entrance")]
    public static void ConnectHomeEntrance()
    {
        const string path = "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab";
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var editing = stage != null && stage.assetPath == path;
        var root = editing ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            var home = root.transform.Find("Stage/FigmaHome");
            if (!home.GetComponent<DifferenceHomeMotion>()) home.gameObject.AddComponent<DifferenceHomeMotion>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (!editing) PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Home Entrance Only")]
    public static void CheckHomeEntranceOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            // Check the generated bindings too: a removed Image node must not block startup.
            typeof(UIDifferences).GetMethod("InitView", Private).Invoke(root.GetComponent<UIDifferences>(), null);
            var home = root.transform.Find("Stage/FigmaHome");
            var motion = home.GetComponent<DifferenceHomeMotion>();
            if (!motion) throw new Exception("Home motion must be saved on the prefab for Inspector tuning.");
            var logo = home.Find("Logo/Spine").GetComponent<Spine.Unity.SkeletonGraphic>();
            var start = home.Find("Start");
            void Call(string method) => typeof(DifferenceHomeMotion).GetMethod(method, Private).Invoke(motion, null);
            void Advance(float dt, bool ready) => typeof(DifferenceHomeMotion).GetMethod("Advance", Private).Invoke(motion, new object[] { dt, ready });
            for (var visit = 0; visit < 2; visit++)
            {
                Call("OnEnable");
                Advance(5, false);
                if (start.localScale != Vector3.zero || !logo.freeze || logo.AnimationState.GetCurrent(0).TrackTime != 0)
                    throw new Exception("Entrance played behind the startup launcher.");
                Advance(0, true);
                if (logo.freeze) throw new Exception("Spine did not resume when home became visible.");
                Advance(.18f, true);
                if (Mathf.Abs(start.localScale.x - 1.2f) > .001f)
                    throw new Exception("Start did not reach the configured overshoot.");
                logo.Update(.5f);
                Advance(.32f, true);
                if (Vector3.Distance(start.localScale, Vector3.one) > .001f ||
                    Mathf.Abs(logo.Skeleton.FindBone("bone16").ScaleX - 1) > .001f)
                    throw new Exception("Start and Spine did not finish their entrance at scale one.");
                Call("OnDisable");
            }
            File.WriteAllText("Temp/FindDifferences-home-entrance.txt", "PASS: generated bindings, startup hold, 0 -> 1.2 -> 1 bounce, synchronized finish and repeat entrance.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/FindDifferences-home-entrance.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Victory Votes Only")]
    public static void CheckVictoryVotesOnly()
    {
        const string report = "Temp/FindDifferences-victory-votes.txt";
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            typeof(UIDifferences).GetMethod("BindFigmaDesign", Private).Invoke(ui, null);
            var like = (Button)Field(ui, "designLikeButton");
            var unlike = (Button)Field(ui, "designUnlikeButton");
            var defaultLike = like.image.sprite; var defaultUnlike = unlike.image.sprite;
            var selectedLike = (Sprite)Field(ui, "likeSelectedSprite");
            var selectedUnlike = (Sprite)Field(ui, "unlikeSelectedSprite");
            if (!selectedLike || !selectedUnlike) throw new Exception("Missing selected vote sprites.");
            typeof(UIDifferences).GetField("figmaResultActive", Private).SetValue(ui, true);
            typeof(UIDifferences).GetField("figmaResultWon", Private).SetValue(ui, true);
            foreach (var positive in new[] { true, true, false, false, true })
            {
                (positive ? like : unlike).onClick.Invoke();
                if (like.image.sprite != (positive ? selectedLike : defaultLike) ||
                    unlike.image.sprite != (positive ? defaultUnlike : selectedUnlike))
                    throw new Exception("Vote buttons are not mutually exclusive or repeated clicks deselect.");
            }
            typeof(UIDifferences).GetField("adInputLocked", Private).SetValue(ui, true);
            unlike.onClick.Invoke();
            if (like.image.sprite != selectedLike || unlike.image.sprite != defaultUnlike)
                throw new Exception("Vote changed while advertising blocked input.");
            typeof(UIDifferences).GetMethod("SetVictoryVote", Private).Invoke(ui, new object[] { 0 });
            if (like.image.sprite != defaultLike || unlike.image.sprite != defaultUnlike)
                throw new Exception("New result did not reset both vote sprites.");
            File.WriteAllText(report, "PASS: prefab sprite references, like/unlike switching, repeated clicks, ad input lock and reset.");
            Debug.Log(File.ReadAllText(report));
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Responsive Play Layout Only")]
    public static void CheckResponsivePlayLayoutOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var layout = ui.play.GetComponent<DifferencePlayLayout>();
            if (!layout) throw new Exception("游戏页没有绑定适配组件");
            ui.play.SetActive(true);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1179, 2556);
            rect.ForceUpdateRectTransforms();
            var items = new[] { layout.upper, layout.lower, layout.frame, layout.progress, layout.back, layout.life, layout.hint, layout.zoom, layout.level };
            var anchors = items.Select(r => new Vector4(r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y)).ToArray();
            foreach (var field in typeof(DifferencePlayLayout).GetFields().Where(field => field.Name.EndsWith("Offset", StringComparison.Ordinal)))
                field.SetValue(layout, Vector2.zero);
            var controls = new[] { layout.back, layout.life, layout.hint, layout.zoom };
            var controlSizes = controls.Select(control => control.sizeDelta).ToArray();
            var controlScales = controls.Select(control => control.localScale).ToArray();
            var titleFontSize = layout.level.GetComponent<Text>().fontSize;
            var dotSizes = ui.progressDots.Select(dot => dot.rectTransform.sizeDelta).ToArray();
            var row = layout.progress.GetComponent<HorizontalLayoutGroup>();
            var dotSpacing = row.spacing;
            var guide = DifferenceHintHand.Create(ui.hintButton, ui.hintHandSprite);
            guide.Show(true);
            var hand = (RectTransform)guide.transform.Find("Hand");
            var advanceHand = typeof(DifferenceHintHand).GetMethod("Advance", Private);
            var report = new System.Text.StringBuilder();
            Rect Bounds(RectTransform item)
            {
                var min = (Vector2)ui.play.transform.InverseTransformPoint(item.TransformPoint(item.rect.min));
                var max = (Vector2)ui.play.transform.InverseTransformPoint(item.TransformPoint(item.rect.max));
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            if (Application.isPlaying) throw new Exception("请先退出 Play 模式，再检查无参数的编辑模式布局");
            var nativeArea = ((RectTransform)ui.play.transform).rect;
            layout.ApplyLayout();
            var nativePositions = items.Select(item => item.localPosition).ToArray();
            var nativeSizes = items.Select(item => item.rect.size).ToArray();
            layout.ApplyLayout(nativeArea);
            for (var i = 0; i < items.Length; i++)
                if (Vector3.Distance(items[i].localPosition, nativePositions[i]) > .01f || Vector2.Distance(items[i].rect.size, nativeSizes[i]) > .01f)
                    throw new Exception("编辑模式无参数布局与显式 Play.rect 布局不同");
            var nativeBack = Bounds(layout.back); var nativeLife = Bounds(layout.life); var nativeLevel = Bounds(layout.level);
            var nativeHeaderWidth = Mathf.Min(nativeArea.width - Mathf.Max(0, layout.edgePadding.x) * 2, Mathf.Max(1, layout.headerMaxWidth));
            if (Mathf.Abs(nativeBack.xMin - (nativeArea.center.x - nativeHeaderWidth * .5f)) > .1f ||
                Mathf.Abs(nativeLife.xMax - (nativeArea.center.x + nativeHeaderWidth * .5f)) > .1f ||
                Mathf.Abs(nativeBack.center.y - nativeLife.center.y) > .1f || Mathf.Abs(nativeBack.center.y - nativeLevel.center.y) > .1f ||
                Mathf.Abs(nativeArea.yMax - Mathf.Max(nativeBack.yMax, Mathf.Max(nativeLife.yMax, nativeLevel.yMax)) - layout.edgePadding.y) > .1f)
                throw new Exception("编辑模式无参数布局没有遵守 Play.rect 的安全边距、Header 最大宽度与顶部基线");
            report.AppendLine("PASS EditMode ApplyLayout() equals ApplyLayout(Play.rect); centered header respects safe margins, maximum width and a shared baseline.");
            var heightLimited = 0;
            foreach (var adHeight in new[] { 0f, 120f, 240f })
            foreach (var screen in new[] { new Vector2(1179,2556), new Vector2(1080,1920), new Vector2(1080,2400),
                         new Vector2(1536,2048), new Vector2(2048,1536), new Vector2(720,1280), new Vector2(1440,2960) })
            foreach (var inset in new[] { false, true })
            {
                layout.bottomAdHeight = adHeight;
                var page = ((RectTransform)ui.play.transform).rect;
                var size = new Vector2(page.height * screen.x / screen.y, page.height);
                var area = new Rect(page.center - size * .5f, size);
                if (inset) area = Rect.MinMaxRect(area.xMin + 35, area.yMin + 55, area.xMax - 65, area.yMax - 110);
                for (var i = 0; i < ui.progressDots.Length; i++) ui.progressDots[i].rectTransform.sizeDelta = dotSizes[i];
                row.spacing = dotSpacing;
                layout.ApplyLayout(area);
                foreach (var item in items) item.ForceUpdateRectTransforms();
                LayoutRebuilder.ForceRebuildLayoutImmediate(layout.progress);
                var a = Bounds(layout.upper); var b = Bounds(layout.lower); var f = Bounds(layout.frame);
                var p = Bounds(layout.progress); var back = Bounds(layout.back); var life = Bounds(layout.life);
                var level = Bounds(layout.level);
                var hint = Bounds(layout.hint); var zoom = Bounds(layout.zoom);
                var gap = Mathf.Max(0, layout.sectionGap);
                var padding = Vector2.Max(Vector2.zero, layout.edgePadding);
                var border = Mathf.Max(0, layout.framePadding);
                foreach (var item in items)
                {
                    var bounds = Bounds(item);
                    if (bounds.xMin < area.xMin - .1f || bounds.xMax > area.xMax + .1f ||
                        bounds.yMin < area.yMin + adHeight - .1f || bounds.yMax > area.yMax + .1f)
                        throw new Exception(screen + ": " + item.name + " 越界");
                }
                for (var step = 0; step < 30; step++)
                {
                    advanceHand.Invoke(guide, new object[] { .05f });
                    if (Bounds(hand).yMin < area.yMin + adHeight - .1f)
                        throw new Exception(screen + ": 提示手势覆盖广告位");
                }
                if (Mathf.Abs(a.width / a.height - UIDifferences.ImageAspect) > .001f ||
                    Mathf.Abs(b.width / b.height - UIDifferences.ImageAspect) > .001f || Mathf.Abs(a.width - b.width) > .1f ||
                    Mathf.Abs(a.xMin - b.xMin) > .1f || Mathf.Abs(a.yMin - b.yMax - Mathf.Max(0, layout.pictureGap)) > .1f ||
                    Mathf.Abs(f.xMin - (a.xMin - border)) > .1f || Mathf.Abs(f.xMax - (a.xMax + border)) > .1f ||
                    Mathf.Abs(f.yMax - (a.yMax + border)) > .1f || Mathf.Abs(f.yMin - (b.yMin - border)) > .1f)
                    throw new Exception(screen + ": 图片变形或操作区重叠");
                var headerBottom = Mathf.Min(back.yMin, Mathf.Min(life.yMin, level.yMin));
                if (headerBottom - p.yMax < gap - .1f || Mathf.Abs(p.yMin - f.yMax - gap) > .1f ||
                    Mathf.Abs(f.yMin - Mathf.Max(hint.yMax, zoom.yMax) - gap) > .1f || Mathf.Abs(hint.center.y - zoom.center.y) > .1f)
                    throw new Exception(screen + ": Header、Progress、Frame、Hint/Zoom 没有按顺序留出间距并对齐底部按钮");
                var headerWidth = Mathf.Min(area.width - padding.x * 2, Mathf.Max(1, layout.headerMaxWidth));
                if (Mathf.Abs(back.xMin - (area.center.x - headerWidth * .5f)) > .1f ||
                    Mathf.Abs(life.xMax - (area.center.x + headerWidth * .5f)) > .1f ||
                    Mathf.Abs(level.center.x - area.center.x) > .1f || Mathf.Abs(back.center.y - life.center.y) > .1f ||
                    Mathf.Abs(back.center.y - level.center.y) > .1f)
                    throw new Exception(screen + ": Header 没有独立遵守安全区域边距和最大宽度");
                var titleReserve = Mathf.Max(back.width + layout.backOffset.x - layout.levelOffset.x,
                    life.width - layout.lifeOffset.x + layout.levelOffset.x);
                var titleWidth = Mathf.Max(1, headerWidth - 2 * (titleReserve + gap));
                if (Mathf.Abs(level.width - titleWidth) > .1f || Mathf.Abs(level.height - Mathf.Max(back.height, life.height)) > .1f)
                    throw new Exception(screen + ": Level 标题没有使用 Header 剩余宽度和按钮行高");
                CheckSingleLineLevelTitle(layout.level, titleFontSize);
                var fullWidth = area.width - padding.x * 2 - border * 2;
                var footerReserve = Mathf.Max(Mathf.Max(hint.height, zoom.height) * .5f,
                    DifferenceHintHand.BottomExtent * Mathf.Abs(layout.hint.localScale.y));
                var topSlack = headerBottom - gap - p.yMax;
                var bottomSlack = hint.center.y - footerReserve - (area.yMin + adHeight + padding.y);
                if (Mathf.Abs(topSlack - bottomSlack) > .1f)
                    throw new Exception(screen + ": 包含提示手势的内容组没有在 Header 与底部广告预留之间垂直居中");
                if (a.width > fullWidth + .1f || bottomSlack < -.1f || a.width < fullWidth - .1f && bottomSlack > .2f)
                    throw new Exception(screen + ": 图片没有在完整显示及底部预留范围内尽量放大");
                if (a.width < fullWidth - .1f)
                {
                    heightLimited++;
                    if (headerWidth > f.width + .2f && (back.xMin >= f.xMin - .1f || life.xMax <= f.xMax + .1f))
                        throw new Exception(screen + ": 高度不足缩图时 Header 仍然跟随图片宽度内缩");
                }
                foreach (var dot in ui.progressDots)
                    if (dot.gameObject.activeSelf && (Bounds(dot.rectTransform).yMax > p.yMax + .1f || Bounds(dot.rectTransform).yMin < p.yMin - .1f))
                        throw new Exception(screen + ": " + dot.name + " " + Bounds(dot.rectTransform) + " 超出进度区域 " + p);
                for (var i = 0; i < items.Length; i++)
                    if (anchors[i] != new Vector4(items[i].anchorMin.x,items[i].anchorMin.y,items[i].anchorMax.x,items[i].anchorMax.y))
                        throw new Exception("适配修改了原有锚点");
                for (var i = 0; i < controls.Length; i++)
                    if (controls[i].sizeDelta != controlSizes[i] || controls[i].localScale != controlScales[i])
                        throw new Exception(screen + ": " + controls[i].name + " 的原有尺寸或缩放被布局修改");
                typeof(UIDifferences).GetMethod("FitProgressRow", Private).Invoke(ui, new object[] { 30 });
                if (ui.progressDots[0].rectTransform.rect.width * 30 + row.spacing * 29 + row.padding.horizontal > p.width + .1f)
                    throw new Exception("30 个进度点超出图片宽度");
                report.AppendLine($"PASS {screen.x}x{screen.y}, area={area.width:F1}x{area.height:F1}, ad={adHeight}, inset={inset}: board={a.width:F1}x{a.height:F1}; capped safe header; centered content; ordered gaps; maximum fitting images; unchanged buttons; hand above banner; 30 dots fit");
            }
            if (heightLimited == 0) throw new Exception("缺少宽屏高度不足时缩图的检查结果");
            foreach (var width in new[] { 1200f, 2400f })
            {
                var size = new Vector2(width, nativeArea.height);
                var area = new Rect(nativeArea.center - size * .5f, size);
                layout.headerMaxWidth = float.MaxValue;
                layout.ApplyLayout(area);
                var unlimited = items.Select(Bounds).ToArray();
                layout.headerMaxWidth = 1600;
                layout.ApplyLayout(area);
                var fullHeaderWidth = area.width - Mathf.Max(0, layout.edgePadding.x) * 2;
                var shift = Mathf.Max(0, fullHeaderWidth - 1600) * .5f;
                for (var i = 0; i < items.Length; i++)
                {
                    var actual = Bounds(items[i]);
                    var expectedCenter = unlimited[i].center;
                    if (i == 4) expectedCenter.x += shift;
                    if (i == 5) expectedCenter.x -= shift;
                    var sizeChanged = Vector2.Distance(actual.size, unlimited[i].size) > .1f;
                    if (i == 8 && shift > 0)
                        sizeChanged = Mathf.Abs(actual.height - unlimited[i].height) > .1f || actual.width > unlimited[i].width + .1f;
                    if (Vector2.Distance(actual.center, expectedCenter) > .1f || sizeChanged)
                        throw new Exception(width + ": Header 限宽改变了手机布局、内容位置/尺寸或 Header 高度");
                }
                report.AppendLine($"PASS headerMaxWidth 1600 vs unlimited at area width {width:F0}: phone unchanged; tablet only narrows Back/Life X and optional title width; content and header Y unchanged.");
            }
            layout.headerMaxWidth = 1600;
            File.WriteAllText("Temp/responsive-play-layout-check.txt", report.ToString());
            Debug.Log(report.ToString());
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/responsive-play-layout-check.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Play Layout Offsets Only")]
    public static void CheckPlayLayoutOffsetsOnly()
    {
        const string report = "Temp/FindDifferences-play-layout-offsets.txt";
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var layout = root.GetComponent<UIDifferences>().play.GetComponent<DifferencePlayLayout>();
            var names = new[] { "upper", "lower", "frame", "progress", "back", "life", "hint", "zoom", "level" };
            var items = names.Select(name => (RectTransform)typeof(DifferencePlayLayout).GetField(name).GetValue(layout)).ToArray();
            var fields = names.Select(name => typeof(DifferencePlayLayout).GetField(name + "Offset")).ToArray();
            var offsets = names.Select((_, i) => new Vector2((i + 1) * 7, -(i + 1) * 11) * (i % 2 == 0 ? 1 : -1)).ToArray();
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1179, 2556);
            rect.ForceUpdateRectTransforms();
            foreach (var screen in new[] { new Vector2(1179, 2556), new Vector2(1440, 2960), new Vector2(1536, 2048), new Vector2(2048, 1536) })
            {
                foreach (var field in fields) field.SetValue(layout, Vector2.zero);
                var page = ((RectTransform)layout.transform).rect;
                var size = new Vector2(page.height * screen.x / screen.y, page.height);
                var area = new Rect(page.center - size * .5f, size);
                layout.ApplyLayout(area);
                var positions = items.Select(item => item.localPosition).ToArray();
                var sizes = items.Select(item => item.rect.size).ToArray();
                var scales = items.Select(item => item.localScale).ToArray();
                var anchors = items.Select(item => new Vector4(item.anchorMin.x, item.anchorMin.y, item.anchorMax.x, item.anchorMax.y)).ToArray();
                var pivots = items.Select(item => item.pivot).ToArray();
                for (var i = 0; i < fields.Length; i++) fields[i].SetValue(layout, new Vector2(offsets[i].x, 0));
                for (var pass = 0; pass < 3; pass++)
                {
                    layout.ApplyLayout(area);
                    for (var i = 0; i < items.Length; i++)
                    {
                        var expected = positions[i] + new Vector3(offsets[i].x, 0, 0);
                        var expectedSize = sizes[i];
                        if (i == 8)
                        {
                            var headerWidth = Mathf.Min(area.width - Mathf.Max(0, layout.edgePadding.x) * 2, Mathf.Max(1, layout.headerMaxWidth));
                            var reserve = Mathf.Max(layout.back.rect.width * Mathf.Abs(layout.back.localScale.x) + layout.backOffset.x - layout.levelOffset.x,
                                layout.life.rect.width * Mathf.Abs(layout.life.localScale.x) - layout.lifeOffset.x + layout.levelOffset.x);
                            expectedSize.x = Mathf.Max(1, headerWidth - 2 * (reserve + Mathf.Max(0, layout.sectionGap))) /
                                Mathf.Max(.001f, Mathf.Abs(layout.level.localScale.x));
                        }
                        if (Vector3.Distance(items[i].localPosition, expected) > .01f || Vector2.Distance(items[i].rect.size, expectedSize) > .01f ||
                            new Vector4(items[i].anchorMin.x, items[i].anchorMin.y, items[i].anchorMax.x, items[i].anchorMax.y) != anchors[i] || items[i].pivot != pivots[i])
                            throw new Exception(screen + ": " + names[i] + " offset changed size/anchors or accumulated across layout passes.");
                    }
                }
                for (var i = 0; i < fields.Length; i++) fields[i].SetValue(layout, offsets[i]);
                layout.ApplyLayout(area);
                var shiftedPositions = items.Select(item => item.localPosition).ToArray();
                var shiftedSizes = items.Select(item => item.rect.size).ToArray();
                Rect Bounds(RectTransform item)
                {
                    var min = (Vector2)layout.transform.InverseTransformPoint(item.TransformPoint(item.rect.min));
                    var max = (Vector2)layout.transform.InverseTransformPoint(item.TransformPoint(item.rect.max));
                    return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                }
                var progress = Bounds(layout.progress); var frame = Bounds(layout.frame);
                var back = Bounds(layout.back); var life = Bounds(layout.life); var level = Bounds(layout.level);
                var hint = Bounds(layout.hint); var zoom = Bounds(layout.zoom);
                var gap = Mathf.Max(0, layout.sectionGap);
                if (Mathf.Min(back.yMin, Mathf.Min(life.yMin, level.yMin)) - progress.yMax < gap - .1f ||
                    progress.yMin - frame.yMax < gap - .1f || frame.yMin - Mathf.Max(hint.yMax, zoom.yMax) < gap - .1f)
                    throw new Exception(screen + ": vertical offsets did not reserve the actual header/progress/frame/footer gaps.");
                var bottom = area.yMin + Mathf.Max(0, layout.bottomAdHeight) + Mathf.Max(0, layout.edgePadding.y);
                if (Mathf.Min(Mathf.Min(hint.yMin, zoom.yMin), hint.center.y - DifferenceHintHand.BottomExtent * Mathf.Abs(layout.hint.localScale.y)) < bottom - .1f)
                    throw new Exception(screen + ": vertical offsets moved buttons or the hint hand into the bottom reservation.");
                for (var pass = 0; pass < 3; pass++)
                {
                    layout.ApplyLayout(area);
                    for (var i = 0; i < items.Length; i++)
                        if (Vector3.Distance(items[i].localPosition, shiftedPositions[i]) > .01f || Vector2.Distance(items[i].rect.size, shiftedSizes[i]) > .01f ||
                            items[i].localScale != scales[i] || items[i].pivot != pivots[i] ||
                            new Vector4(items[i].anchorMin.x, items[i].anchorMin.y, items[i].anchorMax.x, items[i].anchorMax.y) != anchors[i] ||
                            i >= 4 && i < 8 && Vector2.Distance(items[i].rect.size, sizes[i]) > .01f)
                            throw new Exception(screen + ": " + names[i] + " vertical offset did not settle after reserving its space.");
                }
                foreach (var field in fields) field.SetValue(layout, Vector2.zero);
                layout.ApplyLayout(area);
                for (var i = 0; i < items.Length; i++)
                    if (Vector3.Distance(items[i].localPosition, positions[i]) > .01f || Vector2.Distance(items[i].rect.size, sizes[i]) > .01f)
                        throw new Exception(screen + ": clearing " + names[i] + " offset did not restore automatic layout.");
                foreach (var buttonIndex in new[] { 6, 7 })
                {
                    var baselineGap = Bounds(layout.frame).yMin - Bounds(items[buttonIndex]).yMax;
                    foreach (var drop in new[] { 40f, 100f })
                    {
                        foreach (var field in fields) field.SetValue(layout, Vector2.zero);
                        fields[buttonIndex].SetValue(layout, new Vector2(0, -drop));
                        layout.ApplyLayout(area);
                        var actualGap = Bounds(layout.frame).yMin - Bounds(items[buttonIndex]).yMax;
                        if (Mathf.Abs(actualGap - baselineGap - drop) > .1f)
                            throw new Exception(screen + ": " + names[buttonIndex] + " Y=-" + drop + " did not increase the frame-to-button gap by " + drop + ".");
                        var droppedHint = Bounds(layout.hint); var droppedZoom = Bounds(layout.zoom);
                        if (Mathf.Min(Mathf.Min(droppedHint.yMin, droppedZoom.yMin),
                            droppedHint.center.y - DifferenceHintHand.BottomExtent * Mathf.Abs(layout.hint.localScale.y)) < bottom - .1f)
                            throw new Exception(screen + ": " + names[buttonIndex] + " Y=-" + drop + " moved a button or the hint hand into the bottom reservation.");
                        var droppedPositions = items.Select(item => item.localPosition).ToArray();
                        var droppedSizes = items.Select(item => item.rect.size).ToArray();
                        for (var pass = 0; pass < 3; pass++)
                        {
                            layout.ApplyLayout(area);
                            for (var i = 0; i < items.Length; i++)
                                if (Vector3.Distance(items[i].localPosition, droppedPositions[i]) > .01f || Vector2.Distance(items[i].rect.size, droppedSizes[i]) > .01f)
                                    throw new Exception(screen + ": " + names[buttonIndex] + " Y=-" + drop + " accumulated across layout passes.");
                        }
                        foreach (var field in fields) field.SetValue(layout, Vector2.zero);
                        layout.ApplyLayout(area);
                        for (var i = 0; i < items.Length; i++)
                            if (Vector3.Distance(items[i].localPosition, positions[i]) > .01f || Vector2.Distance(items[i].rect.size, sizes[i]) > .01f)
                                throw new Exception(screen + ": clearing " + names[buttonIndex] + " Y=-" + drop + " did not restore " + names[i] + ".");
                    }
                }
            }
            layout.level = null; layout.ApplyLayout(((RectTransform)layout.transform).rect);
            File.WriteAllText(report, "PASS: nine independent offsets at four screen ratios; horizontal offsets preserve size while title width adapts to header reserves at a stable height; vertical offsets reserve section gaps, banner and hand space without accumulation; Hint/Zoom Y=-40/-100 increase their frame gaps by 40/100 and keep buttons/hand above the banner; button sizes/scales, anchors and pivots preserved; zero restores automatic positions and sizes; optional Level is safe.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Level Title Layout Only")]
    public static void CheckLevelTitleLayoutOnly()
    {
        const string report = "Temp/FindDifferences-level-title-layout.txt";
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            ui.play.SetActive(true);
            var layout = ui.play.GetComponent<DifferencePlayLayout>();
            var title = layout.level;
            var fontSize = title.GetComponent<Text>().fontSize;
            var center = ((RectTransform)layout.transform).rect.center;
            var normal = new Rect(center - new Vector2(1179, 2556) * .5f, new Vector2(1179, 2556));
            var narrow = new Rect(center - new Vector2(500, 2556) * .5f, new Vector2(500, 2556));
            title.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            title.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
            layout.ApplyLayout(normal);
            CheckSingleLineLevelTitle(title, fontSize);
            var expected = title.rect.size;
            layout.ApplyLayout(narrow);
            if (title.rect.width <= 0 || title.rect.width >= expected.x - .1f)
                throw new Exception("窄区域没有为 Level 标题生成独立的有效宽度");
            layout.ApplyLayout(normal);
            if (Vector2.Distance(title.rect.size, expected) > .1f)
                throw new Exception("Level 标题宽度依赖上一帧窄区域尺寸");
            var validate = typeof(DifferencePlayLayout).GetMethod("OnValidate", Private);
            for (var pass = 0; pass < 3; pass++)
            {
                validate.Invoke(layout, null);
                layout.ApplyLayout(normal);
                layout.ApplyLayout(normal);
                if (Vector2.Distance(title.rect.size, expected) > .1f)
                    throw new Exception("Level 标题尺寸在 OnValidate 或重复布局后改变");
                CheckSingleLineLevelTitle(title, fontSize);
            }
            File.WriteAllText(report, "PASS: zero-sized Level recovers a valid one-line rect; narrow-to-normal and OnValidate/repeated layout restore independent dimensions; Level 1 stays one line at the authored font size. Text, prefab and player settings unchanged.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CheckSingleLineLevelTitle(RectTransform level, int fontSize)
    {
        var text = level.GetComponent<Text>();
        var original = text.text;
        try
        {
            text.text = "Level 1";
            if (level.rect.width <= 1 || level.rect.height <= 1 || text.fontSize != fontSize || text.preferredHeight > level.rect.height + .1f)
                throw new Exception("Level 标题尺寸不足以容纳原字号单行文本");
            using (var generator = new TextGenerator())
                if (!generator.Populate(text.text, text.GetGenerationSettings(level.rect.size)) || generator.lineCount != 1)
                    throw new Exception("Level 1 标题仍然换行");
        }
        finally { text.text = original; }
    }

    [MenuItem("Tools/Find Differences/Check Settings Actions Only")]
    public static void CheckSettingsActionsOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            foreach (var name in new[] { "designContactButton", "designTermsButton", "designPrivacyButton" })
            {
                var button = (Button)Field(ui, name);
                if (!button || !button.interactable || !button.targetGraphic || !button.targetGraphic.raycastTarget)
                    throw new Exception(name + " 未接入可点击控件");
            }
            if ((string)Field(ui, "contactUrl") != "mailto:contact@joystar.pro" ||
                (string)Field(ui, "termsOfServiceUrl") != "https://joystar.pro/terms" ||
                (string)Field(ui, "privacyPolicyUrl") != "https://joystar.pro/privacy.html")
                throw new Exception("设置链接与拼图使用的官方地址不一致");
            var feedback = typeof(UIDifferences).GetMethod("GetVibrationFeedback", Private);
            var enabled = typeof(UIDifferences).GetField("vibration", Private);
            enabled.SetValue(ui, true);
            var found = (Vector2Int)feedback.Invoke(ui, new object[] { 0 });
            var miss = (Vector2Int)feedback.Invoke(ui, new object[] { Hotfix.Manager.DifferenceRound.Miss });
            if (found != new Vector2Int(30, 100) || miss != new Vector2Int(60, 200))
                throw new Exception("点中应沿用拼图震动，点错应更强");
            if ((Vector2Int)feedback.Invoke(ui, new object[] { Hotfix.Manager.DifferenceRound.Ignored }) != Vector2Int.zero)
                throw new Exception("重复点击已找到位置不应震动");
            enabled.SetValue(ui, false);
            foreach (var result in new[] { 0, Hotfix.Manager.DifferenceRound.Miss })
                if ((Vector2Int)feedback.Invoke(ui, new object[] { result }) != Vector2Int.zero)
                    throw new Exception("关闭震动后仍触发反馈");

            const string gradle = "Temp/SettingsPermissionCheck";
            Directory.CreateDirectory(gradle + "/src/main");
            var manifestPath = gradle + "/src/main/AndroidManifest.xml";
            File.WriteAllText(manifestPath, "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>");
            var build = new DifferenceAndroidBuild();
            build.OnPostGenerateGradleAndroidProject(gradle);
            build.OnPostGenerateGradleAndroidProject(gradle);
            var manifest = new System.Xml.XmlDocument(); manifest.Load(manifestPath);
            if (manifest.SelectNodes("/manifest/uses-permission").Count != 1 ||
                ((System.Xml.XmlElement)manifest.SelectSingleNode("/manifest/uses-permission")).GetAttribute("name", "http://schemas.android.com/apk/res/android") != "android.permission.VIBRATE")
                throw new Exception("Android 震动权限缺失或重复");
            File.WriteAllText("Temp/settings-actions-check.txt", "PASS: links bound; hit/miss strengths; ignored clicks and mute; Android permission is idempotent");
            Debug.Log("PASS: settings actions and vibration checks");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/settings-actions-check.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static TaskCompletionSource<bool> startupGate;
    public static Task DelayedStartupForCheck() => startupGate.Task;

    [MenuItem("Tools/Find Differences/Check Startup Await Only")]
    public static async void CheckStartupAwaitOnly()
    {
        try
        {
            var helper = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("GameFrameX.Startup.Application.HotfixHelper")).First(type => type != null);
            var invoke = helper.GetMethod("InvokeEntry", BindingFlags.Static | BindingFlags.NonPublic);
            for (var fail = 0; fail < 2; fail++)
            {
                startupGate = new TaskCompletionSource<bool>();
                var result = invoke.Invoke(null, new object[] { typeof(DifferenceUiSmokeCheck), nameof(DelayedStartupForCheck) });
                var asTask = result.GetType().Assembly.GetType("Cysharp.Threading.Tasks.UniTaskExtensions").GetMethod("AsTask", new[] { result.GetType() });
                var pending = (Task)asTask.Invoke(null, new[] { result });
                if (pending.IsCompleted) throw new Exception("主页未就绪时启动流程提前完成");
                if (fail == 0) { startupGate.SetResult(true); await pending; }
                else
                {
                    startupGate.SetException(new InvalidOperationException("expected startup failure"));
                    try { await pending; throw new Exception("启动错误被吞掉"); }
                    catch (InvalidOperationException error) when (error.Message == "expected startup failure") { }
                }
            }
            File.WriteAllText("Temp/startup-await-check.txt", "PASS: waits for home readiness; startup errors propagate");
        }
        catch (Exception error) { File.WriteAllText("Temp/startup-await-check.txt", "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Build Android Spine Shader Check")]
    public static void BuildAndroidSpineShaderCheck()
    {
        if (EditorApplication.isPlaying) throw new Exception("请先退出 Play 模式");
        const string folder = "Temp/SpineAlphaCheck";
        const string shaders = "Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/Shaders/SkeletonGraphic/";
        Directory.CreateDirectory(folder);
        var build = new AssetBundleBuild { assetBundleName = "spine-alpha.bundle", assetNames = new[] {
            shaders + "Spine-SkeletonGraphic.shader", shaders + "Spine-SkeletonGraphic-Additive.shader",
            shaders + "Spine-SkeletonGraphic-Multiply.shader", shaders + "Spine-SkeletonGraphic-Screen.shader" } };
        var result = BuildPipeline.BuildAssetBundles(folder, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.Android);
        if (!result) throw new Exception("Android Spine Shader 编译失败");
        Debug.Log("Android Spine shader check bundle: " + folder + "/spine-alpha.bundle");
    }

    [MenuItem("Tools/Find Differences/Check Victory Replay Only")]
    public static void CheckVictoryReplayOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var page = (GameObject)Field(ui, "victoryDesign");
            var replay = typeof(UIDifferences).GetMethod("ReplayVictoryAnimation", Private);
            var animations = page.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true);
            if (animations.Length == 0) throw new Exception("成功页缺少 Spine");
            if (page.transform.Find("TittleSpine").GetComponent<Spine.Unity.SkeletonGraphic>().startingLoop ||
                !page.transform.Find("Light").GetComponent<Spine.Unity.SkeletonGraphic>().startingLoop ||
                !page.transform.Find("Next").GetComponentInChildren<Spine.Unity.SkeletonGraphic>(true).startingLoop)
                throw new Exception("标题应单次播放，背景光和按钮流光应循环播放");
            for (var i = 0; i < 2; i++)
            {
                page.SetActive(true);
                replay.Invoke(ui, null);
                foreach (var spine in animations)
                {
                    if (!spine.gameObject.activeInHierarchy) continue;
                    var track = spine.AnimationState.GetCurrent(0);
                    if (track == null || track.Animation.Name != spine.startingAnimation || track.TrackTime != 0 ||
                        track.Loop != spine.startingLoop || !spine.UnscaledTime)
                        throw new Exception("成功页动画未按预制体设置从头播放: " + spine.name);
                    spine.Update(track.Animation.Duration * 2 + .1f);
                    if (track.Loop && (track.TrackTime <= track.Animation.Duration * 2 || track.AnimationTime >= track.Animation.Duration))
                        throw new Exception("成功页循环动画未持续播放: " + spine.name);
                }
                page.SetActive(false);
            }
            Debug.Log("PASS: victory Spine restarts on first and subsequent openings");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Audio Only")]
    public static void CheckAudioOnly()
    {
        var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab").GetComponent<UIDifferences>();
        foreach (var binding in new[] { "buttonSound|Click_Btn.wav", "failSound|False.wav", "foundSound|Found.wav", "missSound|Miss.wav", "winSound|Win.wav", "coinCreateSound|CoinCreate.WAV", "progressCompleteSound|Successtips.wav", "tipsSound|Tips.WAV", "homeMusic|MusicHome.wav", "gameMusic|MusicGame.wav" })
        {
            var pair = binding.Split('|');
            var clip = (AudioClip)typeof(UIDifferences).GetField(pair[0]).GetValue(ui);
            if (!clip || AssetDatabase.GetAssetPath(clip) != "Assets/Bundles/UI/UIDifferences/Audio/" + pair[1])
                throw new Exception("音频绑定错误：" + binding);
        }
        if (!ui.musicSource.loop || ui.audioSource == ui.musicSource) throw new Exception("背景音乐应循环，并与音效使用独立声源");
        if (Application.isPlaying)
        {
            ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
            if (!ui) throw new Exception("请先打开游戏主页");
            var enabled = (bool)Field(ui, "musicEnabled");
            var toggle = typeof(UIDifferences).GetField("musicEnabled", Private);
            var method = typeof(UIDifferences).GetMethod("PlayMusicForPage", Private);
            try
            {
                toggle.SetValue(ui, true);
                foreach (var page in new[] { ui.home, ui.play, ui.home })
                {
                    method.Invoke(ui, new object[] { page });
                    if (ui.musicSource.clip != (page == ui.play ? ui.gameMusic : ui.homeMusic) || !ui.musicSource.isPlaying)
                        throw new Exception("页面背景音乐切换失败");
                }
                toggle.SetValue(ui, false);
                method.Invoke(ui, new object[] { ui.play });
                if (ui.musicSource.isPlaying) throw new Exception("关闭音乐后仍在播放");
            }
            finally
            {
                toggle.SetValue(ui, enabled);
                method.Invoke(ui, new object[] { Field(ui, "currentPage") });
            }
        }
        Debug.Log("PASS: all 9 audio bindings; in Play mode also checks home/game music switching and mute");
    }

    [MenuItem("Tools/Find Differences/Check Reward Coins Only")]
    public static async void CheckRewardCoinsOnly()
    {
        if (running) return;
        running = true;
        UIDifferences ui = null;
        GameObject page = null;
        var wasActive = false;
        var wasResult = false;
        var wasWon = false;
        var wasAdPending = false;
        var background = Application.runInBackground;
        try
        {
            Application.runInBackground = true;
            ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
            if (!Application.isPlaying || !ui) throw new Exception("请先运行游戏");
            if (ui.AdInputBlocked) throw new Exception("Wait for the active ad to finish before checking reward coins.");
            page = (GameObject)typeof(UIDifferences).GetField("victoryDesign", Private).GetValue(ui);
            wasActive = page.activeSelf;
            wasResult = (bool)Field(ui, "figmaResultActive");
            wasWon = (bool)Field(ui, "figmaResultWon");
            wasAdPending = (bool)Field(ui, "adTransitionPending");
            typeof(UIDifferences).GetMethod("CancelRemoteLoad", Private).Invoke(ui, null);
            typeof(UIDifferences).GetField("figmaResultActive", Private).SetValue(ui, true);
            typeof(UIDifferences).GetField("figmaResultWon", Private).SetValue(ui, true);
            typeof(UIDifferences).GetMethod("UpdateFigmaDesign", Private).Invoke(ui, new object[] { Field(ui, "currentPage") });
            await Task.Delay(400); // Let the editor menu close before measuring frame time.
            page.SetActive(true);
            page.transform.SetAsLastSibling();
            var start = typeof(UIDifferences).GetMethod("StartRewardCoins", Private);
            var cancel = typeof(UIDifferences).GetMethod("CancelRewardCoins", Private);
            var wallet = (RectTransform)typeof(UIDifferences).GetField("rewardWallet", Private).GetValue(ui);
            var origin = (RectTransform)typeof(UIDifferences).GetField("rewardCoinOrigin", Private).GetValue(ui);
            if (!origin || origin.anchorMin.y > .2f) throw new Exception("金币应从结算图片底部产生");
            var label = (Text)typeof(UIDifferences).GetField("rewardCoinsLabel", Private).GetValue(ui);
            var balance = ui.Coins;
            var summary = (RectTransform)Field(ui, "rewardSummary");
            var summaryLabel = (Text)Field(ui, "rewardSummaryLabel");
            if (!summary || !summaryLabel) throw new Exception("RewardCoin 奖励展示未绑定");
            var summaryGroup = summary.GetComponent<CanvasGroup>();
            var source = new Vector3(0, -100, 0);
            var target = new Vector3(-250, 500, 0);
            var atStart = DifferenceRewardCoin.Position(source, target, 100, 0, 0, out var flight);
            var atEnd = DifferenceRewardCoin.Position(source, target, 100, 5, 0, out flight);
            if (atStart != source || Vector3.Distance(atEnd, target) > .001f || flight != 1)
                throw new Exception("金币轨迹起点或终点不正确");
            typeof(UIDifferences).GetField("adTransitionPending", Private).SetValue(ui, true);
            start.Invoke(ui, new object[] { 10 });
            if (summary.gameObject.activeSelf || summaryGroup.alpha != 0 || summaryLabel.text != "x10")
                throw new Exception("奖励展示应先隐藏并使用本次奖励金额");
            if (!wallet.gameObject.activeSelf || label.text != (balance - 10).ToString()) throw new Exception("奖励起始数字不正确");
            await RewardFrames(DifferenceRewardCoin.EndTime(10) + .3f);
            if (!wallet.gameObject.activeInHierarchy || label.text != (balance - 10).ToString() || summary.gameObject.activeSelf ||
                page.GetComponentsInChildren<DifferenceRewardCoin>().Length != 0 || ui.Coins != balance)
                throw new Exception("A fullscreen ad consumed the reward animation, hid the collection wallet or changed the balance.");
            typeof(UIDifferences).GetField("adTransitionPending", Private).SetValue(ui, false);
            await RewardFrames(.45f);
            if (summary.gameObject.activeSelf) throw new Exception("金币飞行结束前不能显示奖励展示");
            var sample = page.GetComponentInChildren<DifferenceRewardCoin>();
            if (!sample) throw new Exception("未生成金币: page=" + page.activeInHierarchy + ", all=" + page.GetComponentsInChildren<DifferenceRewardCoin>(true).Length + ", routine=" + (Field(ui, "rewardRoutine") != null));
            sample.SetAppearance(Mathf.PI / 2 / 10.5f, 0, 0);
            var rim = sample.GetComponentsInChildren<Image>();
            if (rim.Length < 2 || rim[0].rectTransform.anchoredPosition.x == rim[rim.Length - 1].rectTransform.anchoredPosition.x)
                throw new Exception("侧面缺少厚度");
            ScreenCapture.CaptureScreenshot("Temp/reward-coins-pop.png");
            await RewardFrames(.6f);
            ScreenCapture.CaptureScreenshot("Temp/reward-coins-flight.png");
            await Wait(() => !wallet.gameObject.activeSelf, "reward coin flights finish and wallet hides");
            if (wallet.gameObject.activeSelf || label.text != balance.ToString() || ui.Coins != balance)
                throw new Exception("结算显示/隐藏不正确，或动画重复发奖");
            if (!summary.gameObject.activeSelf)
                throw new Exception("金币飞完后应立即开始奖励渐显，不等待礼花特效");
            await RewardFrames(.1f);
            if (summaryGroup.alpha <= 0 || summaryGroup.alpha >= 1)
                throw new Exception("奖励展示应渐显而非立即显示");
            await Wait(() => Field(ui, "rewardRoutine") == null, "reward summary fade completes");
            if (summaryGroup.alpha != 1 || summaryLabel.text != "x10" || ui.Coins != balance)
                throw new Exception("奖励展示金额或淡入结束状态错误");
            ScreenCapture.CaptureScreenshot("Temp/reward-coins-summary.png");
            start.Invoke(ui, new object[] { 10 });
            cancel.Invoke(ui, null);
            if (wallet.gameObject.activeSelf || summary.gameObject.activeSelf || summaryGroup.alpha != 0 || ui.Coins != balance)
                throw new Exception("取消结算未清理");
            start.Invoke(ui, new object[] { 0 });
            await Wait(() => Field(ui, "rewardRoutine") == null, "zero reward summary completes");
            if (summaryLabel.text != "x0" || !summary.gameObject.activeSelf || ui.Coins != balance)
                throw new Exception("零奖励应显示 x0 且不重复发奖");
            File.WriteAllText("Temp/reward-coins-check.txt", "PASS: fullscreen ad preserves the visible collection wallet and defers coin flights/counting; unlock resumes coin flights and delayed summary fade, actual reward, zero reward and cancellation; balance unchanged");
            Debug.Log("PASS: reward coin motion and lifecycle");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/reward-coins-check.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            Application.runInBackground = background;
            if (ui && page) typeof(UIDifferences).GetMethod("CancelRewardCoins", Private).Invoke(ui, null);
            if (ui && page)
            {
                typeof(UIDifferences).GetField("adTransitionPending", Private).SetValue(ui, wasAdPending);
                typeof(UIDifferences).GetField("figmaResultActive", Private).SetValue(ui, wasResult);
                typeof(UIDifferences).GetField("figmaResultWon", Private).SetValue(ui, wasWon);
                typeof(UIDifferences).GetMethod("UpdateFigmaDesign", Private).Invoke(ui, new object[] { Field(ui, "currentPage") });
            }
            if (page) page.SetActive(wasActive);
            running = false;
        }
    }

    static async Task RewardFrames(float seconds)
    {
        var until = Time.unscaledTime + seconds;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (EditorApplication.isPlaying && Time.unscaledTime < until && DateTime.UtcNow < deadline)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(16);
        }
        if (!EditorApplication.isPlaying || Time.unscaledTime < until) throw new Exception("游戏没有推进帧，请取消暂停后检查");
    }

    [MenuItem("Tools/Find Differences/Check Toast Only")]
    public static void CheckToastOnly()
    {
        var root = new GameObject("Toast check", typeof(RectTransform));
        try
        {
            var rect = (RectTransform)root.transform;
            rect.anchoredPosition = new Vector2(0, -288.7f);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(root.transform, false);
            var toast = root.AddComponent<CommonToast>();
            toast.Show("first", 2);
            var advance = typeof(CommonToast).GetMethod("Advance", Private);
            advance.Invoke(toast, new object[] { 1.7f });
            var group = root.GetComponent<CanvasGroup>();
            if (rect.anchoredPosition.y <= -288.7f || group.alpha <= 0 || group.alpha >= 1 || group.blocksRaycasts)
                throw new Exception("Toast must rise, fade and allow clicks through");
            toast.Show("replacement", 2);
            if (rect.anchoredPosition.y != -288.7f || group.alpha != 1 || text.GetComponent<Text>().text != "replacement")
                throw new Exception("Replacement must reset toast");
            advance.Invoke(toast, new object[] { 3f });
            if (root.activeSelf) throw new Exception("Toast must hide after duration");
            Debug.Log("PASS: Toast rise, fade, replace and hide");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("Tools/Find Differences/Check Found Ring Only")]
    public static void CheckFoundRingOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            ui.upperImage.transform.parent.localScale = Vector3.one; // This fixture checks local units; the alignment check covers authored viewport scaling.
            typeof(UIDifferences).GetField("useSpineFoundRing", Private).SetValue(ui, false);
            var marker = ui.topRings[0];
            marker.gameObject.SetActive(true);
            var spine = Spine.Unity.SkeletonGraphic.NewSkeletonGraphicGameObject(ui.foundRingSpine, marker.transform, ui.foundRingMaterial);
            var show = typeof(UIDifferences).GetMethod("ShowFoundRing", Private);
            show.Invoke(ui, new object[] { marker, false });
            var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
            if (!ring || !ring.enabled || !ring.gameObject.activeSelf || spine.gameObject.activeSelf || marker.enabled || !ui.foundRingSpine || !ui.foundRingEffectPrefab)
                throw new Exception("固定线宽圈必须显示，Spine 仅隐藏且资源与粒子保留");
            var draw = typeof(DifferenceFoundRing).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly);
            var advance = typeof(DifferenceFoundRing).GetMethod("Advance", Private);
            ring.style = new DifferenceFoundRing.Style();
            ring.style.minDiameter = 1; ring.style.maxDiameter = 10000;
            foreach (var size in new[] { 46f, 180f })
            {
                marker.rectTransform.sizeDelta = Vector2.one * size;
                ring.rectTransform.ForceUpdateRectTransforms();
                ring.Play(true);
                foreach (var step in new[] { 0f, .18f, .22f })
                {
                    advance.Invoke(ring, new object[] { step });
                    using (var mesh = new VertexHelper())
                    {
                        draw.Invoke(ring, new object[] { mesh });
                        var min = float.MaxValue; var max = 0f; var alpha = 0f; var vertex = new UIVertex();
                        for (var i = 0; i < mesh.currentVertCount; i++)
                        {
                            mesh.PopulateUIVertex(ref vertex, i);
                            var radius = Vector2.Distance(vertex.position, ring.rectTransform.rect.center);
                            min = Mathf.Min(min, radius); max = Mathf.Max(max, radius); alpha = Mathf.Max(alpha, vertex.color.a);
                        }
                        if (mesh.currentVertCount == 0 || Mathf.Abs(max - min - ring.style.lineWidth) > .001f ||
                            (step == 0 ? alpha != 0 || Mathf.Abs(max - size * .4f) > .001f : alpha != 255) ||
                            (step == .18f && max <= size * .5f) || (step == .22f && Mathf.Abs(max - size * .5f) > .001f))
                            throw new Exception("圆环渐显、弹出回弹、大小与动画过程中的固定线宽检查失败");
                    }
                }
                ring.Play(false);
                if ((float)typeof(DifferenceFoundRing).GetField("elapsed", Private).GetValue(ring) != ring.style.animationDuration) throw new Exception("恢复进度必须直接显示最终圆环");
            }
            ring.style = new DifferenceFoundRing.Style {
                sizeMultiplier = 1.5f, radiusOffset = 3, minDiameter = 1, maxDiameter = 10000, lineWidth = 10, opacity = .5f,
                glowIntensity = .6f, glowWidth = 12, glowColor = Color.magenta,
                highlightIntensity = 0, animationDuration = 0, segments = 64
            };
            ring.style.outlineColors = new Gradient {
                colorKeys = new[] { new GradientColorKey(Color.cyan, 0), new GradientColorKey(Color.cyan, 1) },
                alphaKeys = new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            };
            ring.Play(true);
            using (var mesh = new VertexHelper())
            {
                draw.Invoke(ring, new object[] { mesh });
                var vertex = new UIVertex(); var max = 0f; var min = float.MaxValue;
                var cyan = false; var magenta = false;
                for (var i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    var radius = Vector2.Distance(vertex.position, ring.rectTransform.rect.center);
                    max = Mathf.Max(max, radius); min = Mathf.Min(min, radius);
                    cyan |= vertex.color.g == 255 && vertex.color.b == 255 && vertex.color.a == 128;
                    magenta |= vertex.color.r == 255 && vertex.color.b == 255 && vertex.color.a > 0;
                }
                if (Mathf.Abs(max - 150) > .001f || Mathf.Abs(min - 116) > .001f || !cyan || !magenta)
                    throw new Exception("大小、线宽、描边颜色、透明度、光晕颜色与范围或零时长设置未生效");
            }
            ring.style.lineWidth = 1000;
            ring.style.glowIntensity = 0;
            using (var mesh = new VertexHelper())
            {
                draw.Invoke(ring, new object[] { mesh });
                var vertex = new UIVertex(); var min = float.MaxValue;
                for (var i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    min = Mathf.Min(min, Vector2.Distance(vertex.position, ring.rectTransform.rect.center));
                }
                if (Mathf.Abs(min - .5f) > .001f) throw new Exception("过粗线宽必须保留透明圆心");
            }
            ring.style = new DifferenceFoundRing.Style { animationDuration = 0 };
            void CheckRadius(float expected)
            {
                using (var mesh = new VertexHelper())
                {
                    draw.Invoke(ring, new object[] { mesh });
                    var vertex = new UIVertex(); var maximum = 0f;
                    for (var i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        maximum = Mathf.Max(maximum, Vector2.Distance(vertex.position, ring.rectTransform.rect.center));
                    }
                    if (mesh.currentVertCount == 0 || Mathf.Abs(ring.Radius - expected) > .001f || Mathf.Abs(maximum - expected) > .001f)
                        throw new Exception("圆环最终半径不符合直径范围：" + expected);
                }
            }
            foreach (var size in new[] { 20f, 160f, 600f })
            {
                marker.rectTransform.sizeDelta = Vector2.one * size;
                ring.rectTransform.ForceUpdateRectTransforms(); ring.Play(false);
                CheckRadius(Mathf.Clamp(size, 100, 220) * .5f);
            }
            marker.rectTransform.sizeDelta = Vector2.one * 160;
            ring.rectTransform.ForceUpdateRectTransforms();
            ring.style.sizeMultiplier = .25f; ring.style.radiusOffset = -20; CheckRadius(50);
            ring.style.sizeMultiplier = 4; ring.style.radiusOffset = 10; CheckRadius(110);
            ring.style.sizeMultiplier = 1; ring.style.radiusOffset = -10; CheckRadius(70);
            ring.style.minDiameter = 180; ring.style.maxDiameter = 40; CheckRadius(90);
            ring.style.minDiameter = -1; ring.style.maxDiameter = -20;
            if (ring.Radius != .5f || ring.InnerRadius != 0)
                throw new Exception("无效直径范围必须保留有限的最小半径");
            ring.style = new DifferenceFoundRing.Style();
            marker.rectTransform.sizeDelta = Vector2.one * 600;
            ring.rectTransform.ForceUpdateRectTransforms(); ring.Play(true);
            if (Mathf.Abs(ring.Radius - 88) > .001f) throw new Exception("直径上限必须在出现动画之前生效");
            advance.Invoke(ring, new object[] { .18f });
            if (ring.Radius <= 110) throw new Exception("直径上限不能取消出现动画的回弹");
            advance.Invoke(ring, new object[] { .22f }); CheckRadius(110);
            CaptureFoundRingPreview();
            File.WriteAllText("Temp/FindDifferences-found-ring.txt", "PASS: Spine retained; fixed width during animation; configurable size, radius offset, width, colors, opacity, glow and zero duration; oversized width retains clear center; final diameter limits, multiplier/offset clamping, invalid ranges and animated overshoot.");
            Debug.Log("PASS: fixed-width animated found ring");
        }
        catch (Exception error) { File.WriteAllText("Temp/FindDifferences-found-ring.txt", "FAIL: " + error); Debug.LogException(error); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void CaptureFoundRingPreview()
    {
        var preview = new GameObject("Ring preview", typeof(Canvas));
        var cameraNode = new GameObject("Ring preview camera", typeof(Camera));
        var target = new RenderTexture(960, 480, 16);
        var pixels = new Texture2D(960, 480, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            preview.transform.position = new Vector3(10000, 10000, 0);
            preview.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            foreach (var size in new[] { 100f, 180f })
            {
                var node = new GameObject("Circle", typeof(RectTransform), typeof(DifferenceFoundRing));
                node.transform.SetParent(preview.transform, false);
                var ring = node.GetComponent<DifferenceFoundRing>();
                ring.rectTransform.sizeDelta = Vector2.one * size;
                ring.rectTransform.anchoredPosition = new Vector2(size == 100 ? -110 : 110, 0);
                ring.raycastTarget = false;
                ring.Play(false);
            }
            var camera = cameraNode.GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = preview.transform.position + new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 120;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .2f, .24f);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 480), 0, 0); pixels.Apply();
            File.WriteAllBytes("Temp/FindDifferences-found-ring.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraNode); UnityEngine.Object.DestroyImmediate(preview);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    [MenuItem("Tools/Find Differences/Check Found Flash Mask Only")]
    public static void CheckFoundFlashMaskOnly()
    {
        const string report = "Temp/FindDifferences-found-flash-mask.txt";
        var preview = new GameObject("Flash mask preview", typeof(RectTransform), typeof(Canvas));
        var cameraNode = new GameObject("Flash mask preview camera", typeof(Camera));
        var target = new RenderTexture(512, 512, 24);
        var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            preview.transform.position = new Vector3(10000, 10000, 0);
            var canvas = preview.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)preview.transform).sizeDelta = Vector2.one * 512;
            RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
            {
                var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
                return rect;
            }
            var board = Rect(preview.transform, "Board", Vector2.one * 420, Vector2.zero);
            var background = board.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Color.blue; background.raycastTarget = false;
            var marker = Rect(board, "Marker", Vector2.one * 160, new Vector2(11, 17));
            var markerImage = marker.gameObject.AddComponent<UnityEngine.UI.Image>(); markerImage.enabled = false;
            var ringRect = Rect(marker, "Outline", marker.sizeDelta, Vector2.zero);
            var ring = ringRect.gameObject.AddComponent<DifferenceFoundRing>();
            ring.style = new DifferenceFoundRing.Style {
                minDiameter = 1, maxDiameter = 10000, lineWidth = 12, animationDuration = 1,
                initialSize = .6f, overshoot = 0, opacity = 0, segments = 64
            };
            ring.raycastTarget = false;
            var patch = Rect(board, "Flash", new Vector2(100, 140), new Vector2(-13, 9));
            patch.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var picture = Rect(patch, "Picture", board.sizeDelta, -patch.anchoredPosition);
            var flash = picture.gameObject.AddComponent<UnityEngine.UI.Image>();
            flash.color = Color.red; flash.raycastTarget = false;
            typeof(UIDifferences).GetMethod("MaskPatchFlash", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { flash, markerImage });
            var mask = patch.GetComponent<DifferenceFoundMask>();
            var stencil = patch.GetComponent<UnityEngine.UI.Mask>();
            if (!mask || mask.ring != ring || !stencil || stencil.showMaskGraphic || mask.raycastTarget || !flash.maskable)
                throw new Exception("闪烁图必须绑定原生圆形遮罩并保留图片裁剪与点击穿透");
            var camera = cameraNode.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = preview.transform.position + new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 180;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.blue;
            camera.targetTexture = target; canvas.worldCamera = camera;
            var advance = typeof(DifferenceFoundRing).GetMethod("Advance", Private);
            var draw = typeof(DifferenceFoundMask).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly);
            Color Sample(Vector2 point)
            {
                var viewport = camera.WorldToViewportPoint(ringRect.TransformPoint(ringRect.rect.center + point));
                return pixels.GetPixel(Mathf.Clamp((int)(viewport.x * pixels.width), 0, pixels.width - 1),
                    Mathf.Clamp((int)(viewport.y * pixels.height), 0, pixels.height - 1));
            }
            foreach (var zoom in new[] { 1f, 2f })
            foreach (var phase in new[] { 0f, .4f })
            {
                board.localScale = Vector3.one * zoom;
                ring.Play(true); advance.Invoke(ring, new object[] { phase }); mask.SetVerticesDirty();
                using (var mesh = new VertexHelper())
                {
                    draw.Invoke(mask, new object[] { mesh });
                    var vertex = new UIVertex(); var maximum = 0f;
                    for (var i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        var local = (Vector2)ringRect.InverseTransformPoint(patch.TransformPoint(vertex.position)) - ringRect.rect.center;
                        maximum = Mathf.Max(maximum, local.magnitude);
                        if (local.magnitude > ring.InnerRadius + .001f || (i == 0 && local.sqrMagnitude > .000001f))
                            throw new Exception("圆形遮罩未跟随圆圈中心、缩放或动画半径");
                    }
                    if (mesh.currentVertCount < 3 || Mathf.Abs(maximum - ring.InnerRadius) > .001f)
                        throw new Exception("圆形遮罩没有覆盖圆圈内径");
                }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
                var center = Sample(Vector2.zero);
                var corner = Sample(Vector2.one * (-.8f * ring.InnerRadius));
                var outsideRectangle = Sample(Vector2.right * (.9f * ring.InnerRadius));
                if (center.r < .8f || center.b > .2f || corner.b < .8f || corner.r > .2f ||
                    outsideRectangle.b < .8f || outsideRectangle.r > .2f)
                    throw new Exception($"zoom={zoom}, phase={phase}: 圆内应显示红色闪图，圆外及矩形裁剪外必须保留蓝色底图");
            }
            File.WriteAllBytes("Temp/FindDifferences-found-flash-mask.png", pixels.EncodeToPNG());
            File.WriteAllText(report, "PASS: native circular stencil and existing RectMask2D; red center and blue corners; translated patch, shared animated inner radius, zoom 1/2 and two animation phases; mask vertices remain inside the circle.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraNode); UnityEngine.Object.DestroyImmediate(preview);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    [MenuItem("Tools/Find Differences/Check Miss Animation Only")]
    public static void CheckMissAnimationOnly()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var show = typeof(UIDifferences).GetMethod("ShowCross", Private);
            show.Invoke(ui, new object[] { .25f, .75f });
            show.Invoke(ui, new object[] { .6f, .4f });
            foreach (var graphic in new[] { ui.crossTop, ui.crossBottom })
            {
                var animation = graphic as Spine.Unity.SkeletonGraphic;
                if (!animation || animation.raycastTarget || animation.rectTransform.anchorMin != new Vector2(.6f, .6f))
                    throw new Exception("Miss 绑定、点击位置或射线配置不正确");
                var entry = animation.AnimationState.GetCurrent(0);
                if (entry.Animation.Name != "cha" || entry.Loop || !animation.gameObject.activeSelf)
                    throw new Exception("Miss 应播放一次 cha");
                animation.Update(entry.Animation.Duration + .01f);
                if (animation.gameObject.activeSelf) throw new Exception("cha 完成后未隐藏");
            }
            Debug.Log("PASS: both Miss animations restart cha at the new position and hide on completion");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Fix Life Spine Alpha")]
    public static void FixLifeSpineAlpha()
    {
        if (EditorApplication.isPlaying) throw new Exception("请先退出 Play 模式");
        const string path = "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab";
        const string spine = "Assets/Bundles/UI/UIDifferences/Spine/LoveDecode/xin";
        var data = AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(spine + "_SkeletonData.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.esotericsoftware.spine.spine-unity/Runtime/spine-unity/Materials/UI-StraightAlphaTex/SkeletonGraphicDefault-Straight.mat");
        var atlasMaterial = AssetDatabase.LoadAssetAtPath<Material>(spine + "_Material.mat");
        if (!data || !material || !atlasMaterial) throw new Exception("缺少生命值 Spine 资源或 Straight Alpha 材质");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var editing = stage != null && stage.assetPath == path;
        var root = editing ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            var graphics = root.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true).Where(graphic => graphic.skeletonDataAsset == data).ToArray();
            if (graphics.Length != 3) throw new Exception("应有一个生命值和两个错误点击 Spine");
            foreach (var graphic in graphics)
            {
                if (editing) Undo.RecordObject(graphic, "Fix Spine straight alpha");
                graphic.material = material;
                EditorUtility.SetDirty(graphic);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Undo.RecordObject(atlasMaterial, "Fix Spine atlas straight alpha");
            atlasMaterial.SetFloat("_StraightAlphaInput", 1);
            atlasMaterial.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            EditorUtility.SetDirty(atlasMaterial);
            AssetDatabase.SaveAssetIfDirty(atlasMaterial);
            Debug.Log("PASS: 生命值和两个错误点击 Spine 使用 Straight Alpha，图集预览材质同步修正");
        }
        finally { if (!editing) PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Life Animation Only")]
    public static void CheckLifeAnimationOnly()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var heart = ui.play.transform.Find("LifeBadge").GetComponentInChildren<Spine.Unity.SkeletonGraphic>(true);
            if (!heart) throw new Exception("LifeBadge 缺少 Spine 爱心组件");
            heart.Initialize(false);
            typeof(UIDifferences).GetField("lifeAnimation", Private).SetValue(ui, heart);
            var method = typeof(UIDifferences).GetMethod("PlayLifeAnimation", Private);
            method.Invoke(ui, new object[] { false });
            if (heart.AnimationState.GetCurrent(0).Animation.Name != "xin") throw new Exception("正常状态不是 xin");
            for (var i = 0; i < 2; i++)
            {
                method.Invoke(ui, new object[] { true });
                var entry = heart.AnimationState.GetCurrent(0);
                if (entry.Animation.Name != "xin2" || entry.Loop || entry.Next?.Animation.Name != "xin")
                    throw new Exception("扣血应播放一次 xin2，随后回到 xin");
            }
            heart.Update(0);
            heart.Update(heart.AnimationState.GetCurrent(0).Animation.Duration + .01f);
            heart.Update(.01f);
            if (heart.AnimationState.GetCurrent(0).Animation.Name != "xin") throw new Exception("扣血结束未回到 xin");
            method.Invoke(ui, new object[] { true });
            method.Invoke(ui, new object[] { false });
            if (heart.AnimationState.GetCurrent(0).Animation.Name != "xin" || heart.AnimationState.GetCurrent(0).Next != null)
                throw new Exception("重开/复活未清除扣血动画队列");
            Debug.Log("PASS: life xin / xin2 / repeated damage / reset");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Run UI Smoke Checks")]
    public static void Start()
    {
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Pending, true);
        deadline = EditorApplication.timeSinceStartup + 90;
        File.WriteAllText(Path.Combine(Folder, "report.txt"), "RUNNING: waiting for Launcher\n");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Find Differences/Check Spine Progress Play Only")]
    public static void CheckSpineProgressPlayOnly()
    {
        if (running) return;
        SessionState.SetBool(Pending + ".SpineProgressPlay", true);
        Start();
    }

    [MenuItem("Tools/Find Differences/Check Loading Motion Only")]
    public static async void CheckLoadingMotionOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || !Active(ui, "homeDesign"))
        {
            Debug.LogWarning("Enter Play and wait for the home page before checking loading motion.");
            return;
        }
        var layer = (GameObject)Field(ui, "loadingDesign");
        var magnifier = (RectTransform)((GameObject)Field(ui, "loadingMagnifier")).transform;
        var fill = ((Image)Field(ui, "loadingFill")).rectTransform;
        var center = (Vector2)Field(ui, "loadingMagnifierCenter");
        var oldPosition = magnifier.anchoredPosition;
        var oldRotation = magnifier.localRotation;
        var oldFillSize = fill.sizeDelta;
        var oldStarted = Field(ui, "loadingStarted");
        var oldActive = layer.activeSelf;
        var oldSibling = layer.transform.GetSiblingIndex();
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-loading-motion.txt");
        running = true;
        try
        {
            File.WriteAllText(report, "RUNNING: loading motion only; original save and API configuration untouched.\n");
            typeof(UIDifferences).GetField("loadingStarted", Private).SetValue(ui, Time.unscaledTime);
            layer.SetActive(true); layer.transform.SetAsLastSibling();
            await Task.Delay(60);
            var first = magnifier.anchoredPosition;
            var previous = first - center;
            var radius = previous.magnitude;
            var angle = 0f; var travel = 0f; var samples = 0;
            var end = Time.realtimeSinceStartup + 2.05f;
            if (radius < 1) throw new InvalidOperationException("Loading orbit radius must be nonzero.");
            while (Time.realtimeSinceStartup < end)
            {
                await Task.Delay(100);
                if (!ui || !EditorApplication.isPlaying) throw new InvalidOperationException("Play stopped during loading motion check.");
                var offset = magnifier.anchoredPosition - center;
                if (Mathf.Abs(offset.magnitude - radius) > .1f)
                    throw new InvalidOperationException("Loading magnifier did not keep a constant orbit radius.");
                if (Quaternion.Angle(oldRotation, magnifier.localRotation) > .1f)
                    throw new InvalidOperationException("Loading magnifier changed its image orientation.");
                if (previous.x * offset.y - previous.y * offset.x >= 0)
                    throw new InvalidOperationException("Loading magnifier did not move clockwise.");
                angle += Vector2.SignedAngle(previous, offset);
                travel = Mathf.Max(travel, Vector2.Distance(first, magnifier.anchoredPosition));
                previous = offset; samples++;
            }
            if (travel < radius || angle > -300)
                throw new InvalidOperationException("Loading magnifier did not complete the expected circular movement.");
            var result = $"PASS: {samples} samples; constant radius={radius:F2}, clockwise angle={angle:F1}, displacement={travel:F2}; image orientation unchanged.";
            File.AppendAllText(report, result + "\n"); Debug.Log(result);
        }
        catch (Exception error) { File.AppendAllText(report, "FAIL: " + error + "\n"); Debug.LogException(error); }
        finally
        {
            if (ui)
            {
                layer.SetActive(oldActive); layer.transform.SetSiblingIndex(oldSibling);
                magnifier.anchoredPosition = oldPosition; magnifier.localRotation = oldRotation;
                fill.sizeDelta = oldFillSize;
                typeof(UIDifferences).GetField("loadingStarted", Private).SetValue(ui, oldStarted);
                typeof(UIDifferences).GetField("loadingMagnifierCenter", Private).SetValue(ui, center);
            }
            running = false;
            File.AppendAllText(report, "Original loading state restored; save and API configuration untouched.\n");
        }
    }

    [MenuItem("Tools/Find Differences/Check Spine Progress Only")]
    public static async void CheckSpineProgressOnly()
    {
        const string report = "Temp/FindDifferences-spine-progress.txt";
        if (!EditorApplication.isPlaying)
        {
            File.WriteAllText(report, "FAIL: Enter Play mode before checking the isolated Spine progress prefab.");
            Debug.LogError("Enter Play mode before checking the isolated Spine progress prefab.");
            return;
        }
        File.WriteAllText(report, "RUNNING: isolated Spine progress state, capacity and victory animation checks.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        System.Collections.IEnumerator replay = null;
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            object Call(string method, params object[] args) => typeof(UIDifferences).GetMethod(method, Private).Invoke(ui, args);
            void Static(string method, params object[] args) => typeof(UIDifferences).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            void Round(int count, int foundMask = 0)
            {
                ui.spots = Enumerable.Range(0, count).Select(i => new Hotfix.Manager.DifferenceSpot((i % 6 + 1) / 7f, (i / 6 + 1) / 7f)).ToArray();
                typeof(UIDifferences).GetProperty("Round").SetValue(ui, new Hotfix.Manager.DifferenceRound(ui.spots, UIDifferences.ImageAspect, foundMask));
                Call("ResetProgressDots");
            }
            void Advance(float dt)
            {
                foreach (var dot in ui.progressDots)
                    if (dot.gameObject.activeSelf) dot.Update(dt);
            }
            var previewCanvas = root.GetComponent<Canvas>();
            if (!previewCanvas) previewCanvas = root.AddComponent<Canvas>();
            previewCanvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1179, 2556); rect.ForceUpdateRectTransforms();
            ui.play.SetActive(true);
            typeof(UIDifferences).GetField("sound", Private).SetValue(ui, false);
            typeof(UIDifferences).GetField("useSpineFoundRing", Private).SetValue(ui, false);
            Check(ui.progressDots.Length == 15 && ui.progressDots.All(dot => dot && dot.skeletonDataAsset), "All fifteen authored Spine progress dots must be bound.");
            var asset = ui.progressDots[0].skeletonDataAsset;
            Check(AssetDatabase.GetAssetPath(asset).EndsWith("/Dingdan/dingdan_SkeletonData.asset"), "Progress must use the authored dingdan asset.");
            var data = asset.GetSkeletonData(false);
            foreach (var name in new[] { "a1", "a2", "a3", "a4" }) Check(data.FindAnimation(name) != null, "Missing progress animation: " + name);
            Check(data.FindAnimation("a1").Duration == 0 && data.FindAnimation("a3").Duration == 0 &&
                Mathf.Abs(data.FindAnimation("a2").Duration - .6667f) < .001f && Mathf.Abs(data.FindAnimation("a4").Duration - .6667f) < .001f,
                "Progress must retain its authored static poses and full transition durations.");
            Round(3);
            for (var i = 0; i < ui.progressDots.Length; i++)
            {
                var dot = ui.progressDots[i];
                Check(dot.UnscaledTime && !dot.raycastTarget && dot.layoutScaleMode == Spine.Unity.SkeletonGraphic.LayoutMode.WidthControlsHeight &&
                    dot.gameObject.activeSelf == (i < 3) && dot.AnimationState.GetCurrent(0).Animation.Name == "a1",
                    "New-round progress must be a1, unscaled, non-interactive and responsive: " + i);
            }
            var first = ui.progressDots[0];
            Static("SyncProgressDot", first, true);
            var entry = first.AnimationState.GetCurrent(0);
            Check(entry.Animation.Name == "a2" && !entry.Loop && entry.MixDuration == 0 && entry.Next != null &&
                entry.Next.Animation.Name == "a3" && entry.Next.MixDuration == 0 && Mathf.Abs(entry.Next.Delay - entry.Animation.Duration) < .001f,
                "A found dot must play all of a2 before entering a3 without mixing.");
            Static("SyncProgressDot", first, true);
            Check(ReferenceEquals(entry, first.AnimationState.GetCurrent(0)), "Repeated HUD refresh must not restart a2.");
            Advance(entry.Animation.Duration - .01f);
            Check(first.AnimationState.GetCurrent(0).Animation.Name == "a2", "a2 must remain visible until its full duration has elapsed.");
            Advance(.02f);
            Check(first.AnimationState.GetCurrent(0).Animation.Name == "a2" && first.AnimationState.GetCurrent(0).IsComplete,
                "Spine retains the completed transition until the next frame applies its queued animation.");
            Advance(0);
            Check(first.AnimationState.GetCurrent(0).Animation.Name == "a3", "a2 must settle on a3.");
            var settled = first.AnimationState.GetCurrent(0); Static("SyncProgressDot", first, true);
            Check(ReferenceEquals(settled, first.AnimationState.GetCurrent(0)), "Refreshing a found dot must preserve a3.");
            Round(3, 2);
            Check(first.AnimationState.GetCurrent(0).Animation.Name == "a3" && ui.progressDots[1].AnimationState.GetCurrent(0).Animation.Name == "a1" &&
                first.AnimationState.GetCurrent(0).Next == null, "Restored progress must use Count slots and show a3 directly without replaying a2.");
            Round(3);
            Check(ui.progressDots.Take(3).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a1"), "A new round must clear restored progress.");
            foreach (var count in new[] { 25, 30 })
            {
                Call("EnsureSpotCapacity", count); Round(count);
                var row = (RectTransform)ui.progressDots[0].transform.parent;
                LayoutRebuilder.ForceRebuildLayoutImmediate(row);
                var horizontal = row.GetComponent<HorizontalLayoutGroup>();
                Check(ui.progressDots.Length == count && ui.progressDots.All(dot => dot && dot.skeletonDataAsset == asset && dot.transform.parent == row),
                    "Dynamic progress capacity must clone the Spine asset and preserve row parenting: " + count);
                Check(ui.progressDots[0].rectTransform.rect.width * count + horizontal.spacing * (count - 1) + horizontal.padding.horizontal <= row.rect.width + .1f,
                    "Expanded Spine progress must fit its row: " + count);
                var dot = ui.progressDots[count - 1]; dot.Update(0); dot.UpdateMesh();
                var pixelsPerUnit = dot.canvas ? dot.canvas.referencePixelsPerUnit : 100;
                Check(Mathf.Abs(dot.MeshScale - pixelsPerUnit * dot.rectTransform.rect.width / 68) < .01f,
                    "Spine mesh must shrink with its RectTransform instead of overflowing expanded progress.");
            }
            Round(3, 7);
            var end = (System.Collections.IEnumerator)Call("EndRound", true, 0);
            try
            {
                Check(end.MoveNext() && end.Current is System.Collections.IEnumerator,
                    "Winning must start the progress sweep before waiting for the found flight.");
            }
            finally { (end as IDisposable)?.Dispose(); }
            ui.spots[0].x = .5f; ui.spots[1].x = .8f; ui.spots[2].x = .2f;
            var ringOrder = new[] { 2, 0, 1 };
            foreach (var marker in ui.topRings.Take(3).Concat(ui.bottomRings.Take(3)))
            {
                marker.gameObject.SetActive(true);
                Call("ShowFoundRing", marker, false);
            }
            var ringElapsed = typeof(DifferenceFoundRing).GetField("elapsed", Private);
            var progress = ui.progressDots[0].transform.parent;
            var progressParticleCount = progress.GetComponentsInChildren<ParticleSystem>(true).Length;
            void CheckProgressOnly()
            {
                Check(!ui.play.transform.Find("CompletionSweep") && progress.GetComponentsInChildren<ParticleSystem>(true).Length == progressParticleCount,
                    "The upper progress row must not create a CompletionSweep or extra particles.");
            }
            var pending = (bool[])Field(ui, "pendingProgress");
            pending[0] = pending[2] = true;
            Static("PlayProgressTransition", first, "a2");
            Static("PlayProgressAnimation", ui.progressDots[2], "a1");
            replay = (System.Collections.IEnumerator)Call("ReplayCompletedProgress");
            for (var i = 0; i < 3; i++)
            {
                Check(replay.MoveNext() && replay.Current is WaitForSecondsRealtime wait && Mathf.Abs(wait.waitTime - (i + 1) * .065f) < .001f,
                    "Victory progress must schedule .065-second starts against one clock, avoiding accumulated frame delays.");
                for (var j = 0; j < 3; j++)
                {
                    Check(ui.progressDots[j].AnimationState.GetCurrent(0).Animation.Name == (j <= i ? "a4" : j == 2 ? "a1" : "a3"),
                        "Victory animation must start left to right while found flights and a2 are still pending: " + j);
                    var replayed = Array.IndexOf(ringOrder, j) <= i;
                    foreach (var marker in new[] { ui.topRings[j], ui.bottomRings[j] })
                    {
                        var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
                        Check((float)ringElapsed.GetValue(ring) == (replayed ? 0 : ui.foundRingStyle.animationDuration),
                            "Both image rings must restart in spot order while later rings remain settled: " + j);
                        if (replayed && ui.foundRingEffectPrefab)
                        {
                            var particles = marker.transform.Find("FoundRingParticles");
                            Check(particles && particles.gameObject.activeSelf && particles.GetComponentsInChildren<ParticleSystem>(true).Length > 0,
                                "Replayed image rings must retain their own found-ring particles: " + j);
                        }
                    }
                }
                var sweep = ui.progressDots[i].AnimationState.GetCurrent(0);
                Call("UpdateHUD");
                Check(ReferenceEquals(sweep, ui.progressDots[i].AnimationState.GetCurrent(0)),
                    "HUD refresh must preserve a4 while its found flight is pending: " + i);
                if (pending[i])
                {
                    pending[i] = false;
                    Call("UpdateHUD");
                    Check(ReferenceEquals(sweep, ui.progressDots[i].AnimationState.GetCurrent(0)),
                        "Flight arrival HUD refresh must not interrupt an already started a4: " + i);
                }
                CheckProgressOnly();
                Advance(.065f);
            }
            Check(replay.MoveNext(), "Victory replay must wait for the last a4 instead of ending after the final stagger.");
            var last = ui.progressDots[2].AnimationState.GetCurrent(0);
            Advance(last.Animation.Duration - last.TrackTime - .01f);
            Check(replay.MoveNext(), "Victory replay must wait until the last a4 has actually completed.");
            Advance(.02f);
            Check(replay.MoveNext(), "Victory must wait for the queued a3 even when the final a4 is already complete.");
            Advance(0);
            Check(!replay.MoveNext() && ui.progressDots.Take(3).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a3"),
                "All victory dots must settle on a3 before replay completes.");
            (replay as IDisposable)?.Dispose(); replay = null;
            await Task.Delay(50);
            CheckProgressOnly();
            replay = (System.Collections.IEnumerator)Call("ReplayCompletedProgress");
            Check(replay.MoveNext() && ui.progressDots[0].AnimationState.GetCurrent(0).Animation.Name == "a4", "Cancellation check must start the first progress animation.");
            CheckProgressOnly();
            (replay as IDisposable)?.Dispose(); replay = null;
            await Task.Delay(50);
            CheckProgressOnly();
            Check(ui.progressDots.Skip(1).Take(2).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a3"), "Cancelling victory progress must not start the remaining dots.");
            File.WriteAllText(report, "PASS: fifteen authored Spine bindings; a1/a2/a3 states and full transitions; refresh does not restart; restored/new rounds; 25/30-dot cloning and responsive mesh scale; a4 starts while flights/a2 are pending and survives HUD/arrival refresh; overlapping .065s a4 starts left to right, waits for final completion and returns to a3; no extra upper-progress particles or CompletionSweep; lower image rings replay in spot order with their own particles; cancellation stops remaining progress starts. No settings or network touched.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { (replay as IDisposable)?.Dispose(); PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Find Differences/Check Progress Row Only")]
    public static void CheckProgressRowOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || ui.Round == null || !ui.play.activeInHierarchy)
        {
            Debug.LogWarning("Enter Play and load a level before checking the progress row.");
            return;
        }
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-progress-row.txt");
        try
        {
            var row = (RectTransform)ui.progressDots[0].transform.parent;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            var previousRight = row.rect.xMin;
            var centerY = ui.progressDots[0].rectTransform.localPosition.y;
            for (var i = 0; i < ui.Round.Total; i++)
            {
                var dot = ui.progressDots[i].rectTransform;
                var left = row.InverseTransformPoint(dot.TransformPoint(dot.rect.min));
                var right = row.InverseTransformPoint(dot.TransformPoint(dot.rect.max));
                if (!dot.gameObject.activeInHierarchy || dot.parent != row || Mathf.Abs(dot.localPosition.y - centerY) > .1f ||
                    left.x < previousRight - .1f || right.x > row.rect.xMax + .1f ||
                    left.y < row.rect.yMin - .1f || right.y > row.rect.yMax + .1f)
                    throw new InvalidOperationException("Progress dot is hidden, wrapped, overlapping or outside the row: " + i);
                previousRight = right.x;
            }
            var result = $"PASS: {ui.Round.Total} dots in one row; size={ui.progressDots[0].rectTransform.sizeDelta}, row width={row.rect.width:F2}; no overlap or overflow.";
            File.WriteAllText(report, result + "\n"); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error + "\n"); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Capture Arrival Flash")]
    public static async void CaptureArrivalFlash()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) return;
        ui.StartLevel();
        await Wait(() => ui.play.activeInHierarchy && ui.Round != null && !ui.IsLoadingLevel, "level opens");
        var parent = (RectTransform)ui.play.transform;
        DifferenceFoundFlight.Clear(ui.play);
        var source = ui.upperImage.rectTransform;
        DifferenceFoundFlight.Launch(parent, source.TransformPoint(source.rect.center), ui.progressDots[0].rectTransform, null, ui.foundFlightPrefab);
        var flight = parent.GetComponentInChildren<DifferenceFoundFlight>();
        foreach (var particle in flight.GetComponentsInChildren<ParticleSystem>()) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var camera = ui.GetComponentInParent<Canvas>().worldCamera;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var rt = new RenderTexture(Screen.width, Screen.height, 24);
        var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try
        {
            foreach (var state in new[] { "before", "flash" })
            {
                if (state == "flash") typeof(DifferenceFoundFlight).GetMethod("Advance", Private).Invoke(flight, new object[] { .54f });
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                File.WriteAllBytes("Temp/arrival-" + state + ".png", texture.EncodeToPNG());
            }
            Debug.Log($"Arrival flash: cull={flight.canvasRenderer.cull}, rect={flight.rectTransform.rect}");
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(texture);
            DifferenceFoundFlight.Clear(ui.play);
        }
    }

    [MenuItem("Tools/Find Differences/Check Prefab Flight Only")]
    public static async void CheckPrefabFlightOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui) return;
        running = true;
        var calls = 0;
        try
        {
            ui.StartLevel();
            await Wait(() => ui.play.activeInHierarchy && ui.Round != null && !ui.IsLoadingLevel, "level opens");
            var parent = (RectTransform)ui.play.transform;
            var source = ui.upperImage.rectTransform;
            var target = ui.progressDots[0].rectTransform;
            DifferenceFoundFlight.Launch(parent, source.TransformPoint(source.rect.center), target, () => calls++, ui.foundFlightPrefab);
            var flight = parent.GetComponentInChildren<DifferenceFoundFlight>();
            if (!flight.GetComponent<CanvasRenderer>()) throw new InvalidOperationException("Arrival flash CanvasRenderer missing.");
            var systems = flight.GetComponentsInChildren<ParticleSystem>();
            if (systems.Length != ui.foundFlightPrefab.GetComponentsInChildren<ParticleSystem>(true).Length || systems.Length == 0)
                throw new InvalidOperationException("Prefab particles missing.");
            await Task.Delay(220);
            var count = 0;
            var canvas = ui.GetComponentInParent<Canvas>();
            foreach (var system in systems)
            {
                count += system.particleCount;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (!renderer.sharedMaterial.shader.isSupported || renderer.sortingOrder <= canvas.sortingOrder ||
                    (canvas.worldCamera.cullingMask & (1 << system.gameObject.layer)) == 0)
                    throw new InvalidOperationException("Particle shader, sorting or camera layer invalid.");
            }
            if (count == 0 || calls != 0) throw new InvalidOperationException("No visible particles or callback too early.");
            ScreenCapture.CaptureScreenshot("Temp/FindDifferences-prefab-flight.png");
            await Task.Delay(500);
            if (calls != 1) throw new InvalidOperationException("Arrival callback did not occur exactly once.");
            await Task.Delay(3500);
            if (flight) throw new InvalidOperationException("Flight did not clean up.");
            DifferenceFoundFlight.Launch(parent, source.position, target, () => calls++, ui.foundFlightPrefab);
            DifferenceFoundFlight.Clear(ui.play);
            await Task.Delay(700);
            if (calls != 1) throw new InvalidOperationException("Cancelled flight invoked callback.");
            DifferenceFoundFlight.Launch(parent, source.position, target, () => calls++, ui.foundFlightPrefab);
            flight = parent.GetComponentInChildren<DifferenceFoundFlight>();
            var advance = typeof(DifferenceFoundFlight).GetMethod("Advance", Private);
            var arrivalField = typeof(DifferenceFoundFlight).GetField("arrivalEffect", Private);
            if (arrivalField.GetValue(flight) != null) throw new InvalidOperationException("Arrival effect appeared before arrival.");
            advance.Invoke(flight, new object[] { .53f });
            var arrival = (Transform)arrivalField.GetValue(flight);
            if (!arrival || calls != 1 || !arrival.name.StartsWith(ui.foundArrivalPrefab.name))
                throw new InvalidOperationException("Missing arrival prefab before progress update.");
            if (Vector3.Distance(arrival.position, target.TransformPoint(target.rect.center)) > .01f)
                throw new InvalidOperationException("Arrival effect is not centered on target.");
            foreach (var particle in arrival.GetComponentsInChildren<ParticleSystem>())
                if (particle.main.loop || !particle.main.useUnscaledTime || !particle.isPlaying)
                    throw new InvalidOperationException("Arrival particles must play once with unscaled time.");
            advance.Invoke(flight, new object[] { .4f });
            if (arrivalField.GetValue(flight) != arrival || calls != 2)
                throw new InvalidOperationException("Arrival effect repeated or callback failed.");
            typeof(DifferenceFoundFlight).GetMethod("Advance", Private).Invoke(flight, new object[] { 10f });
            if (calls != 2) throw new InvalidOperationException("Long frame lost arrival callback.");
            File.WriteAllText("Temp/FindDifferences-prefab-flight.txt", $"PASS: {systems.Length} prefab particle systems, {count} particles; shader/camera/sorting, arrival callback and cleanup verified.");
        }
        catch (Exception error) { File.WriteAllText("Temp/FindDifferences-prefab-flight.txt", "FAIL: " + error); Debug.LogException(error); }
        finally { running = false; }
    }

    [MenuItem("Tools/Find Differences/Check GM Completion Only")]
    public static async void CheckGMCompletionOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui) return;
        running = true;
        var settings = GameApp.Setting;
        var saved = new Dictionary<string, int>();
        foreach (var key in IntKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetInt(UIDifferences.Key + key);
        var hadContent = settings.HasSetting(UIDifferences.Key + "RoundContent");
        var content = settings.GetString(UIDifferences.Key + "RoundContent", "");
        var url = ui.levelApiUrl;
        var testing = (bool)Field(ui, "testing");
        try
        {
            ui.ShowHome();
            foreach (var key in new[] { "Completed", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint", "RoundContent" }) settings.RemoveSetting(UIDifferences.Key + key);
            ui.levelApiUrl = ""; ui.OnOpen(null); ui.SetTesting(true); ui.StartLevel();
            var coins = ui.Coins;
            DifferenceGMWindow.Complete(ui);
            await Wait(() => Active(ui, "victoryDesign"), "GM completion shows victory");
            if (!ui.Round.Complete || ui.Completed != 1 || ui.Coins != coins + 10 || DifferenceGMWindow.CanComplete(ui))
                throw new InvalidOperationException("GM completion did not use normal settlement.");
            DifferenceGMWindow.Complete(ui);
            if (ui.Coins != coins + 10) throw new InvalidOperationException("Repeated GM click awarded twice.");
            File.WriteAllText("Temp/FindDifferences-gm.txt", "PASS: complete round, normal victory/reward/progress; repeated click ignored; original save restored.");
        }
        catch (Exception error) { File.WriteAllText("Temp/FindDifferences-gm.txt", "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            if (ui) ui.ShowHome();
            foreach (var key in IntKeys) { if (saved.TryGetValue(key, out var value)) settings.SetInt(UIDifferences.Key + key, value); else settings.RemoveSetting(UIDifferences.Key + key); }
            if (hadContent) settings.SetString(UIDifferences.Key + "RoundContent", content); else settings.RemoveSetting(UIDifferences.Key + "RoundContent");
            settings.Save();
            if (ui) { ui.levelApiUrl = url; ui.SetTesting(testing); ui.OnOpen(null); }
            running = false;
        }
    }

    [MenuItem("Tools/Find Differences/Check Baked Pages Only")]
    public static async void CheckBakedPagesOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui) return;
        running = true;
        var report = "Temp/FindDifferences-baked-pages.txt";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            ui.SetTesting(true);
            var names = new[] { "FigmaHome", "FigmaLoading", "FigmaSettings", "FigmaFail", "FigmaHint", "FigmaVictory" };
            foreach (var name in names)
            {
                var saved = prefab.transform.Find("Stage/" + name);
                var live = ui.stage.Find(name);
                if (!saved || !live || saved.GetComponentsInChildren<Transform>(true).Length != live.GetComponentsInChildren<Transform>(true).Length)
                    throw new InvalidOperationException("Page missing or created at runtime: " + name);
                var a = (RectTransform)saved; var b = (RectTransform)live;
                if (a.anchoredPosition != b.anchoredPosition || a.sizeDelta != b.sizeDelta || a.localScale != b.localScale)
                    throw new InvalidOperationException("Authored page layout overwritten: " + name);
            }
            ui.ShowHome();
            ((Button)Field(ui, "designSettingsButton")).onClick.Invoke();
            await Task.Delay(400);
            if (!Active(ui, "settingsDesign")) throw new InvalidOperationException("Settings did not open.");
            ((Button)Field(ui, "designSettingsClose")).onClick.Invoke();
            if (Active(ui, "settingsDesign")) throw new InvalidOperationException("Settings did not close.");
            ui.SetTesting(false);
            ((Button)Field(ui, "designStartButton")).onClick.Invoke();
            if (!Active(ui, "loadingDesign")) throw new InvalidOperationException("Loading did not open.");
            await Wait(() => ui.Round != null && !ui.IsLoadingLevel, "baked loading opens gameplay");
            ui.SetTesting(true);
            foreach (var won in new[] { true, false })
            {
                typeof(UIDifferences).GetMethod("ShowFigmaResult", Private).Invoke(ui, new object[] { won });
                if (won)
                {
                    var nextLabel = ((Button)Field(ui, "designNextButton")).GetComponentInChildren<Text>(true);
                    var levelField = typeof(UIDifferences).GetField("level", Private);
                    var limit = (int)typeof(UIDifferences).GetProperty("LevelLimit", Private).GetValue(ui);
                    var savedLevel = ui.SelectedLevel;
                    try
                    {
                        foreach (var index in new[] { 0, limit - 1, savedLevel })
                        {
                            levelField.SetValue(ui, index);
                            typeof(UIDifferences).GetMethod("ShowFigmaResult", Private).Invoke(ui, new object[] { true });
                            if (nextLabel.text != (index + 1 < limit ? "Level " + (index + 2) : "Home"))
                                throw new Exception("胜利按钮未显示下一关编号，或最后一关未显示 Home");
                        }
                    }
                    finally { levelField.SetValue(ui, savedLevel); }
                }
                await Task.Delay(400);
                if (!Active(ui, won ? "victoryDesign" : "failDesign")) throw new InvalidOperationException("Result page missing.");
                typeof(UIDifferences).GetMethod("CloseFigmaResult", Private).Invoke(ui, null);
            }
            var hint = (GameObject)Field(ui, "hintDesign");
            hint.SetActive(true);
            await Task.Delay(400);
            ((Button)Field(ui, "designHintClose")).onClick.Invoke();
            if (hint.activeSelf) throw new InvalidOperationException("Hint did not close.");
            File.WriteAllText(report, "PASS: six serialized pages, authored root layouts, settings/start/loading/results/hint interactions.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { if (ui) { ui.ShowHome(); ui.SetTesting(false); } running = false; }
    }

    [MenuItem("Tools/Find Differences/Check Wide Screen Controls Only")]
    public static void CheckWideScreenControlsOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) return;
        var parent = (RectTransform)ui.play.transform;
        var size = parent.sizeDelta;
        var controls = (RectTransform[])Field(ui, "widthControls");
        var baseline = (Vector2[])Field(ui, "widthControlPositions");
        var anchors = Array.ConvertAll(controls, r => new Vector4(r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y));
        var align = typeof(UIDifferences).GetMethod("AlignWideScreenControls", Private);
        var report = "";
        try
        {
            foreach (var width in new[] { 1179f, 1917f, 2048f, 1179f })
            {
                parent.sizeDelta += new Vector2(width - parent.rect.width, 0);
                parent.ForceUpdateRectTransforms();
                align.Invoke(ui, null); align.Invoke(ui, null);
                for (var i = 0; i < controls.Length; i++)
                {
                    var r = controls[i];
                    if (anchors[i] != new Vector4(r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y) || r.anchoredPosition.y != baseline[i].y)
                        throw new InvalidOperationException("Anchors or vertical layout changed.");
                    var anchor = Mathf.Lerp(r.anchorMin.x, r.anchorMax.x, r.pivot.x);
                    var movement = (anchor - .5f) * (width - 1179) + r.anchoredPosition.x - baseline[i].x;
                    var expected = (anchor - .5f) * (width - 1179) * ui.wideScreenControlMovement;
                    if (Mathf.Abs(movement - expected) > .01f) throw new InvalidOperationException("Incorrect proportional movement or accumulated drift.");
                    report += $"width={width}, {r.name} movement={movement:F2}\n";
                }
            }
            File.WriteAllText("Temp/FindDifferences-wide-controls.txt", "PASS: baseline, proportional movement, unchanged anchors and repeated resize.\n" + report);
        }
        catch (Exception error) { File.WriteAllText("Temp/FindDifferences-wide-controls.txt", "FAIL: " + error); Debug.LogException(error); }
        finally { parent.sizeDelta = size; parent.ForceUpdateRectTransforms(); align.Invoke(ui, null); }
    }

    [MenuItem("Tools/Find Differences/Check Zoom Reset Only")]
    public static async void CheckZoomResetOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-zoom-reset.txt");
        running = true;
        try
        {
            ui.StartLevel();
            await Wait(() => ui.play.activeInHierarchy && ui.Round != null && !ui.IsLoadingLevel, "real level opens");
            var board = ui.upperImage.GetComponent<DifferenceBoard>();
            var button = ui.stage.Find("Play/Zoom").GetComponent<Button>();
            var update = typeof(UIDifferences).GetMethod("LateUpdate", Private);
            ui.SetTesting(true);
            board.ResetView(); update.Invoke(ui, null);
            if (button.gameObject.activeSelf) throw new InvalidOperationException("Reset button visible at original size.");
            button.onClick.Invoke();
            if (board.IsResetting || ui.upperImage.transform.localScale.x != 1)
                throw new InvalidOperationException("Reset button zoomed in at original size.");
            board.SetZoom(2); update.Invoke(ui, null);
            if (!button.gameObject.activeSelf) throw new InvalidOperationException("Reset button hidden while zoomed in.");
            button.onClick.Invoke();
            var started = (float)typeof(DifferenceBoard).GetField("resetStarted", Private).GetValue(board);
            if (!board.IsResetting || ui.upperImage.transform.localScale.x != 2)
                throw new InvalidOperationException("Reset snapped instead of animating.");
            button.onClick.Invoke();
            if ((float)typeof(DifferenceBoard).GetField("resetStarted", Private).GetValue(board) != started)
                throw new InvalidOperationException("Repeated click restarted reset.");
            var samples = 0;
            while (board.IsResetting)
            {
                await Task.Delay(30);
                if (!ui || !EditorApplication.isPlaying) throw new InvalidOperationException("Play stopped.");
                var zoom = ui.upperImage.transform.localScale.x;
                var expected = Mathf.Lerp(2, 1, (Time.unscaledTime - started) / .45f);
                if (Mathf.Abs(zoom - expected) > .12f || Mathf.Abs(zoom - ui.lowerImage.transform.localScale.x) > .0001f)
                    throw new InvalidOperationException("Reset is not linear or boards are not synchronized.");
                if (zoom > 1.01f && zoom < 1.99f) samples++;
                if (Time.unscaledTime - started > 2) throw new InvalidOperationException("Reset did not finish.");
            }
            update.Invoke(ui, null);
            if (samples == 0 || ui.upperImage.transform.localScale.x != 1 || button.gameObject.activeSelf)
                throw new InvalidOperationException("Reset did not animate to original size and hide the button.");
            board.SetZoom(2); board.ResetViewAnimated(); board.ResetView();
            if (board.IsResetting) throw new InvalidOperationException("Immediate level reset did not cancel animation.");
            var result = $"PASS: {samples} intermediate samples; linear 0.45s reset, synchronized boards, visibility, repeated click and level reset checked.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { if (ui) ui.SetTesting(false); running = false; }
    }

    [MenuItem("Tools/Find Differences/Check Gameplay Layout Only")]
    public static async void CheckGameplayLayoutOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-gameplay-layout.txt");
        running = true;
        try
        {
            ui.StartLevel();
            await Wait(() => ui.play.activeInHierarchy && ui.Round != null && !ui.IsLoadingLevel, "real level opens");
            Canvas.ForceUpdateCanvases();
            var progress = (RectTransform)ui.progressDots[0].transform.parent;
            var board = (RectTransform)ui.upperImage.transform.parent;
            var progressBottom = ui.stage.InverseTransformPoint(progress.TransformPoint(progress.rect.min)).y;
            var boardTop = ui.stage.InverseTransformPoint(board.TransformPoint(board.rect.max)).y;
            VerifyPrefabLayout(ui);
            if (boardTop >= progressBottom)
                throw new InvalidOperationException("The prefab places the board over the progress row.");
            var lower = (RectTransform)ui.lowerImage.transform.parent;
            var upperBottom = ui.stage.InverseTransformPoint(board.TransformPoint(board.rect.min)).y;
            var lowerTop = ui.stage.InverseTransformPoint(lower.TransformPoint(lower.rect.max)).y;
            var lowerBottom = ui.stage.InverseTransformPoint(lower.TransformPoint(lower.rect.min)).y;
            var hint = (RectTransform)ui.hintButton.transform;
            var hintTop = ui.stage.InverseTransformPoint(hint.TransformPoint(hint.rect.max)).y;
            if (!ui.upperImage.gameObject.activeInHierarchy || !ui.lowerImage.gameObject.activeInHierarchy ||
                !ui.upperImage.sprite || !ui.lowerImage.sprite || lowerTop > upperBottom + .1f ||
                hintTop > lowerBottom || lowerBottom < ui.stage.rect.yMin)
                throw new InvalidOperationException("Boards are missing, overlapping, outside the stage or covered by the hint button.");
            typeof(UIDifferences).GetMethod("ShowCross", Private).Invoke(ui, new object[] { .5f, .5f });
            if (!ui.crossTop.gameObject.activeSelf || !ui.crossBottom.gameObject.activeSelf)
                throw new InvalidOperationException("Bound miss feedback did not appear.");
            await Wait(() => !ui.crossTop.gameObject.activeSelf && !ui.crossBottom.gameObject.activeSelf,
                "Bound miss feedback clears after its animation completes.");
            var result = $"PASS: real level {ui.SelectedLevel + 1}, {ui.Round.Total} dots; board below progress by {progressBottom - boardTop:F2}; authored prefab layout and UI sprites preserved.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
        finally { running = false; }
    }

    [MenuItem("Tools/Find Differences/Check UI Initialization Only")]
    public static void CheckInitializationOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-initialization.txt");
        try
        {
            if (!(bool)Field(ui, "interactionsBound") || !(Field(ui, "hintHand") is DifferenceHintHand) ||
                !(Field(ui, "hintSpotlight") is DifferenceHintSpotlight) || !(Field(ui, "homeDesign") is GameObject))
                throw new InvalidOperationException("UI initialization did not finish.");
            var stage = ui.stage;
            var min = stage.anchorMin; var max = stage.anchorMax; var size = stage.sizeDelta;
            var scale = stage.localScale; var position = stage.anchoredPosition;
            var update = typeof(UIDifferences).GetMethod("LateUpdate", Private);
            try
            {
                stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f); stage.sizeDelta = Vector2.zero;
                stage.ForceUpdateRectTransforms(); update.Invoke(ui, null);
                if (stage.localScale != scale) throw new InvalidOperationException("Zero-sized stage changed scale.");
                typeof(UIDifferences).GetField("interactionsBound", Private).SetValue(ui, false);
                update.Invoke(ui, null);
            }
            finally
            {
                stage.anchorMin = min; stage.anchorMax = max; stage.sizeDelta = size;
                stage.localScale = scale; stage.anchoredPosition = position;
                typeof(UIDifferences).GetField("interactionsBound", Private).SetValue(ui, true);
            }
            update.Invoke(ui, null);
            if (stage.rect.width <= 0 || stage.rect.height <= 0 || float.IsNaN(stage.localScale.x) || float.IsInfinity(stage.localScale.x))
                throw new InvalidOperationException("Invalid runtime stage dimensions or scale.");
            VerifyPrefabLayout(ui);
            var result = "PASS: generated bindings and UI initialization succeeded; hint effects created; zero-size layout and pre-initialization updates are safe; live Stage scale=1, full-screen with zero offsets; prefab gameplay bindings and full background preserved.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
    }

    [MenuItem("Tools/Find Differences/Check Board Coordinates Only")]
    public static void CheckBoardCoordinatesOnly()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (running || !EditorApplication.isPlaying || !ui || ui.IsLoadingLevel || ui.Round == null) return;
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-board-coordinates.txt");
        try
        {
            var data = (DifferenceLevel)typeof(UIDifferences).GetProperty("CurrentLevel", Private).GetValue(ui);
            var originals = (Image[])Field(ui, "originalPatchImages");
            var flashes = (Image[])Field(ui, "changedFlashImages");
            foreach (var board in new[] { ui.upperImage, ui.lowerImage })
            {
                var rect = board.rectTransform;
                var size = rect.sizeDelta; var pivot = rect.pivot; var position = rect.anchoredPosition; var scale = rect.localScale;
                try
                {
                    for (var variant = 0; variant < 3; variant++)
                    {
                        if (variant > 0)
                        {
                            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, variant == 1 ? 800 : 360);
                            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, variant == 1 ? 350 : 640);
                            rect.pivot = variant == 1 ? new Vector2(.31f, .73f) : new Vector2(.5f, .5f);
                            rect.localScale = variant == 1 ? new Vector3(1.7f, .8f, 1) : Vector3.one;
                        }
                        rect.ForceUpdateRectTransforms();
                        for (var i = 0; i < data.regions.Length; i++)
                        {
                            var spot = ui.spots[i];
                            var marker = (board == ui.upperImage ? ui.topRings : ui.bottomRings)[i].rectTransform;
                            AssertBoardPoint(rect, marker.TransformPoint(marker.rect.center), new Vector2(spot.x, spot.y), "found ring");
                            foreach (var pictures in new[] { ui.patchImages, originals, flashes })
                            {
                                var picture = pictures[i].rectTransform;
                                var crop = (RectTransform)picture.parent;
                                if (crop.parent != rect) continue;
                                var r = data.regions[i];
                                AssertBoardPoint(rect, crop.TransformPoint(new Vector2(crop.rect.xMin, crop.rect.yMax)),
                                    new Vector2(r.x - r.z / 2, r.y - r.w / 2), "crop top-left");
                                AssertBoardPoint(rect, crop.TransformPoint(new Vector2(crop.rect.xMax, crop.rect.yMin)),
                                    new Vector2(r.x + r.z / 2, r.y + r.w / 2), "crop bottom-right");
                                var cropped = pictures != originals && data.croppedPatches != null;
                                AssertBoardPoint(rect, picture.TransformPoint(new Vector2(picture.rect.xMin, picture.rect.yMax)),
                                    cropped ? new Vector2(r.x - r.z / 2, r.y - r.w / 2) : Vector2.zero, "picture top-left");
                                AssertBoardPoint(rect, picture.TransformPoint(new Vector2(picture.rect.xMax, picture.rect.yMin)),
                                    cropped ? new Vector2(r.x + r.z / 2, r.y + r.w / 2) : Vector2.one, "picture bottom-right");
                            }
                            var canvas = board.GetComponentInParent<Canvas>();
                            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                            var screen = RectTransformUtility.WorldToScreenPoint(camera, marker.TransformPoint(marker.rect.center));
                            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out var local))
                                throw new InvalidOperationException("Screen click conversion failed.");
                            var point = new Vector2((local.x - rect.rect.xMin) / rect.rect.width, 1 - (local.y - rect.rect.yMin) / rect.rect.height);
                            var round = new Hotfix.Manager.DifferenceRound(new[] {spot}, UIDifferences.ImageAspect);
                            if (round.Click(point.x, point.y) != 0) throw new InvalidOperationException("Visual marker does not match hit coordinates.");
                        }
                        typeof(UIDifferences).GetMethod("ShowCross", Private).Invoke(ui, new object[] {.73f, .29f});
                        var cross = board == ui.upperImage ? ui.crossTop.rectTransform : ui.crossBottom.rectTransform;
                        AssertBoardPoint(rect, cross.TransformPoint(cross.rect.center), new Vector2(.73f, .29f), "miss feedback");
                    }
                }
                finally { rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; rect.localScale = scale; }
            }
            var result = $"PASS: {data.regions.Length} spots on both boards; authored size, 800x350 and 360x640; changed pivots and zoom; crops, flash pictures, rings, screen hit coordinates and miss feedback aligned. Prefab unchanged.";
            File.WriteAllText(report, result); Debug.Log(result);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error); }
    }

    static void AssertBoardPoint(RectTransform board, Vector3 world, Vector2 expected, string label)
    {
        var point = board.InverseTransformPoint(world);
        var normalized = new Vector2((point.x - board.rect.xMin) / board.rect.width, (board.rect.yMax - point.y) / board.rect.height);
        if (Vector2.Distance(normalized, expected) > .001f)
            throw new InvalidOperationException(label + " mismatch: " + normalized + " expected " + expected);
    }

    static void VerifyPrefabLayout(UIDifferences ui)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab").GetComponent<UIDifferences>();
        if (ui.stage.sizeDelta != prefab.stage.sizeDelta) throw new InvalidOperationException("Stage size overwritten at runtime.");
        if (ui.stage.localScale != Vector3.one || ui.stage.anchoredPosition != Vector2.zero ||
            ui.stage.anchorMin != Vector2.zero || ui.stage.anchorMax != Vector2.one ||
            ui.stage.offsetMin != Vector2.zero || ui.stage.offsetMax != Vector2.zero)
            throw new InvalidOperationException("Stage must remain full-screen at scale 1 with zero position and offsets.");
        foreach (var path in new[] { "Play", "Play/Background", "Play/Back", "Play/Zoom", "Play/Hint", "Play/Hint/Bulb", "Play/Frame", "Play/Progress", "Play/UpperViewport", "Play/LowerViewport" })
        {
            var actual = (RectTransform)ui.stage.Find(path);
            var expected = (RectTransform)prefab.stage.Find(path);
            if (path == "Play/Background")
            {
                var aspect = expected.GetComponent<AspectRatioFitter>().aspectRatio;
                if (actual.rect.height <= 0 || !(Mathf.Abs(actual.rect.width / actual.rect.height - aspect) < .001f))
                    throw new InvalidOperationException("Play background no longer preserves its authored aspect ratio.");
                var corners = new Vector3[4];
                ((RectTransform)ui.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = actual.InverseTransformPoint(corner);
                    if (point.x < actual.rect.xMin - .1f || point.x > actual.rect.xMax + .1f ||
                        point.y < actual.rect.yMin - .1f || point.y > actual.rect.yMax + .1f)
                        throw new InvalidOperationException("Play background does not cover the full UI root outside its safe area.");
                }
            }
            // Other layout components legitimately drive runtime RectTransform values.
            else if (!actual.drivenByObject && (actual.anchorMin != expected.anchorMin || actual.anchorMax != expected.anchorMax ||
                actual.pivot != expected.pivot || actual.anchoredPosition != expected.anchoredPosition ||
                actual.sizeDelta != expected.sizeDelta || actual.localScale != expected.localScale))
                throw new InvalidOperationException("Prefab layout overwritten: " + path);
            var image = actual.GetComponent<Image>();
            var source = expected.GetComponent<Image>();
            if (image && source && image.sprite != source.sprite)
                throw new InvalidOperationException("Prefab sprite overwritten: " + path);
        }
    }

    static void Tick()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            var check = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            if (check == "reward") { CheckRewardCoinsOnly(); return; }
            if (check == "found-ring") { CheckFoundRingOnly(); return; }
            if (check == "failure-close") { SessionState.SetBool(Pending + ".FailureClose", true); Start(); return; }
            if (check == "tutorial") { SessionState.SetBool(Pending + ".Tutorial", true); Start(); return; }
            if (check == "spine-progress-play") { CheckSpineProgressPlayOnly(); return; }
            if (check == "spine-alpha") { BuildAndroidSpineShaderCheck(); return; }
            if (check == "startup-await") { CheckStartupAwaitOnly(); return; }
            if (check == "settings-connect") { DifferenceGameBuilder.ConnectSettingsActions(); CheckSettingsActionsOnly(); return; }
            if (check == "settings") { CheckSettingsActionsOnly(); return; }
            if (check == "responsive-connect") { DifferenceGameBuilder.ConnectResponsivePlayLayout(); CheckResponsivePlayLayoutOnly(); return; }
            if (check == "responsive") { CheckResponsivePlayLayoutOnly(); return; }
            if (check == "sdk-check") { DifferenceSdkCheck.Run(); return; }
            if (check == "victory-votes") { CheckVictoryVotesOnly(); return; }
            if (check == "victory-replay") { CheckVictoryReplayOnly(); File.WriteAllText("Temp/victory-replay-check.txt", "PASS: victory Spine loop settings and replay on repeated openings"); return; }
            if (check == "coin-audio-connect") { DifferenceGameBuilder.RefreshUIBindingsOnly(); CheckAudioOnly(); File.WriteAllText("Temp/FindDifferences-coin-audio.txt", "PASS: CoinCreate and all audio bindings verified."); return; }
            if (check == "victory-votes-connect") { DifferenceGameBuilder.RefreshUIBindingsOnly(); CheckVictoryVotesOnly(); return; }
            if (check == "home-entrance") { CheckHomeEntranceOnly(); return; }
            if (check == "home-entrance-connect") { ConnectHomeEntrance(); CheckHomeEntranceOnly(); return; }
            if (check == "sdk-enable") { DifferenceSdkBuild.SetEnabled(true); return; }
            if (check == "sdk-disable") { DifferenceSdkBuild.SetEnabled(false); return; }
            if (check == "board-coordinates") { CheckBoardCoordinatesOnly(); return; }
            if (check == "gameplay-layout") { CheckGameplayLayoutOnly(); return; }
            Start();
        }
        if (!SessionState.GetBool(Pending, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (EditorApplication.isPlaying && !ui && !launcherCaptured &&
            (GameObject.Find("UILauncher(Clone)") || GameObject.Find("UILauncher")))
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "00-launcher.png"));
            launcherCaptured = true;
        }
        if (EditorApplication.isPlaying && ui && GameApp.Setting != null && (bool)Field(ui, "interactionsBound"))
        { SessionState.SetBool(Pending, false); Run(ui); }
        else if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool(Pending, false);
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "FAIL: Launcher did not create UIDifferences in 90 seconds.\n");
            EditorApplication.isPlaying = false;
        }
    }

    static async void Run(UIDifferences ui)
    {
        running = true;
        var saved = new Dictionary<string, object>();
        var settings = GameApp.Setting;
        var oldUrl = ui.levelApiUrl;
        var oldTesting = (bool)Field(ui, "testing");
        var errors = new List<string>();
        Application.LogCallback collect = (message, trace, type) =>
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); };
        Application.logMessageReceived += collect;
        try
        {
            foreach (var key in IntKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetInt(UIDifferences.Key + key);
            foreach (var key in BoolKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetBool(UIDifferences.Key + key);
            foreach (var key in StringKeys) if (settings.HasSetting(UIDifferences.Key + key)) saved[key] = settings.GetString(UIDifferences.Key + key);
            Hotfix.Manager.DifferenceRound.SelfCheck();
            ui.SetTesting(true); ui.ShowHome();
            foreach (var key in IntKeys) settings.RemoveSetting(UIDifferences.Key + key);
            settings.RemoveSetting(UIDifferences.Key + "RoundContent");
            settings.SetInt(UIDifferences.Key + "Coins", 300);
            ui.levelApiUrl = ""; ui.OnOpen(null);
            if (SessionState.GetBool(Pending + ".SpineProgressPlay", false))
            {
                SessionState.SetBool(Pending + ".SpineProgressPlay", false);
                settings.SetBool(UIDifferences.Key + "TutorialComplete", true);
                settings.SetInt(UIDifferences.Key + "Completed", 2);
                settings.SetInt(UIDifferences.Key + "RoundLevel", -1);
                ui.OnOpen(null); ui.SetTesting(false); ui.StartLevel();
                await Wait(() => ui.Round != null && ui.play.activeInHierarchy && !ui.IsLoadingLevel, "local third level opens for Spine progress play check");
                Check(ui.SelectedLevel == 2 && ui.Round.Total == 15 && ui.Round.Count == 0 &&
                    ui.progressDots.Take(ui.Round.Total).All(dot => dot.gameObject.activeInHierarchy && dot.AnimationState.GetCurrent(0).Animation.Name == "a1"),
                    "all fifteen unfound progress dots start on a1");
                var first = ui.progressDots[0];
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
                Check(ui.Round.Count == 1 && ((bool[])Field(ui, "pendingProgress"))[0] && first.AnimationState.GetCurrent(0).Animation.Name == "a1" &&
                    ui.play.GetComponentInChildren<DifferenceFoundFlight>(), "found dot remains a1 while its real progress flight is pending");
                await Wait(() => first.AnimationState.GetCurrent(0).Animation.Name == "a2", "flight arrival starts the found-dot a2 transition");
                var transition = first.AnimationState.GetCurrent(0);
                Check(!transition.Loop && transition.MixDuration == 0 && transition.Next != null && transition.Next.Animation.Name == "a3" &&
                    transition.Next.MixDuration == 0 && Mathf.Abs(transition.Next.Delay - transition.Animation.Duration) < .001f &&
                    Mathf.Abs(transition.Animation.Duration - .6667f) < .001f,
                    "a2 is non-looping and queues a3 after its entire authored duration without mixing");
                await Wait(() => first.AnimationState.GetCurrent(0).Animation.Name == "a3", "found progress settles on a3");
                ui.ShowHome(); ui.StartLevel();
                await Wait(() => ui.Round != null && ui.play.activeInHierarchy && !ui.IsLoadingLevel, "partially completed third level reopens");
                Check(ui.SelectedLevel == 2 && ui.Round.Count == 1 && ui.progressDots[0].AnimationState.GetCurrent(0).Animation.Name == "a3" &&
                    ui.progressDots[0].AnimationState.GetCurrent(0).Next == null &&
                    ui.progressDots.Skip(1).Take(ui.Round.Total - 1).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a1"),
                    "restoring a partial round shows a3 directly and keeps unfound dots on a1");
                for (var i = 1; i < ui.Round.Total; i++) ui.ClickImage(ui.spots[i].x, ui.spots[i].y);
                Check(ui.Round.Complete, "all fifteen real difference clicks complete the local third level");
                await Wait(() => ui.progressDots[0].AnimationState.GetCurrent(0).Animation.Name == "a4", "completed progress starts its leftmost a4 sweep");
                Check(((bool[])Field(ui, "pendingProgress"))[ui.Round.Total - 1] && ui.play.GetComponentInChildren<DifferenceFoundFlight>(),
                    "leftmost a4 starts before the final found flight reveals its progress dot");
                Check(!(bool)Field(ui, "figmaResultActive"), "victory popup waits while progress a4 is playing");
                var sweepStarted = Time.realtimeSinceStartupAsDouble - ui.progressDots[0].AnimationState.GetCurrent(0).TrackTime;
                await Wait(() => (bool)Field(ui, "figmaResultActive"), "victory popup opens after the last progress animation completes");
                var sweepElapsed = Time.realtimeSinceStartupAsDouble - sweepStarted;
                Check((bool)Field(ui, "figmaResultWon") && ui.progressDots.Take(ui.Round.Total).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a3"),
                    "every active progress dot has returned to a3 before victory is displayed: " +
                    string.Join(",", ui.progressDots.Take(ui.Round.Total).Select(dot => dot.AnimationState.GetCurrent(0).Animation.Name)));
                File.AppendAllText(Path.Combine(Folder, "report.txt"),
                    $"Progress sweep elapsed={sweepElapsed:F3}s; authored expectation={(ui.Round.Total - 1) * .065f + .6667f:F3}s (.065s overlapping starts).\n");
                Check(sweepElapsed < (ui.Round.Total - 1) * .065f + .6667f + .4f,
                    "overlapping progress sweep stays near 1.6 seconds without accumulating a frame delay per dot");
                var completedRound = ui.Round;
                await Click((Button)Field(ui, "designNextButton"));
                await Wait(() => ui.home.activeInHierarchy || ui.Round != null && ui.Round != completedRound && !ui.IsLoadingLevel,
                    "victory Next advances or returns home after the last local level");
                if (ui.home.activeInHierarchy) ui.StartLevel();
                await Wait(() => ui.Round != null && ui.Round != completedRound && !ui.Round.Finished && ui.play.activeInHierarchy && !ui.IsLoadingLevel,
                    "the next new round opens after victory");
                Check(ui.Round.Count == 0 && ui.progressDots.Take(ui.Round.Total).All(dot => dot.AnimationState.GetCurrent(0).Animation.Name == "a1"),
                    "a new round resets every active progress dot to a1");
                Check(errors.Count == 0, "Spine progress play check has no runtime errors: " + string.Join(" | ", errors));
                File.AppendAllText(Path.Combine(Folder, "report.txt"), "PASS: real flight a1 -> full a2 -> a3, direct a3 restoration, a4 sweep overlaps the final real flight before victory, and new-round a1.\n");
                return;
            }
            if (SessionState.GetBool(Pending + ".Tutorial", false))
            {
                SessionState.SetBool(Pending + ".Tutorial", false);
                settings.SetBool(UIDifferences.Key + "TutorialComplete", false);
                ui.SetTesting(false);
                typeof(UIDifferences).GetMethod("InitializeTutorial", Private).Invoke(ui, null);
                await Wait(() => ui.Round != null && !ui.IsLoadingLevel, "new player opens first level directly");
                await Task.Delay(500);
                Check(ui.SelectedLevel == 0 && !Active(ui, "homeDesign") && (bool)Field(ui, "tutorialPending") && ui.backButton.gameObject.activeSelf,
                    "tutorial bypasses home and keeps the pause button available");
                await Wait(() => Field(ui, "tutorialShade") is DifferenceHintSpotlight shade && shade.Ready,
                    "first tutorial automatically zooms and focuses before allowing a click");
                Check(!ui.hintButton.interactable &&
                    ((DifferenceHintSpotlight)Field(ui, "tutorialShade")).transform.GetSiblingIndex() < ui.hintButton.transform.GetSiblingIndex() &&
                    ui.hintButton.transform.GetSiblingIndex() < ((RectTransform)Field(ui, "tutorialVisuals")).GetSiblingIndex(),
                    "the disabled hint stays above the dark tutorial shade and below the guide graphics");
                Check(Mathf.Abs(ui.upperImage.transform.localScale.x - 2) < .001f &&
                    ui.lowerImage.transform.localScale == ui.upperImage.transform.localScale,
                    "first tutorial uses the hint spotlight zoom on both images");
                await Capture("tutorial-01-click");
                var inventory = ui.Hints;
                ui.ClickImage(.99f, .02f); ui.UseHint(); ui.ShowHome();
                Check(ui.Round.Count == 0 && ui.Round.Lives == 3 && ui.Hints == inventory && ui.play.activeSelf,
                    "first tutorial blocks wrong clicks, early hints and home navigation");
                void ClickTarget()
                {
                    var index = (int)Field(ui, "tutorialTarget");
                    ui.ClickImage(ui.spots[index].x, ui.spots[index].y);
                }
                void RefreshGuide()
                {
                    typeof(UIDifferences).GetField("tutorialShowAt", Private).SetValue(ui, 0f);
                    typeof(UIDifferences).GetMethod("AdvanceTutorial", Private).Invoke(ui, null);
                }
                ClickTarget();
                Check(ui.Round.Count == 1 && settings.GetInt(UIDifferences.Key + "TutorialStep") == 1,
                    "first hit saves zoom lesson and found progress");
                Check(Mathf.Abs(ui.upperImage.transform.localScale.x - 2) < .001f && !ui.TutorialAllowsView,
                    "first tutorial click starts the tips close animation without snapping back");
                var firstShade = (DifferenceHintSpotlight)Field(ui, "tutorialShade");
                typeof(DifferenceHintSpotlight).GetMethod("Advance", Private).Invoke(firstShade, new object[] { .1f });
                RefreshGuide();
                Check(ui.upperImage.transform.localScale.x > 1 && ui.upperImage.transform.localScale.x < 2 &&
                    ui.lowerImage.transform.localScale == ui.upperImage.transform.localScale &&
                    settings.GetInt(UIDifferences.Key + "TutorialStep") == 1 &&
                    !((RectTransform)Field(ui, "tutorialVisuals")).gameObject.activeSelf,
                    "both images shrink smoothly before the manual zoom guide appears");
                await Task.Delay(500); RefreshGuide();
                Check(ui.upperImage.transform.localScale == Vector3.one && ui.lowerImage.transform.localScale == Vector3.one,
                    "tips close animation restores both images to their original size");
                Check(settings.GetInt(UIDifferences.Key + "TutorialStep") == 1,
                    "automatic first-step zoom does not skip the manual zoom lesson");
                ui.OnOpen(null);
                await Wait(() => ui.Round != null && !ui.IsLoadingLevel, "interrupted tutorial restores directly into first level");
                Check(ui.Round.Count == 1 && (int)settings.GetInt(UIDifferences.Key + "TutorialStep") == 1,
                    "resume preserves found point and tutorial step");
                RefreshGuide();
                Check(ui.tutorialPinchHandSprite && ui.tutorialPinchHandOpenSprite && ui.tutorialZoomArrowSprite, "closed/open pinch hand and zoom arrow sprites are bound");
                var arrows = (RectTransform[])Field(ui, "tutorialZoomArrows");
                var hands = (RectTransform[])Field(ui, "tutorialPinchHands");
                Check(arrows[0].gameObject.activeSelf && arrows[1].gameObject.activeSelf && hands[0].gameObject.activeSelf,
                    "zoom lesson uses one pinch hand and two arrows per image");
                for (var i = 0; i < hands.Length; i++)
                {
                    var viewport = (RectTransform)(i == 0 ? ui.upperImage.transform.parent : ui.lowerImage.transform.parent);
                    Check(Vector3.Distance(hands[i].position, viewport.TransformPoint(viewport.rect.center)) < .01f,
                        "zoom gesture is centered in each image viewport");
                }
                var initialArrowPosition = arrows[0].anchoredPosition;
                void CheckGesture()
                {
                    var opening = Mathf.Repeat(Time.unscaledTime, 2.4f) / 2.4f < .5f;
                    foreach (var hand in hands)
                        Check(hand.localScale == Vector3.one && hand.GetComponent<Image>().sprite ==
                            (opening ? ui.tutorialPinchHandOpenSprite : ui.tutorialPinchHandSprite),
                            "gesture uses the matching open/closed sprite at a fixed scale");
                }
                CheckGesture();
                var initialHandSprite = hands[0].GetComponent<Image>().sprite;
                await Wait(() => { RefreshGuide(); return Vector2.Distance(initialArrowPosition, arrows[0].anchoredPosition) > 1; },
                    "zoom arrows continue animating");
                await Wait(() => { RefreshGuide(); return hands[0].GetComponent<Image>().sprite != initialHandSprite; },
                    "pinch hand switches between open and closed sprites");
                CheckGesture();
                await Capture("tutorial-02-zoom");
                ui.upperImage.GetComponent<DifferenceBoard>().SetZoom(1.5f); RefreshGuide();
                Check(ui.Round.Count == 1 && settings.GetInt(UIDifferences.Key + "TutorialStep") == 3 && ui.hintButton.interactable &&
                    !hands[0].gameObject.activeSelf && !hands[1].gameObject.activeSelf,
                    "actual image zoom goes directly to the clickable hint button lesson");
                ClickTarget();
                Check(ui.Round.Count == 1, "hint button lesson blocks additional difference clicks");
                settings.SetInt(UIDifferences.Key + "TutorialStep", 2);
                ui.OnOpen(null);
                await Wait(() => ui.Round != null && !ui.IsLoadingLevel, "legacy second-difference lesson restores");
                RefreshGuide();
                Check(ui.Round.Count == 1 && settings.GetInt(UIDifferences.Key + "TutorialStep") == 3 && ui.hintButton.interactable,
                    "legacy second-difference save migrates directly to the hint button lesson");
                await Task.Delay(450);
                Check(((DifferenceHintSpotlight)Field(ui, "tutorialShade")).Ready, "hint button lesson mask is ready");
                var hintRect = (RectTransform)ui.hintButton.transform;
                var guidePoints = (RectTransform[])Field(ui, "tutorialPoints");
                Check(Vector3.Distance(guidePoints[0].position, hintRect.TransformPoint(hintRect.rect.center)) < .01f &&
                    !guidePoints[1].gameObject.activeSelf,
                    "third-step hand targets the hint button center regardless of its pivot");
                await Capture("tutorial-03-hint");
                ui.UseHint();
                await Wait(() => ((DifferenceHintSpotlight)Field(ui, "hintSpotlight")).Ready, "tutorial hint opens dual spotlights");
                RefreshGuide();
                Check(ui.Hints == inventory && settings.GetInt(UIDifferences.Key + "TutorialStep") == 4 && ui.hintLabel.text == "∞",
                    "first level hints are unlimited and do not spend inventory");
                ClickTarget();
                Check(settings.GetBool(UIDifferences.Key + "TutorialComplete") && !(bool)Field(ui, "tutorialPending") && ui.Round.Count == 2,
                    "finding hinted target completes tutorial without ending level");
                await Task.Delay(400); ui.UseHint();
                Check(ui.Hints == inventory, "first level remains unlimited after tutorial");
                ui.OnOpen(null);
                await Wait(() => ui.Round != null && !ui.IsLoadingLevel, "completed tutorial still opens first level directly");
                Check(ui.SelectedLevel == 0 && ui.play.activeSelf && !Active(ui, "homeDesign") && !(bool)Field(ui, "tutorialPending") && ui.Round.Count == 2,
                    "first level resumes directly without repeating completed tutorial");
                settings.SetBool(UIDifferences.Key + "TutorialComplete", false);
                settings.SetInt(UIDifferences.Key + "Completed", 1); ui.OnOpen(null);
                Check(ui.home.activeSelf && !(bool)Field(ui, "tutorialPending"), "existing players are not forced into tutorial");
                File.AppendAllText(Path.Combine(Folder, "report.txt"), "PASS: direct tutorial startup, hit/zoom/hint steps, resume, infinite hints and completed/existing-player startup.\n");
                return;
            }
            if (SessionState.GetBool(Pending + ".FailureClose", false))
            {
                SessionState.SetBool(Pending + ".FailureClose", false);
                ui.StartLevel();
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
                for (var i = 0; i < 3; i++) ui.ClickImage(.99f, .02f);
                await Wait(() => Active(ui, "failDesign"), "failure dialog opens with saved progress");
                var failedMask = ui.Round.FoundMask;
                Check(failedMask != 0 && ui.Round.Lives == 0 && settings.GetInt(UIDifferences.Key + "RoundMask") == failedMask,
                    "failure immediately saves found points and zero lives");
                typeof(UIDifferences).GetMethod("OnApplicationPause", Private).Invoke(ui, new object[] { true });
                ui.OnOpen(null); ui.StartLevel();
                Check(ui.Round.FoundMask == failedMask && ui.Round.Lives == 0 && Active(ui, "failDesign"),
                    "reopening without dismissing restores failed progress and dialog");
                typeof(UIDifferences).GetField("adInputLocked", Private).SetValue(ui, true);
                ((Button)Field(ui, "designFailClose")).onClick.Invoke();
                Check(ui.Round != null && Active(ui, "failDesign"), "ad input lock prevents discarding failed progress");
                typeof(UIDifferences).GetField("adInputLocked", Private).SetValue(ui, false);
                ((Button)Field(ui, "designFailClose")).onClick.Invoke();
                Check(ui.home.activeSelf && ui.Round == null && settings.GetInt(UIDifferences.Key + "RoundLevel") == -1,
                    "failure close returns home and clears saved round");
                ui.OnOpen(null); ui.StartLevel();
                Check(ui.Round.Count == 0 && ui.Round.Lives == 3 && !Active(ui, "failDesign"),
                    "reopening after failure close starts the same level fresh");
                ui.ClickImage(ui.spots[0].x, ui.spots[0].y); ui.ShowHome(); ui.StartLevel();
                Check(ui.Round.Count == 1 && ui.Round.Lives == 3, "ordinary return home still preserves active progress");
                File.AppendAllText(Path.Combine(Folder, "report.txt"), "PASS: failure close/restart and untouched failure restoration.\n");
                return;
            }
            await Capture("01-home");
            Check(Active(ui, "homeDesign"), "new home visible");
            VerifyPrefabLayout(ui);
            Check(!ui.shopButton.gameObject.activeInHierarchy && !ui.trophyButton.gameObject.activeInHierarchy,
                "shop and ranking navigation hidden");
            foreach (var oldButton in ui.home.GetComponentsInChildren<Button>(true))
                Check(!oldButton.gameObject.activeInHierarchy, "old home button excluded from pointer and keyboard navigation: " + oldButton.name);

            await Click(((Button)Field(ui, "designSettingsButton")));
            await Task.Delay(350); await Capture("02-settings");
            Check(Active(ui, "settingsDesign"), "new settings visible");
            var oldSound = settings.GetBool(UIDifferences.Key + "Sound", true);
            var soundButton = ui.stage.Find("FigmaSettings/Card/Sound");
            Check(soundButton && soundButton.GetComponent<Button>(), "settings sound button exists");
            await Click(soundButton.GetComponent<Button>());
            Check(settings.GetBool(UIDifferences.Key + "Sound", true) != oldSound, "sound toggle saves new state");
            await Capture("03-settings-toggle");
            await Click(((Button)Field(ui, "designSettingsClose")));

            ui.SetTesting(false);
            await Click(((Button)Field(ui, "designStartButton")));
            Check(Active(ui, "loadingDesign"), "local load shows loading page");
            var spinner = (RectTransform)((GameObject)Field(ui, "loadingMagnifier")).transform;
            await Task.Delay(40);
            var rotation = spinner.localRotation;
            var center = (Vector2)Field(ui, "loadingMagnifierCenter");
            var offset = spinner.anchoredPosition - center;
            await Task.Delay(120);
            var nextOffset = spinner.anchoredPosition - center;
            Check(Vector2.Distance(offset, nextOffset) > 1 && offset.magnitude > 1 &&
                Mathf.Abs(offset.magnitude - nextOffset.magnitude) < .1f &&
                offset.x * nextOffset.y - offset.y * nextOffset.x < 0, "loading magnifier moves clockwise on a circular path");
            Check(Quaternion.Angle(rotation, spinner.localRotation) < .1f, "loading magnifier keeps its image orientation");
            await Capture("04-loading");
            ui.ShowHome(); await Task.Delay(1000);
            Check(ui.home.activeSelf && ui.Round == null && !Active(ui, "loadingDesign"), "cancelled load cannot reopen gameplay");
            await Click(((Button)Field(ui, "designStartButton")));
            await Wait(() => ui.play.activeSelf && ui.Round != null && !Active(ui, "loadingDesign"), "local level opens");
            await Capture("05-gameplay");
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y, true);
            Check(ui.Round.Count == 1 && ui.topRings[0].GetComponentInChildren<DifferenceFoundRing>().isActiveAndEnabled &&
                ui.bottomRings[0].GetComponentInChildren<DifferenceFoundRing>().isActiveAndEnabled, "correct click adds both rings");
            var flight = ui.play.GetComponentInChildren<DifferenceFoundFlight>();
            Check(flight, "correct click launches progress flight");
            Check(flight.GetComponentsInChildren<ParticleSystem>().Length > 0, "prefab particles are present");
            var patch = ((Image[])Field(ui, "originalPatchImages"))[0];
            await Task.Delay(200);
            Check(patch.isActiveAndEnabled && patch.color.a > 0, "difference crop visibly flashes after correct click");
            await Capture("06-found-effect");
            await Task.Delay(2200);
            Check(ui.progressLabel.text.StartsWith("1 /"), "flight updates progress");
            Check(!patch.isActiveAndEnabled && ui.topRings[0].isActiveAndEnabled && ui.bottomRings[0].isActiveAndEnabled,
                "crop flash clears while both found rings remain");
            for (var i = 1; i < ui.Round.Total; i++) ui.ClickImage(ui.spots[i].x, ui.spots[i].y);
            await Wait(() => (bool)Field(ui, "figmaResultActive"), "victory appears after last effect");
            Check((bool)Field(ui, "figmaResultWon") && Active(ui, "victoryDesign"), "victory visual active");
            await Capture("07-victory");
            var previousLevel = ui.SelectedLevel;
            Check(((Button)Field(ui, "designNextButton")).GetComponentInChildren<Text>(true).text == "Level " + (previousLevel + 2),
                "victory button shows the level it will open");
            await Click(((Button)Field(ui, "designNextButton")));
            await Wait(() => ui.Round != null && !ui.Round.Finished && !Active(ui, "loadingDesign"), "next level opens");
            Check(ui.SelectedLevel == previousLevel + 1 && !(bool)Field(ui, "figmaResultActive"), "victory next level advances once");
            for (var i = 0; i < 3; i++) { ui.ClickImage(.99f, .02f); await Task.Delay(400); }
            await Wait(() => (bool)Field(ui, "figmaResultActive"), "failure appears");
            Check(ui.Round.Lives == 0 && !(bool)Field(ui, "figmaResultWon") && Active(ui, "failDesign"), "failure visual active");
            await Task.Delay(350); await Capture("08-failure");
            var failedLevel = ui.SelectedLevel;
            await Click(((Button)Field(ui, "designRetryButton")));
            await Wait(() => ui.Round != null && !ui.Round.Finished && !Active(ui, "loadingDesign"), "retry opens");
            Check(ui.SelectedLevel == failedLevel && ui.Round.Count == 0 && ui.Round.Lives == 3, "failure retry resets the same level");
            await Capture("09-retry");
            ui.ShowHome();
            settings.SetInt(UIDifferences.Key + "Hints", 0);
            settings.SetString(UIDifferences.Key + "GiftDate", "");
            ui.OnOpen(null);
            await Click(((Button)Field(ui, "designStartButton")));
            await Wait(() => ui.Round != null && !Active(ui, "loadingDesign"), "hint test level opens");
            await Click(ui.hintButton);
            Check(Active(ui, "hintDesign"), "empty hints open new hint popup");
            await Task.Delay(350); await Capture("10-hint");
            await Click(ui.stage.Find("FigmaHint/Card/Free").GetComponent<Button>());
            Check(!Active(ui, "hintDesign") && ui.HintActive && ui.Hints == 0, "free hint opens spotlight and consumes exactly one hint");
            await Task.Delay(450); await Capture("11-hint-spotlight");
            var spotlight = ui.play.GetComponentInChildren<DifferenceHintSpotlight>();
            var rendered = spotlight.canvasRenderer.GetMaterial(0);
            File.AppendAllText(Path.Combine(Folder, "report.txt"),
                "Spotlight diagnostic: color=" + spotlight.color + " rect=" + spotlight.rectTransform.rect +
                " rendererCull=" + spotlight.canvasRenderer.cull + " depth=" + spotlight.depth + " shader=" + rendered.shader.name +
                " supported=" + rendered.shader.isSupported + " keywords=" + string.Join(",", rendered.shaderKeywords) +
                " _Size=" + rendered.GetVector("_Size") + " _HoleA=" + rendered.GetVector("_HoleA") +
                " _HoleB=" + rendered.GetVector("_HoleB") +
                " baseSize=" + spotlight.material.GetVector("_Size") + "\n");
            foreach (var shaderName in new[] { "HintSpotlight", "FoundGlow" })
            {
                var local = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Bundles/UI/UIDifferences/Art/" + shaderName + ".shader");
                var loaded = shaderName == "HintSpotlight" ? ui.hintSpotlightMaterial.shader : ui.foundFlightMaterial.shader;
                File.AppendAllText(Path.Combine(Folder, "report.txt"), shaderName + ": runtime=" + loaded.GetInstanceID() +
                    ", runtimePath=" + AssetDatabase.GetAssetPath(loaded) + ", runtimeSupported=" + loaded.isSupported +
                    ", local=" + local.GetInstanceID() + ", localSupported=" + local.isSupported + "\n");
                foreach (var message in ShaderUtil.GetShaderMessages(local))
                    File.AppendAllText(Path.Combine(Folder, "report.txt"), "Shader compiler: " + message.severity + " " + message.message + "\n");
            }
            Check(rendered.shader.isSupported, "spotlight shader supports the current graphics device");
            ui.ShowHome();
            settings.SetInt(UIDifferences.Key + "Completed", ui.levels.Length);
            settings.SetInt(UIDifferences.Key + "RoundLevel", -1);
            ui.OnOpen(null);
            await Click(((Button)Field(ui, "designStartButton")));
            await Wait(() => ui.Round != null && !Active(ui, "loadingDesign"), "completed game can restart");
            Check(ui.SelectedLevel == 0 && ui.Round.Count == 0 && !ui.album.activeSelf,
                "Start after all local levels replays level zero without opening the hidden album");
            await Capture("12-completed-replay");
            Check(errors.Count == 0, "no runtime errors: " + string.Join(" | ", errors));
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "PASS: all UI smoke checks completed.\n");
        }
        catch (Exception error)
        {
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "FAIL: " + error + "\n");
            Debug.LogException(error);
            await Capture("failure-state");
        }
        finally
        {
            Application.logMessageReceived -= collect;
            if (ui) ui.ShowHome();
            foreach (var key in IntKeys) { if (saved.TryGetValue(key, out var value)) settings.SetInt(UIDifferences.Key + key, (int)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            foreach (var key in BoolKeys) { if (saved.TryGetValue(key, out var value)) settings.SetBool(UIDifferences.Key + key, (bool)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            foreach (var key in StringKeys) { if (saved.TryGetValue(key, out var value)) settings.SetString(UIDifferences.Key + key, (string)value); else settings.RemoveSetting(UIDifferences.Key + key); }
            settings.Save();
            if (ui) { ui.levelApiUrl = oldUrl; ui.SetTesting(oldTesting); ui.OnOpen(null); }
            File.AppendAllText(Path.Combine(Folder, "report.txt"), "Original save restored.\n");
            running = false; EditorApplication.isPlaying = false;
        }
    }

    static async Task Click(Button button)
    {
        await Task.Delay(100); Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "actual raycast reaches " + button.name);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    static async Task Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var path = Path.Combine(Folder, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await Task.Delay(250);
        if (!File.Exists(path)) throw new InvalidOperationException("Game View did not produce screenshot: " + name);
    }

    static async Task Wait(Func<bool> condition, string message)
    {
        var end = DateTime.UtcNow.AddSeconds(15);
        while (EditorApplication.isPlaying && !condition() && DateTime.UtcNow < end) await Task.Delay(50);
        Check(condition(), message);
    }
    static object Field(UIDifferences ui, string name) => typeof(UIDifferences).GetField(name, Private).GetValue(ui);
    static bool Active(UIDifferences ui, string name) => Field(ui, name) is GameObject go && go.activeInHierarchy;
    static void Check(bool success, string message)
    {
        Directory.CreateDirectory(Folder);
        File.AppendAllText(Path.Combine(Folder, "report.txt"), (success ? "OK: " : "FAILED: ") + message + "\n");
        if (!success) throw new InvalidOperationException(message);
    }
}
#endif
