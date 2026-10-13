using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Uses Unity's supported C# Resources extension point; original game calls stay intact.
public sealed class DuckovNativeResources : ResourcesAPI
{
    static DuckovNativeResources instance;
    readonly AssetBundle bundle;
    readonly string[] names;
    readonly Dictionary<string,Object> loaded = new Dictionary<string,Object>(StringComparer.OrdinalIgnoreCase);
    public static string Root { get; private set; }
    DuckovNativeResources(string root)
    {
        Root = root;
        bundle = AssetBundle.LoadFromFile(Path.Combine(root,"duckov-native-assets.bundle"));
        if (!bundle) throw new Exception("Recovered native asset bundle could not be loaded");
        names = bundle.GetAllAssetNames();
    }
    public static void Initialize(string root)
    {
        if (instance != null) return;
        instance = new DuckovNativeResources(root);
        ResourcesAPI.overrideAPI = instance;
        Debug.Log("DUCKOV_NATIVE_RESOURCES_READY " + instance.names.Length);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Bootstrap()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args,"--duckov-native-root");
        if (index >= 0 && index+1 < args.Length) Initialize(args[index+1]);
    }
    public static Object GetObject(string id) => instance == null ? null : instance.Get("objects/" + id.ToLowerInvariant(), typeof(Object));
    Object Get(string key, Type type)
    {
        if (loaded.TryGetValue(key,out Object value) && value && type.IsInstanceOfType(value)) return value;
        if (!names.Contains(key,StringComparer.OrdinalIgnoreCase)) return null;
        value = bundle.LoadAsset(key,type);
        if (value) loaded[key] = value;
        return value;
    }
    protected override Object Load(string path,Type type) => Get("resources/"+path.ToLowerInvariant().Replace('\\','/'),type) ?? base.Load(path,type);
    protected override Object[] LoadAll(string path,Type type)
    {
        string key = "resources/" + path.ToLowerInvariant().Replace('\\','/').TrimEnd('/');
        return names.Where(n=>n.Equals(key,StringComparison.OrdinalIgnoreCase)||n.StartsWith(key+"/",StringComparison.OrdinalIgnoreCase))
            .Select(n=>Get(n,type)).Where(o=>o).Concat(base.LoadAll(path,type)).Distinct().ToArray();
    }
    protected override Shader FindShaderByName(string name)
    {
        var shader = base.FindShaderByName(name);
        if (shader) return shader;
        foreach (string key in names.Where(n=>n.StartsWith("objects/",StringComparison.OrdinalIgnoreCase)))
        {
            var candidate = Get(key,typeof(Shader)) as Shader;
            if (candidate && candidate.name == name) return candidate;
        }
        return null;
    }
    protected override void UnloadAsset(Object value)
    {
        if (loaded.Values.Contains(value)) return; // Retain lifetime of original shared resource roots.
        base.UnloadAsset(value);
    }
}
