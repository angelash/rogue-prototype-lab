using UnityEditor;
using UnityEngine;

// Editor recovery uses the cached Editor-capable packages. Keep managed Burst
// fallbacks during recovery checks; the assembled native Player is separate.
[InitializeOnLoad]
public static class DuckovEditorCompatibility
{
    static DuckovEditorCompatibility()
    {
        DisablePlayerBurstInEditor();
        // Recovered menu components inspect slots while opening a scene in
        // Editor, before Startup has created the game's save directory.
        if (PlayerSettings.companyName == "RoguePrototypeLab")
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.persistentDataPath, "Saves"));
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DisablePlayerBurstInEditor();
                if (!System.IO.File.Exists(Application.dataPath + "/Scenes/Startup/Startup.unity") &&
                    System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--duckov-editor-play-qa") < 0)
                {
                    EditorApplication.isPlaying = false;
                    Debug.LogWarning("当前是资源实验宿主，不含完整游戏场景。请用 Duckov 菜单打开 RestoredUnityProject。");
                }
            }
        };
    }

    static void DisablePlayerBurstInEditor()
    {
        // RuntimeInitializeOnLoad executes before Editor InitializeOnLoad on the
        // Play-mode domain reload. The process-scoped flag survives that reload.
        System.Environment.SetEnvironmentVariable("UNITY_BURST_DISABLE_COMPILATION", "1");
        Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
        Debug.Log("DUCKOV_EDITOR_MANAGED_BURST_FALLBACK");
    }

    [MenuItem("Duckov/打开场景与资产恢复工程")]
    static void OpenRecoveryProject()
    {
        string local = System.IO.Directory.GetParent(Application.dataPath).Parent.FullName;
        string project = System.IO.Path.Combine(local, "RestoredUnityProject");
        if (!System.IO.Directory.Exists(project))
            throw new System.IO.DirectoryNotFoundException(project);
        EditorApplication.OpenProject(project);
    }
}
