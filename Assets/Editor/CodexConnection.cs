using UnityEditor;
using UnitySkills;

public static class CodexConnection
{
    [MenuItem("Tools/Codex/Start Unity Skills")]
    public static void Start()
    {
        // Warm the reflection metadata on Unity's main thread before HTTP discovery.
        SkillRouter.GetMeta();
        SkillsHttpServer.Start();
    }
}
