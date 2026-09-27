using System;
using System.IO;
using System.Linq;
using GameFrameX.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[FilePath("ProjectSettings/DifferenceSdkSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class DifferenceSdkSettings : ScriptableSingleton<DifferenceSdkSettings>
{
    public string androidMaxSdkKey = "";
    public string androidAdjustAppToken = "";
    public string androidAdMobAppId = "";
    public string androidGoogleServicesJson = "";
    public string androidInterstitialAdUnitId = "", androidRewardedAdUnitId = "", androidBannerAdUnitId = "", androidAppOpenAdUnitId = "";
    public string iosMaxSdkKey = "";
    public string iosAdjustAppToken = "";
    public string iosAdMobAppId = "";
    public string iosGoogleServicesPlist = "";
    public string iosInterstitialAdUnitId = "", iosRewardedAdUnitId = "", iosBannerAdUnitId = "", iosAppOpenAdUnitId = "";
    public string iosTrackingDescription = "Your permission helps us provide relevant ads and measure their performance.";
    public bool adjustSandbox = true;
    [Serializable]
    public sealed class AdjustEventToken
    {
        public string eventName;
        public string androidToken = "";
        public string iosToken = "";
    }
    public AdjustEventToken[] adjustEventTokens = new AdjustEventToken[0];

    public void EnsureAdjustEventTokens()
    {
        var entries = (adjustEventTokens ?? new AdjustEventToken[0]).Where(entry => entry != null).ToList();
        foreach (var name in Enumerable.Range(1, 50).Select(level => "level_click_" + level).Concat(new[] { "allow_att_adjust_ios" }))
            if (!entries.Any(entry => entry.eventName == name)) entries.Add(new AdjustEventToken { eventName = name });
        adjustEventTokens = entries.ToArray();
    }

    public void SaveSettings() => Save(true);
}

public sealed class DifferenceSdkBuildWindow : EditorWindow
{
    Vector2 scroll;
    bool showAdjustEvents, actionPending;

    [MenuItem("GameFrameX/Build/Mobile Build (SDK)", false, 401)]
    public static void Open() => GetWindow<DifferenceSdkBuildWindow>("移动端打包 / SDK");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Android / iOS", EditorStyles.boldLabel);
        var ready = !actionPending && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledScope(!ready))
        {
            bool enabled = DifferenceSdkBuild.Enabled(BuildTarget.Android);
            bool next = EditorGUILayout.Toggle("包含 SDK（两个平台）", enabled);
            if (next != enabled)
            {
                QueueAction(() => DifferenceSdkBuild.SetEnabled(next));
            }
            EditorGUILayout.HelpBox(enabled
                ? "包含 MAX、Adjust、Firebase。按关卡规则展示插屏和 Banner，激励视频由玩家点击触发。"
                : "普通包：排除 SDK 程序集、原生库和平台依赖。", MessageType.Info);
            var settings = DifferenceSdkSettings.instance;
            EditorGUI.BeginChangeCheck();
            settings.adjustSandbox = EditorGUILayout.Toggle("Adjust 测试环境", settings.adjustSandbox);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Android 配置", EditorStyles.boldLabel);
            settings.androidMaxSdkKey = EditorGUILayout.TextField("MAX SDK Key", settings.androidMaxSdkKey);
            settings.androidAdjustAppToken = EditorGUILayout.TextField("Adjust App Token", settings.androidAdjustAppToken);
            settings.androidAdMobAppId = EditorGUILayout.TextField("AdMob App ID", settings.androidAdMobAppId);
            settings.androidGoogleServicesJson = ConfigFile("google-services.json", settings.androidGoogleServicesJson, "json");
            settings.androidInterstitialAdUnitId = EditorGUILayout.TextField("MAX 插屏广告位", settings.androidInterstitialAdUnitId);
            settings.androidRewardedAdUnitId = EditorGUILayout.TextField("MAX 激励广告位", settings.androidRewardedAdUnitId);
            settings.androidBannerAdUnitId = EditorGUILayout.TextField("MAX Banner 广告位", settings.androidBannerAdUnitId);
            settings.androidAppOpenAdUnitId = EditorGUILayout.TextField("MAX 开屏位（未启用）", settings.androidAppOpenAdUnitId);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("iOS 配置", EditorStyles.boldLabel);
            settings.iosMaxSdkKey = EditorGUILayout.TextField("MAX SDK Key", settings.iosMaxSdkKey);
            settings.iosAdjustAppToken = EditorGUILayout.TextField("Adjust App Token", settings.iosAdjustAppToken);
            settings.iosAdMobAppId = EditorGUILayout.TextField("AdMob App ID", settings.iosAdMobAppId);
            settings.iosGoogleServicesPlist = ConfigFile("GoogleService-Info.plist", settings.iosGoogleServicesPlist, "plist");
            settings.iosInterstitialAdUnitId = EditorGUILayout.TextField("MAX 插屏广告位", settings.iosInterstitialAdUnitId);
            settings.iosRewardedAdUnitId = EditorGUILayout.TextField("MAX 激励广告位", settings.iosRewardedAdUnitId);
            settings.iosBannerAdUnitId = EditorGUILayout.TextField("MAX Banner 广告位", settings.iosBannerAdUnitId);
            settings.iosAppOpenAdUnitId = EditorGUILayout.TextField("MAX 开屏位（未启用）", settings.iosAppOpenAdUnitId);
            settings.iosTrackingDescription = EditorGUILayout.TextField("ATT 权限说明", settings.iosTrackingDescription);
            showAdjustEvents = EditorGUILayout.Foldout(showAdjustEvents, "Adjust 事件 Token（Android / iOS）", true);
            if (showAdjustEvents)
            {
                settings.EnsureAdjustEventTokens();
                EditorGUILayout.HelpBox("按平台填写 Adjust 后台的 6 位事件 Token。空白项会保留待发，不会使用事件名代替 Token。ATT 事件仅适用于 iOS。", MessageType.Info);
                foreach (var entry in settings.adjustEventTokens)
                {
                    EditorGUILayout.LabelField(entry.eventName, EditorStyles.boldLabel);
                    if (entry.eventName != "allow_att_adjust_ios")
                        entry.androidToken = EditorGUILayout.TextField("Android Token", entry.androidToken);
                    entry.iosToken = EditorGUILayout.TextField("iOS Token", entry.iosToken);
                }
            }
            if (EditorGUI.EndChangeCheck()) settings.SaveSettings();
            EditorGUILayout.Space();
            if (GUILayout.Button("打 Android APK")) QueueAction(() => Build(BuildTarget.Android, false));
            if (GUILayout.Button("打 Android AAB")) QueueAction(() => Build(BuildTarget.Android, true));
            if (GUILayout.Button("导出 iOS Xcode 工程")) QueueAction(() => Build(BuildTarget.iOS, false));
        }
        if (!ready) EditorGUILayout.HelpBox("请退出运行模式，并等待 Unity 编译完成。", MessageType.Info);
        EditorGUILayout.HelpBox("Android 安装测试选择 APK，提交商店选择 AAB。iOS 导出后，在 Mac 上执行 pod install，再打开 .xcworkspace 编译和签名。SDK 包会检查配置，普通包不需要这些配置。", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void QueueAction(Action action)
    {
        if (actionPending) return;
        actionPending = true;
        // Finish the current IMGUI layout before compilation, dialogs or build failures can interrupt it.
        EditorApplication.delayCall += () =>
        {
            try { action(); }
            catch (BuildFailedException exception)
            {
                Debug.LogError(exception.Message);
                EditorUtility.DisplayDialog("打包未完成", exception.Message, "确定");
            }
            finally { actionPending = false; if (this) Repaint(); }
        };
    }

    static string ConfigFile(string label, string value, string extension)
    {
        EditorGUILayout.BeginHorizontal();
        value = EditorGUILayout.TextField(label, value);
        if (GUILayout.Button("选择", GUILayout.Width(45)))
        {
            var selected = EditorUtility.OpenFilePanel(label, "", extension);
            if (!string.IsNullOrEmpty(selected)) value = selected;
        }
        EditorGUILayout.EndHorizontal();
        return value;
    }

    public static void Build(BuildTarget target, bool aab)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            // A platform switch triggers a domain reload. Build only after that compilation finishes.
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            Debug.Log("平台已切换；Unity 编译完成后再次点击打包。");
            return;
        }
        DifferenceSdkBuild.Validate(target);
        var mode = DifferenceSdkBuild.Enabled(target) ? "sdk" : "plain";
        var extension = aab ? "aab" : "apk";
        var output = target == BuildTarget.iOS
            ? EditorUtility.SaveFolderPanel("导出到新的 Xcode 目录", "Builds", "iOS-" + mode)
            : EditorUtility.SaveFilePanel("Android " + extension, "Builds", "Differences-" + mode, extension);
        if (string.IsNullOrEmpty(output)) return;
        if (target == BuildTarget.iOS && Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new BuildFailedException("请选一个空目录导出 Xcode，避免旧 SDK 的 Pod/Framework 残留。");

        var minAndroid = PlayerSettings.Android.minSdkVersion;
        var minIos = PlayerSettings.iOS.targetOSVersionString;
        bool oldBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        bool oldDevelopment = EditorUserBuildSettings.development;
        try
        {
            if (DifferenceSdkBuild.Enabled(target))
            {
                if (target == BuildTarget.Android && (int)minAndroid < 24)
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                if (target == BuildTarget.iOS && (!Version.TryParse(minIos, out var version) || version < new Version(15, 0)))
                    PlayerSettings.iOS.targetOSVersionString = "15.0";
            }
            EditorUserBuildSettings.buildAppBundle = aab;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = false;
            new DifferenceAndroidBuild().Run(target, output);
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).ToArray(), output, target, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("构建失败，请查看 Console 中的第一条错误。");
            Debug.Log("构建完成：" + output);
        }
        finally
        {
            PlayerSettings.Android.minSdkVersion = minAndroid;
            PlayerSettings.iOS.targetOSVersionString = minIos;
            EditorUserBuildSettings.buildAppBundle = oldBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExport;
            EditorUserBuildSettings.development = oldDevelopment;
            HotFixEditorCompilerHelper.RemoveEditor();
        }
    }
}

public sealed partial class DifferenceSdkBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public const string Define = "ENABLE_DIFFERENCE_SDK";
    public int callbackOrder => -1000;
    internal static string ConfigPath(string path) => string.IsNullOrWhiteSpace(path) ? "" :
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Application.dataPath, "..", path));
    public static bool Enabled(BuildTarget target) =>
        (target == BuildTarget.Android || target == BuildTarget.iOS) &&
        PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(target)).Split(';').Contains(Define);

    public static void SetEnabled(bool enabled)
    {
        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.iOS })
        {
            var definitions = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d) && d != Define).ToList();
            if (enabled) definitions.Add(Define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", definitions));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void Validate(BuildTarget target)
    {
        if (!Enabled(target)) return;
        var settings = DifferenceSdkSettings.instance;
        bool android = target == BuildTarget.Android;
        if (string.IsNullOrWhiteSpace(android ? settings.androidMaxSdkKey : settings.iosMaxSdkKey) ||
            string.IsNullOrWhiteSpace(android ? settings.androidAdjustAppToken : settings.iosAdjustAppToken))
            throw new BuildFailedException("SDK 配置未填写：请在 Mobile Build (SDK) 填写当前平台的 MAX SDK Key 和 Adjust App Token；或取消 SDK 勾选打普通包。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(android ? settings.androidAdMobAppId : settings.iosAdMobAppId,
                @"^ca-app-pub-\d+~\d+$"))
            throw new BuildFailedException("请填写当前平台有效的 AdMob App ID（包含 ~，不是广告单元 ID）。");
        foreach (var adUnit in android
            ? new[] { settings.androidInterstitialAdUnitId, settings.androidRewardedAdUnitId, settings.androidBannerAdUnitId }
            : new[] { settings.iosInterstitialAdUnitId, settings.iosRewardedAdUnitId, settings.iosBannerAdUnitId })
            if (string.IsNullOrWhiteSpace(adUnit))
                throw new BuildFailedException("请填写当前平台的 MAX 插屏、激励和 Banner 广告位 ID。");
        var config = ConfigPath(android ? settings.androidGoogleServicesJson : settings.iosGoogleServicesPlist);
        if (string.IsNullOrWhiteSpace(config) || !File.Exists(config))
            throw new BuildFailedException("找不到 " + (android ? "google-services.json" : "GoogleService-Info.plist") +
                "：\n" + (string.IsNullOrEmpty(config) ? "尚未选择文件" : config) + "\n请在对应配置行点击“选择”，重新选择文件。");
        if (android) ReadGoogleServices(config, PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        else if (string.IsNullOrWhiteSpace(settings.iosTrackingDescription))
            throw new BuildFailedException("请填写 iOS 的 ATT 权限说明。");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Validate(report.summary.platform);
        if (Enabled(report.summary.platform) && Type.GetType("DifferenceSdkBootstrap, Difference.Sdk") == null)
            throw new BuildFailedException("SDK 程序集尚未完成编译，请等待 Unity 编译完成后重试。");
        if (report.summary.platform == BuildTarget.Android && Enabled(BuildTarget.Android) && (int)PlayerSettings.Android.minSdkVersion < 24)
            throw new BuildFailedException("SDK 包最低支持 Android API 24。请使用 Mobile Build (SDK) 窗口打包，或调整 Minimum API Level。");
    }

    [Serializable] class BootstrapConfig
    {
        public string maxSdkKey, adjustAppToken, interstitialAdUnitId, rewardedAdUnitId, bannerAdUnitId, appOpenAdUnitId;
        public bool adjustSandbox;
        public BootstrapEventToken[] adjustEventTokens;
    }
    [Serializable] class BootstrapEventToken { public string eventName, token; }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !Enabled(report.summary.platform)) return;
        var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
        if (first == null || scene.path != first.path) return;
        var type = Type.GetType("DifferenceSdkBootstrap, Difference.Sdk");
        if (type == null) throw new BuildFailedException("未找到 Difference.Sdk 程序集。");
        var root = new GameObject("DifferenceSdk");
        SceneManager.MoveGameObjectToScene(root, scene);
        var component = root.AddComponent(type);
        ConfigureBootstrap(component, report.summary.platform);
    }

    internal static void ConfigureBootstrap(Component component, BuildTarget target)
    {
        var settings = DifferenceSdkSettings.instance;
        bool android = target == BuildTarget.Android;
        settings.EnsureAdjustEventTokens();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new BootstrapConfig
        {
            maxSdkKey = android ? settings.androidMaxSdkKey : settings.iosMaxSdkKey,
            adjustAppToken = android ? settings.androidAdjustAppToken : settings.iosAdjustAppToken,
            interstitialAdUnitId = android ? settings.androidInterstitialAdUnitId : settings.iosInterstitialAdUnitId,
            rewardedAdUnitId = android ? settings.androidRewardedAdUnitId : settings.iosRewardedAdUnitId,
            bannerAdUnitId = android ? settings.androidBannerAdUnitId : settings.iosBannerAdUnitId,
            appOpenAdUnitId = android ? settings.androidAppOpenAdUnitId : settings.iosAppOpenAdUnitId,
            adjustSandbox = settings.adjustSandbox,
            adjustEventTokens = settings.adjustEventTokens
                .Where(entry => !android || entry.eventName != "allow_att_adjust_ios")
                .Select(entry => new BootstrapEventToken { eventName = entry.eventName, token = android ? entry.androidToken : entry.iosToken }).ToArray()
        }), component);
        var written = JsonUtility.FromJson<BootstrapConfig>(JsonUtility.ToJson(component));
        if (string.IsNullOrWhiteSpace(written.maxSdkKey) || string.IsNullOrWhiteSpace(written.adjustAppToken))
            throw new BuildFailedException("SDK 启动配置未写入场景，已停止打包。");
    }
}
