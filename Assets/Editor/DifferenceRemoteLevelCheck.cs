#if ENABLE_UI_UGUI
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceRemoteLevelCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Endpoint = "https://jaspergame-diff.tiaoya.com/api/greyfun/levels/1";
    static bool running;

    [MenuItem("Tools/Find Differences/Run Remote Level Checks")]
    public static async void Run()
    {
        if (running) return;
        var ui = FindUI();
        var settings = GameApp.Setting;
        const string key = UIDifferences.Key;
        var intKeys = new[] { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint" };
        var boolKeys = new[] { "Sound", "Music" };
        var stringKeys = new[] { "Nickname", "GiftDate", "RoundContent" };
        var original = new Dictionary<string, object>();
        foreach (var name in intKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetInt(key + name);
        foreach (var name in boolKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetBool(key + name);
        foreach (var name in stringKeys) if (settings.HasSetting(key + name)) original[name] = settings.GetString(key + name);
        var originalUrl = ui.levelApiUrl;
        var originalEnabled = ui.enabled;
        var originalTesting = (bool)typeof(UIDifferences).GetField("testing", Private).GetValue(ui);
        var report = Path.Combine(Application.dataPath, "../Temp/FindDifferences-remote-check.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(report));
        DifferenceLevel data = null;
        string contentKey = null;
        var failed = false;
        running = true;
        try
        {
            DifferenceRound.SelfCheck();
            DifferenceRemoteLevel.SelfCheck();
            ui.ShowHome();
            Invoke(ui, "ReleaseRemoteLevel");
            ui.SetTesting(false);
            foreach (var name in intKeys) settings.RemoveSetting(key + name);
            foreach (var name in boolKeys) settings.RemoveSetting(key + name);
            foreach (var name in stringKeys) settings.RemoveSetting(key + name);
            settings.SetInt(key + "Coins", 100);
            settings.SetBool(key + "Sound", false);
            settings.SetBool(key + "Music", false);
            ui.levelApiUrl = Endpoint;
            ui.OnOpen(null);
            ui.SelectLevel(0);
            await WaitForLoad(ui);
            Check(ui.play.activeSelf && ui.Round != null && ui.Round.Total == 15 && ui.Round.Count == 0, "真实线上第一关加载 15 处差异");
            data = (DifferenceLevel)typeof(UIDifferences).GetProperty("CurrentLevel", Private).GetValue(ui);
            contentKey = data.contentKey;
            Check(!string.IsNullOrEmpty(contentKey) && data.changedOnTop, "后台内容标识与上图覆盖方向");
            Check(data.original.texture.width == 1500 && data.original.texture.height == 1000 &&
                data.changed.texture.width == 1500 && data.changed.texture.height == 1000, "线上两张图片均为 1500 × 1000");
            Check(data.croppedPatches.Length == 15 && data.hitSpots.Length == 15, "完整裁剪块与矩形点击区域");
            var originals = OriginalPatches(ui);
            var changedFlashes = ChangedFlashes(ui);
            for (var i = 0; i < 15; i++)
            {
                var originalRect = (RectTransform)originals[i].transform.parent;
                var changedRect = (RectTransform)changedFlashes[i].transform.parent;
                var center = new Vector2(data.regions[i].x * 600, -data.regions[i].y * 400);
                Check(ui.spots[i].rectangular && data.croppedPatches[i].texture == data.changed.texture &&
                    ui.patchImages[i].sprite == data.croppedPatches[i] && ui.differencePatches[i].transform.parent == ui.upperImage.transform &&
                    originals[i].sprite.texture == data.original.texture && originalRect.parent == ui.upperImage.transform &&
                    changedFlashes[i].sprite.texture == data.changed.texture && changedRect.parent == ui.lowerImage.transform &&
                    originalRect.GetComponent<UnityEngine.UI.RectMask2D>() && changedRect.GetComponent<UnityEngine.UI.RectMask2D>() &&
                    originalRect.pivot == Vector2.one * .5f && changedRect.pivot == Vector2.one * .5f &&
                    Vector2.Distance(originalRect.anchoredPosition, center) < .001f && Vector2.Distance(changedRect.anchoredPosition, center) < .001f && RestoredPatch(ui, i),
                    "临时裁块使用对侧纹理并原位对齐，常驻图片保持原样 " + i);
            }
            Check(data.croppedPatches[0].rect == new Rect(969, 563, 204, 377), "D01 裁剪包含左上坐标到 Unity Y 的转换");
            Check(data.croppedPatches[1].rect == new Rect(0, 510, 87, 295) &&
                data.croppedPatches[5].rect == new Rect(0, 94, 128, 188), "D02、D06 左边缘裁剪");
            Check(Near(ui.spots[1].left, 0) && Near(ui.spots[1].top, .2f) && Near(ui.spots[1].width, 81f / 1500) && Near(ui.spots[1].height, .283f) &&
                Near(ui.spots[5].left, 0) && Near(ui.spots[5].top, .724f) && Near(ui.spots[5].width, 122f / 1500) && Near(ui.spots[5].height, .175f), "边缘点击范围使用 hit_bounds");

            ui.enabled = false;
            ui.noticeLabel.transform.parent.gameObject.SetActive(false);
            await CaptureFlash("before");
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
            Check(ui.Round.Count == 1 && ui.Round.IsFound(0) && FlashPhase(ui, 0, 0), "上图点击记分，临时互换层从透明开始");
            Invoke(ui, "AdvancePatches", .35f);
            Check(FlashPhase(ui, 0, 1), "第一轮双侧局部完整互换，原色原尺寸");
            await CaptureFlash("during");
            Invoke(ui, "AdvancePatches", .35f);
            Check(FlashPhase(ui, 0, 0), "第一轮淡出还原，保留临时层等待下一轮");
            await CaptureFlash("restored");
            for (var pulse = 1; pulse < 3; pulse++)
            {
                Invoke(ui, "AdvancePatches", .35f);
                Check(FlashPhase(ui, 0, 1), "第 " + (pulse + 1) + " 轮互换，原色原尺寸");
                Invoke(ui, "AdvancePatches", .35f);
                Check(pulse == 2 ? RestoredPatch(ui, 0) : FlashPhase(ui, 0, 0), "第 " + (pulse + 1) + " 轮还原");
            }
            Check(RestoredPatch(ui, 0) && ui.topRings[0].gameObject.activeSelf && ui.bottomRings[0].gameObject.activeSelf,
                "2.1 秒三轮互换后恢复各自原样并保留双圈");
            await CaptureFlash("after");
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y, true);
            Invoke(ui, "AdvancePatches", .1f);
            Check(ui.Round.Count == 1 && ui.Round.Lives == 3 && RestoredPatch(ui, 0), "另一张图重复点击不扣血、不重播闪动");
            ui.ClickImage(ui.spots[1].left, ui.spots[1].y, true);
            Check(ui.Round.Count == 2 && ui.Round.IsFound(1), "下图左边缘点击计数");
            Invoke(ui, "AdvancePatches", .1f);
            Check(ui.patchImages[1].color == Color.white && changedFlashes[1].color.a < 1 && originals[1].isActiveAndEnabled &&
                changedFlashes[1].isActiveAndEnabled, "准备原色裁块闪动中途离页");
            ui.ShowHome();
            Check(ui.Round == null && RestoredPatch(ui, 0) && RestoredPatch(ui, 1), "离页后即使 Round 已清空也立即恢复图片");
            ui.enabled = originalEnabled;
            ui.StartLevel();
            await WaitForLoad(ui);
            Check(ui.Round.Count == 2 && RestoredPatch(ui, 0) && RestoredPatch(ui, 1) &&
                ui.topRings[1].gameObject.activeSelf && ui.bottomRings[1].gameObject.activeSelf, "返回首页继续保留原图差异与已找到双圈");

            ui.ShowHome();
            ui.levelApiUrl = "invalid-remote-check-url";
            ui.SelectLevel(0);
            await WaitForLoad(ui);
            Check(ui.Round == null && ui.modal.activeSelf && ui.modalTitle.text == "关卡加载失败" && settings.GetInt(key + "RoundMask") == 3, "加载失败保留原关卡进度并提供重试");
            ui.levelApiUrl = Endpoint;
            ui.modalAction.onClick.Invoke();
            await WaitForLoad(ui);
            Check(ui.play.activeSelf && ui.Round.Count == 2 && RestoredPatch(ui, 0) && RestoredPatch(ui, 1), "重试后恢复进度，两侧图片保持原样");
            ui.ShowHome();
            data.contentKey = contentKey + "-check-revision";
            ui.StartLevel();
            Check(ui.Round.Count == 0 && ui.Round.FoundMask == 0 && RestoredPatch(ui, 0), "内容版本变化拒绝旧 mask 且无残留闪动");
            data.contentKey = contentKey;
            ui.ClickImage(ui.spots[0].x, ui.spots[0].y);
            Check(ui.Round.Count == 1, "准备远程进度用于切换来源检查");
            ui.ShowHome(); ui.levelApiUrl = ""; ui.SelectLevel(0);
            Check(ui.Round.Total == 10 && ui.Round.Count == 0, "远程进度不能恢复到本地关卡");
            ui.ShowHome(); ui.levelApiUrl = Endpoint; ui.SelectLevel(0);
            Check(ui.Round.Total == 15 && ui.Round.Count == 0, "本地进度不能恢复到远程关卡");

            ui.ShowHome();
            settings.SetInt(key + "Completed", 3);
            ui.OnOpen(null);
            var baseImages = new HashSet<int>();
            var changedImages = new HashSet<int>();
            var contentKeys = new HashSet<string>();
            for (var index = 0; index < 4; index++)
            {
                var url = Endpoint.Substring(0, Endpoint.Length - 1) + (index + 1);
                Check(ui.LevelUrl(index) == url, "按当前关卡编号生成接口地址 " + (index + 1));
                var previous = (DifferenceRemoteLevel)typeof(UIDifferences).GetField("remoteLevel", Private).GetValue(ui);
                ui.ShowHome(); ui.SelectLevel(index);
                await WaitForLoad(ui);
                var current = CurrentLevel(ui);
                Check(ui.play.activeSelf && ui.Round != null && ui.Round.Count == 0 &&
                    ui.SelectedLevel == index && ui.levelLabel.text == "第" + (index + 1) + "关" &&
                    current.contentKey.StartsWith(url + "|", StringComparison.Ordinal) &&
                    (string)typeof(UIDifferences).GetField("loadedApiUrl", Private).GetValue(ui) == url,
                    "关卡编号、画面和进度一致 " + (index + 1));
                Check(baseImages.Add(current.original.texture.GetInstanceID()) && changedImages.Add(current.changed.texture.GetInstanceID()) &&
                    contentKeys.Add(current.contentKey), "不同关卡使用独立内容与两张新图片 " + (index + 1));
                if (index > 0) Check(previous.Level == null, "换关成功释放上一关资源 " + index);
                Check(current.croppedPatches.Length == ui.Round.Total && current.hitSpots.Length == ui.Round.Total &&
                    ui.progressDots.Length >= ui.Round.Total && ui.differencePatches.Length >= ui.Round.Total &&
                    ui.patchImages.Length >= ui.Round.Total && OriginalPatches(ui).Length >= ui.Round.Total && ChangedFlashes(ui).Length >= ui.Round.Total &&
                    ui.topRings.Length >= ui.Round.Total && ui.bottomRings.Length >= ui.Round.Total,
                    "按远程差异数量配置完整 UI " + (index + 1));
                var last = ui.Round.Total - 1;
                var spot = ui.spots[last];
                Check(spot.rectangular && new DifferenceRound(new[] { spot }, UIDifferences.ImageAspect).Click(
                    spot.left + spot.width * .01f, spot.top + spot.height * .01f) == 0, "矩形角落点击命中 " + (index + 1));
                ui.ClickImage(spot.x, spot.y);
                Check(ui.Round.Count == 1 && ui.Round.IsFound(last), "最后一个差异点可点击 " + (index + 1));
                Invoke(ui, "AdvancePatches", 2.1f);
                Check(RestoredPatch(ui, last) && OriginalPatches(ui)[last].sprite.texture == current.original.texture &&
                    ChangedFlashes(ui)[last].sprite.texture == current.changed.texture &&
                    ui.patchImages[last].sprite.texture == current.changed.texture && ui.topRings[last].gameObject.activeSelf &&
                    ui.bottomRings[last].gameObject.activeSelf, "最后一个差异点闪动后恢复各自图片并保留双圈 " + (index + 1));
            }
            Check(ui.Round.Total == 25 && ui.Round.IsFound(24), "第 4 关完整加载 25 处差异并命中第 25 处");
            Check(ui.progressDots[0].transform.parent != ui.progressDots[24].transform.parent, "25 处差异进度分成两行");
            ui.ShowHome(); ui.ShowAlbum();
            var firstRow = ui.stage.Find("Album/Card/Level0");
            var rowTitle = firstRow.Find("Title").GetComponent<UnityEngine.UI.Text>();
            Check((int)typeof(UIDifferences).GetField("albumPage", Private).GetValue(ui) == 1 && rowTitle.text.StartsWith("4 "), "相册定位第 4 关所在页");
            ui.stage.Find("Album/Card/PreviousPage").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(rowTitle.text.StartsWith("1 "), "相册上一页显示第 1 关");
            ui.stage.Find("Album/Card/NextPage").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(rowTitle.text.StartsWith("4 "), "相册下一页返回第 4 关");
            firstRow.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            await WaitForLoad(ui);
            Check(ui.SelectedLevel == 3 && ui.Round.Count == 1 && ui.Round.IsFound(24), "相册第 4 关按钮恢复已有进度");
            ui.ShowHome();
            Invoke(ui, "ReleaseRemoteLevel");
            ui.OnOpen(null);
            Check(ui.SelectedLevel == 3 && ui.startLabel.text == "继续 · 第4关", "重新打开后继续第 4 关");
            ui.StartLevel();
            await WaitForLoad(ui);
            Check(ui.SelectedLevel == 3 && ui.levelLabel.text == "第4关" && ui.Round.Total == 25 && ui.Round.Count == 1 &&
                ui.Round.IsFound(24) && RestoredPatch(ui, 24) && ui.topRings[24].gameObject.activeSelf &&
                ui.bottomRings[24].gameObject.activeSelf, "重新下载第 4 关恢复第 25 处进度与双圈，原差异仍保留");

            ui.ShowHome(); ui.SelectLevel(2);
            await WaitForLoad(ui);
            if (ui.Round.Total <= 15)
                Check(ui.progressDots[0].transform.parent == ui.progressDots[ui.Round.Total - 1].transform.parent, "切回较少差异的第 3 关恢复单行进度");
            for (var i = 0; i < ui.Round.Total; i++) ui.ClickImage(ui.spots[i].x, ui.spots[i].y);
            var completedAt = Time.unscaledTime;
            Check(ui.Round.Complete && !ui.modal.activeSelf, "实际找到第 3 关全部差异，最后一处动画尚未结束时不弹结算");
            var resultDeadline = DateTime.UtcNow.AddSeconds(5);
            while (ui && EditorApplication.isPlaying && !ui.modal.activeSelf && DateTime.UtcNow < resultDeadline) await Task.Delay(50);
            Check(ui && ui.modal.activeSelf && ui.modalTitle.text == "太棒了！" && ui.modalActionLabel.text == "下一关", "第 3 关通关后显示下一关按钮");
            Check(Time.unscaledTime - completedAt >= 2.05f && RestoredPatch(ui, ui.Round.Total - 1), "通关弹窗等待最后一处约 2.1 秒互换动画结束");
            ui.modalAction.onClick.Invoke();
            await WaitForLoad(ui);
            Check(ui.SelectedLevel == 3 && ui.levelLabel.text == "第4关" && ui.Round.Total == 25 && ui.Round.Count == 0 &&
                CurrentLevel(ui).contentKey.StartsWith(ui.LevelUrl(3) + "|", StringComparison.Ordinal), "第 3 关通关按钮进入全新第 4 关");
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath, "Screenshots/differences-greyfun-level4.png"));
            await Task.Delay(250);

            ui.ShowHome();
            var remoteField = typeof(UIDifferences).GetField("remoteLevel", Private);
            var completedRequest = (DifferenceRemoteLevel)remoteField.GetValue(ui);
            Check(completedRequest != null && completedRequest.Level != null, "已下载关卡可用于接管前取消检查");
            remoteField.SetValue(ui, null);
            typeof(UIDifferences).GetField("loadingLevel", Private).SetValue(ui, completedRequest);
            ui.ShowHome();
            Check(!ui.IsLoadingLevel && completedRequest.Level == null, "下载完成但界面尚未接管时取消也释放关卡资源");
            ui.SelectLevel(0);
            Check(ui.IsLoadingLevel, "启动新的真实网络请求");
            var cancelled = (DifferenceRemoteLevel)typeof(UIDifferences).GetField("loadingLevel", Private).GetValue(ui);
            ui.ShowHome();
            Check(!ui.IsLoadingLevel && ui.home.activeSelf && ui.Round == null, "立即返回首页取消加载");
            await Task.Delay(1000);
            Check(ui && EditorApplication.isPlaying && !ui.IsLoadingLevel && ui.home.activeSelf && ui.Round == null && cancelled.Level == null,
                "取消请求完成后不能跳回游戏或安装关卡");
        }
        catch (Exception error)
        {
            failed = true; File.WriteAllText(report, "FAIL: " + error); Debug.LogException(error);
        }
        finally
        {
            try
            {
                if (data != null) data.contentKey = contentKey;
                try { if (ui) ui.ShowHome(); }
                finally
                {
                    foreach (var name in intKeys)
                        if (original.TryGetValue(name, out var value)) settings.SetInt(key + name, (int)value); else settings.RemoveSetting(key + name);
                    foreach (var name in boolKeys)
                        if (original.TryGetValue(name, out var value)) settings.SetBool(key + name, (bool)value); else settings.RemoveSetting(key + name);
                    foreach (var name in stringKeys)
                        if (original.TryGetValue(name, out var value)) settings.SetString(key + name, (string)value); else settings.RemoveSetting(key + name);
                    settings.Save();
                    if (ui) { ui.levelApiUrl = originalUrl; ui.SetTesting(originalTesting); ui.enabled = originalEnabled; }
                }
                if (ui) ui.OnOpen(null);
                foreach (var name in intKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetInt(key + name) == (int)original[name]), "恢复整数存档 " + name);
                foreach (var name in boolKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetBool(key + name) == (bool)original[name]), "恢复开关存档 " + name);
                foreach (var name in stringKeys)
                    Check(settings.HasSetting(key + name) == original.ContainsKey(name) && (!original.ContainsKey(name) || settings.GetString(key + name) == (string)original[name]), "恢复文本存档 " + name);
            }
            catch (Exception error)
            {
                failed = true; File.AppendAllText(report, "\nFAIL restoring original save: " + error); Debug.LogException(error);
            }
            finally { running = false; }
        }
        if (failed) return;
        File.WriteAllText(report, "PASS: live levels 1-4 use numbered URLs and distinct image pairs; all 25 differences on level 4 retain their original pictures and found rings after flash and save reload; corresponding regions swap the opposite picture three times in 2.1 seconds, alternating full swap and original appearance with unchanged RGB and unit scale; permanent images remain visible and white; completion waits for the last swap animation; repeated clicks do not replay, leaving mid-flash resets both effects even after Round is null; before/during/restored/after screenshots captured; album paging, level 3 completion advances to level 4, dynamic slots and two-row progress, rectangular hit bounds and shared texture crops, failed-load retry, revision/source mismatch, resource release and request cancellation; all 20 original settings and enabled state restored.");
        Debug.Log("Find Differences remote level checks passed; original settings restored.");
    }

    [MenuItem("Tools/Find Differences/Preview Remote Level")]
    public static async void Preview()
    {
        if (running) return;
        var ui = FindUI();
        ui.SetTesting(false);
        ui.levelApiUrl = Endpoint;
        ui.SelectLevel(0);
        await WaitForLoad(ui);
        Check(ui.play.activeSelf && ui.Round != null, "线上关卡预览加载成功");
        Canvas.ForceUpdateCanvases();
        Debug.Log("Remote level preview loaded; no differences clicked and no coins or hints consumed.");
    }

    static UIDifferences FindUI()
    {
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) throw new InvalidOperationException("请先通过 Launcher 启动找不同");
        return ui;
    }

    static async Task WaitForLoad(UIDifferences ui)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (ui && EditorApplication.isPlaying && ui.IsLoadingLevel && DateTime.UtcNow < deadline) await Task.Delay(50);
        Check(ui && EditorApplication.isPlaying && !ui.IsLoadingLevel, "在线加载应在 90 秒内结束");
    }

    static DifferenceLevel CurrentLevel(UIDifferences ui) =>
        (DifferenceLevel)typeof(UIDifferences).GetProperty("CurrentLevel", Private).GetValue(ui);

    static UnityEngine.UI.Image[] OriginalPatches(UIDifferences ui) =>
        (UnityEngine.UI.Image[])typeof(UIDifferences).GetField("originalPatchImages", Private).GetValue(ui);

    static UnityEngine.UI.Image[] ChangedFlashes(UIDifferences ui) =>
        (UnityEngine.UI.Image[])typeof(UIDifferences).GetField("changedFlashImages", Private).GetValue(ui);

    static bool RestoredPatch(UIDifferences ui, int index)
    {
        var original = OriginalPatches(ui)[index];
        var changed = ChangedFlashes(ui)[index];
        return ui.differencePatches[index].activeSelf && ui.patchImages[index].color == Color.white &&
            original.color == Color.white && changed.color == Color.white &&
            !original.transform.parent.gameObject.activeSelf && !changed.transform.parent.gameObject.activeSelf &&
            original.transform.parent.localScale == Vector3.one && changed.transform.parent.localScale == Vector3.one;
    }

    static bool FlashPhase(UIDifferences ui, int index, float alpha)
    {
        var original = OriginalPatches(ui)[index];
        var changed = ChangedFlashes(ui)[index];
        var tint = changed.color;
        return original.isActiveAndEnabled && changed.isActiveAndEnabled && tint == original.color &&
            tint.r == 1 && tint.g == 1 && tint.b == 1 && Near(tint.a, alpha) &&
            original.transform.parent.localScale == Vector3.one && changed.transform.parent.localScale == Vector3.one &&
            ui.patchImages[index].color == Color.white && ui.differencePatches[index].activeSelf;
    }

    static async Task CaptureFlash(string phase)
    {
        var directory = Path.Combine(Application.dataPath, "Screenshots");
        Directory.CreateDirectory(directory);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "differences-flash-" + phase + ".png"));
        await Task.Delay(250);
    }

    static void Invoke(UIDifferences ui, string name, params object[] args)
    { typeof(UIDifferences).GetMethod(name, Private).Invoke(ui, args); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .00001f;
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("远程关卡检查失败：" + message); }
}
#endif
