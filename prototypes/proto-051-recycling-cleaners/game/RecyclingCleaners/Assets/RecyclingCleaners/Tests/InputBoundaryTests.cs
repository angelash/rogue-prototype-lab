using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace RecyclingCleaners.Tests {
public sealed class InputBoundaryTests {
    GameObject host; CleanerRuntime runtime;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    void Set(string name,object value){typeof(CleanerRuntime).GetField(name,Private).SetValue(runtime,value);}
    object Invoke(string name,params object[] args){return typeof(CleanerRuntime).GetMethod(name,Private).Invoke(runtime,args);}
    Session Current {get{return (Session)typeof(CleanerRuntime).GetField("session",Private).GetValue(runtime);}}
    [SetUp] public void Setup(){host=new GameObject("isolated-input-boundary");runtime=host.AddComponent<CleanerRuntime>();Set("session",Session.CreatePrototype());}
    [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(host);}
    [Test] public void RetryThenMoveInSameFrameCannotOverwriteFreshRound(){
        var old=Current;old.Move(1,0);Invoke("Retry");
        bool called=false;Func<CommandResult> move=()=>{called=true;return Current.Move(1,0);};
        Assert.DoesNotThrow(()=>Invoke("Execute",move));Assert.That(called,Is.False);
        Assert.That(Current,Is.Not.SameAs(old));Assert.That(Current.State.ActionsRemaining,Is.EqualTo(Rules.ActionBudget));
        Assert.That(Current.State.PlayerX,Is.Zero);
    }
    [Test] public void ScenarioButtonCannotReplaceRoundAfterSameFrameCommand(){
        var old=Current;old.Move(1,0);Set("lastCommandFrame",Time.frameCount);
        Invoke("Reset",LayoutKind.Separated,Rules.LargeBucketCapacity);
        Assert.That(Current,Is.SameAs(old));Assert.That(Current.State.PlayerX,Is.EqualTo(1));
        Assert.That(Current.State.Layout,Is.EqualTo(LayoutKind.Compact));
    }
}}
