using System;
using System.IO;
using NUnit.Framework;
using PrototypeKit;
namespace SushiWorkshop.Tests {
public sealed class PersistenceTests {
    [Serializable] public sealed class Sample {public int turn;public int cash;}
    string directory;CheckpointStore<Sample> store;
    [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"rogue-proto-checkpoints",Guid.NewGuid().ToString("N"));store=new CheckpointStore<Sample>(directory);}
    [TearDown] public void Cleanup(){if(!Directory.Exists(directory))return;foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    [Test] public void InvalidNewSnapshotDoesNotCommit(){store.Save(new Sample{turn=1,cash=18},out _);Assert.That(store.Save(new Sample{turn=2,cash=-5},x=>x.cash>=0,out _),Is.False);Assert.That(store.TryLoad(x=>x.cash>=0,out var restored,out _),Is.True);Assert.That(restored.turn,Is.EqualTo(1));}
    [Test] public void RoundTripReturnsCommittedValues(){Assert.That(store.Save(new Sample{turn=3,cash=12},out _),Is.True);Assert.That(store.TryLoad(x=>x.cash>=0,out var restored,out _),Is.True);Assert.That(restored.turn,Is.EqualTo(3));Assert.That(restored.cash,Is.EqualTo(12));}
    [Test] public void InterruptedCommitDoesNotReplaceCurrent(){store.Save(new Sample{turn=2,cash=8},out _);store.BeforeCommit=()=>{throw new IOException("injected interruption");};Assert.That(store.Save(new Sample{turn=3,cash=5},out _),Is.False);Assert.That(store.TryLoad(x=>true,out var restored,out _),Is.True);Assert.That(restored.turn,Is.EqualTo(2));Assert.That(restored.cash,Is.EqualTo(8));}
    [Test] public void CorruptPrimaryFallsBackToLastValidBackup(){store.Save(new Sample{turn=1,cash=18},out _);store.Save(new Sample{turn=2,cash=15},out _);File.WriteAllText(Path.Combine(directory,"session.json"),"corrupt partial data");Assert.That(store.TryLoad(x=>x.cash>=0,out var restored,out var message),Is.True);Assert.That(restored.turn,Is.EqualTo(1));Assert.That(restored.cash,Is.EqualTo(18));Assert.That(message,Does.Contain("备份"));}
    [Test] public void InvalidDomainStateFallsBackRatherThanApplying(){store.Save(new Sample{turn=1,cash=18},out _);store.Save(new Sample{turn=2,cash=-5},out _);Assert.That(store.TryLoad(x=>x.cash>=0,out var restored,out _),Is.True);Assert.That(restored.cash,Is.EqualTo(18));}
    [Test] public void UnknownSchemaDoesNotConsumePendingOrInventState(){store.Save(new Sample{turn=2,cash=15},out _);File.WriteAllText(Path.Combine(directory,"session.json"),"{\"schema\":99,\"payload\":\"{}\",\"sha256\":\"bad\"}");Assert.That(store.TryLoad(x=>true,out var restored,out _),Is.False);Assert.That(restored,Is.Null);}
    [TestCase(false)]
    [TestCase(true)]
    public void SaveAfterBackupRecoveryPreservesThatBackupWhenPrimaryIsInvalid(bool domainInvalid)
    {
        Assert.That(store.Save(new Sample{turn=1,cash=18},out _),Is.True);
        // A valid envelope with illegal cash must receive the same protection as corrupt bytes.
        Assert.That(store.Save(new Sample{turn=2,cash=domainInvalid?-5:15},out _),Is.True);
        string primary=Path.Combine(directory,"session.json");
        if(!domainInvalid)File.WriteAllText(primary,"corrupt primary before recovery");
        Assert.That(store.TryLoad(x=>x.cash>=0,out var recovered,out var recoveryMessage),Is.True);
        Assert.That(recovered.turn,Is.EqualTo(1));
        Assert.That(recovered.cash,Is.EqualTo(18));
        Assert.That(recoveryMessage,Does.Contain("备份"));

        Assert.That(store.Save(new Sample{turn=3,cash=12},x=>x.cash>=0,out var saveMessage),Is.True,saveMessage);
        Assert.That(store.TryLoad(x=>x.cash>=0,out var current,out _),Is.True);
        Assert.That(current.turn,Is.EqualTo(3));
        Assert.That(current.cash,Is.EqualTo(12));
        File.WriteAllText(primary,"corrupt primary again after successful save");

        Assert.That(store.TryLoad(x=>x.cash>=0,out var protectedBackup,out var finalMessage),Is.True);
        Assert.That(protectedBackup.turn,Is.EqualTo(1));
        Assert.That(protectedBackup.cash,Is.EqualTo(18));
        Assert.That(finalMessage,Does.Contain("备份"));
    }
}}
