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
    static readonly string[] IntKeys = { "Completed", "Coins", "Hints", "Avatar", "Frame", "MusicTrack", "OwnedAvatars", "OwnedFrames", "TotalFound", "Perfect", "RoundLevel", "RoundMask", "RoundLives", "RoundMistakes", "RoundHint" };
    static readonly string[] BoolKeys = { "Sound", "Music", "Vibration" };
    static readonly string[] StringKeys = { "Nickname", "GiftDate", "RoundContent" };
    static bool running;
    static bool launcherCaptured;
    static double deadline;

    static DifferenceUiSmokeCheck() { EditorApplication.update += Tick; }

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
            var spine = page.GetComponentInChildren<Spine.Unity.SkeletonGraphic>(true);
            if (!spine) throw new Exception("成功页缺少 Spine");
            for (var i = 0; i < 2; i++)
            {
                page.SetActive(true);
                replay.Invoke(ui, null);
                var track = spine.AnimationState.GetCurrent(0);
                if (track == null || track.Animation.Name != "animation" || track.TrackTime != 0 || track.Loop || !spine.UnscaledTime)
                    throw new Exception("成功页动画未从头播放");
                spine.Update(track.Animation.Duration + .1f);
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
        foreach (var binding in new[] { "buttonSound|Click_Btn.wav", "failSound|False.wav", "foundSound|Found.wav", "missSound|Miss.wav", "winSound|Win.wav", "tipsSound|Tips.WAV", "homeMusic|MusicHome.wav", "gameMusic|MusicGame.wav" })
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
        Debug.Log("PASS: all 8 audio bindings; in Play mode also checks home/game music switching and mute");
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
        var background = Application.runInBackground;
        try
        {
            Application.runInBackground = true;
            ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
            if (!Application.isPlaying || !ui) throw new Exception("请先运行游戏");
            page = (GameObject)typeof(UIDifferences).GetField("victoryDesign", Private).GetValue(ui);
            wasActive = page.activeSelf;
            wasResult = (bool)Field(ui, "figmaResultActive");
            wasWon = (bool)Field(ui, "figmaResultWon");
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
            var source = new Vector3(0, -100, 0);
            var target = new Vector3(-250, 500, 0);
            var atStart = DifferenceRewardCoin.Position(source, target, 100, 0, 0, out var flight);
            var atEnd = DifferenceRewardCoin.Position(source, target, 100, 5, 0, out flight);
            if (atStart != source || Vector3.Distance(atEnd, target) > .001f || flight != 1)
                throw new Exception("金币轨迹起点或终点不正确");
            start.Invoke(ui, new object[] { 10 });
            if (!wallet.gameObject.activeSelf || label.text != (balance - 10).ToString()) throw new Exception("奖励起始数字不正确");
            await RewardFrames(.45f);
            var sample = page.GetComponentInChildren<DifferenceRewardCoin>();
            if (!sample) throw new Exception("未生成金币: page=" + page.activeInHierarchy + ", all=" + page.GetComponentsInChildren<DifferenceRewardCoin>(true).Length + ", routine=" + (Field(ui, "rewardRoutine") != null));
            sample.SetAppearance(Mathf.PI / 2 / 10.5f, 0, 0);
            var rim = sample.GetComponentsInChildren<Image>();
            if (rim.Length < 2 || rim[0].rectTransform.anchoredPosition.x == rim[rim.Length - 1].rectTransform.anchoredPosition.x)
                throw new Exception("侧面缺少厚度");
            ScreenCapture.CaptureScreenshot("Temp/reward-coins-pop.png");
            await RewardFrames(.6f);
            ScreenCapture.CaptureScreenshot("Temp/reward-coins-flight.png");
            await RewardFrames(2.2f);
            if (wallet.gameObject.activeSelf || label.text != balance.ToString() || ui.Coins != balance)
                throw new Exception("结算显示/隐藏不正确，或动画重复发奖");
            start.Invoke(ui, new object[] { 10 });
            cancel.Invoke(ui, null);
            if (wallet.gameObject.activeSelf || ui.Coins != balance) throw new Exception("取消结算未清理");
            File.WriteAllText("Temp/reward-coins-check.txt", "PASS: bottom origin, trajectory, edge thickness, count, hide, cancellation; balance unchanged");
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
            if (ui) typeof(UIDifferences).GetMethod("CancelRewardCoins", Private).Invoke(ui, null);
            if (ui)
            {
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
        if (!Application.isPlaying) throw new Exception("请进入 Play 并打开关卡后检查");
        var ui = UnityEngine.Object.FindObjectOfType<UIDifferences>();
        if (!ui || ui.Round == null || !ui.play.activeInHierarchy) throw new Exception("请先打开关卡");
        var node = UnityEngine.Object.Instantiate(ui.topRings[0].gameObject, ui.topRings[0].transform.parent);
        try
        {
            node.SetActive(true);
            var marker = node.GetComponent<Image>();
            var show = typeof(UIDifferences).GetMethod("ShowFoundRing", Private);
            show.Invoke(ui, new object[] { marker, true });
            var spine = marker.GetComponentInChildren<Spine.Unity.SkeletonGraphic>();
            var track = spine.AnimationState.GetCurrent(0);
            if (track.Animation.Name != "a2" || track.Loop || track.Next?.Animation.Name != "a1" || marker.enabled)
                throw new Exception("命中应由 Spine a2 切换到 a1，替换旧圆圈");
            var particles = marker.GetComponentsInChildren<ParticleSystem>();
            if (particles.Length == 0) throw new Exception("缺少命中粒子");
            foreach (var particle in particles)
                if (particle.main.loop || !particle.isPlaying) throw new Exception("粒子必须播放一次");
            show.Invoke(ui, new object[] { marker, false });
            if (spine.AnimationState.GetCurrent(0).Animation.Name != "a1" || marker.GetComponentsInChildren<ParticleSystem>().Length != particles.Length)
                throw new Exception("恢复进度不应再次生成粒子");
            Debug.Log("PASS: found ring particles + a2, restored a1");
        }
        finally { UnityEngine.Object.Destroy(node); }
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
            heart.AnimationState.Update(heart.AnimationState.GetCurrent(0).Animation.Duration + .01f);
            heart.AnimationState.Update(.01f);
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
            await Task.Delay(750);
            if (ui.crossTop.gameObject.activeSelf || ui.crossBottom.gameObject.activeSelf)
                throw new InvalidOperationException("Bound miss feedback did not clear.");
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
            var result = "PASS: generated bindings and UI initialization succeeded; hint effects created; zero-size layout and pre-initialization updates are safe; authored stage restored.";
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
        foreach (var path in new[] { "Play", "Play/Background", "Play/Back", "Play/Zoom", "Play/Hint", "Play/Hint/Bulb", "Play/Frame", "Play/Progress", "Play/UpperViewport", "Play/LowerViewport" })
        {
            var actual = (RectTransform)ui.stage.Find(path);
            var expected = (RectTransform)prefab.stage.Find(path);
            // Layout/AspectRatio fitters legitimately drive runtime RectTransform values.
            if (!actual.drivenByObject && (actual.anchorMin != expected.anchorMin || actual.anchorMax != expected.anchorMax ||
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
            if (check == "spine-alpha") { BuildAndroidSpineShaderCheck(); return; }
            if (check == "startup-await") { CheckStartupAwaitOnly(); return; }
            if (check == "settings-connect") { DifferenceGameBuilder.ConnectSettingsActions(); CheckSettingsActionsOnly(); return; }
            if (check == "settings") { CheckSettingsActionsOnly(); return; }
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
        if (EditorApplication.isPlaying && ui) { SessionState.SetBool(Pending, false); Run(ui); }
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
            Check(ui.Round.Count == 1 && ui.topRings[0].isActiveAndEnabled && ui.bottomRings[0].isActiveAndEnabled, "correct click adds both rings");
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
