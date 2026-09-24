#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public static class DifferenceGamePlayCheck
{
    [MenuItem("Tools/Find Differences/Preview Hint Spotlight")]
    public static void PreviewHintSpotlight()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        if (!ui.play.activeSelf) ui.StartLevel();
        if (ui.Round == null || ui.Round.Finished || ui.modal.activeSelf) return;
        Canvas.ForceUpdateCanvases();
        ui.noticeLabel.transform.parent.gameObject.SetActive(false);
        ui.hintButton.GetComponentInChildren<DifferenceHintHand>(true).Show(false);
        var index = ui.Round.Hint(); var spot = ui.spots[index];
        ui.play.GetComponentInChildren<DifferenceHintSpotlight>(true).Show(ui.upperImage.GetComponent<DifferenceBoard>(),
            ui.upperImage.rectTransform, ui.lowerImage.rectTransform, new Vector2(spot.x, spot.y), index);
    }

    [MenuItem("Tools/Find Differences/Preview Hint Hand")]
    public static void PreviewHintHand()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        if (!ui.play.activeSelf) ui.StartLevel();
        ui.play.GetComponentInChildren<DifferenceHintSpotlight>(true).Cancel();
        typeof(UIDifferences).GetField("hintIdle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(ui, 8f);
        typeof(UIDifferences).GetMethod("AdvanceHintGuide", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ui, new object[] { 0f });
    }

    [MenuItem("Tools/Find Differences/Preview Found Flight")]
    public static void PreviewFoundFlight()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        if (!ui.play.activeSelf) ui.StartLevel();
        if (!ui.play.activeSelf || ui.modal.activeSelf || ActiveFlights(ui).Length > 0) return;
        Canvas.ForceUpdateCanvases();
        ui.noticeLabel.transform.parent.gameObject.SetActive(false);
        var source = ui.lowerImage.rectTransform;
        var dot = ui.progressDots[Mathf.Max(0, ui.Round.Count - 1)];
        var label = dot.GetComponentInChildren<UnityEngine.UI.Text>();
        var oldSprite = dot.sprite; var oldText = label.text;
        dot.sprite = ui.progressQuestion; label.text = "?";
        DifferenceFoundFlight.Launch((RectTransform)ui.play.transform, source.TransformPoint(source.rect.center),
            dot.rectTransform, () => { dot.sprite = oldSprite; label.text = oldText; }, ui.foundFlightPrefab);
    }

    [MenuItem("Tools/Find Differences/Run Popup Motion Checks")]
    public static void RunPopupMotion()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-popup-motion-check.txt");
        var step = typeof(DifferencePopupMotion).GetMethod("Advance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var timeScale = Time.timeScale;
        try
        {
            ui.SetTesting(true); ui.ShowHome(); Canvas.ForceUpdateCanvases();
            Time.timeScale = 0;
            foreach (var popup in new[] { ui.modal, ui.profile, ui.musicPanel, ui.achievements, ui.album })
            {
                var card = (RectTransform)popup.transform.Find("Card");
                var close = (RectTransform)popup.transform.Find("Close");
                var scrim = popup.transform.Find("Scrim").GetComponent<UnityEngine.UI.Image>();
                var center = card.TransformPoint(card.rect.center);
                var closeCenter = close.TransformPoint(close.rect.center);
                var scrimAlpha = scrim.color.a;
                popup.SetActive(true);
                var motion = popup.GetComponent<DifferencePopupMotion>();
                var fade = card.GetComponent<CanvasGroup>();
                Check(Vector3.Distance(center, card.TransformPoint(card.rect.center)) < .01f && card.pivot == new Vector2(.5f, .5f), "首帧改变轴心不改变窗口中心 " + popup.name);
                Check(Near(card.localScale.x, .78f) && fade.alpha == 0 && scrim.color.a == 0 && !fade.blocksRaycasts && scrim.raycastTarget, "小尺寸淡入且遮罩拦截底层点击");
                step.Invoke(motion, new object[] { .08f });
                Check(card.localScale.x > .78f && card.localScale.x < 1 && fade.alpha > 0 && fade.alpha < 1 && scrim.color.a > 0 && scrim.color.a < scrimAlpha, "卡片和遮罩连续淡入");
                Check(Vector3.Distance(center, card.TransformPoint(card.rect.center)) < .01f &&
                    Vector3.Distance(close.TransformPoint(close.rect.center), center + (closeCenter - center) * card.localScale.x) < .01f, "卡片保持中心、关闭按钮同步缩放位移");
                step.Invoke(motion, new object[] { .12f });
                Check(card.localScale.x > 1 && card.localScale.x < 1.04f, "轻微越过原尺寸产生回弹");
                popup.SetActive(false);
                Check(card.localScale == Vector3.one && Vector3.Distance(closeCenter, close.TransformPoint(close.rect.center)) < .01f, "中途关闭恢复布局");
                popup.SetActive(true);
                Check(Near(card.localScale.x, .78f) && fade.alpha == 0, "快速重新打开从干净状态播放");
                step.Invoke(motion, new object[] { .4f });
                Check(card.localScale == Vector3.one && close.localScale == Vector3.one && fade.alpha == 1 && fade.blocksRaycasts && fade.interactable && Near(scrim.color.a, scrimAlpha), "回弹后精确恢复尺寸与交互");
                popup.SetActive(false);
            }
            ui.ShowDialog("无广告", "当前版本没有插屏与横幅广告。\n可以安心找不同。", "开始游戏", null);
            var dialogMotion = ui.modal.GetComponent<DifferencePopupMotion>();
            step.Invoke(dialogMotion, new object[] { .1f });
            var previousScale = ui.modal.transform.Find("Card").localScale;
            ui.ShowDialog("无广告", "当前版本没有插屏与横幅广告。\n可以安心找不同。", "开始游戏", null);
            Check(ui.modal.transform.Find("Card").localScale == previousScale, "重复刷新弹窗不叠加动画或跳变");
            ui.modal.SetActive(false);
            File.WriteAllText(report, "PASS: all 5 popup types preserve their center, coordinated close button, scale/fade/overshoot/settle, input guard, mid-animation close/reopen, repeated-dialog update, unscaled-time advance.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally { Time.timeScale = timeScale; ui.ShowHome(); ui.SetTesting(false); }
    }

    [MenuItem("Tools/Find Differences/Run Button Shine Checks")]
    public static void RunButtonShine()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-button-shine-check.txt");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var step = typeof(DifferenceButtonShine).GetMethod("Advance", flags);
        var time = typeof(DifferenceButtonShine).GetField("elapsed", flags);
        var draw = typeof(DifferenceButtonShine).GetMethod("OnPopulateMesh", flags | System.Reflection.BindingFlags.DeclaredOnly);
        var timeScale = Time.timeScale;
        try
        {
            ui.SetTesting(true); ui.ShowHome(); Canvas.ForceUpdateCanvases();
            var shine = ui.startButton.GetComponentInChildren<DifferenceButtonShine>();
            Check(shine, "关卡按钮存在流光");
            var clip = shine.GetComponentInParent<UnityEngine.UI.Mask>();
            Check(clip && !clip.showMaskGraphic && !clip.graphic.raycastTarget && !shine.raycastTarget, "圆角裁切且流光不拦截点击");
            Check(clip.transform.GetSiblingIndex() < ui.startLabel.transform.GetSiblingIndex(), "流光在文字下方");
            Check(shine.materialForRendering.GetInt("_Stencil") > 0, "实际使用圆角模板裁切");
            Time.timeScale = 0;
            time.SetValue(shine, 0f);
            using (var mesh = new UnityEngine.UI.VertexHelper())
            {
                step.Invoke(shine, new object[] { .25f }); draw.Invoke(shine, new object[] { mesh });
                var edge = new UIVertex(); var center = new UIVertex();
                Check(mesh.currentVertCount == 6, "扫光网格显示");
                mesh.PopulateUIVertex(ref edge, 0); mesh.PopulateUIVertex(ref center, 2);
                Check(edge.color.a == 0 && center.color.a > 0 && center.color.a < 150, "渐变边缘透明且亮度受控");
                var x = center.position.x;
                step.Invoke(shine, new object[] { .25f }); draw.Invoke(shine, new object[] { mesh });
                mesh.PopulateUIVertex(ref center, 2); Check(center.position.x > x, "光带向右扫过");
                step.Invoke(shine, new object[] { 1f }); draw.Invoke(shine, new object[] { mesh });
                Check(mesh.currentVertCount == 0, "扫完后停顿且清除网格");
                step.Invoke(shine, new object[] { 2.4f }); draw.Invoke(shine, new object[] { mesh });
                Check(mesh.currentVertCount == 6, "下一轮自动扫光");
            }
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var canvas = ui.startButton.GetComponentInParent<Canvas>().rootCanvas;
            eventData.position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                ui.startButton.transform.TransformPoint(((RectTransform)ui.startButton.transform).rect.center));
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == ui.startButton, "扫光期间实际射线仍命中关卡按钮");
            ui.ShowShop(); Check(!shine.isActiveAndEnabled, "离开主页停止流光更新");
            ui.ShowHome(); Check(shine.isActiveAndEnabled && (float)time.GetValue(shine) > 1.05f, "返回主页重新等待扫光");
            File.WriteAllText(report, "PASS: rightward gradient sweep, blank interval and repeat, rounded stencil mask, text layering, actual button raycast, hidden-page stop/reopen, unscaled-time advance.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally { Time.timeScale = timeScale; ui.SetTesting(false); }
    }

    [MenuItem("Tools/Find Differences/Run Tab Motion Checks")]
    public static async void RunTabMotion()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-tab-motion-check.txt");
        var step = typeof(UIDifferences).GetMethod("AdvanceTabTransition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pages = new[] { (RectTransform)ui.shop.transform, (RectTransform)ui.home.transform, (RectTransform)ui.ranking.transform };
        var tabs = new[] { (RectTransform)ui.shopButton.transform, (RectTransform)Button(ui, "Navigation/HomeTab").transform, (RectTransform)ui.trophyButton.transform };
        var timeScale = Time.timeScale;
        var scroll = ui.shop.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        var scrollPosition = scroll.content.anchoredPosition;
        var scrollVelocity = scroll.velocity;
        PointerEventData activeSwipe = null;
        try
        {
            ui.SetTesting(true); ui.ShowHome(); ui.SetTesting(false);
            var coins = ui.Coins; var hints = ui.Hints;
            Check(GameApp.Base.FrameRate == 60 && Application.targetFrameRate == 60, "启动通过框架设置 60 帧目标");
            Check(ui.stage.GetComponent<UnityEngine.UI.RectMask2D>(), "滑动页面被屏幕边界裁切");
            var selection = ui.stage.Find("Navigation/Selection") as RectTransform;
            Check(selection && !selection.GetComponent<UnityEngine.UI.Image>().raycastTarget, "独立选中底板不遮挡按钮点击");
            Check(selection.GetSiblingIndex() < tabs[0].GetSiblingIndex(), "选中底板绘制在图标下方");
            var selectionSize = selection.sizeDelta;
            var selectionStart = selection.anchoredPosition;
            ui.ShowShop();
            Check(ui.home.activeSelf && ui.shop.activeSelf && Near(pages[1].anchoredPosition.x, 0), "切换首帧保留原页位置");
            Check(selection.anchoredPosition == selectionStart && Near(tabs[0].sizeDelta.x, 308) && Near(tabs[1].sizeDelta.x, 206), "图标布局先切换、选中底板从旧位置出发");
            var shopIconPosition = tabs[0].Find("Icon").position;
            step.Invoke(ui, new object[] { .08f });
            Check(pages[1].anchoredPosition.x > 0 && pages[1].anchoredPosition.x < ui.stage.rect.width && pages[0].anchoredPosition.x < 0, "商店从左侧进入、主页向右退出");
            Check(selection.anchoredPosition.x > 0 && selection.anchoredPosition.x < selectionStart.x && selection.sizeDelta == selectionSize, "固定尺寸的选中底板独立向左滑动");
            Check(Vector3.Distance(shopIconPosition, tabs[0].Find("Icon").position) < .001f && Near(tabs[0].sizeDelta.x, 308), "图标与按钮宽度不跟着底板滑动");
            Check(Near(tabs[0].sizeDelta.x + tabs[1].sizeDelta.x + tabs[2].sizeDelta.x, 720), "底栏过渡无空隙");
            foreach (var page in pages) Check(!page.GetComponent<CanvasGroup>().blocksRaycasts, "过渡期间禁止点击滑动内容");
            var position = pages[1].anchoredPosition;
            var highlightPosition = selection.anchoredPosition;
            ui.ShowShop();
            Check(Vector2.Distance(position, pages[1].anchoredPosition) < .001f && selection.anchoredPosition == highlightPosition, "重复选择不重启动画");
            ui.ShowHome();
            Check(Vector2.Distance(position, pages[1].anchoredPosition) < .001f && selection.anchoredPosition == highlightPosition, "快速反向切换保持底板与页面当前位置");
            step.Invoke(ui, new object[] { .08f });
            Check(pages[1].anchoredPosition.x < position.x && selection.anchoredPosition.x > highlightPosition.x, "反向点击从当前位置回滑");
            position = pages[1].anchoredPosition;
            highlightPosition = selection.anchoredPosition;
            Click(ui.trophyButton);
            Check(Vector2.Distance(position, pages[1].anchoredPosition) < .001f && selection.anchoredPosition == highlightPosition, "连续点击第三个页签无跳变");
            Time.timeScale = 0;
            step.Invoke(ui, new object[] { .35f });
            Check(ui.ranking.activeSelf && !ui.home.activeSelf && !ui.shop.activeSelf && Near(pages[2].anchoredPosition.x, 0), "过渡结束只保留目标页面");
            Check(tabs[2].sizeDelta.x > tabs[1].sizeDelta.x && tabs[2].anchoredPosition.y > 0, "选中按钮展开并抬高");
            Check(selection.anchoredPosition == tabs[2].anchoredPosition && selection.sizeDelta == selectionSize, "底板最终准确对齐目标页签且无伸缩");
            Check(ui.ranking.GetComponent<CanvasGroup>().blocksRaycasts && ui.ranking.GetComponent<CanvasGroup>().interactable, "结束后恢复页面交互");
            ui.ShowShop(); step.Invoke(ui, new object[] { .08f });
            Check(pages[2].anchoredPosition.x > 0 && pages[0].anchoredPosition.x < 0, "跨两个页签滑动方向正确");
            ui.OnOpen(null);
            Check(ui.home.activeSelf && !ui.shop.activeSelf && !ui.ranking.activeSelf && Near(pages[1].anchoredPosition.x, 0), "重新打开界面清理未完成过渡");
            ui.ShowShop(); step.Invoke(ui, new object[] { .17f });
            Check(ui.home.activeSelf && pages[0].anchoredPosition.x < 0, "完整切页在 170 毫秒仍连续收尾");
            step.Invoke(ui, new object[] { .02f });
            Check(!ui.home.activeSelf && Near(pages[0].anchoredPosition.x, 0), "完整切页 180 毫秒完成并恢复交互");
            ui.ShowHome(); step.Invoke(ui, new object[] { .2f });

            var canvas = ui.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 ScreenPoint(float x, float y) => RectTransformUtility.WorldToScreenPoint(camera,
                ui.stage.TransformPoint(new Vector3(ui.stage.rect.xMin + x, ui.stage.rect.yMax - y)));
            Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            async Task<PointerEventData> Press(Vector2 point, GameObject expectedDrag, GameObject expectedClick = null)
            {
                var pointer = new PointerEventData(EventSystem.current) { pointerId = -100, button = PointerEventData.InputButton.Left,
                    position = point, pressPosition = point, eligibleForClick = true };
                var hits = new List<RaycastResult>();
                var deadline = DateTime.UtcNow.AddSeconds(1);
                do
                {
                    await Task.Delay(20); Canvas.ForceUpdateCanvases(); hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                    if (hits.Count > 0 && ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) == expectedDrag &&
                        ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == expectedClick) break;
                } while (ui && EditorApplication.isPlaying && DateTime.UtcNow < deadline);
                Check(hits.Count > 0, "手势起点有真实 UI 射线命中");
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
                pointer.pointerClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Check(pointer.pointerDrag == expectedDrag && pointer.pointerClick == expectedClick,
                    "真实手势与点击路由：" + hits[0].gameObject.name);
                pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler) ?? pointer.pointerClick;
                activeSwipe = pointer;
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
                return pointer;
            }
            void Drag(PointerEventData pointer, Vector2 distance)
            {
                var first = distance.normalized * 10;
                pointer.position = pointer.pressPosition + ScreenPoint(first.x, first.y) - ScreenPoint(0, 0);
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.beginDragHandler);
                pointer.dragging = true;
                pointer.position = pointer.pressPosition + ScreenPoint(distance.x, distance.y) - ScreenPoint(0, 0);
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.dragHandler);
            }
            void Release(PointerEventData pointer)
            {
                ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                if (pointer.eligibleForClick && hits.Count > 0 && pointer.pointerClick == ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject))
                    ExecuteEvents.Execute(pointer.pointerClick, pointer, ExecuteEvents.pointerClickHandler);
                if (pointer.dragging) ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
                activeSwipe = null;
            }
            void Settle() => step.Invoke(ui, new object[] { .35f });

            foreach (var index in new[] { 0, 2, 1 })
            {
                var button = tabs[index].GetComponent<UnityEngine.UI.Button>();
                var invoked = 0;
                UnityEngine.Events.UnityAction countPress = () => invoked++;
                button.onClick.AddListener(countPress);
                try
                {
                    var down = await Press(Center(tabs[index]), null, button.gameObject);
                    Check(invoked == 1 && !down.eligibleForClick && tabs[index].Find("Label").gameObject.activeSelf &&
                        Near(tabs[index].sizeDelta.x, 308), "底栏按下同一调用立即执行并更新选中状态，不等待松手 " + index);
                    Check(button.targetGraphic == tabs[index].Find("Icon").GetComponent<UnityEngine.UI.Image>() &&
                        button.colors.fadeDuration == 0, "底栏按压反馈直接作用于可见图标");
                    var remaining = Mathf.Abs(pages[index].anchoredPosition.x);
                    step.Invoke(ui, new object[] { .08f });
                    Check(remaining > 0 && Mathf.Abs(pages[index].anchoredPosition.x) < remaining,
                        "手指未松开时页面已开始移动");
                    Release(down);
                    Check(invoked == 1, "底栏松手不重复执行切页");
                    Settle();
                    Check(Near(pages[index].anchoredPosition.x, 0), "底栏立即响应后仍正确完成切页");
                }
                finally { button.onClick.RemoveListener(countPress); }
            }
            var rightPress = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(tabs[0].gameObject, rightPress, ExecuteEvents.pointerDownHandler);
            Check(ui.home.activeSelf && !ui.shop.activeSelf, "右键不触发底栏切页");
            ExecuteEvents.Execute(tabs[0].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Settle(); Check(ui.shop.activeSelf && !ui.home.activeSelf, "底栏键盘提交仍然可用");
            ui.ShowHome(); Settle();

            var swipe = await Press(ScreenPoint(360, 500), ui.home);
            Drag(swipe, new Vector2(180, 0));
            Check(Near(pages[1].anchoredPosition.x, 180) && Near(pages[0].anchoredPosition.x, -540) &&
                Near(selection.anchoredPosition.x, 154.5f), "主页向右拖动时商店和底栏随手指移动");
            var screenshotDirectory = Path.Combine(Application.dataPath, "Screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            ScreenCapture.CaptureScreenshot(Path.Combine(screenshotDirectory, "differences-home-swipe.png"));
            await Task.Delay(250);
            Release(swipe); step.Invoke(ui, new object[] { .15f });
            Check(ui.shop.activeSelf && !ui.home.activeSelf && Near(pages[0].anchoredPosition.x, 0) && Near(selection.anchoredPosition.x, 0), "松手吸附商店并同步底栏");

            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
            var beforeScroll = scroll.content.anchoredPosition;
            swipe = await Press(ScreenPoint(200, 450), scroll.viewport.gameObject);
            Drag(swipe, new Vector2(0, -160)); Release(swipe);
            Check(scroll.content.anchoredPosition.y > beforeScroll.y + 100 && Near(pages[0].anchoredPosition.x, 0), "商店纵向拖动交给原 ScrollRect，页面不横移");
            scroll.StopMovement();
            swipe = await Press(ScreenPoint(200, 450), scroll.viewport.gameObject);
            Drag(swipe, new Vector2(-180, 0));
            Check(Near(pages[0].anchoredPosition.x, -180) && Near(pages[1].anchoredPosition.x, 540), "商店视口横向拖动进入主页");
            Release(swipe); Settle();
            Check(ui.home.activeSelf && Near(pages[1].anchoredPosition.x, 0), "商店横向手势返回主页");

            swipe = await Press(ScreenPoint(360, 500), ui.home);
            Drag(swipe, new Vector2(50, 0)); Release(swipe); step.Invoke(ui, new object[] { .09f });
            Check(ui.home.activeSelf && !ui.shop.activeSelf && Near(pages[1].anchoredPosition.x, 0), "短拖松手 80 毫秒回弹原页，不再固定等待 300 毫秒");
            swipe = await Press(Center(ui.startButton.GetComponent<RectTransform>()), ui.home, ui.startButton.gameObject);
            Drag(swipe, new Vector2(-180, 0));
            Check(!swipe.eligibleForClick && Near(pages[1].anchoredPosition.x, -180) && Near(pages[2].anchoredPosition.x, 540), "从开始按钮向左拖动跟手且取消按钮点击");
            Release(swipe); Settle();
            Check(ui.ranking.activeSelf && ui.Round == null && !ui.IsLoadingLevel && Near(pages[2].anchoredPosition.x, 0), "按钮手势进入排行榜，不误启动关卡");
            swipe = await Press(ScreenPoint(360, 400), ui.ranking);
            Drag(swipe, new Vector2(-500, 0)); Release(swipe); Settle();
            Check(ui.ranking.activeSelf && Near(pages[2].anchoredPosition.x, 0), "最右侧不允许拖出页面边界");

            swipe = await Press(Center(tabs[1]), null, tabs[1].gameObject); Release(swipe); Settle();
            Check(ui.home.activeSelf && Near(selection.anchoredPosition.x, tabs[1].anchoredPosition.x), "底部主页按钮仍通过真实点击切页");
            ui.ShowDialog("手势检查", "弹窗期间页面保持不动", "关闭", null);
            swipe = await Press(ScreenPoint(100, 400), null);
            Drag(swipe, new Vector2(250, 0)); Release(swipe); Settle();
            Check(ui.modal.activeSelf && ui.home.activeSelf && !ui.shop.activeSelf && Near(pages[1].anchoredPosition.x, 0), "弹窗射线隔离底层页面手势");
            Click(ui.modalClose);
            swipe = await Press(Center(tabs[0]), null, tabs[0].gameObject); Release(swipe); Settle();
            swipe = await Press(ScreenPoint(25, 400), ui.shop);
            Drag(swipe, new Vector2(500, 0)); Release(swipe); Settle();
            Check(ui.shop.activeSelf && Near(pages[0].anchoredPosition.x, 0), "最左侧不允许拖出页面边界");
            swipe = await Press(Center(tabs[1]), null, tabs[1].gameObject); Release(swipe); Settle();
            Check(ui.Coins == coins && ui.Hints == hints, "切页不消耗金币或提示");
            ui.ShowShop();
            var frameStart = Time.frameCount;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (ui.home.activeSelf && clock.Elapsed.TotalSeconds < 3) await Task.Delay(1);
            Check(!ui.home.activeSelf && ui.shop.activeSelf && Near(pages[0].anchoredPosition.x, 0), "实际 LateUpdate 连续帧完成切页");
            File.WriteAllText(report, $"PASS: 60 fps target; full transition completes in 180 ms, short snapback in 80 ms, partial swipe settles by remaining distance; live LateUpdate transition observed {clock.Elapsed.TotalMilliseconds:F1} ms / {Time.frameCount - frameStart} game frames (Editor observation, not device FPS guarantee). All three navigation buttons execute on pointer down with immediate visible icon/selection feedback; release does not replay, right click is ignored and keyboard submit still works; actual raycast drag routing on page backgrounds, start button and shop viewport; home follows horizontal drags toward both neighbors with linked selection plate, release snaps, short drags return and outer boundaries clamp; shop vertical drags scroll while horizontal drags change pages; button drag cancels click, popup blocks swipe, bottom buttons still click; mid-drag screenshot captured; existing tab click/reversal/alignment/reopen checks, no coin/hint consumption. Transition steps use unscaled time.");
            Debug.Log("Find Differences tab motion checks passed.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally
        {
            if (activeSwipe != null)
            {
                ExecuteEvents.Execute(activeSwipe.pointerPress, activeSwipe, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(activeSwipe.pointerDrag, activeSwipe, ExecuteEvents.endDragHandler);
            }
            Time.timeScale = timeScale; ui.SetTesting(true); ui.ShowHome(); ui.SetTesting(false);
            scroll.content.anchoredPosition = scrollPosition; scroll.velocity = scrollVelocity;
        }
    }

    [MenuItem("Tools/Find Differences/Run Play Checks")]
    public static async void Run()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        const string key = UIDifferences.Key;
        var settings = GameApp.Setting;
        var intKeys = new[] { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint" };
        var boolKeys = new[] { "Sound", "Music" };
        var stringKeys = new[] { "Nickname", "GiftDate", "RoundContent" };
        var original = new Dictionary<string, object>();
        foreach (var name in intKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetInt(key + name);
        foreach (var name in boolKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetBool(key + name);
        foreach (var name in stringKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetString(key + name);
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-play-check.txt");
        var simulatorWheelReport = "Simulator wheel SKIP (no open Simulator window)";
        try
        {
            ui.SetTesting(true);
            foreach (var name in intKeys) settings.RemoveSetting(key + name);
            foreach (var name in boolKeys) settings.RemoveSetting(key + name);
            foreach (var name in stringKeys) settings.RemoveSetting(key + name);
            settings.SetInt(key + "Coins", 100);
            settings.SetString(key + "Nickname", "原始玩家");
            ui.OnOpen(null);

            var counts = new[] { 10, 10, 15 };
            Check(ui.levels.Length == 3, "三组独立关卡");
            for (var i = 0; i < counts.Length; i++)
            {
                Check(ui.levels[i].regions.Length == counts[i] && ui.levels[i].original && ui.levels[i].changed && ui.levels[i].original != ui.levels[i].changed, "关卡图片及差异数量 " + i);
                for (var j = 0; j < i; j++)
                    Check(ui.levels[i].original != ui.levels[j].original && ui.levels[i].changed != ui.levels[j].changed, "独立图片 " + i);
            }
            Check(ui.home.activeSelf && ui.Coins == 100 && ui.Hints == 5 && ui.SelectedLevel == 0, "首页初始状态");
            ui.ShowAlbum();
            Check(!Button(ui, "Album/Card/Level1").interactable, "未通关场景锁定");
            ui.SelectLevel(1);
            Check(ui.Round == null && ui.album.activeSelf, "拒绝越级进入");
            Click(ui, "Album/Close");
            Click(ui.startButton);
            Check(ui.Round.Total == 10 && ui.Round.Lives == 3, "首关初始状态");
            CheckGameplayArt(ui);
            Canvas.ForceUpdateCanvases();
            CheckHintGuide(ui);
            ui.lowerImage.GetComponent<DifferenceBoard>().SetZoom(2);
            ui.lowerImage.rectTransform.anchoredPosition += new Vector2(35, 42);
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y, true);
            Check(ui.Round.Count == 1 && ui.topRings[0].isActiveAndEnabled && ui.bottomRings[0].isActiveAndEnabled, "双图圈选");
            var patchFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var patchStep = typeof(UIDifferences).GetMethod("AdvancePatches", patchFlags);
            var originalPatches = (UnityEngine.UI.Image[])typeof(UIDifferences).GetField("originalPatchImages", patchFlags).GetValue(ui);
            var changedFlashes = (UnityEngine.UI.Image[])typeof(UIDifferences).GetField("changedFlashImages", patchFlags).GetValue(ui);
            var originalRect = (RectTransform)originalPatches[0].transform.parent;
            var changedRect = (RectTransform)changedFlashes[0].transform.parent;
            var cropCenter = new Vector2(ui.levels[0].regions[0].x * 600, -ui.levels[0].regions[0].y * 400);
            Check(originalPatches[0].sprite.texture == ui.levels[0].original.texture && changedFlashes[0].sprite.texture == ui.levels[0].changed.texture &&
                ui.patchImages[0].sprite.texture == ui.levels[0].changed.texture && originalRect.parent == ui.lowerImage.transform &&
                changedRect.parent == ui.upperImage.transform && ui.differencePatches[0].transform.parent == ui.lowerImage.transform &&
                originalRect.pivot == Vector2.one * .5f && changedRect.pivot == Vector2.one * .5f &&
                Vector2.Distance(originalRect.anchoredPosition, cropCenter) < .001f && Vector2.Distance(changedRect.anchoredPosition, cropCenter) < .001f,
                "本地双侧临时裁块使用对侧图片，保持原位对齐");
            Check(originalPatches[0].isActiveAndEnabled && changedFlashes[0].isActiveAndEnabled &&
                Near(originalPatches[0].color.a, 0) && Near(changedFlashes[0].color.a, 0), "局部互换从透明开始");
            for (var pulse = 0; pulse < 3; pulse++)
            {
                patchStep.Invoke(ui, new object[] { .35f });
                Check(originalPatches[0].isActiveAndEnabled && changedFlashes[0].isActiveAndEnabled &&
                    originalPatches[0].color == Color.white && changedFlashes[0].color == Color.white &&
                    originalRect.localScale == Vector3.one && changedRect.localScale == Vector3.one &&
                    ui.patchImages[0].color == Color.white && ui.differencePatches[0].activeSelf,
                    "本地第 " + (pulse + 1) + " 轮完整互换，原色原尺寸");
                patchStep.Invoke(ui, new object[] { .35f });
                if (pulse < 2)
                {
                    var patchTint = changedFlashes[0].color;
                    Check(originalPatches[0].isActiveAndEnabled && changedFlashes[0].isActiveAndEnabled && patchTint == originalPatches[0].color &&
                        patchTint.r == 1 && patchTint.g == 1 && patchTint.b == 1 && Near(patchTint.a, 0) &&
                        originalRect.localScale == Vector3.one && changedRect.localScale == Vector3.one && ui.patchImages[0].color == Color.white,
                        "本地第 " + (pulse + 1) + " 轮淡出后恢复原图，等待下一轮");
                }
            }
            Check(ui.differencePatches[0].activeSelf && ui.patchImages[0].color == Color.white && originalPatches[0].color == Color.white && changedFlashes[0].color == Color.white &&
                !originalRect.gameObject.activeSelf && !changedRect.gameObject.activeSelf && originalRect.localScale == Vector3.one && changedRect.localScale == Vector3.one &&
                ui.topRings[0].gameObject.activeSelf && ui.bottomRings[0].gameObject.activeSelf,
                "本地 2.1 秒三轮互换结束恢复各自原样，保留不同内容及双圈");
            Check(Text(ui, "Play/Progress/Dot0/Text") == "?" && ui.progressLabel.text == "0 / 10" && settings.GetInt(key + "RoundMask") == 1, "飞行期间保留问号，但命中立即保存");
            var firstFlight = ActiveFlights(ui)[0];
            CheckFoundFlight(firstFlight, ui.bottomRings[0].transform.position, ui.progressDots[0].rectTransform);
            ui.lowerImage.GetComponent<DifferenceBoard>().ResetView();
            Find(ui, 0);
            patchStep.Invoke(ui, new object[] { .1f });
            Check(ui.Round.Count == 1 && ui.Round.Lives == 3 && ActiveFlights(ui).Length == 1 && ui.patchImages[0].color == Color.white &&
                !originalRect.gameObject.activeSelf && !changedRect.gameObject.activeSelf, "重复点击不扣心、不重播飞行或区域闪动");
            Miss(ui);
            Check(ui.Round.Lives == 2 && ui.crossTop.gameObject.activeSelf && ui.crossBottom.gameObject.activeSelf && ActiveFlights(ui).Length == 1, "误点与双图错误反馈、不会生成飞行特效");
            Click(ui.hintButton);
            Check(ui.Round.Count == 1 && ui.Hints == 4 && ui.HintActive && !ui.topRings[1].gameObject.activeSelf, "提示只消耗一次道具，不提前圈选或修改进度");
            var spotlight = ui.play.GetComponentInChildren<DifferenceHintSpotlight>();
            var hintStep = typeof(DifferenceHintSpotlight).GetMethod("Advance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(spotlight && spotlight.material.shader.isSupported && spotlight.material != ui.hintSpotlightMaterial, "独立提示材质受资源引用且Shader可用");
            ui.UseHint(); Find(ui, 1);
            Check(ui.Hints == 4 && ui.Round.Count == 1 && ui.Round.Lives == 2, "聚光打开期间拦截重复消耗和点击");
            hintStep.Invoke(spotlight, new object[] { .2f });
            Check(ui.upperImage.transform.localScale.x > 1 && ui.upperImage.transform.localScale.x < 2, "平滑放大，非瞬间跳变");
            hintStep.Invoke(spotlight, new object[] { .2f });
            Check(spotlight.Ready && Near(ui.upperImage.transform.localScale.x, 2) && ui.lowerImage.transform.localScale == ui.upperImage.transform.localScale, "提示双图同步放大后等待玩家点击");
            var canvas = ui.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var marker in new[] { ui.topRings[1], ui.bottomRings[1] })
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, marker.transform.position);
                Check(!spotlight.IsRaycastLocationValid(screen, camera), "上下两处亮区都允许玩家点击");
                Check(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)marker.transform.parent.parent, screen, camera), "聚焦目标落在裁切窗口内");
            }
            Check(spotlight.IsRaycastLocationValid(RectTransformUtility.WorldToScreenPoint(camera, ui.hintButton.transform.position), camera), "暗区拦截灯泡，不能重复消耗");
            ui.ClickImage(0, 0); Click(ui, "Play/Zoom");
            Check(ui.Round.Lives == 2 && ui.Round.Count == 1 && Near(ui.upperImage.transform.localScale.x, 2), "聚光期间误点不扣心，缩放按钮不干扰引导");
            var hintedOrigin = ui.topRings[1].transform.position;
            Find(ui, 1);
            Check(ui.Round.Count == 2 && ui.Hints == 4 && ui.topRings[1].gameObject.activeSelf && !spotlight.Ready, "实际点击提示目标才圈选并退出聚光");
            Check(ActiveFlights(ui).Length == 2, "连续找到的特效独立飞向各自进度圆点");
            Check(Text(ui, "Play/Progress/Dot0/Text") == "?" && Text(ui, "Play/Progress/Dot1/Text") == "?", "刷新生命与提示时不会提前显示飞行中的勾");
            CheckFoundFlight(ActiveFlights(ui)[1], hintedOrigin, ui.progressDots[1].rectTransform);
            hintStep.Invoke(spotlight, new object[] { .3f });
            Check(!ui.HintActive && Near(ui.upperImage.transform.localScale.x, 1) && Near(ui.lowerImage.transform.localScale.x, 1), "命中后聚光消失、双图缩回全图");
            var flightStep = typeof(DifferenceFoundFlight).GetMethod("Advance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            flightStep.Invoke(firstFlight, new object[] { .30f });
            Check(Text(ui, "Play/Progress/Dot0/Text") == "?" && ui.progressDots[0].sprite == ui.progressQuestion, "已抵达但闪光阶段仍不显示勾");
            flightStep.Invoke(firstFlight, new object[] { .14f });
            Check(Text(ui, "Play/Progress/Dot0/Text") == "✓" && Text(ui, "Play/Progress/Dot1/Text") == "?" && ui.progressLabel.text == "1 / 10" && ui.progressDots[0].transform.localScale.x > 1, "先闪亮再显示当前圆点的勾，其他飞行不受影响");
            Check(ui.progressDots[0].sprite == ui.progressCheck && ui.progressDots[1].sprite == ui.progressQuestion, "实际图标与延迟进度同步");
            Check(firstFlight.GetComponentsInChildren<ParticleSystem>().Length > 0, "抵达后保留预制体拖尾粒子");
            flightStep.Invoke(firstFlight, new object[] { .17f });
            Check(firstFlight.isActiveAndEnabled && ui.progressDots[0].transform.localScale == Vector3.one, "短拖尾结束前勾已落稳");
            flightStep.Invoke(firstFlight, new object[] { DifferenceFoundFlight.Duration });
            Check(!firstFlight.isActiveAndEnabled && ui.progressDots[0].transform.localScale == Vector3.one, "抵达后清除特效并恢复进度圆点尺寸");
            var failedMask = ui.Round.FoundMask;
            Miss(ui); Miss(ui);
            Check(ui.Round.Lives == 0 && ui.modal.activeSelf && !ui.modalClose.gameObject.activeSelf, "失败弹窗");
            Check(settings.GetInt(key + "RoundLives") == 0 && settings.GetInt(key + "RoundMask") == failedMask, "失败存档记录零心");
            Click(ui.modalTertiary);
            Check(ui.home.activeSelf && ui.Round == null && ui.SelectedLevel == 0 && ui.startLabel.text.Contains("继续"), "失败后首页续局");
            Check(ActiveFlights(ui).Length == 0, "返回首页清除所有飞行特效");
            ui.OnOpen(null);
            Click(ui.startButton);
            Check(ui.Round.Lives == 0 && ui.Round.FoundMask == failedMask && ui.modal.activeSelf && ui.Coins == 100, "重载失败局不会免费复活");
            Check(Text(ui, "Play/Progress/Dot1/Text") == "✓" && ui.progressLabel.text == "2 / 10", "中途离开后重载恢复真实进度，不残留等待标记");
            Click(ui.modalAction);
            Check(ui.Round.Lives == 3 && ui.Round.FoundMask == failedMask && ui.Round.Count == 2 && ui.Coins == 70, "三十金币续命保留位置");
            ui.Revive();
            Check(ui.Coins == 70 && settings.GetInt(key + "RoundLives") == 3 && settings.GetInt(key + "RoundMistakes") == 3, "续命只扣费一次且保留失误");
            Miss(ui); Miss(ui); Miss(ui);
            Click(ui.modalSecondary);
            Check(ui.Round.Count == 0 && ui.Round.FoundMask == 0 && ui.Round.Lives == 3 && ui.Coins == 70 && settings.GetInt(key + "RoundMistakes") == 0, "免费重试清空进度和失误");

            Find(ui, 0);
            var upper = ui.upperImage.GetComponent<DifferenceBoard>();
            var lower = ui.lowerImage.GetComponent<DifferenceBoard>();
            Check(upper.peer == lower && lower.peer == upper, "双图同步绑定");
            upper.SetZoom(99);
            Check(Near(upper.transform.localScale.x, 2.5f) && Near(lower.transform.localScale.x, 2.5f), "缩放上限及双图同步");
            lower.SetZoom(.5f);
            Check(Near(upper.transform.localScale.x, 1) && Near(lower.transform.localScale.x, 1), "缩放下限及反向同步");
            Click(ui, "Play/Zoom");
            Check(Near(upper.transform.localScale.x, 2) && Near(lower.transform.localScale.x, 2), "缩放按钮");
            var previousRound = ui.Round;
            var previousPosition = ui.upperImage.rectTransform.anchoredPosition;
            patchStep.Invoke(ui, new object[] { .1f });
            Check(ui.patchImages[0].color == Color.white && changedFlashes[0].color.a < 1 && originalPatches[0].isActiveAndEnabled &&
                changedFlashes[0].isActiveAndEnabled, "商店离页前正处于原色裁块闪动");
            ui.ShowShop();
            Check(ui.shop.activeSelf && !ui.play.activeSelf && Button(ui, "Shop/Close").gameObject.activeSelf, "局内商店入口");
            Click(ui, "Shop/Close");
            Check(ui.play.activeSelf && ReferenceEquals(previousRound, ui.Round) && ui.Round.Count == 1 && ui.Round.Lives == 3, "商店返回原局");
            Check(ui.differencePatches[0].activeSelf && ui.patchImages[0].color == Color.white && originalPatches[0].color == Color.white && changedFlashes[0].color == Color.white &&
                !originalRect.gameObject.activeSelf && !changedRect.gameObject.activeSelf && originalRect.localScale == Vector3.one && changedRect.localScale == Vector3.one,
                "商店中途往返清除双侧临时裁块并恢复缩放，保留原差异");
            Check(Near(upper.transform.localScale.x, 2) && Near(lower.transform.localScale.x, 2) && Vector2.Distance(previousPosition, ui.upperImage.rectTransform.anchoredPosition) < .01f, "商店往返保留缩放和位置");
            upper.ResetView();
            Check(Near(upper.transform.localScale.x, 1) && Near(lower.transform.localScale.x, 1) && ui.upperImage.rectTransform.anchoredPosition.sqrMagnitude < .001f && ui.lowerImage.rectTransform.anchoredPosition.sqrMagnitude < .001f, "重置双图位置");
            Click(ui.backButton);
            Click(ui.startButton);
            Check(ui.Round.Count == 1 && ui.Round.Lives == 3, "正常局退出后恢复");

            Complete(ui);
            Check(ui.Completed == 1 && ui.Coins == 80 && settings.GetInt(key + "RoundLevel") == -1, "首关首次奖励十金币并清除存档");
            Find(ui, 0); ui.UseHint();
            Check(ui.Coins == 80 && ui.Round.Count == 10 && ui.Hints == 4, "终局不可重复结算或消耗提示");
            previousRound = ui.Round;
            ui.ShowShop();
            Click(ui, "Shop/Close");
            Check(ReferenceEquals(previousRound, ui.Round) && ui.modal.activeSelf && ui.modalActionLabel.text == "下一关", "离开结算页后仍能进入下一关");
            Click(ui.modalSecondary);
            Check(ui.home.activeSelf && ui.SelectedLevel == 1 && !ui.startLabel.text.Contains("继续"), "通关后首页指向第二关");
            Click(ui.startButton);
            Check(ui.SelectedLevel == 1 && ui.Round.Total == 10 && ui.upperImage.sprite == ui.levels[1].original, "第二组图片");
            Complete(ui);
            Check(ui.Completed == 2 && ui.Coins == 90, "第二关首次奖励");
            Click(ui.modalAction);
            Check(ui.SelectedLevel == 2 && ui.Round.Total == 15 && ui.upperImage.sprite == ui.levels[2].original, "第三组图片与十五处差异");
            Complete(ui);
            Check(ui.Completed == 3 && ui.Coins == 100 && settings.GetInt(key + "Perfect") == 3, "三关完成与无失误成就");
            Click(ui.modalAction);
            Check(ui.home.activeSelf && ui.album.activeSelf, "全部完成进入相册");
            Click(ui, "Album/Close");
            Click(ui.startButton);
            Check(ui.album.activeSelf, "首页相册入口");
            Click(ui, "Album/Card/Level0");
            Complete(ui);
            Check(ui.Coins == 100 && ui.Completed == 3 && settings.GetInt(key + "Perfect") == 3, "重玩不重复奖励或累计首次完美成就");
            Click(ui.modalSecondary);

            Click(ui.settingsButton);
            Click(ui, "Settings/Scroll/Viewport/Content/Sound");
            Click(ui, "Settings/Scroll/Viewport/Content/Music");
            Check(!settings.GetBool(key + "Sound") && !settings.GetBool(key + "Music"), "音效和音乐开关保存");
            Click(ui, "Settings/Scroll/Viewport/Content/SelectMusic");
            Click(ui, "MusicPanel/Card/Track2");
            Check(settings.GetInt(key + "MusicTrack") == 2 && settings.GetBool(key + "Music") && ui.musicSource.clip == ui.musicTracks[2], "选择音乐并启用");
            Click(ui, "MusicPanel/Close");
            Click(ui, "Settings/Scroll/Viewport/Content/Music");
            Click(ui, "Settings/Close");
            ui.OnOpen(null);
            Check(!settings.GetBool(key + "Sound") && !settings.GetBool(key + "Music") && !ui.musicSource.isPlaying, "重载保留关闭状态");
            Click(ui.settingsButton);
            Click(ui, "Settings/Scroll/Viewport/Content/Sound");
            Click(ui, "Settings/Scroll/Viewport/Content/Music");
            Check(settings.GetBool(key + "Sound") && settings.GetBool(key + "Music") && ui.musicSource.clip == ui.musicTracks[2], "重载后开关与选曲一致");
            Click(ui, "Settings/Scroll/Viewport/Content/Achievements");
            Check(ui.achievements.activeSelf && Text(ui, "Achievements/Card/Row3/State") == "已达成", "成就页面进度");
            Click(ui, "Achievements/Close");
            Click(ui, "Settings/Scroll/Viewport/Content/Album");
            Check(Button(ui, "Album/Card/Level2").interactable, "相册已通关解锁");
            Click(ui, "Album/Close");
            Click(ui, "Settings/Close");
            Click(ui.trophyButton);
            Check(ui.ranking.activeSelf && Text(ui, "Ranking/Locked").Contains("50"), "排行榜当前锁定状态");
            Click(ui, "Navigation/HomeTab");

            Click(ui, "Home/Avatar");
            Click(ui, "Profile/Card/Choices/Choice1");
            Click(ui, "Profile/Card/FrameTab");
            Click(ui, "Profile/Card/Choices/Choice3");
            ui.nicknameInput.text = "未保存";
            Check(!Button(ui, "Profile/Card/Save").interactable, "不足金币无法购买头像和框");
            Click(ui, "Profile/Close");
            Check(ui.Coins == 100 && ui.homeAvatar.sprite == ui.avatarSprites[0] && ui.homeFrame.color == UIDifferences.FrameColors[0], "取消资料编辑不扣费");
            Click(ui, "Home/Avatar");
            Check(ui.nicknameInput.text == "原始玩家" && ui.profileAvatar.sprite == ui.avatarSprites[0], "取消后恢复已保存资料");
            ui.nicknameInput.text = " ";
            Click(ui, "Profile/Card/Save");
            Check(ui.modal.activeSelf && ui.profile.activeSelf && ui.Coins == 100, "空昵称有可见反馈");
            Click(ui.modalAction);
            ui.nicknameInput.text = "金币不足测试";
            Click(ui, "Profile/Card/Choices/Choice1");
            ui.SaveProfile();
            Check(ui.modal.activeSelf && ui.Coins == 100 && (settings.GetInt(key + "OwnedAvatars", 1) & 2) == 0, "购买入口再次校验金币");
            Click(ui.modalAction);
            Click(ui, "Profile/Close");
            settings.SetInt(key + "Coins", 10000);
            ui.OnOpen(null);
            Click(ui, "Home/Avatar");
            Click(ui, "Profile/Card/Choices/Choice1");
            Click(ui, "Profile/Card/FrameTab");
            Click(ui, "Profile/Card/Choices/Choice3");
            ui.nicknameInput.text = "找不同测试员";
            Click(ui, "Profile/Card/Save");
            Check(ui.Coins == 2000 && !ui.profile.activeSelf && ui.homeAvatar.sprite == ui.avatarSprites[1] && ui.homeFrame.color == UIDifferences.FrameColors[3], "头像三千加框五千解锁并保存");
            Check((settings.GetInt(key + "OwnedAvatars") & 2) != 0 && (settings.GetInt(key + "OwnedFrames") & 8) != 0 && settings.GetString(key + "Nickname") == "找不同测试员", "资料所有权及昵称存档");
            ui.OnOpen(null);
            Click(ui, "Home/Avatar");
            Check(ui.nicknameInput.text == "找不同测试员" && ui.profileAvatar.sprite == ui.avatarSprites[1], "资料重载");
            Click(ui, "Profile/Card/Save");
            Check(ui.Coins == 2000, "已拥有的头像与框重复保存不收费");

            Click(ui.shopButton);
            var initialHints = ui.Hints;
            Click(ui, "Shop/Scroll/Viewport/Content/Free/Buy");
            Check(ui.Hints == initialHints + 1 && ui.Coins == 2000, "每日免费提示");
            Click(ui.modalAction);
            ui.ClaimDailyHint();
            Check(ui.Hints == initialHints + 1 && !Button(ui, "Shop/Scroll/Viewport/Content/Free/Buy").interactable, "每日礼物重复入口不重复发放");
            ui.OnOpen(null);
            Click(ui.shopButton);
            ui.ClaimDailyHint();
            Check(ui.Hints == initialHints + 1, "重载后每日礼物仍不可重复领取");
            settings.SetString(key + "GiftDate", DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"));
            ui.OnOpen(null); Click(ui.shopButton); ui.ClaimDailyHint();
            Check(ui.Hints == initialHints + 1, "日期回退不重复领取");
            settings.SetString(key + "GiftDate", DateTime.UtcNow.ToString("yyyy-MM-dd"));
            ui.OnOpen(null); Click(ui.shopButton);
            Click(ui, "Shop/Scroll/Viewport/Content/HintOffer/Buy");
            Check(ui.Coins == 1100 && ui.Hints == initialHints + 2, "九百金币兑换一次提示");
            Click(ui.modalAction);
            Click(ui, "Shop/Scroll/Viewport/Content/HintOffer/Buy");
            Click(ui.modalAction);
            ui.BuyHint();
            Check(ui.Coins == 200 && ui.Hints == initialHints + 3 && !Button(ui, "Shop/Scroll/Viewport/Content/HintOffer/Buy").interactable, "提示购买不足金币保护");
            ui.OnOpen(null);
            Check(ui.Coins == 200 && ui.Hints == initialHints + 3, "商店数据重载");

            settings.SetInt(key + "Coins", 29); settings.SetInt(key + "Completed", 0); settings.SetInt(key + "Perfect", 0);
            settings.SetInt(key + "RoundLevel", 0); settings.SetInt(key + "RoundMask", 1); settings.SetInt(key + "RoundLives", 0); settings.SetInt(key + "RoundMistakes", 3);
            ui.OnOpen(null); Click(ui.startButton); Click(ui.modalAction);
            Check(ui.Round.Lives == 0 && ui.Round.Count == 1 && ui.Coins == 29 && ui.modalTitle.text == "金币不足", "续命不足金币不会免费恢复");
            Click(ui.modalAction);
            Check(ui.Round.Lives == 3 && ui.Round.Count == 0 && ui.Coins == 29, "不足金币仍可清空重试");
            settings.SetInt(key + "Coins", 100); settings.SetInt(key + "RoundMask", 1); settings.SetInt(key + "RoundLives", 0); settings.SetInt(key + "RoundMistakes", 3);
            ui.OnOpen(null); Click(ui.startButton); Click(ui.modalAction);
            Check(ui.Coins == 70 && ui.Round.Count == 1 && ui.Round.Lives == 3, "恢复失败存档后付费续命");
            Complete(ui);
            Check(settings.GetInt(key + "Perfect") == 0 && ui.Coins == 80, "复活后三心通关不算无失误成就");
            ui.ShowHome(); ui.StartLevel();
            var hintBalance = ui.Hints;
            ui.UseHint();
            spotlight = ui.play.GetComponentInChildren<DifferenceHintSpotlight>();
            hintStep.Invoke(spotlight, new object[] { .4f });
            var pendingHint = spotlight.TargetIndex;
            ui.OnPause(); ui.OnResume();
            Check(spotlight.Ready && spotlight.TargetIndex == pendingHint, "框架暂停恢复不会丢失聚光目标");
            ui.ShowSettings(); Click(ui, "Settings/Close");
            Check(ui.HintActive && ui.Hints == hintBalance - 1, "关闭设置后恢复已付费提示，不重复消耗");
            ui.ShowHome();
            Check(!spotlight.gameObject.activeSelf && settings.GetInt(key + "RoundHint", -1) == pendingHint, "离开时清理遮罩并保存待点击提示");
            ui.OnOpen(null); ui.StartLevel();
            Check(ui.HintActive && ui.Round.Count == 0 && ui.Hints == hintBalance - 1 && spotlight.TargetIndex == pendingHint, "重载恢复提示位置，未自动算找到或再次扣道具");
            hintStep.Invoke(spotlight, new object[] { .4f });
            // GraphicRaycaster ignores depth -1 until Unity has rendered the newly activated page.
            for (var wait = 0; wait < 50 && (ui.upperImage.depth < 0 || spotlight.depth < 0); wait++)
                await System.Threading.Tasks.Task.Delay(20);
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var raycasts = new List<UnityEngine.EventSystems.RaycastResult>();
            foreach (var target in new[] { ui.upperImage, ui.lowerImage, (UnityEngine.UI.Image)spotlight })
            {
                var position = target == ui.upperImage ? ui.topRings[pendingHint].transform.position :
                    target == ui.lowerImage ? ui.bottomRings[pendingHint].transform.position : ui.hintButton.transform.position;
                pointer.position = RectTransformUtility.WorldToScreenPoint(camera, position);
                raycasts.Clear(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, raycasts);
                Check(raycasts.Count > 0 && raycasts[0].gameObject == target.gameObject, "真实UI射线：" + target.name + "，命中：" + string.Join(",", raycasts.ConvertAll(hit => hit.gameObject.name)));
            }
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, ui.bottomRings[pendingHint].transform.position);
            raycasts.Clear(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, raycasts);
            pointer.pointerPressRaycast = raycasts[0];
            UnityEngine.EventSystems.ExecuteEvents.Execute(ui.lowerImage.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Check(ui.Round.Count == 1 && settings.GetInt(key + "RoundHint", -1) == -1, "提示完成后清理待点击存档");
            hintStep.Invoke(spotlight, new object[] { .3f });
            ui.upperImage.GetComponent<DifferenceBoard>().ResetView();
            Canvas.ForceUpdateCanvases();

            PointerEventData BoardClick(UnityEngine.UI.Image image, float x, float y, int clickCount = 1)
            {
                var rect = image.rectTransform.rect;
                var input = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                    clickCount = clickCount, eligibleForClick = true,
                    position = RectTransformUtility.WorldToScreenPoint(camera, image.transform.TransformPoint(
                        new Vector3(rect.xMin + x * rect.width, rect.yMax - y * rect.height))) };
                raycasts.Clear(); EventSystem.current.RaycastAll(input, raycasts);
                Check(raycasts.Count > 0 && raycasts[0].gameObject == image.gameObject, "普通点按实际射线命中图片");
                input.pressPosition = input.position; input.pointerCurrentRaycast = input.pointerPressRaycast = raycasts[0];
                ExecuteEvents.Execute(image.gameObject, input, ExecuteEvents.pointerClickHandler);
                return input;
            }

            var instantIndex = 0;
            while (ui.Round.IsFound(instantIndex)) instantIndex++;
            var countBeforeClick = ui.Round.Count;
            var inputTimer = System.Diagnostics.Stopwatch.StartNew();
            BoardClick(ui.upperImage, ui.spots[instantIndex].x, ui.spots[instantIndex].y);
            Check(ui.Round.Count == countBeforeClick + 1 && ui.topRings[instantIndex].gameObject.activeSelf &&
                ui.bottomRings[instantIndex].gameObject.activeSelf, "普通单击同一调用立即判定并显示双图绿圈，无双击等待");
            var firstClickMs = inputTimer.Elapsed.TotalMilliseconds;
            do { instantIndex++; } while (ui.Round.IsFound(instantIndex));
            BoardClick(ui.lowerImage, ui.spots[instantIndex].x, ui.spots[instantIndex].y, 2);
            Check(ui.Round.Count == countBeforeClick + 2 && Near(ui.lowerImage.transform.localScale.x, 1), "连续点击第二处立即计分，不切换成双击缩放");
            BoardClick(ui.lowerImage, ui.spots[instantIndex].x, ui.spots[instantIndex].y, 3);
            Check(ui.Round.Count == countBeforeClick + 2 && ui.Round.Lives == 3, "立即重复点击不重记或扣心");
            pointer = BoardClick(ui.upperImage, .99f, .02f);
            Check(ui.Round.Lives == 2 && ui.crossTop.gameObject.activeSelf && ui.crossBottom.gameObject.activeSelf,
                "普通误点同一调用立即扣心并显示双图错误反馈");
            var board = ui.upperImage.GetComponent<DifferenceBoard>();
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(ui.Round.Lives == 2, "移除等待后拖拽及松手保护仍不误扣心");
            board.ResetView();
            pointer.scrollDelta = Vector2.up;
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.scrollHandler);
            ExecuteEvents.Execute(board.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(ui.Round.Lives == 2 && ui.upperImage.transform.localScale.x > 1 &&
                ui.upperImage.transform.localScale == ui.lowerImage.transform.localScale, "滚轮仍双图缩放并屏蔽缩放误触");
            board.ResetView(); Click(ui, "Play/Zoom");
            Check(Near(ui.upperImage.transform.localScale.x, 2) && ui.upperImage.transform.localScale == ui.lowerImage.transform.localScale,
                "缩放按钮继续可用");
            Debug.Log("Find Differences immediate pointer click handled in " + firstClickMs.ToString("F2") + " ms (including UI raycast and save).");

            var bridgeFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var simulator = (EditorWindow)typeof(DifferenceSimulatorWheel).GetField("window", bridgeFlags).GetValue(null);
            if (!simulator) simulator = Array.Find(Resources.FindObjectsOfTypeAll<EditorWindow>(),
                window => window.GetType().FullName == "UnityEditor.DeviceSimulation.SimulatorWindow");
            if (simulator)
            {
                var publicFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                var simulatorMain = simulator.GetType().GetProperty("main", publicFlags).GetValue(simulator);
                var simulatorUI = simulatorMain.GetType().GetProperty("userInterface", publicFlags).GetValue(simulatorMain);
                var deviceView = (VisualElement)simulatorUI.GetType().GetProperty("DeviceView", publicFlags).GetValue(simulatorUI);
                Check(deviceView.panel != null && ReferenceEquals(deviceView,
                    typeof(DifferenceSimulatorWheel).GetField("view", bridgeFlags).GetValue(null)), "Simulator 滚轮桥已挂载真实 DeviceView；运行前将鼠标移入 Simulator");
                var touchInput = simulatorMain.GetType().GetField("m_TouchInput", patchFlags).GetValue(simulatorMain);
                var toTouch = touchInput.GetType().GetMethod("ScreenPixelToTouchCoordinate", patchFlags);
                Vector2 TouchAt(Vector2 raw) { return (Vector2)toTouch.Invoke(touchInput, new object[] { raw }); }
                var origin = TouchAt(Vector2.zero);
                var axisX = TouchAt(Vector2.right) - origin;
                var axisY = TouchAt(Vector2.up) - origin;
                var determinant = axisX.x * axisY.y - axisX.y * axisY.x;
                Check(Mathf.Abs(determinant) > .000001f, "Simulator 屏幕坐标映射可逆");
                void WheelAt(UnityEngine.UI.Image image, float deltaY)
                {
                    Canvas.ForceUpdateCanvases();
                    var viewport = (RectTransform)image.transform.parent;
                    var point = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(viewport.rect.center));
                    var offset = point - origin;
                    var raw = new Vector2((offset.x * axisY.y - offset.y * axisY.x) / determinant,
                        (axisX.x * offset.y - axisX.y * offset.x) / determinant);
                    var viewToScreen = (Matrix4x4)deviceView.GetType().GetProperty("ViewToScreen", publicFlags).GetValue(deviceView);
                    var local = (Vector2)viewToScreen.inverse.MultiplyPoint(raw);
                    using (var wheel = WheelEvent.GetPooled(new Event { type = EventType.ScrollWheel,
                        mousePosition = deviceView.LocalToWorld(local), delta = new Vector2(0, deltaY) }))
                    {
                        wheel.target = deviceView;
                        deviceView.SendEvent(wheel);
                    }
                }
                void CheckSimulatorZoom(float expected, string label)
                {
                    Check(Near(ui.upperImage.transform.localScale.x, expected) &&
                        ui.upperImage.transform.localScale == ui.lowerImage.transform.localScale, label);
                }

                board.ResetView();
                WheelAt(ui.upperImage, -3);
                CheckSimulatorZoom(1.2f, "Simulator 真实上图滚轮事件经过桥接和射线后双图放大一次");
                WheelAt(ui.lowerImage, 3);
                CheckSimulatorZoom(1, "Simulator 真实下图滚轮事件双图缩小");
                WheelAt(ui.lowerImage, -3);
                CheckSimulatorZoom(1.2f, "Simulator 真实下图滚轮事件双图放大");
                ui.ShowDialog("滚轮检查", "弹窗期间不能缩放底图", "知道了", null);
                Canvas.ForceUpdateCanvases();
                var scrim = ui.modal.transform.Find("Scrim").GetComponent<UnityEngine.UI.Image>();
                for (var wait = 0; wait < 50 && scrim.depth < 0; wait++) await Task.Delay(20);
                Check(scrim.depth >= 0 && scrim.raycastTarget, "Simulator 滚轮检查时弹窗遮罩已加入实际射线");
                WheelAt(ui.upperImage, -3); WheelAt(ui.lowerImage, -3);
                CheckSimulatorZoom(1.2f, "Simulator 滚轮真实射线被弹窗拦截，不穿透上下图");
                ui.modal.SetActive(false);
                WheelAt(ui.upperImage, -3);
                CheckSimulatorZoom(1.4f, "关闭弹窗后 Simulator 滚轮恢复");
                board.ResetView();
                simulatorWheelReport = "Simulator DeviceView WheelEvent passed through the registered bridge and real UI raycast: upper/lower zoom, linked views, popup block and resume";
            }
            else Debug.Log(simulatorWheelReport);
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally
        {
            try
            {
                foreach (var name in intKeys)
                    if (original.TryGetValue(name, out var value)) settings.SetInt(key + name, (int)value); else settings.RemoveSetting(key + name);
                foreach (var name in boolKeys)
                    if (original.TryGetValue(name, out var value)) settings.SetBool(key + name, (bool)value); else settings.RemoveSetting(key + name);
                foreach (var name in stringKeys)
                    if (original.TryGetValue(name, out var value)) settings.SetString(key + name, (string)value); else settings.RemoveSetting(key + name);
                settings.Save();
                ui.OnOpen(null);
                foreach (var name in intKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetInt(key + name) == (int)original[name]), "恢复原整数存档 " + name);
                foreach (var name in boolKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetBool(key + name) == (bool)original[name]), "恢复原开关存档 " + name);
                foreach (var name in stringKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetString(key + name) == (string)original[name]), "恢复原文本存档 " + name);
            }
            catch (Exception error) { File.WriteAllText(report, "FAIL restoring original save: " + error); throw; }
            finally { ui.SetTesting(false); }
        }
        File.WriteAllText(report, "PASS: " + simulatorWheelReport + "; ordinary board pointer clicks immediately update hit/miss feedback in the same call, rapid clicks score without zooming, repeats and drag/scroll release guards remain safe, zoom button and linked wheel zoom work; local corresponding regions swap the opposite picture three times in 2.1 seconds, alternating full swap and original appearance without tinting or scaling; permanent pictures and found rings remain, repeated clicks do not replay, mid-flash shop return clears both effects; dual hint spotlights, smooth linked focus/return, real UI raycast and pointer click through spotlight, dark-area/repeated-charge guards, idle hand press/ripple without blocking input, pending hint survives settings/pause/reload, wave flights and delayed checks, existing three-level hit/miss/save/revive/shop/reward/profile checks; all 20 original setting values and missing keys restored.");
        Debug.Log("Find Differences V2 Play checks passed; all original settings restored.");
    }

    static DifferenceFoundFlight[] ActiveFlights(UIDifferences ui)
    { return Array.FindAll(ui.play.GetComponentsInChildren<DifferenceFoundFlight>(true), flight => flight.isActiveAndEnabled); }

    static void CheckHintGuide(UIDifferences ui)
    {
        var hand = ui.hintButton.GetComponentInChildren<DifferenceHintHand>(true);
        Check(ui.hintHandSprite && hand && !hand.Visible, "手指素材已绑定、开局不挡操作");
        var step = typeof(UIDifferences).GetMethod("AdvanceHintGuide", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        step.Invoke(ui, new object[] { 8.1f });
        Check(hand.Visible, "闲置后自动展示灯泡手指引导");
        typeof(DifferenceHintHand).GetMethod("Advance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hand, new object[] { .44f });
        var graphic = hand.transform.Find("Hand").GetComponent<UnityEngine.UI.Image>();
        Check(Near(graphic.transform.localScale.x, .92f) && graphic.rectTransform.anchoredPosition.sqrMagnitude < .01f, "指尖按压时对准按钮中心");
        Check(!hand.raycastTarget && !graphic.raycastTarget, "手和波纹均不会阻挡点击");
        using (var mesh = new UnityEngine.UI.VertexHelper())
        {
            typeof(DifferenceHintHand).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Invoke(hand, new object[] { mesh });
            Check(mesh.currentVertCount > 0, "按压带扩散波纹");
        }
        ui.ShowSettings(); step.Invoke(ui, new object[] { 10f });
        Check(!hand.Visible, "弹窗期间隐藏手指");
        Click(ui, "Settings/Close");
    }

    static void CheckGameplayArt(UIDifferences ui)
    {
        Check(ui.progressQuestion && ui.progressCheck && ui.progressQuestion != ui.progressCheck, "独立问号与勾素材引用");
        foreach (var dot in ui.progressDots)
            Check(dot.sprite == ui.progressQuestion && dot.color == Color.white && !dot.GetComponentInChildren<UnityEngine.UI.Text>(true).enabled, "顶部使用原色图标，没有字体字符重叠");
        var hint = ui.hintButton.GetComponent<UnityEngine.UI.Image>();
        Check(hint.sprite.name == "HintButton" && hint.type == UnityEngine.UI.Image.Type.Simple && hint.raycastTarget && ui.hintButton.targetGraphic == hint, "圆形提示按钮仍能接收点击");
        Check(ui.hintButton.transform.Find("Bulb").GetComponent<UnityEngine.UI.Image>().sprite.name == "HintBulb", "提示灯泡已替换");
        Check(ui.topRings.Length >= ui.Round.Total && ui.bottomRings.Length >= ui.Round.Total &&
            ui.play.GetComponentsInChildren<DifferenceFoundRing>(true).Length == ui.topRings.Length + ui.bottomRings.Length, "双图固定线宽圈与扩容后的数组完整对应");
        var marker = ui.topRings[0];
        var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
        Check(ring && !ring.raycastTarget && marker.color.a == 0 && ring.GetComponentInParent<DifferenceBoard>(), "细圈跟随图片且不挡点击，旧粗圈已透明");
        var originalSize = marker.rectTransform.sizeDelta;
        var draw = typeof(DifferenceFoundRing).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
        try
        {
            foreach (var size in new[] { 46f, 180f })
            {
                marker.rectTransform.sizeDelta = Vector2.one * size;
                Canvas.ForceUpdateCanvases();
                using (var mesh = new UnityEngine.UI.VertexHelper())
                {
                    draw.Invoke(ring, new object[] { mesh });
                    var minRadius = float.MaxValue; var maxRadius = 0f; var vertex = new UIVertex();
                    for (var i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        var radius = Vector2.Distance(vertex.position, ring.rectTransform.rect.center);
                        minRadius = Mathf.Min(minRadius, radius); maxRadius = Mathf.Max(maxRadius, radius);
                    }
                    Check(Near(maxRadius, size * .5f) && Near(maxRadius - minRadius, 8), "大小差异圈均保持八单位线宽，中心透明");
                }
            }
        }
        finally { marker.rectTransform.sizeDelta = originalSize; }
    }

    static void CheckFoundFlight(DifferenceFoundFlight flight, Vector3 source, RectTransform destination)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var point = typeof(DifferenceFoundFlight).GetMethod("Point", flags);
        Vector2 At(float t) { return (Vector2)point.Invoke(flight, new object[] { t }); }
        var start = At(0); var end = At(1);
        Check(Vector3.Distance(flight.transform.TransformPoint(start), source) < .01f &&
            Vector3.Distance(flight.transform.TransformPoint(end), destination.TransformPoint(destination.rect.center)) < .01f, "波浪线从点击处出发并准确落到对应进度圆点");
        var direction = end - start; var sideways = new Vector2(-direction.y, direction.x).normalized;
        for (var i = 0; i < 4; i++)
        {
            var t = .125f + i * .25f;
            var offset = Vector2.Dot(At(t) - Vector2.Lerp(start, end, t), sideways);
            Check(offset * (i % 2 == 0 ? 1 : -1) > 1, "波浪线交替向两侧摆动");
        }
        Check(!flight.raycastTarget && flight.transform.parent.name == "Play", "星光不拦截点击且不受图片视口裁切");
        var systems = flight.GetComponentsInChildren<ParticleSystem>();
        Check(systems.Length > 0, "飞行使用预制体粒子");
        foreach (var system in systems)
            Check(system.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported, "粒子材质可用");
    }

    static UnityEngine.UI.Button Button(UIDifferences ui, string path)
    {
        var node = ui.stage.Find(path);
        Check(node, "存在按钮节点 " + path);
        var button = node.GetComponent<UnityEngine.UI.Button>();
        Check(button, "存在按钮组件 " + path);
        return button;
    }
    static void Click(UIDifferences ui, string path) { Click(Button(ui, path)); }
    static void Click(UnityEngine.UI.Button button)
    {
        Check(button && button.gameObject.activeInHierarchy && button.interactable, "按钮可用 " + (button ? button.name : "null"));
        button.onClick.Invoke();
    }
    static string Text(UIDifferences ui, string path) { return ui.stage.Find(path).GetComponent<UnityEngine.UI.Text>().text; }
    static void Find(UIDifferences ui, int index) { ui.ClickImage(ui.spots[index].x, ui.spots[index].y); }
    static void Miss(UIDifferences ui) { ui.ClickImage(.99f, .02f); }
    static void Complete(UIDifferences ui)
    {
        for (var i = 0; i < ui.Round.Total; i++) Find(ui, i);
        Check(ui.Round.Complete && ui.modal.activeSelf && !ui.modalClose.gameObject.activeSelf, "完成关卡并显示结算");
    }
    static bool Near(float a, float b) { return Mathf.Abs(a - b) < .001f; }
    static void Check(bool pass, string label)
    { if (!pass) throw new InvalidOperationException("Play 检查失败：" + label); }
}
#endif
