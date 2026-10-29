#if ENABLE_UI_UGUI
using System;
using Hotfix.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DifferenceSafeAreaHeaderUpdate
{
    [MenuItem("Tools/Find Differences/Update Safe Area Header")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式");
        const string path = "Assets/Bundles/UI/UIDifferences/UIDifferences.prefab";
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var editing = stage != null && stage.assetPath == path;
        var root = editing ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
        try
        {
            var layout = root.transform.Find("Stage/Play").GetComponent<DifferencePlayLayout>();
            if (!layout) throw new InvalidOperationException("游戏页没有适配组件");
            if (editing) Undo.RecordObject(layout, "Align safe area header");
            layout.edgePadding = new Vector2(layout.edgePadding.x, 40);
            layout.bottomAdHeight = 120;
            layout.sectionGap = 20;
            layout.upperOffset = layout.lowerOffset = layout.frameOffset = layout.progressOffset = Vector2.zero;
            layout.backOffset = layout.lifeOffset = layout.levelOffset = Vector2.zero;
            layout.hintOffset = layout.zoomOffset = Vector2.zero;
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (!editing) PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("游戏页使用安全区顶部固定组和顺序内容组，间距20、底部 Banner 预留120，旧位置补偿已清零。");
    }
}
#endif
