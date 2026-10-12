// Isolated QA of production predicates. No rule implementation is duplicated here.
#if NIGHT_MARKET_VALIDATION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NightRuleChecks
{
    [Serializable] public sealed class Result {public string name;public int expected,actual;public bool passed;}
    static void Set(object owner,string field,object value){owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).SetValue(owner,value);}
    public static List<Result> Run()
    {
        var manager=hGameVars.oCardManager;var human=manager.m_aoPlayersCards[0];var rival=manager.m_aoPlayersCards[1];
        var pool=new[]{human.aoDeck.Concat(human.aoInHand).ToArray(),rival.aoDeck.Concat(rival.aoInHand).ToArray()};
        Func<int,int,GameObject> card=(player,id)=>pool[player].First(g=>g.GetComponent<PlayCard>().iID==id);
        var results=new List<Result>();
        var hands=new[]{human.aoInHand.ToArray(),rival.aoInHand.ToArray()};
        System.Action reset=()=>{human.aoPlaying.Clear();rival.aoPlaying.Clear();human.aoNegas.Clear();rival.aoNegas.Clear();human.aoInHand.Clear();rival.aoInHand.Clear();Set(manager,"m_iDepartmentAllowed",-2);};
        System.Action<int,int> check=(id,expected)=>{int actual=manager.iCanIPlay(0,card(0,id));results.Add(new Result{name="card "+id+" / "+(results.Count+1),expected=expected,actual=actual,passed=actual==expected});};
        System.Action fill=()=>{for(int id=1;id<=5;id++)human.aoPlaying.Add(card(0,id));};
        reset();int wrongTurn=manager.iCanIPlay(1,card(1,1));results.Add(new Result{name="rival cannot play during human shift",expected=0,actual=wrongTurn,passed=wrongTurn==0});check(1,-1); // Empty table: a helper is legal.
        check(87,9); // A mediator cannot be played proactively.
        check(63,4); // Taking a rival helper requires one to exist.
        check(94,5); // Removing completed work requires completed work.
        check(69,7);check(72,7); // Progress/finish need an unblocked helper.
        check(76,8); // Department opening needs a helper in hand.
        check(82,9);check(85,9); // Repairs require matching trouble.
        check(43,-1);check(89,-1); // Sustained trouble and added requirements need no working victim.
        reset();fill();check(1,1);
        reset();Set(manager,"m_iDepartmentAllowed",1);check(1,2);
        reset();rival.aoPlaying.Add(card(1,1));check(1,6);
        reset();for(int id=43;id<=47;id++)rival.aoNegas.Add(card(1,id));check(43,3);
        reset();fill();rival.aoPlaying.Add(card(1,6));check(63,1);check(65,-1);check(67,-1);
        reset();human.aoInHand.Add(card(0,1));check(76,-1);fill();check(76,1);
        reset();human.aoNegas.Add(card(0,44));check(82,-1);check(85,9);
        reset();human.aoNegas.Add(card(0,47));check(82,9);check(85,-1);
        reset();human.aoPlaying.Add(card(0,1));check(69,-1);Set(card(0,1).GetComponent<PlayCard>(),"m_bBlocked",true);check(69,7);Set(card(0,1).GetComponent<PlayCard>(),"m_bBlocked",false);
        reset();check(79,-1);
        reset();human.aoInHand.AddRange(hands[0]);rival.aoInHand.AddRange(hands[1]);
        System.Action<string,int,int> expect=(name,expected,actual)=>results.Add(new Result{name=name,expected=expected,actual=actual,passed=expected==actual});
        var project=manager.oGetProject(0);int initialScore=project.iScore,initialDone=project.iGetTasksDone(0),initialGoal=project.iGetTasksGoal(0);
        project.IncreaseScore(500);expect("played-card score is 500",initialScore+500,project.iScore);
        project.CompleteTask(0);expect("complete one task",initialDone+1,project.iGetTasksDone(0));expect("task score is 1250",initialScore+1750,project.iScore);
        project.ReopenTask(0);expect("reopen removes one completed task",initialDone,project.iGetTasksDone(0));expect("reopen retains awarded score",initialScore+1750,project.iScore);
        project.ReopenTask(0);expect("completed work has zero floor",0,project.iGetTasksDone(0));
        project.AddTask(0);expect("inspection adds one required task",initialGoal+1,project.iGetTasksGoal(0));
        for(int i=0;i<initialGoal+3;i++)project.RemoveTask(0);expect("required work has zero floor",0,project.iGetTasksGoal(0));
        project.CompleteTask(0);expect("overcompletion can produce negative pending work",-1,project.iGetTasksPending(0));
        for(int i=0;i<initialGoal;i++)project.AddTask(0);project.ReopenTask(0);
        // Isolated QA identity: exercise the original strict comparison and 10-slot shift.
        var scores=new int[10];var projects=new int[10];var names=new string[10];
        for(int i=0;i<10;i++){scores[i]=PlayerPrefs.GetInt("ScorePts"+i,0);projects[i]=PlayerPrefs.GetInt("ScorePrj"+i,0);names[i]=PlayerPrefs.GetString("ScorePlayer"+i,"");PlayerPrefs.SetInt("ScorePts"+i,10000);PlayerPrefs.SetInt("ScorePrj"+i,project.m_iID);PlayerPrefs.SetString("ScorePlayer"+i,"QA slot "+i);}
        Set(project,"m_fScore",10000f);hGameVars.iWinner=0;hGameVars.oScoreCard.UpdateRecords(null);
        expect("equal score does not replace top entry",1,PlayerPrefs.GetString("ScorePlayer0")=="QA slot 0"?1:0);
        Set(project,"m_fScore",10001f);hGameVars.oScoreCard.UpdateRecords(null);
        expect("greater score enters first place",10001,PlayerPrefs.GetInt("ScorePts0"));expect("previous first place shifts to second",1,PlayerPrefs.GetString("ScorePlayer1")=="QA slot 0"?1:0);
        for(int i=0;i<10;i++){PlayerPrefs.SetInt("ScorePts"+i,scores[i]);PlayerPrefs.SetInt("ScorePrj"+i,projects[i]);PlayerPrefs.SetString("ScorePlayer"+i,names[i]);}PlayerPrefs.Save();
        return results;
    }
}
#endif
