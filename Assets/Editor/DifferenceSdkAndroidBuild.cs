using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

public sealed partial class DifferenceSdkBuild : IPostGenerateGradleAndroidProject
{
    const string Begin = "// Difference SDK Begin";
    const string End = "// Difference SDK End";
    const string AndroidDependencies = "SdkDependencies/Android";

    [Serializable] internal sealed class GoogleServices
    {
        public ProjectInfo project_info;
        public Client[] client;
    }
    [Serializable] internal sealed class ProjectInfo
    {
        public string project_number, project_id, storage_bucket, firebase_url;
    }
    [Serializable] internal sealed class Client
    {
        public ClientInfo client_info;
        public ApiKey[] api_key;
        public OAuthClient[] oauth_client;
    }
    [Serializable] internal sealed class ClientInfo
    {
        public string mobilesdk_app_id;
        public AndroidClient android_client_info;
    }
    [Serializable] internal sealed class AndroidClient { public string package_name; }
    [Serializable] internal sealed class ApiKey { public string current_key; }
    [Serializable] internal sealed class OAuthClient { public int client_type; public string client_id; }

    internal static GoogleServices ReadGoogleServices(string path, string packageName)
    {
        GoogleServices config;
        try { config = JsonUtility.FromJson<GoogleServices>(File.ReadAllText(ConfigPath(path))); }
        catch (Exception) { throw new BuildFailedException("无法解析 google-services.json，请重新从 Firebase 下载 Android 配置。"); }
        var client = config?.client?.FirstOrDefault(c => c.client_info?.android_client_info?.package_name == packageName);
        if (client == null) throw new BuildFailedException("Firebase Android 配置与当前包名不匹配：" + packageName);
        if (string.IsNullOrEmpty(config.project_info?.project_id) || string.IsNullOrEmpty(config.project_info?.project_number) ||
            string.IsNullOrEmpty(client.client_info?.mobilesdk_app_id) || string.IsNullOrEmpty(client.api_key?.FirstOrDefault()?.current_key))
            throw new BuildFailedException("Firebase Android 配置缺少 project_id、project_number、app_id 或 API key。");
        return config;
    }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        Validate(BuildTarget.Android);
        ConfigureAndroid(path, Enabled(BuildTarget.Android), DifferenceSdkSettings.instance,
            PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
    }

    // Only generated Gradle output is modified. Assets/Plugins and user's templates stay untouched.
    internal static void ConfigureAndroid(string path, bool enabled, DifferenceSdkSettings settings, string packageName)
    {
        path = Path.GetFullPath(path);
        var root = Directory.GetParent(path).FullName;
        var payload = Path.GetFullPath(Path.Combine(path, "difference-sdk"));
        if (!payload.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new BuildFailedException("SDK 输出目录越界。");
        if (Directory.Exists(payload)) Directory.Delete(payload, true);
        var valuesPath = Path.Combine(path, "src/main/res/values/difference_sdk.xml");
        var maxPath = Path.Combine(path, "src/main/res/raw/applovin_settings.json");
        var activityPath = Path.Combine(path, "src/main/java/com/google/firebase/MessagingUnityPlayerActivity.java");
        foreach (var owned in new[] { valuesPath, maxPath, activityPath })
            if (File.Exists(owned)) File.Delete(owned);

        ReplaceBlock(Path.Combine(path, "build.gradle"), enabled ? "apply from: 'difference-sdk/sdk.gradle'" : "");
        ReplaceBlock(Path.Combine(root, "settings.gradle"), enabled
            ? "dependencyResolutionManagement { repositories {\n" +
              "    exclusiveContent {\n" +
              "        forRepository { maven { url uri(\"${rootDir}/unityLibrary/difference-sdk/m2repository\") } }\n" +
              "        filter { includeModuleByRegex('com\\\\.google\\\\.firebase', 'firebase-(app|analytics|config|crashlytics|messaging)-unity') }\n" +
              "    }\n" +
              "    flatDir { dirs \"${rootDir}/unityLibrary/difference-sdk/libs\" }\n" +
              File.ReadAllText(AndroidDependencies + "/repositories.gradle") + "\n} }" : "");
        ReplaceBlock(Path.Combine(root, "gradle.properties"), enabled
            ? "android.useAndroidX=true\nandroid.enableJetifier=true\nandroid.jetifier.ignorelist=annotation-experimental-1.4.0.aar" : "", "#");
        ReplaceBlock(Path.Combine(root, "launcher/build.gradle"), enabled
            ? "apply from: '../unityLibrary/difference-sdk/compatibility.gradle'" : "");

        XNamespace android = "http://schemas.android.com/apk/res/android";
        var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        var manifest = XDocument.Load(manifestPath);
        var application = manifest.Root.Element("application");
        if (application == null) throw new BuildFailedException("AndroidManifest 缺少 application。");
        const string appIdKey = "com.google.android.gms.ads.APPLICATION_ID";
        application.Elements("meta-data").Where(e => (string)e.Attribute(android + "name") == appIdKey).Remove();
        var activity = application.Elements("activity").FirstOrDefault(e =>
            (string)e.Attribute(android + "name") == "com.unity3d.player.UnityPlayerActivity" ||
            (string)e.Attribute(android + "name") == "com.google.firebase.MessagingUnityPlayerActivity");
        if (activity == null && enabled)
            throw new BuildFailedException("存在自定义 Android Activity，需要先合并 Firebase 的 onNewIntent 处理。");
        if (activity != null) activity.SetAttributeValue(android + "name", enabled
            ? "com.google.firebase.MessagingUnityPlayerActivity" : "com.unity3d.player.UnityPlayerActivity");
        if (enabled)
        {
            var config = ReadGoogleServices(settings.androidGoogleServicesJson, packageName);
            CopyDirectory("SdkDependencies/native/Android", payload);
            File.Copy(AndroidDependencies + "/sdk.gradle", payload + "/sdk.gradle");
            File.Copy(AndroidDependencies + "/compatibility.gradle", payload + "/compatibility.gradle");
            File.Copy(AndroidDependencies + "/sdk-proguard.pro", payload + "/sdk-proguard.pro");
            Directory.CreateDirectory(Path.GetDirectoryName(activityPath));
            File.Copy(AndroidDependencies + "/MessagingUnityPlayerActivity.java", activityPath);
            application.Add(new XElement("meta-data", new XAttribute(android + "name", appIdKey),
                new XAttribute(android + "value", settings.androidAdMobAppId)));
            WriteFirebaseResources(valuesPath, config, packageName);
            Directory.CreateDirectory(Path.GetDirectoryName(maxPath));
            File.WriteAllText(maxPath, JsonUtility.ToJson(new MaxSettings
            {
                sdk_key = settings.androidMaxSdkKey,
                render_outside_safe_area = PlayerSettings.Android.renderOutsideSafeArea
            }));
        }
        manifest.Save(manifestPath);
    }

    [Serializable] sealed class MaxSettings { public string sdk_key; public bool render_outside_safe_area; }

    static void WriteFirebaseResources(string path, GoogleServices config, string packageName)
    {
        var client = config.client.First(c => c.client_info?.android_client_info?.package_name == packageName);
        var xml = new XElement("resources");
        void Add(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) xml.Add(new XElement("string", new XAttribute("name", key),
                new XAttribute("translatable", "false"), value));
        }
        Add("google_app_id", client.client_info.mobilesdk_app_id);
        Add("google_api_key", client.api_key[0].current_key);
        Add("google_crash_reporting_api_key", client.api_key[0].current_key);
        Add("gcm_defaultSenderId", config.project_info.project_number);
        Add("project_id", config.project_info.project_id);
        Add("google_storage_bucket", config.project_info.storage_bucket);
        Add("firebase_database_url", config.project_info.firebase_url);
        Add("default_web_client_id", client.oauth_client?.FirstOrDefault(c => c.client_type == 3)?.client_id);
        Add("com.crashlytics.android.build_id", GUID.Generate().ToString());
        Add("com.google.firebase.crashlytics.unity_version", "12.8.0");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        new XDocument(xml).Save(path);
    }

    internal static void ReplaceBlock(string path, string content, string comment = "//")
    {
        var begin = Begin.Replace("//", comment);
        var end = End.Replace("//", comment);
        var text = File.ReadAllText(path);
        text = Regex.Replace(text, Regex.Escape(begin) + @".*?" + Regex.Escape(end) + @"\r?\n?", "", RegexOptions.Singleline);
        if (!string.IsNullOrEmpty(content)) text = text.TrimEnd() + "\n" + begin + "\n" + content + "\n" + end + "\n";
        File.WriteAllText(path, text);
    }

    static void CopyDirectory(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta")) continue;
            var destination = Path.Combine(target, file.Substring(source.Length).TrimStart('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination, true);
        }
    }
}
