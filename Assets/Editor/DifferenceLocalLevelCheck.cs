#if ENABLE_UI_UGUI
using System;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceLocalLevelCheck
{
    [MenuItem("Tools/Find Differences/Check Local Fallback Levels")]
    public static void Run()
    {
        var ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab").GetComponent<UIDifferences>();
        for (var i = 0; i < 2; i++)
        {
            using (var level = new DifferenceRemoteLevel())
            {
                level.LoadLocal("https://example.com/levels/" + (i + 1), i + 1,
                    ui.localLevelDiffs[i], ui.localLevelOriginals[i], ui.localLevelChanged[i]);
                if (level.Error != null) throw new Exception(level.Error);
                if (level.Level.hitSpots.Length != 15 || level.Level.croppedPatches.Length != 15 ||
                    level.Level.original.texture.width != 1500 || level.Level.changed.texture.height != 1000)
                    throw new Exception("Invalid local level " + (i + 1));
                level.LoadLocal("https://example.com/levels/1", 1, null, null, null);
                if (level.Error == null || level.Level != null) throw new Exception("Missing assets must fail cleanly.");
            }
        }
        Debug.Log("PASS: both local fallback levels decode with 15 differences and cropped patches; missing assets fail cleanly.");
    }
}
#endif
