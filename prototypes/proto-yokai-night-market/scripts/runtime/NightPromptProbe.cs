// QA-only presentation fixture. Actual Continue transitions run on a frozen
// production table; this is visual evidence, not native mediator gameplay.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

public sealed class NightPromptProbe : MonoBehaviour
{
    [Serializable] public class Report
    {
        public bool passed;
        public string scope="Production table, Continue tutorial/auto-pause/decision/hidden fixtures and 28 isolated project headers with the collapsed title gap, QA-only frozen Loader at 60 FPS; not native input or mediator acceptance";
        public int width,height,language,boundsChecks,projectHeaderChecks;
        public List<string> captures=new List<string>(),errors=new List<string>();
    }
    Report report=new Report();string folder;float start;bool configured,started;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Application.productName.Contains(" QA") && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-prompts")>=0)new GameObject("Validation prompts").AddComponent<NightPromptProbe>();}
    void Awake()
    {
        Application.targetFrameRate=60;start=Time.realtimeSinceStartup;
        report.width=Screen.width;report.height=Screen.height;
        report.language=Array.IndexOf(Environment.GetCommandLineArgs(),"--night-prompts-en")>=0?1:0;
        folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"prompt-evidence");Directory.CreateDirectory(folder);
        Application.logMessageReceived+=(c,s,t)=>{if(t==LogType.Error || t==LogType.Exception || c.StartsWith("[ERROR]"))report.errors.Add(c+"\n"+s);};
    }
    void Update()
    {
        if(!configured && hGameVars.oCardManager!=null && hGameVars.oCardManager.m_aoCards.Count==95){configured=true;hGameVars.bAutoPause=false;hGameVars.bTutorial=false;hLanguage.SetLanguage(report.language);}
        if(configured && !started && hGameState.iState==26){started=true;StartCoroutine(Survey());}
        if(!started && Time.realtimeSinceStartup-start>240){report.errors.Add("Opening timeout");Finish();}
    }
    IEnumerator Survey()
    {
        yield return new WaitForSecondsRealtime(1);
        UnityEngine.Object.FindObjectOfType<Loader>().enabled=false;
        // Freeze a bright point of the original blink for comparable lettering.
        typeof(hGameVars).GetField("m_fBlinkFast",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,.85f);
        Capture("01-table.png",false);
        var prompt=hGameVars.oContinue;
        prompt.EnableTutorial();yield return new WaitForSecondsRealtime(.8f);Capture("02-tutorial-continue.png",true);
        prompt.Disable();yield return new WaitForSecondsRealtime(.8f);
        hGameVars.bAutoPause=true;prompt.Enable();yield return new WaitForSecondsRealtime(.8f);Capture("03-card-pause.png",true);
        prompt.Disable();yield return new WaitForSecondsRealtime(.8f);
        prompt.EnableDecision();yield return new WaitForSecondsRealtime(.8f);Capture("04-mediator.png",true);
        prompt.Disable();yield return new WaitForSecondsRealtime(.8f);Capture("05-hidden.png",false);
        yield return CheckProjectHeaders();
        Finish();
    }
    IEnumerator CheckProjectHeaders()
    {
        var originalCamera=Camera.main;var go=new GameObject("QA project header camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(originalCamera);camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.055f,1);
        camera.transform.position=originalCamera.transform.position;camera.transform.rotation=originalCamera.transform.rotation;
        var anchor=hGameVars.oCardManager.oGetPosObjects(0);
        foreach(var cardObject in hGameVars.oCardManager.m_aoProjectCards)
        {
            cardObject.SetActive(true);var card=cardObject.GetComponent<ProjectCard>();card.enabled=false;card.SetPlayer(0);card.Expand();
            var top=((GameObject)typeof(ProjectCard).GetField("m_oTopPart",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(card)).transform;
            var pos=top.localPosition;pos.y=0;top.localPosition=pos;
            cardObject.transform.position=anchor.position;cardObject.transform.rotation=anchor.rotation;cardObject.transform.localScale=anchor.localScale;
            foreach(var child in cardObject.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            yield return null;
            camera.Render(); // Force current text geometry before querying bounds.
            var title=cardObject.GetComponentsInChildren<TextMesh>().First(t=>t.name=="12 Title");
            var subtitle=cardObject.GetComponentsInChildren<TextMesh>().First(t=>t.name=="13 Subtitle");
            var frame=cardObject.GetComponentsInChildren<SpriteRenderer>().First(t=>t.name=="02 Base");
            // Collapse moves the title down .1 local units. Use that tighter gap
            // for both aspect ratios, including 4:3 where production stays open.
            var titlePosition=title.transform.localPosition;title.transform.localPosition=titlePosition-new Vector3(0,.1f,0);
            var renderers=cardObject.GetComponentsInChildren<Renderer>();
            var titleInk=InkBounds(title,camera,renderers);var subtitleInk=InkBounds(subtitle,camera,renderers);
            title.transform.localPosition=titlePosition;
            if(titleInk.height==0 || subtitleInk.height==0 || titleInk.yMin<=subtitleInk.yMax)report.errors.Add("Project title/subtitle ink overlap or empty: "+card.m_iID+" title="+titleInk+" subtitle="+subtitleInk);
            if(subtitle.GetComponent<MeshRenderer>().bounds.size.x>frame.bounds.size.x)report.errors.Add("Project subtitle wider than frame: "+card.m_iID);
            if(subtitle.text!=hLanguage.sGetWordWrapText("SubTitle"+card.m_iID,40))report.errors.Add("Project subtitle language mismatch: "+card.m_iID);
            report.projectHeaderChecks++;Save("project-header-"+card.m_iID.ToString("000")+".png",camera);
            cardObject.SetActive(false);
        }
        Destroy(go);
    }
    Rect InkBounds(TextMesh text,Camera camera,Renderer[] renderers)
    {
        // Legacy TextMesh bounds include blank font padding; compare the actual
        // rendered light glyphs on black instead of treating padding as visible ink.
        var enabled=renderers.Select(r=>r.enabled).ToArray();foreach(var r in renderers)r.enabled=false;
        text.GetComponent<MeshRenderer>().enabled=true;var background=camera.backgroundColor;camera.backgroundColor=Color.black;
        var rt=new RenderTexture(Screen.width,Screen.height,24);var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        int minX=Screen.width,minY=Screen.height,maxX=-1,maxY=-1;var pixels=image.GetPixels32();
        for(int i=0;i<pixels.Length;i++)if(Mathf.Max(pixels[i].r,Mathf.Max(pixels[i].g,pixels[i].b))>32){int x=i%Screen.width,y=i/Screen.width;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
        camera.targetTexture=null;camera.backgroundColor=background;RenderTexture.active=active;
        for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];Destroy(image);rt.Release();Destroy(rt);
        return maxX<0?Rect.zero:Rect.MinMaxRect(minX,minY,maxX+1,maxY+1);
    }
    void Capture(string name,bool visible)
    {
        var prompt=hGameVars.oContinue;var main=prompt.GetComponent<TextMesh>();var camera=Camera.main;
        if(visible && main.color.a<.5f)report.errors.Add("Prompt not legible: "+name);
        if(!visible && main.color.a>.01f)report.errors.Add("Prompt remains visible: "+name);
        foreach(var text in prompt.GetComponentsInChildren<TextMesh>())
        {
            var renderer=text.GetComponent<MeshRenderer>();if(!renderer.enabled || text.color.a<.01f || string.IsNullOrEmpty(text.text))continue;
            var bounds=renderer.bounds;
            for(int i=0;i<8;i++)
            {
                var p=camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(p.x<0 || p.x>1 || p.y<0 || p.y>1)report.errors.Add("Prompt outside screen: "+name+" / "+text.name);
            }
            report.boundsChecks++;
        }
        var background=prompt.transform.Find("Background").GetComponent<SpriteRenderer>();
        if(visible && main.GetComponent<MeshRenderer>().bounds.size.x>background.bounds.size.x)report.errors.Add("Prompt wider than background: "+name);
        Save(name,camera);
    }
    void Save(string name,Camera camera)
    {
        var rt=new RenderTexture(Screen.width,Screen.height,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        if(name.StartsWith("project-header-")){float lo=1,hi=0;foreach(var pixel in image.GetPixels()){lo=Mathf.Min(lo,pixel.grayscale);hi=Mathf.Max(hi,pixel.grayscale);}if(hi-lo<.1f)report.errors.Add("Empty project header capture: "+name);}
        File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
        Destroy(image);rt.Release();Destroy(rt);report.captures.Add(name);
    }
    void Finish(){report.passed=report.errors.Count==0 && report.captures.Count==33 && report.boundsChecks>=4 && report.projectHeaderChecks==28;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);}
}
#endif
