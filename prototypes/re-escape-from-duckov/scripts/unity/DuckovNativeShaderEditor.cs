using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Recovery preview bridge for shaders whose exported YAML loses GPU programs.
// Originals remain in the native bundle; this changes only Editor instances.
[InitializeOnLoad]
public static class DuckovNativeShaderEditor
{
    static AssetBundle bundle;
    static readonly Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();
    static readonly Dictionary<Material, Shader> materialOriginals = new Dictionary<Material, Shader>();
    static readonly List<ShaderProperty> propertyOriginals = new List<ShaderProperty>();
    static DuckovNativeShaderEditor()
    {
        // Full native shader loading is an experiment, not a default project
        // startup dependency; a failed native bundle must not hang the Editor.
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--duckov-native-shader-editor") < 0) return;
        EditorApplication.delayCall += RestoreShaders;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreOriginalReferences;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                RestoreShaders();
            if (state == PlayModeStateChange.ExitingPlayMode) RestoreOriginalReferences();
        };
    }

    static void RestoreShaders()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RestoreShaders;
            return;
        }
        string local = Directory.GetParent(Application.dataPath).Parent.FullName;
        string path = Path.Combine(local, "NativeRuntime/duckov-native-assets.bundle");
        if (!File.Exists(path)) return;
        if (!bundle)
        {
            bundle = AssetBundle.LoadFromFile(path);
            if (!bundle) { Debug.LogError("DUCKOV_NATIVE_SHADER_BUNDLE_REJECTED"); return; }
            string keysFile = Application.dataPath + "/Editor/Recovery/native-shader-keys.txt";
            if (!File.Exists(keysFile)) { Debug.LogError("DUCKOV_NATIVE_SHADER_KEYS_MISSING"); return; }
            foreach (string key in File.ReadAllLines(keysFile))
            {
                var shader = bundle.LoadAsset<Shader>(key);
                if (!shader) continue;
                shaders[shader.name] = shader;
                ShaderUtil.RegisterShader(shader);
            }
        }
        int materials = 0, properties = 0;
        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            if (material.shader && shaders.TryGetValue(material.shader.name, out var native) && material.shader != native)
            { if (!materialOriginals.ContainsKey(material)) materialOriginals.Add(material, material.shader);
              material.shader = native; materials++; }
        foreach (var asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
        {
            if (asset.GetType().Assembly.GetName().Name != "Unity.RenderPipelines.Universal.Runtime") continue;
            var serialized = new SerializedObject(asset);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is Shader old &&
                    shaders.TryGetValue(old.name, out var native) && old != native)
                { propertyOriginals.Add(new ShaderProperty { asset = asset, path = iterator.propertyPath, shader = old });
                  iterator.objectReferenceValue = native; properties++; }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log("DUCKOV_NATIVE_SHADER_EDITOR " + shaders.Count + " shaders, " + materials + " materials, " + properties + " properties");
    }

    static void RestoreOriginalReferences()
    {
        foreach (var pair in materialOriginals) if (pair.Key) pair.Key.shader = pair.Value;
        foreach (var property in propertyOriginals)
            if (property.asset)
            {
                var serialized = new SerializedObject(property.asset);
                var field = serialized.FindProperty(property.path);
                if (field != null) { field.objectReferenceValue = property.shader; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            }
        materialOriginals.Clear(); propertyOriginals.Clear();
    }

    class ShaderProperty { public ScriptableObject asset; public string path; public Shader shader; }
    class PreserveShaderReferences : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            RestoreOriginalReferences();
            EditorApplication.delayCall += RestoreShaders;
            return paths;
        }
    }
}
