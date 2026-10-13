using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class DuckovHybridEditor
{
    static DuckovHybridEditor()
    {
        Environment.SetEnvironmentVariable("UNITY_BURST_DISABLE_COMPILATION", "1");
        Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
        Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "Saves"));
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            EditorApplication.isPlaying = false;
            // Native Player Resources files are a runtime format. Open the
            // assembled Player from the source project without an empty Play.
            EditorApplication.delayCall += Launch;
        };
    }
    [MenuItem("Duckov/混合源码版/构建 Unity 宿主")]
    public static void BuildHost()
    {
        string local = Directory.GetParent(Application.dataPath).Parent.FullName;
        string directory = Path.Combine(local, "HybridUnityHost");
        Directory.CreateDirectory(directory);
        PlayerSettings.companyName = "RoguePrototypeLab";
        PlayerSettings.productName = "DuckovHybridRestorationLab";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
        var scripts = new List<UnityEngine.Object>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
            if (path.StartsWith("Assets/", StringComparison.Ordinal) &&
                (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is MonoScript script && script.GetClass() != null) scripts.Add(script);
        string assetPath = "Assets/NativeProbe/ScriptRegistry.asset";
        var registry = AssetDatabase.LoadAssetAtPath<DuckovScriptRegistry>(assetPath);
        if (!registry) { registry = ScriptableObject.CreateInstance<DuckovScriptRegistry>(); AssetDatabase.CreateAsset(registry, assetPath); }
        registry.scripts = scripts.ToArray(); EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets(); PlayerSettings.SetPreloadedAssets(new UnityEngine.Object[] {registry});
        var report = BuildPipeline.BuildPlayer(new[] {"Assets/NativeProbe/Probe.unity"},
            Path.Combine(directory, "DuckovNativeProbe.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
        File.WriteAllText(Path.Combine(directory, "build-report.json"), JsonUtility.ToJson(new Report {
            result = report.summary.result.ToString(), errors = report.summary.totalErrors,
            warnings = report.summary.totalWarnings, bytes = report.summary.totalSize,
            unity = Application.unityVersion, registeredScripts = scripts.Count}, true));
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Hybrid source Unity Player build failed");
        Debug.Log("DUCKOV_HYBRID_UNITY_BUILD_PASSED");
    }
    [MenuItem("Duckov/混合源码版/运行原生资源游戏")]
    public static void Launch()
    {
        string local = Directory.GetParent(Application.dataPath).Parent.FullName;
        string directory = Path.Combine(local, "HybridPlayer");
        string player = Path.Combine(directory, "DuckovRestored.exe");
        if (!File.Exists(player)) throw new FileNotFoundException("先运行 scripts/build-hybrid-project.ps1 生成混合运行包。", player);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(player) {
            WorkingDirectory = directory, UseShellExecute = true});
    }
    [Serializable] class Report
    {
        public string result, unity;
        public int errors, warnings, registeredScripts;
        public ulong bytes;
    }
}
