using System;
using System.IO;
using NUnit.Framework;
using PrototypeKit;
namespace SnakeHatchery.Tests {
public sealed class PersistenceTests {
    string directory;CheckpointStore<RoundSnapshot> store;
    static bool Valid(RoundSnapshot snapshot){return Session.ValidateSnapshot(snapshot,out _);}
    [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"rogue-snake-checkpoints",Guid.NewGuid().ToString("N"));store=new CheckpointStore<RoundSnapshot>(directory);}
    [TearDown]public void Cleanup(){if(!Directory.Exists(directory))return;foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    static Session Advanced(int count){var s=Session.CreatePrototype();for(int i=0;i<count;i++)Assert.That(s.Wait().Success,Is.True);return s;}
    [Test]public void RestoredSnakeAndBudgetMatchCommittedAction(){var s=Advanced(1);Assert.That(store.Save(s.ExportSnapshot(),Valid,out _),Is.True);Assert.That(store.TryLoad(Valid,out var snapshot,out _),Is.True);Assert.That(Session.TryRestore(snapshot,out var restored,out _),Is.True);Assert.That(restored.State.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));Assert.That(restored.State.Main.Length,Is.EqualTo(s.State.Main.Length));Assert.That(restored.State.Delivered,Is.Zero);Assert.That(restored.State.Modules.Length,Is.EqualTo(s.State.Modules.Length));}
    [Test]public void InterruptedSaveLeavesOldCompleteSnake(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.BeforeCommit=()=>{throw new IOException("injected before commit");};Assert.That(store.Save(Advanced(2).ExportSnapshot(),Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));}
    [Test]public void InvalidNewDeliveryNeverReplacesLegalWorld(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);var bad=Advanced(2).ExportSnapshot();bad.Delivered=-1;Assert.That(store.Save(bad,Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.Delivered,Is.Zero);Assert.That(s.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));}
    [Test]public void RecoveryThenSavePreservesOnlyValidOldSnake(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.Save(Advanced(2).ExportSnapshot(),Valid,out _);var primary=Path.Combine(directory,"session.json");File.WriteAllText(primary,"broken");Assert.That(store.TryLoad(Valid,out var old,out _),Is.True);Assert.That(old.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));Assert.That(store.Save(Advanced(3).ExportSnapshot(),Valid,out _),Is.True);File.WriteAllText(primary,"broken new");Assert.That(store.TryLoad(Valid,out old,out _),Is.True);Assert.That(old.ActionsRemaining,Is.EqualTo(Rules.ActionBudget-1));}
}}
