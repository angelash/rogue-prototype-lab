using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Duckov.Utilities;

public static class DuckovRuntimeProbe
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(),"--duckov-runtime-probe") < 0) return;
        var report=new Report { unity=Application.unityVersion, persistentDataPath=Application.persistentDataPath };
        try
        {
            var created=ScriptableObject.CreateInstance<GameplayDataSettings>();
            Debug.Log("DUCKOV_CREATED_CONFIG " + (created ? created.GetType().Assembly.FullName:"null"));
            var seedPath=DuckovNativeResources.Root==null ? "" : Path.Combine(Path.GetDirectoryName(DuckovNativeResources.Root),"seed-script");
            if(File.Exists(seedPath))
            {
                var seed=AssetBundle.LoadFromFile(seedPath);
                var seedData=seed.LoadAsset("assets/scriptprobe/config.asset");
                Debug.Log("DUCKOV_PLAYER_SEED " + (seedData ? seedData.GetType().FullName:"null"));
            }
            var data=Resources.Load<GameplayDataSettings>("GameplayDataSettings");
            if (!data) throw new Exception("GameplayDataSettings native deserialization failed");
            report.gameplayData=data.name;
            report.items=Resources.Load<ScriptableObject>("ItemAssetsCollection").name;
            report.quests=GameplayDataSettings.QuestCollection.name;
            report.sceneInfo=Resources.Load<ScriptableObject>("SceneInfoCollection").name;
            var pipeline=DuckovNativeResources.Root==null ? GraphicsSettings.defaultRenderPipeline : DuckovNativeResources.GetObject("globalgamemanagers.assets:5150");
            if (!pipeline) throw new Exception("Native render pipeline object failed");
            report.pipeline=pipeline.name;
            var sourceShader=DuckovNativeResources.Root==null ? Shader.Find("Shader Graphs/SodaDecalWaterV2") : DuckovNativeResources.GetObject("resources.assets:4606") as Shader;
            if (!sourceShader || !sourceShader.isSupported) throw new Exception("Original graph shader unavailable");
            report.shader=sourceShader.name;report.passed=true;
        }
        catch(Exception e) {report.error=e.ToString();Debug.LogException(e);}
        var args=Environment.GetCommandLineArgs();int outputIndex=Array.IndexOf(args,"--duckov-report-root");
        string output=DuckovNativeResources.Root??(outputIndex>=0?args[outputIndex+1]:Application.persistentDataPath);
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"runtime-probe.json"),JsonUtility.ToJson(report,true));
        Debug.Log("DUCKOV_RUNTIME_PROBE " + JsonUtility.ToJson(report));
        Application.Quit(report.passed?0:1);
    }
    [Serializable] class Report { public string unity,persistentDataPath,gameplayData,items,quests,sceneInfo,pipeline,shader,error;public bool passed; }
}
