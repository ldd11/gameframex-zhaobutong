#if ENABLE_UI_UGUI
using Hotfix.UI;
using UnityEditor;

[CustomEditor(typeof(DifferenceFoundRing)), CanEditMultipleObjects]
public sealed class DifferenceFoundRingEditor : UnityEditor.UI.GraphicEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("style"), true);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
