using System;
using System.IO;
using NUnit.Framework;
using PrototypeKit;
namespace RecyclingCleaners.Tests {
public sealed class PersistenceTests {
    string directory;CheckpointStore<RoundSnapshot> store;
    static bool Valid(RoundSnapshot snapshot){return Session.ValidateSnapshot(snapshot,out _);}
    [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"rogue-cleaner-checkpoints",Guid.NewGuid().ToString("N"));store=new CheckpointStore<RoundSnapshot>(directory);}
    [TearDown] public void Cleanup(){if(!Directory.Exists(directory))return;foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    static Session Moved(int count){var s=Session.CreatePrototype(LayoutKind.Compact,Rules.SmallBucketCapacity,false);for(int i=0;i<count;i++)Assert.That(s.Move(1,0).Success,Is.True);return s;}
    [Test] public void RestoredPlayerAndBudgetMatchCommittedAction(){var s=Moved(1);Assert.That(store.Save(s.ExportSnapshot(),Valid,out _),Is.True);Assert.That(store.TryLoad(Valid,out var snapshot,out _),Is.True);Assert.That(Session.TryRestore(snapshot,out var restored,out _),Is.True);Assert.That(restored.State.PlayerX,Is.EqualTo(1));Assert.That(restored.State.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));Assert.That(restored.State.Wallet,Is.EqualTo(Rules.InitialWallet));Assert.That(restored.State.Durability,Is.EqualTo(Rules.MaxDurability));}
    [Test] public void InterruptedMoveSaveLeavesOldCompleteCheckpoint(){store.Save(Moved(1).ExportSnapshot(),Valid,out _);store.BeforeCommit=()=>{throw new IOException("injected before commit");};Assert.That(store.Save(Moved(2).ExportSnapshot(),Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.PlayerX,Is.EqualTo(1));Assert.That(s.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));}
    [Test] public void InvalidNewWalletNeverReplacesLegalRoom(){store.Save(Moved(1).ExportSnapshot(),Valid,out _);var bad=Moved(2).ExportSnapshot();bad.Wallet=-1;Assert.That(store.Save(bad,Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.PlayerX,Is.EqualTo(1));Assert.That(s.Wallet,Is.EqualTo(Rules.InitialWallet));}
    [Test] public void RecoveryThenSaveDoesNotDestroySoleValidOldRoom(){store.Save(Moved(1).ExportSnapshot(),Valid,out _);store.Save(Moved(2).ExportSnapshot(),Valid,out _);var primary=Path.Combine(directory,"session.json");File.WriteAllText(primary,"broken primary");Assert.That(store.TryLoad(Valid,out var old,out _),Is.True);Assert.That(old.PlayerX,Is.EqualTo(1));Assert.That(store.Save(Moved(3).ExportSnapshot(),Valid,out _),Is.True);File.WriteAllText(primary,"broken new primary");Assert.That(store.TryLoad(Valid,out old,out _),Is.True);Assert.That(old.PlayerX,Is.EqualTo(1));Assert.That(old.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));}
}}
