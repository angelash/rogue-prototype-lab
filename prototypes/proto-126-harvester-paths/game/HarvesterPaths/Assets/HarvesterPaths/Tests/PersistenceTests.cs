using System;
using System.IO;
using NUnit.Framework;
using PrototypeKit;
namespace HarvesterPaths.Tests {
public sealed class PersistenceTests {
    string directory;CheckpointStore<RoundSnapshot> store;
    static bool Valid(RoundSnapshot s){return Session.ValidateSnapshot(s,out _);}
    [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"rogue-harvester-checkpoints",Guid.NewGuid().ToString("N"));store=new CheckpointStore<RoundSnapshot>(directory);}
    [TearDown]public void Cleanup(){if(!Directory.Exists(directory))return;foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    static Session Advanced(int count){var s=Session.CreatePrototype();for(int i=0;i<count;i++)Assert.That(s.Turn(-1).Success,Is.True);return s;}
    [Test]public void RestoredRoundVehicleAndPersistentFieldMatchCommittedAction(){var s=Advanced(1);Assert.That(store.Save(s.ExportSnapshot(),Valid,out _),Is.True);Assert.That(store.TryLoad(Valid,out var snapshot,out _),Is.True);Assert.That(Session.TryRestore(snapshot,out var restored,out _),Is.True);Assert.That(restored.State.ActionsRemaining,Is.EqualTo(23));Assert.That(restored.State.Vehicle.Facing,Is.EqualTo(Direction.Up));Assert.That(restored.State.Vehicle.Fuel,Is.EqualTo(24));Assert.That(restored.State.RoundIndex,Is.EqualTo(1));Assert.That(restored.State.RemainingCrops,Is.EqualTo(10));}
    [Test]public void InterruptedSaveLeavesOldCompleteField(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.BeforeCommit=()=>{throw new IOException("injected before commit");};Assert.That(store.Save(Advanced(2).ExportSnapshot(),Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.ActionsRemaining,Is.EqualTo(23));}
    [Test]public void InvalidNewWalletNeverReplacesLegalResourceLedger(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);var bad=Advanced(2).ExportSnapshot();bad.Wallet=-1;Assert.That(store.Save(bad,Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.Wallet,Is.EqualTo(4));Assert.That(s.ActionsRemaining,Is.EqualTo(23));}
    [Test]public void RecoveryThenSavePreservesOnlyValidOldField(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.Save(Advanced(2).ExportSnapshot(),Valid,out _);var primary=Path.Combine(directory,"session.json");File.WriteAllText(primary,"broken");Assert.That(store.TryLoad(Valid,out var old,out _),Is.True);Assert.That(old.ActionsRemaining,Is.EqualTo(23));Assert.That(store.Save(Advanced(3).ExportSnapshot(),Valid,out _),Is.True);File.WriteAllText(primary,"broken new");Assert.That(store.TryLoad(Valid,out old,out _),Is.True);Assert.That(old.ActionsRemaining,Is.EqualTo(23));}
}}
