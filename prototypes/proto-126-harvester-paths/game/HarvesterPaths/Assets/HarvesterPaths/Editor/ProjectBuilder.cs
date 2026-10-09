using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
namespace PrototypeBuild.Editor {
public static class ProjectBuilder {
    const string Scene="Assets/HarvesterPaths/Scenes/Bootstrap.unity";
    public static void Prepare() {
        if(!File.Exists(Scene)) { Directory.CreateDirectory(Path.GetDirectoryName(Scene)); var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); EditorSceneManager.SaveScene(s,Scene); }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
        EditorSettings.serializationMode=SerializationMode.ForceText;
        PlayerSettings.companyName="Rogue Prototype Lab"; PlayerSettings.productName="Harvester Paths Prototype";
        PlayerSettings.bundleVersion="0.2.0-slice"; PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        // Procedural scenes have no serialized renderer to retain their shader.
        string materialPath="Assets/HarvesterPaths/Resources/RuntimeDefault.mat";
        if(AssetDatabase.LoadAssetAtPath<Material>(materialPath)==null) {
            var shader=Shader.Find("Standard");
            if(shader==null) throw new BuildFailedException("Standard shader is unavailable");
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            AssetDatabase.CreateAsset(new Material(shader),materialPath);
        }
        AssetDatabase.SaveAssets(); Debug.Log("PREPARE_OK HarvesterPaths Unity="+Application.unityVersion);
    }
    public static void BuildWindows() {
        Prepare(); string output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Builds","Windows64")); Directory.CreateDirectory(output);
        var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{Scene},locationPathName=Path.Combine(output,"HarvesterPaths.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.StrictMode});
        File.WriteAllText(Path.Combine(output,"build-summary.json"),JsonUtility.ToJson(new Summary {unityVersion=Application.unityVersion,result=r.summary.result.ToString(),totalBytes=r.summary.totalSize,errors=(int)r.summary.totalErrors,warnings=(int)r.summary.totalWarnings,builtAtUtc=DateTime.UtcNow.ToString("o")},true));
        foreach(var step in r.steps) foreach(var message in step.messages)
            if(message.type==LogType.Warning || message.type==LogType.Error) Debug.Log("BUILD_DIAGNOSTIC "+message.type+" "+message.content);
        if(r.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("Build failed "+r.summary.result);
        Debug.Log("BUILD_OK HarvesterPaths bytes="+r.summary.totalSize);
    }
    [Serializable] sealed class Summary {public string unityVersion,result,builtAtUtc;public ulong totalBytes;public int errors,warnings;}
}}
