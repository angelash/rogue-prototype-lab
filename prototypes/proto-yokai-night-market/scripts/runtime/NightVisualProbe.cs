// QA-only visual survey. It observes production tutorial and options, then poses
// already loaded cards for inspection. This is not native input or match evidence.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightVisualProbe : MonoBehaviour
{
    [Serializable] public class Report {public bool passed;public string scope="QA tutorial/options observation and posed 95-card/28-project survey at a QA-only 60 FPS cap, not native input";public int width,height,cards,projects,tutorialPages,tutorialTextBoundsChecks,qaFrameRate=60,language,verifiedCardLanguages,verifiedProjectLanguages;public List<string> errors=new List<string>();public List<string> captures=new List<string>();}
    Report report=new Report();string folder;float start,nextLog;bool configured,title,started;int pageCaptured=-1;
    int verifiedCards,verifiedProjects;
    int SurveyLanguage => Array.IndexOf(Environment.GetCommandLineArgs(),"--night-visual-en")>=0?1:0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Application.productName.Contains(" QA") && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-visual")>=0)new GameObject("Validation visual survey").AddComponent<NightVisualProbe>();}
    void Awake(){Application.targetFrameRate=60;folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"visual-evidence");Directory.CreateDirectory(folder);start=Time.realtimeSinceStartup;report.width=Screen.width;report.height=Screen.height;Application.logMessageReceived+=(c,s,t)=>{if(t==LogType.Exception || t==LogType.Error || c.StartsWith("[ERROR]"))report.errors.Add(c+"\n"+s);};}
    static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);}
    static void Set(object owner,string name,object value){owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);}
    void Update()
    {
        if(!configured && hGameVars.oCardManager!=null && hGameVars.oCardManager.m_aoCards.Count==95){configured=true;hGameVars.bAutoPause=false;hGameVars.bTutorial=true;report.cards=95;report.projects=hGameVars.oCardManager.m_aoProjectCards.Count;}
        if(configured && !title && hGameState.iState==1 && Time.realtimeSinceStartup-start>3.5f){title=true;hLanguage.SetLanguage(SurveyLanguage);Capture("title.png",Camera.main);}
        if(configured && Time.realtimeSinceStartup>nextLog){nextLog=Time.realtimeSinceStartup+10;var t=hGameVars.oTutorial;Debug.Log("NIGHT_VISUAL_STATE "+hGameState.iState+" tutorial="+Get<int>(t,"m_iState")+" page="+Get<int>(t,"m_iCurrentPage")+" percentage="+Get<float>(t,"m_fPercentage")+" timer="+Get<float>(t,"m_fTimer")+" active="+t.gameObject.activeInHierarchy+" enabled="+t.enabled);Capture("progress.png",Camera.main);report.captures.Remove("progress.png");}
        if(configured && hGameState.iState==9 && Get<int>(hGameVars.oTutorial,"m_iState")==2)
        {
            int page=Get<int>(hGameVars.oTutorial,"m_iCurrentPage");
            if(pageCaptured!=page)
            {
                CheckTutorialBounds(Camera.main,page);Capture("tutorial-"+(page+1)+".png",Camera.main);pageCaptured=page;report.tutorialPages++;
                // Advance the waiting page once; allow the production transition to finish.
                Set(hGameVars.oTutorial,"m_fPercentage",.01f);hGameVars.oContinue.Disable();
            }
        }
        if(configured && !started && hGameState.iState==26){started=true;StartCoroutine(Survey());}
        if(!started && (Time.realtimeSinceStartup-start>240 || report.errors.Count>0)){Finish(false);}
    }
    IEnumerator Survey()
    {
        hGameState.Pause();hLanguage.SetLanguage(0);yield return new WaitForSecondsRealtime(1);Capture("options-zh.png",Camera.main);
        hLanguage.SetLanguage(1);yield return new WaitForSecondsRealtime(.5f);Capture("options-en.png",Camera.main);
        hLanguage.SetLanguage(SurveyLanguage);hGameVars.oOptions.bEnabled=false;yield return new WaitForSecondsRealtime(1);
        var sourceCamera=Camera.main;var cameraObject=new GameObject("QA isolated card camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(sourceCamera);camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.055f,1);camera.enabled=false;
        var anchor=hGameVars.oCardManager.oGetPosObjects(0);camera.transform.position=sourceCamera.transform.position;camera.transform.rotation=sourceCamera.transform.rotation;
        foreach(var cardObject in hGameVars.oCardManager.m_aoCards)
        {
            cardObject.SetActive(true);var card=cardObject.GetComponent<PlayCard>();card.ReloadTexts();card.ExpandFast();Get<GameObject>(card,"m_oHideCard").SetActive(false);
            cardObject.transform.position=anchor.position;cardObject.transform.rotation=anchor.rotation;cardObject.transform.localScale=anchor.localScale;
            foreach(var child in cardObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            yield return null;
            var phrase=Get<TextMesh>(card,"m_oPhrase");var description=Get<GameObject>(card,"m_oDescription").GetComponent<TextMesh>();
            if(hLanguage.iLanguage!=SurveyLanguage || phrase.text!=hLanguage.sGetWordWrapText("Quote"+card.iID,50) || description.text!=hLanguage.sGetWordWrapText("Description"+card.iID,44))report.errors.Add("Card language mismatch: "+card.iID);
            else verifiedCards++;
            var frame=cardObject.GetComponentsInChildren<SpriteRenderer>(true).First(s=>s.gameObject.name=="02 Base");
            CheckWidth(phrase,frame,"Quote"+card.iID);CheckWidth(description,frame,"Description"+card.iID);
            Capture("card-"+card.iID.ToString("000")+".png",camera);
            foreach(var child in cardObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
        }
        foreach(var projectObject in hGameVars.oCardManager.m_aoProjectCards)
        {
            projectObject.SetActive(true);var project=projectObject.GetComponent<ProjectCard>();project.SetPlayer(0);project.Expand();
            var top=Get<GameObject>(project,"m_oTopPart");if(top){var pos=top.transform.localPosition;pos.y=0;top.transform.localPosition=pos;}
            projectObject.transform.position=anchor.position;projectObject.transform.rotation=anchor.rotation;projectObject.transform.localScale=anchor.localScale;
            foreach(var child in projectObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            yield return null;
            var subtitle=projectObject.GetComponentsInChildren<TextMesh>(true).First(t=>t.gameObject.name=="13 Subtitle");
            if(hLanguage.iLanguage!=SurveyLanguage || subtitle.text!=hLanguage.sGetWordWrapText("SubTitle"+project.m_iID,40))report.errors.Add("Project language mismatch: "+project.m_iID);
            else verifiedProjects++;
            CheckWidth(subtitle,projectObject.GetComponentsInChildren<SpriteRenderer>(true).First(s=>s.gameObject.name=="02 Base"),"SubTitle"+project.m_iID);
            Capture("project-"+project.m_iID.ToString("000")+".png",camera);
            foreach(var child in projectObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
        }
        Destroy(cameraObject);Finish(report.errors.Count==0 && report.tutorialPages==2 && report.captures.Count==128);
    }
    void CheckWidth(TextMesh text,SpriteRenderer frame,string key)
    {
        if(text.GetComponent<MeshRenderer>().bounds.size.x>frame.bounds.size.x)report.errors.Add("Text wider than card frame: "+key);
    }
    void CheckTutorialBounds(Camera camera,int page)
    {
        foreach(var text in hGameVars.oTutorial.GetComponentsInChildren<TextMesh>())
        {
            var renderer=text.GetComponent<MeshRenderer>();
            if(!renderer.enabled || text.color.a<.01f || string.IsNullOrEmpty(text.text))continue;
            var bounds=renderer.bounds;bool outside=false;
            for(int i=0;i<8;i++)
            {
                var point=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(point.x<0 || point.x>1 || point.y<0 || point.y>1)outside=true;
            }
            report.tutorialTextBoundsChecks++;
            if(outside)report.errors.Add("Tutorial text outside screen: page "+(page+1)+" / "+text.gameObject.name);
        }
    }
    void Capture(string name,Camera camera)
    {
        int w=Screen.width,h=Screen.height;var rt=new RenderTexture(w,h,24);var target=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(w,h,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
        if(name.StartsWith("card-") || name.StartsWith("project-")){float lo=1,hi=0;foreach(var pixel in image.GetPixels()){lo=Mathf.Min(lo,pixel.grayscale);hi=Mathf.Max(hi,pixel.grayscale);}if(hi-lo<.1f)report.errors.Add("Empty posed capture: "+name);}
        File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());camera.targetTexture=target;RenderTexture.active=active;Destroy(image);rt.Release();Destroy(rt);report.captures.Add(name);
    }
    void Finish(bool ok){report.language=SurveyLanguage;report.verifiedCardLanguages=verifiedCards;report.verifiedProjectLanguages=verifiedProjects;report.passed=ok && verifiedCards==95 && verifiedProjects==28;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);}
}
#endif
