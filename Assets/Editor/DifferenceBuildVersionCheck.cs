using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class DifferenceBuildVersionCheck
{
    [MenuItem("Tools/Find Differences/Check Build Version Increment Only")]
    public static void Run()
    {
        var original = PlayerSettings.bundleVersion;
        var android = PlayerSettings.Android.bundleVersionCode;
        var ios = PlayerSettings.iOS.buildNumber;
        var report = new StringBuilder();
        try
        {
            foreach (var pair in new[] { "1.0.1|1.0.2", "1.0.9|1.0.10", "1.9.99|1.9.100", "2.9|2.10", "1.2.3.9|1.2.3.10" })
            {
                var parts = pair.Split('|');
                Check(DifferenceBuildVersion.TryNextVersion(parts[0], out var next) && next == parts[1], "Incorrect next version: " + pair);
                report.AppendLine("PASS: " + parts[0] + " -> " + next);
            }
            foreach (var value in new[] { null, "", "text", "1", "1.0.beta", "1.0.-1", "1.2.3.4.5", "1.0.2147483647", "1.0.2147483648" })
                Check(!DifferenceBuildVersion.TryNextVersion(value, out var unchanged) && unchanged == value,
                    "Invalid or overflowing versions must remain unchanged: " + value);

            var callbacks = typeof(DifferenceBuildVersion).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => method.IsDefined(typeof(PostProcessBuildAttribute), false)).ToArray();
            Check(callbacks.Length == 1 && callbacks[0].Name == "OnPostprocessBuild", "Register exactly one version increment callback.");
            var attribute = callbacks[0].GetCustomAttributesData().Single(data => data.AttributeType == typeof(PostProcessBuildAttribute));
            Check(attribute.ConstructorArguments.Count == 1 && (int)attribute.ConstructorArguments[0].Value == 1001,
                "Version increment must follow GameFrameX and iOS SDK build callbacks.");

            foreach (var target in new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.StandaloneWindows64 })
            {
                PlayerSettings.bundleVersion = "3.4.5";
                DifferenceBuildVersion.OnPostprocessBuild(target, "unused-no-build");
                Check(PlayerSettings.bundleVersion == (target == BuildTarget.StandaloneWindows64 ? "3.4.5" : "3.4.6"),
                    "Incorrect callback platform scope: " + target);
                Check(PlayerSettings.Android.bundleVersionCode == android && PlayerSettings.iOS.buildNumber == ios,
                    "The display-version callback must not increment platform build codes.");
            }
            report.AppendLine("PASS: invalid/overflow input, one order-1001 callback, Android/iOS increment, Windows unchanged, platform build codes unchanged.");
            Debug.Log(report.ToString());
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error); throw; }
        finally
        {
            PlayerSettings.bundleVersion = original;
            PlayerSettings.Android.bundleVersionCode = android;
            PlayerSettings.iOS.buildNumber = ios;
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/FindDifferences-build-version.txt", report.ToString());
        }
    }

    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
