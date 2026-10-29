#if ENABLE_UI_UGUI
using System.Reflection;
using Hotfix.Manager;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public sealed class DifferenceGMWindow : EditorWindow
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/GM 工具")]
    public static void OpenGamePanel()
    {
        var ui = FindObjectOfType<UIDifferences>();
        if (!EditorApplication.isPlaying || !ui) { Open(); return; }
        ui.ShowGmPanel();
        if (ui.GmVisible) EditorApplication.ExecuteMenuItem("Window/General/Game");
    }

    [MenuItem("Tools/Find Differences/GM 工具")]
    public static void Open() => GetWindow<DifferenceGMWindow>("找不同 GM");

    void OnInspectorUpdate() => Repaint();

    void OnGUI()
    {
        var ui = FindObjectOfType<UIDifferences>();
        EditorGUILayout.HelpBox("请先通过 Launcher 进入 Play 模式，再点击 Tools → GM 工具打开游戏内 GM 面板。通关会正常发放奖励并保存进度。", MessageType.Info);
        if (ui && ui.Round != null)
            EditorGUILayout.LabelField($"第 {ui.SelectedLevel + 1} 关    已找到 {ui.Round.Count}/{ui.Round.Total}");
        using (new EditorGUI.DisabledScope(!CanComplete(ui)))
            if (GUILayout.Button("完成当前关卡", GUILayout.Height(40))) Complete(ui);
        if (!CanComplete(ui)) EditorGUILayout.LabelField("请进入尚未结束的关卡，并关闭设置或弹窗。");
    }

    public static bool CanComplete(UIDifferences ui) => EditorApplication.isPlaying && !EditorApplication.isPaused &&
        ui && ui.play.activeInHierarchy && !ui.IsLoadingLevel && ui.Round != null && !ui.Round.Finished &&
        !ui.modal.activeSelf && !ui.settings.activeSelf &&
        !((GameObject)typeof(UIDifferences).GetField("hintDesign", Private).GetValue(ui)).activeSelf;

    public static void Complete(UIDifferences ui)
    {
        if (!CanComplete(ui)) return;
        // Editor-only access: reuse normal feedback, rewards and save logic without shipping a cheat API.
        typeof(UIDifferences).GetMethod("ClearFoundFeedback", Private).Invoke(ui, null);
        var markFound = typeof(DifferenceRound).GetMethod("Find", Private);
        var apply = typeof(UIDifferences).GetMethod("ApplyResult", Private);
        for (var i = 0; i < ui.Round.Total; i++)
        {
            if (ui.Round.IsFound(i)) continue;
            markFound.Invoke(ui.Round, new object[] { i });
            apply.Invoke(ui, new object[] { i, null });
        }
    }
}
#endif
