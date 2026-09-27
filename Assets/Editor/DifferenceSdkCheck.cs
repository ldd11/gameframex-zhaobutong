using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

public static class DifferenceSdkCheck
{
    [MenuItem("Tools/Find Differences/Check Mobile SDK Switch")]
    public static void Run()
    {
        var settings = DifferenceSdkSettings.instance;
        var saved = JsonUtility.ToJson(settings);
        const string report = "Temp/difference-sdk-check.txt";
        try
        {
            CheckBootstrapConfiguration();
            var directory = Path.GetFullPath("Temp/difference-sdk-check/" + GUID.Generate());
            var library = directory + "/unityLibrary";
            Directory.CreateDirectory(library + "/src/main");
            Directory.CreateDirectory(directory + "/launcher");
            File.WriteAllText(library + "/build.gradle", "apply plugin: 'com.android.library'\n");
            File.WriteAllText(directory + "/launcher/build.gradle", "apply plugin: 'com.android.application'\n");
            File.WriteAllText(directory + "/settings.gradle", "pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }\n");
            File.WriteAllText(directory + "/gradle.properties", "org.gradle.jvmargs=-Xmx4096M\n");
            File.WriteAllText(library + "/src/main/AndroidManifest.xml",
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application><activity android:name=\"com.unity3d.player.UnityPlayerActivity\" /></application></manifest>");
            settings.androidGoogleServicesJson = directory + "/google-services.json";
            settings.androidAdMobAppId = "ca-app-pub-3940256099942544~3347511713"; // Google's documented Android test app.
            settings.androidMaxSdkKey = "local-build-check-only";
            File.WriteAllText(settings.androidGoogleServicesJson,
                "{\"project_info\":{\"project_id\":\"local-check\",\"project_number\":\"1234\"},\"client\":[{\"client_info\":{\"mobilesdk_app_id\":\"1:1234:android:local\",\"android_client_info\":{\"package_name\":\"com.example.sdkcheck\"}},\"api_key\":[{\"current_key\":\"local-check-only\"}]}]}");
            bool mismatchRejected = false;
            try { DifferenceSdkBuild.ReadGoogleServices(settings.androidGoogleServicesJson, "wrong.package"); }
            catch (BuildFailedException) { mismatchRejected = true; }
            Require(mismatchRejected, "Firebase 包名校验没有拒绝错误配置");
            var previousDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(Path.GetTempPath());
                var relativeConfig = "Temp/difference-sdk-check/" + Path.GetFileName(directory) + "/google-services.json";
                Require(DifferenceSdkBuild.ConfigPath(relativeConfig) == Path.GetFullPath(settings.androidGoogleServicesJson),
                    "相对配置路径必须以 Unity 项目为根目录");
                DifferenceSdkBuild.ReadGoogleServices(relativeConfig, "com.example.sdkcheck");
                Require(DifferenceSdkBuild.ConfigPath("") == "", "空配置路径应保留为空");
            }
            finally { Directory.SetCurrentDirectory(previousDirectory); }

            for (int repeat = 0; repeat < 2; repeat++)
                DifferenceSdkBuild.ConfigureAndroid(library, true, settings, "com.example.sdkcheck");
            Require(Directory.GetFiles(library + "/difference-sdk/libs", "*.aar").Length == 3, "缺少 Android SDK JNI bridge");
            Require(Directory.GetFiles(library + "/difference-sdk/m2repository", "*.aar", SearchOption.AllDirectories).Length == 5, "缺少 Firebase Android C++ AAR");
            Require(File.ReadAllText(library + "/difference-sdk/sdk.gradle").Contains("com.adjust.sdk:adjust-android:5.3.0"), "缺少 Adjust Maven 依赖");
            Require(XDocument.Load(library + "/src/main/res/values/difference_sdk.xml").Descendants("string").Any(e => (string)e.Attribute("name") == "google_app_id"), "Firebase 资源未生成");
            Require(File.ReadAllText(library + "/build.gradle").Split(new[] { "apply from:" }, StringSplitOptions.None).Length == 2, "重复回调导致 Gradle 重复注入");
            Require(File.ReadAllText(directory + "/settings.gradle").Contains("exclusiveContent"), "本地 Firebase Unity 库未限定为本地解析");

            for (int repeat = 0; repeat < 2; repeat++)
                DifferenceSdkBuild.ConfigureAndroid(library, false, settings, "com.example.sdkcheck");
            Require(!Directory.Exists(library + "/difference-sdk"), "普通包遗留原生库");
            foreach (var path in new[] { library + "/build.gradle", directory + "/settings.gradle", directory + "/gradle.properties", directory + "/launcher/build.gradle", library + "/src/main/AndroidManifest.xml" })
            {
                var text = File.ReadAllText(path);
                Require(!text.Contains("Difference SDK") && !text.Contains("com.google.firebase") && !text.Contains("com.google.android.gms.ads"), "普通包遗留 SDK 配置：" + path);
            }
            Require(!Directory.EnumerateFiles(library + "/src/main", "*", SearchOption.AllDirectories).Any(p => p.EndsWith("difference_sdk.xml") || p.EndsWith("applovin_settings.json") || p.EndsWith("MessagingUnityPlayerActivity.java")), "普通包遗留 SDK 资源或 Activity");

            var plugins = PluginImporter.GetAllImporters().Where(p => p.assetPath.StartsWith("Assets/ThirdParty/DifferenceSdk/", StringComparison.Ordinal)).ToArray();
            Require(plugins.Length >= 20, "SDK 插件尚未导入完成");
            foreach (var plugin in plugins)
                Require(plugin.DefineConstraints.Contains(DifferenceSdkBuild.Define), "缺少宏约束：" + plugin.assetPath);
            bool enabled = DifferenceSdkBuild.Enabled(EditorUserBuildSettings.activeBuildTarget);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            foreach (var name in new[] { "Difference.Sdk", "MaxSdk.Scripts", "AdjustSdk.Scripts" })
                Require(assemblies.Any(a => a.name == name) == enabled, "程序集开关不一致：" + name);
            if (!enabled)
                Require(!assemblies.SelectMany(a => a.compiledAssemblyReferences).Any(p => p.Contains("/DifferenceSdk/") || p.Contains("\\DifferenceSdk\\")), "普通包仍引用 SDK DLL");

            File.WriteAllText(report, "PASS: repeated Android SDK on/off transformations, native payload + Firebase resources, wrong package rejected, " + plugins.Length + " constrained plugins, player assemblies match SDK=" + enabled + ".\nFixture: " + directory);
            Debug.Log(File.ReadAllText(report));
        }
        catch (Exception exception)
        {
            File.WriteAllText(report, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally { JsonUtility.FromJsonOverwrite(saved, settings); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [MenuItem("Tools/Find Differences/Check SDK Scene Configuration")]
    public static void CheckBootstrapConfiguration()
    {
        var type = Type.GetType("DifferenceSdkBootstrap, Difference.Sdk");
        if (type == null) return; // SDK-free builds have no bootstrap assembly.
        var root = new GameObject("SDK configuration check") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var component = root.AddComponent(type);
            var settings = DifferenceSdkSettings.instance;
            foreach (var target in new[] { BuildTarget.Android, BuildTarget.iOS })
            {
                DifferenceSdkBuild.ConfigureBootstrap(component, target);
                var android = target == BuildTarget.Android;
                Require((string)type.GetField("maxSdkKey").GetValue(component) ==
                    (android ? settings.androidMaxSdkKey : settings.iosMaxSdkKey), "MAX Key 未写入组件");
                Require((string)type.GetField("adjustAppToken").GetValue(component) ==
                    (android ? settings.androidAdjustAppToken : settings.iosAdjustAppToken), "Adjust Token 未写入组件");
                Require((string)type.GetField("bannerAdUnitId").GetValue(component) ==
                    (android ? settings.androidBannerAdUnitId : settings.iosBannerAdUnitId), "Banner 广告位未写入组件");
            }
            Debug.Log("[Difference SDK] Android/iOS scene configuration injection PASS.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
