// Integration fixtures drive original callbacks; they are not native input or full-match evidence.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NightFlowProbe : MonoBehaviour
{
    [Serializable] public sealed class Check {public string name;public bool passed;}
    [Serializable] public sealed class Report
    {
        public bool passed;public string scope="QA-only fixture completion, original pause/replay callbacks and settings reload; not native input or full AI match";
        public int reloads,finalScore,width,height;public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();public List<int> states=new List<int>();
    }
    struct Saved {public string key,text;public int number;public bool exists,isText;}
    readonly List<Saved> saved=new List<Saved>();readonly Report report=new Report();
    string folder;float started;int phase,firstManager;bool finished;
    const string Company="QA 月灯试摊";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!Application.productName.Contains(" QA") || Array.IndexOf(Environment.GetCommandLineArgs(),"--night-flow")<0 || FindObjectOfType<NightFlowProbe>())return;
        new GameObject("Validation flow driver").AddComponent<NightFlowProbe>();
    }
    void Remember(string key,bool text)
    {
        saved.Add(new Saved{key=key,exists=PlayerPrefs.HasKey(key),isText=text,text=text?PlayerPrefs.GetString(key):"",number=text?0:PlayerPrefs.GetInt(key)});
    }
    void Awake()
    {
        DontDestroyOnLoad(gameObject);Application.targetFrameRate=60;started=Time.realtimeSinceStartup;
        folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"flow-evidence");Directory.CreateDirectory(folder);
        foreach(string key in new[]{"AutoPause","FastMode","Sound","Tutorial","Language"})Remember(key,false);Remember("Company",true);
        for(int i=0;i<10;i++){Remember("ScorePts"+i,false);Remember("ScorePrj"+i,false);Remember("ScorePlayer"+i,true);PlayerPrefs.SetInt("ScorePts"+i,0);PlayerPrefs.SetInt("ScorePrj"+i,0);PlayerPrefs.SetString("ScorePlayer"+i,"");}
        PlayerPrefs.SetInt("AutoPause",0);PlayerPrefs.SetInt("FastMode",0);PlayerPrefs.SetInt("Tutorial",0);PlayerPrefs.SetInt("Language",0);PlayerPrefs.SetInt("Sound",1);PlayerPrefs.SetString("Company","");PlayerPrefs.Save();
        report.width=Screen.width;report.height=Screen.height;Application.logMessageReceived+=Log;SceneManager.sceneLoaded+=Loaded;
    }
    void Loaded(Scene scene,LoadSceneMode mode){report.reloads++;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || message.StartsWith("[ERROR]"))report.errors.Add(message+"\n"+stack);}
    void CheckValue(string name,bool passed){report.checks.Add(new Check{name=name,passed=passed});}
    void Update()
    {
        if(finished)return;int state=hGameState.iState;
        if(report.states.Count==0 || report.states[report.states.Count-1]!=state)report.states.Add(state);
        if(report.errors.Count>0 || Time.realtimeSinceStartup-started>240){Finish();return;}
        var manager=hGameVars.oCardManager;
        if(phase==0 && state==26 && manager && manager.bAllInCalm() && hGameState.oActionMgr.m_iActionsEnabled==0)
        {
            firstManager=manager.GetInstanceID();CheckValue("initial 95 cards and 28 projects",manager.m_aoCards.Count==95 && manager.m_aoProjectCards.Count==28);
            CheckValue("initial five cards each",manager.m_aoPlayersCards[0].aoInHand.Count==5 && manager.m_aoPlayersCards[1].aoInHand.Count==5);
            hGameState.oButtonOptions.Click();CheckValue("options button pauses original state",hGameState.iState==80 && hGameVars.oOptions.bEnabled);
            hGameVars.bAutoPause=false;hGameVars.bFastMode=true;hGameVars.bSound=false;hGameVars.bTutorial=false;hLanguage.SetLanguage(1);hGameVars.sCompanyName=Company;hGameVars.SaveConfig();
            CheckValue("all six settings persisted",PlayerPrefs.GetInt("AutoPause")==0 && PlayerPrefs.GetInt("FastMode")==1 && PlayerPrefs.GetInt("Sound")==0 && PlayerPrefs.GetInt("Tutorial")==0 && PlayerPrefs.GetInt("Language")==1 && PlayerPrefs.GetString("Company")==Company);
            hGameState.oButtonOptions.Click();CheckValue("resume button restores human shift",hGameState.iState==26 && !hGameVars.oOptions.bEnabled && !hGameState.oButtonPlay.bLock);
            var project=manager.oGetProject(0);for(int d=0;d<5;d++){int pending=project.iGetTasksPending(d);for(int n=0;n<pending;n++)project.CompleteTask(d);}phase=1;
        }
        else if(phase==1 && state==50)
        {
            var button=hGameState.oButtonPlay;
            string key=(string)typeof(Button).GetField("m_sTextID",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(button);
            if(key!="PlayAgain" || button.bLock || !hGameVars.oScoreCard.bInIdleState())return;
            CheckValue("fixture completion entered original human victory",hGameVars.iWinner==0);
            report.finalScore=manager.oGetProject(0).iScore;CheckValue("scoreboard saved winner score",report.finalScore>0 && PlayerPrefs.GetInt("ScorePts0")==report.finalScore);
            CheckValue("replay button has original reload callback",key=="PlayAgain");button.Click();CheckValue("replay click starts original fade/reload state",hGameState.iState==-1);phase=2;
        }
        else if(phase==2 && report.reloads>0 && state==26 && manager && manager.bAllInCalm() && hGameState.oActionMgr.m_iActionsEnabled==0)
        {
            CheckValue("exactly one scene reload and a new card manager",report.reloads==1 && manager.GetInstanceID()!=firstManager);
            CheckValue("new round and original 95/28 decks",hGameState.iGameTurn==1 && manager.m_aoCards.Count==95 && manager.m_aoProjectCards.Count==28);
            CheckValue("replayed opening hand resets to five each",manager.m_aoPlayersCards[0].aoInHand.Count==5 && manager.m_aoPlayersCards[1].aoInHand.Count==5);
            CheckValue("new project has no completed work or score",manager.oGetProject(0).iScore==0 && !manager.oGetProject(0).bAnyTaskStarted());
            CheckValue("all six settings restored by original initialization",!hGameVars.bAutoPause && hGameVars.bFastMode && !hGameVars.bSound && !hGameVars.bTutorial && hLanguage.iLanguage==1 && hGameVars.sCompanyName==Company);
            CheckValue("saved scoreboard survives replay",PlayerPrefs.GetInt("ScorePts0")==report.finalScore);Finish();
        }
    }
    void Finish()
    {
        finished=true;report.passed=report.errors.Count==0 && report.checks.Count==15 && report.checks.TrueForAll(c=>c.passed);
        foreach(var item in saved){if(!item.exists)PlayerPrefs.DeleteKey(item.key);else if(item.isText)PlayerPrefs.SetString(item.key,item.text);else PlayerPrefs.SetInt(item.key,item.number);}PlayerPrefs.Save();
        Application.logMessageReceived-=Log;SceneManager.sceneLoaded-=Loaded;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);
    }
}
#endif
