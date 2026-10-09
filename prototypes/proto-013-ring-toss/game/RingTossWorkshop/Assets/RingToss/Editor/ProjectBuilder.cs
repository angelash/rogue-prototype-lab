using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RingToss.Editor
{
    public static class ProjectBuilder
    {
        public const string ScenePath = "Assets/RingToss/Scenes/Bootstrap.unity";

        [MenuItem("Ring Toss/Prepare Prototype")]
        public static void Prepare()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Rogue Prototype Lab";
            PlayerSettings.productName = "Ring Toss Workshop Prototype";
            PlayerSettings.bundleVersion = "0.1.0-prototype";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            Debug.Log("RING_PREPARE_OK Unity=" + UnityEngine.Application.unityVersion);
        }

        [MenuItem("Ring Toss/Build Windows Prototype")]
        public static void BuildWindows()
        {
            Prepare();
            var output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Windows64"));
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                locationPathName = Path.Combine(output, "RingTossWorkshop.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode | BuildOptions.Development
            });
            var summary = new BuildSummaryRecord
            {
                unityVersion = UnityEngine.Application.unityVersion,
                result = report.summary.result.ToString(),
                totalBytes = report.summary.totalSize,
                errors = (int)report.summary.totalErrors,
                warnings = (int)report.summary.totalWarnings,
                builtAtUtc = DateTime.UtcNow.ToString("o")
            };
            File.WriteAllText(Path.Combine(output, "build-summary.json"), JsonUtility.ToJson(summary, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Ring Toss build failed: " + report.summary.result);
            Debug.Log("RING_BUILD_OK " + report.summary.totalSize + " bytes");
        }

        [Serializable]
        private sealed class BuildSummaryRecord
        {
            public string unityVersion, result, builtAtUtc;
            public ulong totalBytes;
            public int errors, warnings;
        }
    }
}
