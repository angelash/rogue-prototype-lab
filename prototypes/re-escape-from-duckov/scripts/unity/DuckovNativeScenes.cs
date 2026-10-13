using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

public sealed class DuckovNativeScenes : SceneManagerAPI
{
    static DuckovNativeScenes instance;
    readonly AssetBundle bundle;
    readonly string[] originalPaths;
    DuckovNativeScenes(string root)
    {
        var manifest=JObject.Parse(File.ReadAllText(Path.Combine(root,"scenes.json")));
        originalPaths=manifest["scenes"].Select(s=>(string)s["path"]).ToArray();
        bundle=AssetBundle.LoadFromFile(Path.Combine(root,"duckov-native-scenes.bundle"));
        if (!bundle || !bundle.isStreamedSceneAssetBundle) throw new Exception("Original scene archive rejected");
        string[] actual=bundle.GetAllScenePaths();
        if (actual.Length!=originalPaths.Length) throw new Exception("Recovered scene directory count mismatch");
    }
    public static void Initialize(string root)
    {
        if (instance!=null) return;
        instance=new DuckovNativeScenes(root);SceneManagerAPI.overrideAPI=instance;
        Debug.Log("DUCKOV_NATIVE_SCENES_READY " + instance.originalPaths.Length);
    }
    string PathFor(string name,int index)
    {
        if (index>=0 && index<originalPaths.Length) return originalPaths[index];
        return originalPaths.FirstOrDefault(p=>p.Equals(name,StringComparison.OrdinalIgnoreCase)||System.IO.Path.GetFileNameWithoutExtension(p).Equals(name,StringComparison.OrdinalIgnoreCase))??name;
    }
    protected override int GetNumScenesInBuildSettings() => originalPaths.Length;
    protected override Scene GetSceneByBuildIndex(int index) => SceneManager.GetSceneByPath(PathFor(null,index));
    protected override AsyncOperation LoadSceneAsyncByNameOrIndex(string name,int index,LoadSceneParameters parameters,bool nextFrame)
        => base.LoadSceneAsyncByNameOrIndex(PathFor(name,index),-1,parameters,nextFrame);
    protected override AsyncOperation UnloadSceneAsyncByNameOrIndex(string name,int index,bool immediately,UnloadSceneOptions options,out bool success)
        => base.UnloadSceneAsyncByNameOrIndex(PathFor(name,index),-1,immediately,options,out success);
}
