#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Linq;
using GameFrameX.Editor;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using YooAsset.Editor;

// GameFrameX calls this before BuildPlayer, while asset bundle builds are still allowed.
public sealed class DifferenceAndroidBuild : IBuilderPreHookHandler, UnityEditor.Android.IPostGenerateGradleAndroidProject
{
    public int Priority => 0;
    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        var manifest = new System.Xml.XmlDocument();
        manifest.Load(manifestPath);
        const string android = "http://schemas.android.com/apk/res/android";
        foreach (System.Xml.XmlElement permission in manifest.DocumentElement.SelectNodes("uses-permission"))
            if (permission.GetAttribute("name", android) == "android.permission.VIBRATE") return;
        var vibration = manifest.CreateElement("uses-permission");
        vibration.SetAttribute("name", android, "android.permission.VIBRATE");
        manifest.DocumentElement.AppendChild(vibration);
        manifest.Save(manifestPath);
    }

    public void Run(BuildTarget target, string path)
    {
        if (target != BuildTarget.Android && target != BuildTarget.iOS) return;
        DifferenceSdkBuild.Validate(target);
        if (target == BuildTarget.Android && !EditorUserBuildSettings.exportAsGoogleAndroidProject && EditorUserBuildSettings.development)
            throw new BuildFailedException("GameFrameX APK builds use release mode. Disable Development Build before building.");
        Debug.Log("Find Differences " + target + ": preparing HybridCLR and bundled resources.");
        // The existing build menu excludes Hotfix from Editor; our editor tools reference its types.
        // HybridCLR's player filter already removes hot-update assemblies from the player build.
        HotFixEditorCompilerHelper.RemoveEditor();
        PrebuildCommand.GenerateAll();

        var hotfixDirectory = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
        Directory.CreateDirectory("Assets/Bundles/Code");
        foreach (var assembly in SettingsUtil.HotUpdateAssemblyNamesExcludePreserved)
        {
            var file = assembly + ".dll";
            // Use the selected mobile target compilation, not the Editor DLL.
            File.Copy(Path.Combine(hotfixDirectory, file), "Assets/Bundles/Code/" + file + ".bytes", true);
        }
        Directory.CreateDirectory("Assets/Bundles/AOTCode");
        var strippedAssemblies = Directory.GetFiles(SettingsUtil.GetAssembliesPostIl2CppStripDir(target), "*.dll");
        // HybridCLR regenerates its stripped folder, but these generated bundle copies persist.
        // Remove obsolete SDK metadata when switching back to a normal mobile build.
        var metadataFiles = new System.Collections.Generic.HashSet<string>(
            strippedAssemblies.Select(file => Path.GetFileName(file) + ".bytes"), StringComparer.OrdinalIgnoreCase);
        foreach (var oldFile in Directory.GetFiles("Assets/Bundles/AOTCode", "*.dll.bytes"))
            if (!metadataFiles.Contains(Path.GetFileName(oldFile))) AssetDatabase.DeleteAsset(oldFile.Replace('\\', '/'));
        foreach (var file in strippedAssemblies)
            File.Copy(file, "Assets/Bundles/AOTCode/" + Path.GetFileName(file) + ".bytes", true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var version = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var parameters = new BuiltinBuildParameters
        {
            BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
            BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
            BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
            BuildTarget = target,
            BuildMode = EBuildMode.IncrementalBuild,
            PackageName = "DefaultPackage",
            PackageVersion = version,
            EnableSharePackRule = true,
            VerifyBuildingResult = true,
            FileNameStyle = EFileNameStyle.HashName,
            BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll,
            CompressOption = ECompressOption.LZ4
        };
        var result = new BuiltinBuildPipeline().Run(parameters, true);
        if (!result.Success) throw new BuildFailedException(result.FailedTask + ": " + result.ErrorInfo);

        var manifest = File.ReadAllText(Path.Combine(result.OutputPackageDirectory,
            "PackageManifest_DefaultPackage_" + version + ".json"));
        if (manifest.IndexOf("UIDifferences.prefab", StringComparison.OrdinalIgnoreCase) < 0 ||
            manifest.IndexOf("Unity.HotFix.dll.bytes", StringComparison.OrdinalIgnoreCase) < 0)
            throw new BuildFailedException(target + " package is missing the find-differences UI or hotfix DLL.");
        foreach (var file in Directory.GetFiles(SettingsUtil.GetAssembliesPostIl2CppStripDir(target), "*.dll"))
            if (manifest.IndexOf("Assets/Bundles/AOTCode/" + Path.GetFileName(file) + ".bytes", StringComparison.OrdinalIgnoreCase) < 0)
                throw new BuildFailedException(target + " package is missing AOT metadata: " + Path.GetFileName(file));
        File.WriteAllText("Temp/FindDifferences-" + target.ToString().ToLowerInvariant() + "-resources-check.txt",
            "PASS: " + target + " HybridCLR generated, target DLLs copied, UI/hotfix manifest checked, all bundles copied to StreamingAssets. Version: " + version);
        Debug.Log("Find Differences " + target + ": bundled resources ready, version " + version);
    }
}
#endif
