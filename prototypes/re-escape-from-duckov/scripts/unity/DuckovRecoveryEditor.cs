using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Original recovery tooling. No original game code or media is embedded here.
public static class DuckovRecoveryEditor
{
    static string Local => Directory.GetParent(Application.dataPath).Parent.FullName;
    public static void CreateNativeProbe()
    {
        PlayerSettings.companyName = "RoguePrototypeLab";
        PlayerSettings.productName = "DuckovRestorationLab";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        Directory.CreateDirectory("Assets/NativeProbe");
        Directory.CreateDirectory(Local + "/NativeProbe");
        var material = new Material(Shader.Find("Standard")) { name = "ProbeMaterial" };
        AssetDatabase.CreateAsset(material, "Assets/NativeProbe/ProbeMaterial.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Probe Camera").AddComponent<Camera>();
        EditorSceneManager.SaveScene(scene, "Assets/NativeProbe/Probe.unity");
        AssetDatabase.SaveAssets();
        var builds = new[] {
            new AssetBundleBuild { assetBundleName = "seed-assets", assetNames = new[] { "Assets/NativeProbe/ProbeMaterial.mat" } },
            new AssetBundleBuild { assetBundleName = "seed-scenes", assetNames = new[] { "Assets/NativeProbe/Probe.unity" } }
        };
        if (BuildPipeline.BuildAssetBundles(Local + "/NativeProbe", builds, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64) == null)
            throw new Exception("Native bundle seed build failed");
        File.WriteAllText(Local + "/NativeProbe/editor.json", JsonUtility.ToJson(new EnvironmentReport {
            unity = Application.unityVersion, persistentDataPath = Application.persistentDataPath,
            company = PlayerSettings.companyName, product = PlayerSettings.productName
        }, true));
        Debug.Log("DUCKOV_NATIVE_PROBE_SEED_COMPLETE");
    }
    [Serializable] class EnvironmentReport { public string unity, persistentDataPath, company, product; }

    public static void BuildNativeProbe()
    {
        PlayerSettings.companyName = "RoguePrototypeLab";
        PlayerSettings.productName = "DuckovRestorationLab";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
        var scripts=new System.Collections.Generic.List<UnityEngine.Object>();
        foreach(string path in AssetDatabase.GetAllAssetPaths())
            if((path.StartsWith("Assets/Plugins/",StringComparison.Ordinal)||path.StartsWith("Assets/Runtime/",StringComparison.Ordinal)) &&
               (path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)))
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if(asset is MonoScript script && script.GetClass()!=null) scripts.Add(script);
        var registry=AssetDatabase.LoadAssetAtPath<DuckovScriptRegistry>("Assets/NativeProbe/ScriptRegistry.asset");
        if(!registry){registry=ScriptableObject.CreateInstance<DuckovScriptRegistry>();AssetDatabase.CreateAsset(registry,"Assets/NativeProbe/ScriptRegistry.asset");}
        registry.scripts=scripts.ToArray();EditorUtility.SetDirty(registry);AssetDatabase.SaveAssets();
        PlayerSettings.SetPreloadedAssets(new UnityEngine.Object[]{registry});
        Debug.Log("DUCKOV_PLAYER_SCRIPT_REGISTRY "+scripts.Count);
        Directory.CreateDirectory(Local + "/NativeProbe/Player");
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/NativeProbe/Probe.unity" },
            Local + "/NativeProbe/Player/DuckovNativeProbe.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        File.WriteAllText(Local + "/NativeProbe/build-report.json", JsonUtility.ToJson(new ProbeBuildReport {
            result = report.summary.result.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
            bytes = report.summary.totalSize, unity = Application.unityVersion
        }, true));
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Native probe Player build failed");
    }
    [Serializable] class ProbeBuildReport { public string result, unity; public int errors, warnings; public ulong bytes; }

    public static void CreateScriptProbe()
    {
        Directory.CreateDirectory("Assets/ScriptProbe");
        var config=ScriptableObject.CreateInstance<Duckov.Utilities.GameplayDataSettings>();
        config.name="NativeScriptBindingProbe";
        AssetDatabase.CreateAsset(config,"Assets/ScriptProbe/Config.asset");
        AssetDatabase.SaveAssets();
        var builds=new[]{new AssetBundleBuild {assetBundleName="seed-script",assetNames=new[]{"Assets/ScriptProbe/Config.asset"}}};
        if (BuildPipeline.BuildAssetBundles(Local+"/NativeProbe",builds,BuildAssetBundleOptions.UncompressedAssetBundle,BuildTarget.StandaloneWindows64)==null)
            throw new Exception("Native script binding seed failed");
    }

    public static void InspectNativeBinding()
    {
        string root=Environment.GetEnvironmentVariable("DUCKOV_NATIVE_ROOT")??Local+"/NativeRuntime";
        var bundle=AssetBundle.LoadFromFile(root+"/duckov-native-assets.bundle");
        if (!bundle)throw new Exception("Native binding archive rejected");
        foreach(string key in new[]{"resources/gameplaydatasettings","resources/sceneinfocollection","objects/globalgamemanagers.assets:5150","debug/monoscript"})
        {
            var obj=bundle.LoadAsset(key);
            Debug.Log("DUCKOV_BINDING "+key+" = "+(obj ? obj.GetType().FullName+" / "+obj.name:"null"));
            if(obj is MonoScript script)Debug.Log("DUCKOV_SCRIPT_CLASS "+script.GetClass());
        }
        var seed=AssetBundle.LoadFromFile(Local+"/NativeProbe/seed-script");
        var native=seed.LoadAsset("assets/scriptprobe/config.asset");
        Debug.Log("DUCKOV_BINDING_SEED "+native.GetType().FullName+" / "+native.name);
    }

    public static void LoadNativeProbe()
    {
        var variant = Environment.GetEnvironmentVariable("DUCKOV_NATIVE_VARIANT") ?? "original";
        var path = Local + "/NativeProbe/" + (variant == "original" ? "original-resources.bundle" : "original-resources-" + variant + ".bundle");
        var bundle = AssetBundle.LoadFromFile(path);
        if (!bundle) throw new Exception("Original payload bundle rejected by Unity");
        var names = bundle.GetAllAssetNames();
        var report = new NativeReport { unity = Application.unityVersion, assetNames = names };
        foreach (string name in names)
        {
            var asset = bundle.LoadAsset(name);
            if (!asset) throw new Exception("Native object load failed: " + name);
            if (asset is Texture2D texture) { report.textureName = texture.name; report.textureWidth = texture.width; report.textureHeight = texture.height; }
            if (asset is Shader shader) { report.shaderName = shader.name; report.shaderSupported = shader.isSupported; }
            if (asset is Mesh mesh) { report.meshName = mesh.name; report.vertices = mesh.vertexCount; report.subMeshes = mesh.subMeshCount; }
            if (asset is AnimationClip clip) { report.animationName = clip.name; report.animationLength = clip.length; }
            Debug.Log("DUCKOV_NATIVE_ASSET " + name + " = " + asset.GetType().FullName + " / " + asset.name);
        }
        File.WriteAllText(Local + "/NativeProbe/load-report.json", JsonUtility.ToJson(report, true));
        bundle.Unload(true);
        Debug.Log("DUCKOV_NATIVE_PROBE_LOAD_COMPLETE");
    }
    [Serializable] class NativeReport
    {
        public string unity, textureName, shaderName, meshName, animationName;
        public string[] assetNames;
        public int textureWidth, textureHeight, vertices, subMeshes;
        public bool shaderSupported;
        public float animationLength;
    }
}
