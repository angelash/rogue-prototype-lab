using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Own recovery checks. A Play result for a probe scene is not gameplay evidence.
[InitializeOnLoad]
public static class DuckovEditorQA
{
    const string StartedKey = "DuckovEditorQA.Started";
    const string TimeKey = "DuckovEditorQA.PlayStarted";
    const string ErrorsKey = "DuckovEditorQA.Errors";
    static string Local => Directory.GetParent(Application.dataPath).Parent.FullName;

    static DuckovEditorQA()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--duckov-editor-play-qa") < 0) return;
        EditorApplication.playModeStateChanged += state =>
        {
            // Batch checks have no OS devices. This supplies an unpressed test
            // keyboard, and is not evidence of native input or visual gameplay.
            if (state == PlayModeStateChange.EnteredPlayMode && Application.isBatchMode &&
                UnityEngine.InputSystem.Keyboard.current == null)
                UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>().MakeCurrent();
        };
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
        };
        EditorApplication.update += Update;
        if (!SessionState.GetBool(StartedKey, false)) EditorApplication.delayCall += Begin;
    }

    static void Begin()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Begin;
            return;
        }
        SessionState.SetBool(StartedKey, true);
        SessionState.SetInt(ErrorsKey, 0);
        SessionState.SetFloat(TimeKey, 0);
        string requested = Environment.GetEnvironmentVariable("DUCKOV_EDITOR_QA_SCENE");
        string scene = string.IsNullOrEmpty(requested) ? "Assets/NativeProbe/Probe.unity" : requested;
        EditorSceneManager.OpenScene(scene);
        EditorApplication.isPlaying = true;
    }

    static void Update()
    {
        if (!SessionState.GetBool(StartedKey, false) || !EditorApplication.isPlaying) return;
        if (Application.isBatchMode && UnityEngine.InputSystem.Keyboard.current == null)
            UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>().MakeCurrent();
        float start = SessionState.GetFloat(TimeKey, 0);
        if (start == 0)
        {
            SessionState.SetFloat(TimeKey, (float)EditorApplication.timeSinceStartup);
            return;
        }
        if (EditorApplication.timeSinceStartup - start < 8) return;
        var scene = SceneManager.GetActiveScene();
        var result = new PlayReport {
            unity = Application.unityVersion, project = Directory.GetParent(Application.dataPath).FullName,
            scene = scene.path, sceneName = scene.name, errors = SessionState.GetInt(ErrorsKey, 0),
            company = Application.companyName, product = Application.productName,
            persistentDataPath = Application.persistentDataPath, seconds = 8,
            burstEnabled = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation
        };
        string output = Environment.GetEnvironmentVariable("DUCKOV_EDITOR_QA_REPORT");
        if (string.IsNullOrEmpty(output)) output = Path.Combine(Local, "editor-play-qa.json");
        File.WriteAllText(output, JsonUtility.ToJson(result, true));
        Debug.Log("DUCKOV_EDITOR_PLAY_QA_COMPLETE");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(result.errors == 0 ? 0 : 2);
    }

    public static void AuditAuthoringAssets()
    {
        var scenes = new List<SceneReport>();
        foreach (var entry in EditorBuildSettings.scenes)
        {
            var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            int objects = 0, missing = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    objects++;
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                }
            scenes.Add(new SceneReport { path = entry.path, gameObjects = objects, missingScripts = missing });
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Startup/Startup.unity", OpenSceneMode.Single);
        File.WriteAllText(Path.Combine(Local, "editor-authoring-audit.json"), JsonUtility.ToJson(new AssetReport {
            unity = Application.unityVersion, scenes = scenes.ToArray(),
            assetPaths = AssetDatabase.GetAllAssetPaths().Length
        }, true));
        Debug.Log("DUCKOV_EDITOR_AUTHORING_AUDIT_COMPLETE");
    }

    public static void DumpPluginScriptMap()
    {
        var scripts = new List<ScriptReport>();
        foreach (string name in new[] { "Unity.InputSystem.dll", "UnityEngine.UI.dll" })
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Plugins/" + name))
                if (asset is MonoScript script && script.GetClass() != null &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId))
                    scripts.Add(new ScriptReport { type = script.GetClass().FullName, guid = guid, fileId = fileId });
        File.WriteAllText(Path.Combine(Local, "editor-plugin-script-map.json"),
            JsonUtility.ToJson(new ScriptMap { scripts = scripts.ToArray() }, true));
        Debug.Log("DUCKOV_EDITOR_PLUGIN_SCRIPT_MAP_COMPLETE " + scripts.Count);
    }

    [Serializable] class PlayReport {
        public string unity, project, scene, sceneName, company, product, persistentDataPath;
        public int errors, seconds;
        public bool burstEnabled;
    }
    [Serializable] class AssetReport { public string unity; public int assetPaths; public SceneReport[] scenes; }
    [Serializable] class SceneReport { public string path; public int gameObjects, missingScripts; }
    [Serializable] class ScriptMap { public ScriptReport[] scripts; }
    [Serializable] class ScriptReport { public string type, guid; public long fileId; }
}
