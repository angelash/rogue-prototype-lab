// QA-only visual survey. It observes production tutorial and options, then poses
// already loaded cards for inspection. This is not native input or match evidence.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightVisualProbe : MonoBehaviour
{
    [Serializable] public class Report {public bool passed;public string scope="QA tutorial/options observation and posed 95-card/28-project survey at a QA-only 60 FPS cap, not native input";public int width,height,cards,projects,tutorialPages,qaFrameRate=60;public List<string> errors=new List<string>();public List<string> captures=new List<string>();}
    Report report=new Report();string folder;float start,nextLog;bool configured,title,started;int pageCaptured=-1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Application.productName.Contains(" QA") && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-visual")>=0)new GameObject("Validation visual survey").AddComponent<NightVisualProbe>();}
    void Awake(){Application.targetFrameRate=60;folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"visual-evidence");Directory.CreateDirectory(folder);start=Time.realtimeSinceStartup;report.width=Screen.width;report.height=Screen.height;Application.logMessageReceived+=(c,s,t)=>{if(t==LogType.Exception || t==LogType.Error || c.StartsWith("[ERROR]"))report.errors.Add(c+"\n"+s);};}
    static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);}
    static void Set(object owner,string name,object value){owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);}
    void Update()
    {
        if(!configured && hGameVars.oCardManager!=null && hGameVars.oCardManager.m_aoCards.Count==95){configured=true;hGameVars.bAutoPause=false;hGameVars.bTutorial=true;report.cards=95;report.projects=hGameVars.oCardManager.m_aoProjectCards.Count;}
        if(configured && !title && hGameState.iState==1 && Time.realtimeSinceStartup-start>3.5f){title=true;Capture("title.png",Camera.main);}
        if(configured && Time.realtimeSinceStartup>nextLog){nextLog=Time.realtimeSinceStartup+10;var t=hGameVars.oTutorial;Debug.Log("NIGHT_VISUAL_STATE "+hGameState.iState+" tutorial="+Get<int>(t,"m_iState")+" page="+Get<int>(t,"m_iCurrentPage")+" percentage="+Get<float>(t,"m_fPercentage")+" timer="+Get<float>(t,"m_fTimer")+" active="+t.gameObject.activeInHierarchy+" enabled="+t.enabled);Capture("progress.png",Camera.main);report.captures.Remove("progress.png");}
        if(configured && hGameState.iState==9 && Get<int>(hGameVars.oTutorial,"m_iState")==2)
        {
            int page=Get<int>(hGameVars.oTutorial,"m_iCurrentPage");
            if(pageCaptured!=page)
            {
                Capture("tutorial-"+(page+1)+".png",Camera.main);pageCaptured=page;report.tutorialPages++;
                // Advance the waiting page once; allow the production transition to finish.
                Set(hGameVars.oTutorial,"m_fPercentage",.01f);hGameVars.oContinue.Disable();
            }
        }
        if(configured && !started && hGameState.iState==26){started=true;StartCoroutine(Survey());}
        if(!started && (Time.realtimeSinceStartup-start>240 || report.errors.Count>0)){Finish(false);}
    }
    IEnumerator Survey()
    {
        hGameState.Pause();yield return new WaitForSecondsRealtime(1);Capture("options-zh.png",Camera.main);
        hLanguage.SetLanguage(1);yield return new WaitForSecondsRealtime(.5f);Capture("options-en.png",Camera.main);
        hLanguage.SetLanguage(0);hGameVars.oOptions.bEnabled=false;yield return new WaitForSecondsRealtime(1);
        var sourceCamera=Camera.main;var cameraObject=new GameObject("QA isolated card camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(sourceCamera);camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.055f,1);camera.enabled=false;
        var anchor=hGameVars.oCardManager.oGetPosObjects(0);camera.transform.position=sourceCamera.transform.position;camera.transform.rotation=sourceCamera.transform.rotation;
        foreach(var cardObject in hGameVars.oCardManager.m_aoCards)
        {
            cardObject.SetActive(true);var card=cardObject.GetComponent<PlayCard>();card.ExpandFast();Get<GameObject>(card,"m_oHideCard").SetActive(false);
            cardObject.transform.position=anchor.position;cardObject.transform.rotation=anchor.rotation;cardObject.transform.localScale=anchor.localScale;
            foreach(var child in cardObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            yield return null;Capture("card-"+card.iID.ToString("000")+".png",camera);
            foreach(var child in cardObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
        }
        foreach(var projectObject in hGameVars.oCardManager.m_aoProjectCards)
        {
            projectObject.SetActive(true);var project=projectObject.GetComponent<ProjectCard>();project.SetPlayer(0);project.Expand();
            var top=Get<GameObject>(project,"m_oTopPart");if(top){var pos=top.transform.localPosition;pos.y=0;top.transform.localPosition=pos;}
            projectObject.transform.position=anchor.position;projectObject.transform.rotation=anchor.rotation;projectObject.transform.localScale=anchor.localScale;
            foreach(var child in projectObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            yield return null;Capture("project-"+project.m_iID.ToString("000")+".png",camera);
            foreach(var child in projectObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
        }
        Destroy(cameraObject);Finish(report.errors.Count==0 && report.tutorialPages==2 && report.captures.Count==128);
    }
    void Capture(string name,Camera camera)
    {
        int w=Screen.width,h=Screen.height;var rt=new RenderTexture(w,h,24);var target=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(w,h,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
        if(name.StartsWith("card-") || name.StartsWith("project-")){float lo=1,hi=0;foreach(var pixel in image.GetPixels()){lo=Mathf.Min(lo,pixel.grayscale);hi=Mathf.Max(hi,pixel.grayscale);}if(hi-lo<.1f)report.errors.Add("Empty posed capture: "+name);}
        File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());camera.targetTexture=target;RenderTexture.active=active;Destroy(image);rt.Release();Destroy(rt);report.captures.Add(name);
    }
    void Finish(bool ok){report.passed=ok;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(ok?0:2);}
}
#endif
