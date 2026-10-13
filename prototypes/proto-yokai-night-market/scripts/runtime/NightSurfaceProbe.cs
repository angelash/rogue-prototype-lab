// QA-only. Exercise original options/phase callbacks over the production table.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightSurfaceProbe : MonoBehaviour
{
    [Serializable] public class Report
    {
        public bool passed;
        public string scope="Actual QA Player: production table, original options pause/resume callbacks and seven phase animations (enter/display/exit). Loader frozen after a normal opening, 60 FPS. Not native mouse input or a complete played match.";
        public int width,height,language,phaseChecks,optionChecks;
        public List<string> captures=new List<string>(),errors=new List<string>();
    }
    Report report=new Report();string folder;float start;bool configured,started;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Application.productName.Contains(" QA") && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-surfaces")>=0)new GameObject("Validation UI surfaces").AddComponent<NightSurfaceProbe>();}
    void Awake()
    {
        Application.targetFrameRate=60;start=Time.realtimeSinceStartup;report.width=Screen.width;report.height=Screen.height;
        report.language=Array.IndexOf(Environment.GetCommandLineArgs(),"--night-surfaces-en")>=0?1:0;
        folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"surface-evidence");Directory.CreateDirectory(folder);
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
        Capture("01-table.png");
        hGameState.oButtonOptions.Click();yield return new WaitForSecondsRealtime(1);
        if(hGameState.iState!=80 || !hGameVars.oOptions.bEnabled)report.errors.Add("Options failed to pause original state");else report.optionChecks++;
        Capture("02-options.png");
        hGameVars.bAutoPause=false;hGameVars.bFastMode=false;hGameVars.bSound=false;hGameVars.bTutorial=false;
        yield return new WaitForSecondsRealtime(.4f);Capture("03-options-disabled.png");CheckLamps("OptionRed");
        hGameVars.bAutoPause=true;hGameVars.bFastMode=true;hGameVars.bSound=true;hGameVars.bTutorial=true;
        yield return new WaitForSecondsRealtime(.4f);Capture("04-options-enabled.png");CheckLamps("OptionGreen");
        hGameState.oButtonOptions.Click();yield return new WaitForSecondsRealtime(1);
        if(hGameState.iState!=26 || hGameVars.oOptions.bEnabled)report.errors.Add("Options failed to restore original state");else report.optionChecks++;
        Capture("05-options-closed.png");
        string[] keys={"Recruiting","SelectingProjects","YourTurn","AITurn","YouWin","YouLose","NewRecord"};
        var title=hGameVars.oTitleManager;
        for(int i=0;i<keys.Length;i++)
        {
            title.PlayTitle(keys[i],i==4?title.COLOR_GOOD:i==5?title.COLOR_BAD:title.COLOR_NEUTRAL,.4f);
            yield return new WaitForSecondsRealtime(.2f);Capture("phase-"+keys[i]+"-enter.png");
            float deadline=Time.realtimeSinceStartup+12;
            while(!title.bIsDisplayed() && Time.realtimeSinceStartup<deadline)yield return null;
            var text=title.GetComponent<TextMesh>();
            if(!title.bIsDisplayed() || text.text!=hLanguage.sGetText(keys[i]))report.errors.Add("Phase failed to display: "+keys[i]);
            var left=GameObject.Find("TitleBackground Left").GetComponent<SpriteRenderer>();var right=GameObject.Find("TitleBackground Right").GetComponent<SpriteRenderer>();
            var bounds=left.bounds;bounds.Encapsulate(right.bounds);
            if(text.GetComponent<MeshRenderer>().bounds.size.x>bounds.size.x)report.errors.Add("Phase text wider than plate: "+keys[i]);
            Capture("phase-"+keys[i]+"-display.png");
            while(!title.bFinished() && Time.realtimeSinceStartup<deadline)yield return null;
            if(!title.bFinished() || text.color.a!=0 || Mathf.Abs(left.transform.localPosition.x+27)>.01f || Mathf.Abs(right.transform.localPosition.x-27)>.01f)report.errors.Add("Phase failed to hide: "+keys[i]);
            else report.phaseChecks++;
            Capture("phase-"+keys[i]+"-exit.png");
        }
        Finish();
    }
    void CheckLamps(string expected)
    {
        var expectedSprite=Resources.Load<Sprite>(expected);
        foreach(var lamp in hGameVars.oOptions.GetComponentsInChildren<SpriteRenderer>())if(lamp.name=="Light")
        {if(expectedSprite==null || lamp.sprite!=expectedSprite)report.errors.Add("Wrong option lamp: "+lamp.transform.parent.name);else report.optionChecks++;}
    }
    void Capture(string name)
    {
        var camera=Camera.main;var rt=new RenderTexture(Screen.width,Screen.height,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
        Destroy(image);rt.Release();Destroy(rt);report.captures.Add(name);
    }
    void Finish(){report.passed=report.errors.Count==0 && report.captures.Count==26 && report.phaseChecks==7 && report.optionChecks==10;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);}
}
#endif
