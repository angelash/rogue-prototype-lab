// Observation and isolated smoke driver. Active only in a QA product with explicit validation arguments.
// This is automated production-path evidence, not native input QA.
#if NIGHT_MARKET_VALIDATION
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightSmokeProbe : MonoBehaviour
{
    [Serializable] public sealed class Report {public bool passed,replayReady;public string scope="automated smoke, not native input";public int cards,projects;public int state,width,height,winner=-1,rounds;public int humanHand,aiHand;public List<string> errors=new List<string>();public List<int> states=new List<int>();public List<int> winnerDone=new List<int>(),winnerGoals=new List<int>();public List<NightRuleChecks.Result> rules=new List<NightRuleChecks.Result>();}
    Report report=new Report();string folder;float start,nextObservation,endingAt=-1;bool configured,started,shot;bool match;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(!Application.productName.Contains(" QA"))return;
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--night-smoke")<0 && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-rules")<0 && Array.IndexOf(Environment.GetCommandLineArgs(),"--night-match")<0)return;
        new GameObject("Validation smoke driver").AddComponent<NightSmokeProbe>();
    }
    void Awake(){Application.targetFrameRate=60;match=Array.IndexOf(Environment.GetCommandLineArgs(),"--night-match")>=0;folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),match?"match-evidence":"smoke-evidence");Directory.CreateDirectory(folder);start=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;report.width=Screen.width;report.height=Screen.height;}
    void Log(string condition,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || condition.StartsWith("[ERROR]"))report.errors.Add(condition+"\n"+stack);}
    void Update()
    {
        int state=hGameState.iState;
        if(report.states.Count==0 || report.states[report.states.Count-1]!=state)report.states.Add(state);
        if(!configured && hGameVars.oCardManager!=null && hGameVars.oCardManager.m_aoCards.Count==95)
        {
            configured=true;report.cards=hGameVars.oCardManager.m_aoCards.Count;report.projects=hGameVars.oCardManager.m_aoProjectCards.Count;
            // QA product identity isolates its settings and leaderboard from the deliverable.
            hGameVars.bAutoPause=false;hGameVars.bTutorial=false;
            if(match){hGameVars.bFastMode=true;Time.timeScale=3;}
        }
        if(configured && state==26 && !started)
        {
            started=true;report.humanHand=hGameVars.oCardManager.m_aoPlayersCards[0].aoInHand.Count;report.aiHand=hGameVars.oCardManager.m_aoPlayersCards[1].aoInHand.Count;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--night-rules")>=0)
            {
                report.scope="isolated production legality predicates, not native input";report.rules=NightRuleChecks.Run();
                report.passed=report.errors.Count==0 && report.rules.Count>0 && report.rules.TrueForAll(r=>r.passed);
                File.WriteAllText(Path.Combine(folder,"rules.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);return;
            }
        }
        if(match)
        {
            if(state==50 && endingAt<0)endingAt=Time.realtimeSinceStartup;
            var replayButton=typeof(hGameState).GetField("m_oButtonPlay",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null) as Button;
            report.replayReady=replayButton!=null && (string)typeof(Button).GetField("m_sTextID",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(replayButton)=="PlayAgain";
            report.scope="automated full AI match with human passing and QA-only timeScale=3, not native input";
            if(Time.realtimeSinceStartup>nextObservation && configured)
            {
                nextObservation=Time.realtimeSinceStartup+10;report.state=state;report.rounds=hGameState.iGameTurn;
                var observation=new {state,round=report.rounds,player=hGameState.iPlayerTurn,calm=hGameVars.oCardManager.bAllInCalm(),actions=hGameState.oActionMgr.m_iActionsEnabled,humanHand=hGameVars.oCardManager.m_aoPlayersCards[0].aoInHand.Count,aiHand=hGameVars.oCardManager.m_aoPlayersCards[1].aoInHand.Count,elapsed=Time.realtimeSinceStartup-start};
                Debug.Log("NIGHT_MATCH_OBSERVATION "+observation);File.WriteAllText(Path.Combine(folder,"progress.json"),JsonUtility.ToJson(report,true));Capture("progress.png");
            }
            if(started && state==26 && hGameState.iPlayerTurn==0 && hGameVars.oCardManager.bAllInCalm() && hGameState.oActionMgr.m_iActionsEnabled==0)hGameState.EndTurn();
            if((endingAt>=0 && report.replayReady && hGameVars.oScoreCard.bInIdleState()) || Time.realtimeSinceStartup-start>2400 || report.errors.Count>0)
            {
                report.winner=hGameVars.iWinner;report.state=state;
                if(report.winner>=0){var project=hGameVars.oCardManager.oGetProject(report.winner);for(int d=0;d<5;d++){report.winnerDone.Add(project.iGetTasksDone(d));report.winnerGoals.Add(project.iGetTasksGoal(d));}}
                report.passed=report.errors.Count==0 && report.states.Contains(50) && report.winnerDone.Count==5 && report.replayReady;
                for(int d=0;d<report.winnerDone.Count;d++)report.passed &= report.winnerDone[d]>=report.winnerGoals[d];
                Capture("ending.png");File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);
            }
            return;
        }
        if(started && !shot && Time.realtimeSinceStartup-start>20)
        {shot=true;Capture("opening.png");hGameState.EndTurn();}
        if((started && report.states.Contains(17) && Time.realtimeSinceStartup-start>100) || Time.realtimeSinceStartup-start>150 || (report.errors.Count>0 && Time.realtimeSinceStartup-start>8))
        {
            report.state=state;report.passed=report.errors.Count==0 && report.cards==95 && report.projects==28 && report.humanHand==5 && report.aiHand==5 && report.states.Contains(26) && report.states.Contains(17);
            Capture("last.png");File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:2);
        }
    }
    void Capture(string filename)
    {
        var camera=Camera.main;if(!camera)return;
        int width=Screen.width,height=Screen.height;var rt=new RenderTexture(width,height,24);var before=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var t=new Texture2D(width,height,TextureFormat.RGBA32,false);
        t.ReadPixels(new Rect(0,0,width,height),0,0);t.Apply();File.WriteAllBytes(Path.Combine(folder,filename),t.EncodeToPNG());
        camera.targetTexture=before;RenderTexture.active=active;Destroy(t);rt.Release();Destroy(rt);
    }
}
#endif
