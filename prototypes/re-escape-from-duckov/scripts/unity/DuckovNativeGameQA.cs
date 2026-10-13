using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Integration evidence only; no gameplay or platform checks are changed.
public sealed class DuckovNativeGameQA : MonoBehaviour
{
    readonly List<string> errors=new List<string>();
    string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--duckov-game-qa");
        if(i<0||i+1>=args.Length)return;
        var go=new GameObject("Recovery integration evidence");DontDestroyOnLoad(go);
        var qa=go.AddComponent<DuckovNativeGameQA>();qa.output=args[i+1];qa.StartCoroutine(qa.Observe());
    }
    void OnEnable()=>Application.logMessageReceived+=Log;
    void OnDisable()=>Application.logMessageReceived-=Log;
    void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
            if(errors.Count<200) errors.Add(message+"\n"+stack);
    }
    IEnumerator Observe()
    {
        Directory.CreateDirectory(output);
        yield return new WaitForSecondsRealtime(35);
        var scenes=new List<string>();for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);scenes.Add(s.buildIndex+" / "+s.path);}
        var active=SceneManager.GetActiveScene();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"game.png"));
        yield return null;yield return null;
        var report=new Report{unity=Application.unityVersion,persistentDataPath=Application.persistentDataPath,
            activeScene=active.path,activeBuildIndex=active.buildIndex,buildSceneCount=SceneManager.sceneCountInBuildSettings,
            loadedScenes=scenes.ToArray(),errors=errors.ToArray(),cameras=Camera.allCamerasCount,
            mainMenuReached=active.name=="MainMenu"};
        File.WriteAllText(Path.Combine(output,"game-qa.json"),JsonUtility.ToJson(report,true));
        Debug.Log("DUCKOV_GAME_QA "+JsonUtility.ToJson(report));Application.Quit(report.mainMenuReached&&errors.Count==0?0:1);
    }
    [Serializable]class Report
    {
        public string unity,persistentDataPath,activeScene;public int activeBuildIndex,buildSceneCount,cameras;
        public string[] loadedScenes,errors;public bool mainMenuReached;
    }
}
