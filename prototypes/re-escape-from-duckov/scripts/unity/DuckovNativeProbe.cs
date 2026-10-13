using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class DuckovNativeProbe
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--duckov-native-probe");
        if (index < 0 || index + 1 >= args.Length) return;
        string path = args[index + 1], output = path + ".player-report.json";
        var report = new ProbeReport { unity = Application.unityVersion, editor = Application.isEditor,
            persistentDataPath = Application.persistentDataPath, graphics = SystemInfo.graphicsDeviceType.ToString(), path = path };
        try
        {
            var bundle = AssetBundle.LoadFromFile(path);
            if (!bundle) throw new Exception("AssetBundle.LoadFromFile rejected original serialized player files");
            report.assets = bundle.GetAllAssetNames().Select(name => {
                Debug.Log("DUCKOV_NATIVE_LOADING " + name);
                var asset = bundle.LoadAsset(name);
                if (!asset) throw new Exception("Object load failed: " + name);
                var r = new AssetReport { key = name, name = asset.name, type = asset.GetType().FullName };
                if (asset is Texture2D tex) { r.width = tex.width; r.height = tex.height; r.textureFormat = tex.format.ToString(); }
                if (asset is Shader shader) r.shaderSupported = shader.isSupported;
                if (asset is Mesh mesh) { r.vertices = mesh.vertexCount; r.subMeshes = mesh.subMeshCount; r.blendShapes = mesh.blendShapeCount; }
                if (asset is AnimationClip clip) { r.length = clip.length; r.frameRate = clip.frameRate; }
                Debug.Log("DUCKOV_NATIVE_LOADED " + JsonUtility.ToJson(r));
                return r;
            }).ToArray();
            report.passed = report.assets.Length == 4;
            bundle.Unload(true);
        }
        catch (Exception ex) { report.error = ex.ToString(); Debug.LogException(ex); }
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Application.Quit(report.passed ? 0 : 1);
    }
    [Serializable] class ProbeReport { public string unity, path, persistentDataPath, graphics, error; public bool passed, editor; public AssetReport[] assets; }
    [Serializable] class AssetReport { public string key, name, type, textureFormat; public int width,height,vertices,subMeshes,blendShapes; public float length,frameRate; public bool shaderSupported; }
}
