using System;
using System.IO;
using NUnit.Framework;
using PrototypeKit;
namespace MagnetScavenger.Tests {
public sealed class PersistenceTests {
    string directory;CheckpointStore<RoundSnapshot> store;
    static bool Valid(RoundSnapshot s){return Session.ValidateSnapshot(s,out _);}
    [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"rogue-magnet-checkpoints",Guid.NewGuid().ToString("N"));store=new CheckpointStore<RoundSnapshot>(directory);}
    [TearDown]public void Cleanup(){if(!Directory.Exists(directory))return;foreach(var f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    static Session Advanced(int count){var s=Session.CreatePrototype();for(int i=0;i<count;i++)Assert.That(s.Move(Direction.Up).Success,Is.True);return s;}
    [Test]public void RestoredGeometryAndEnergyMatchCommittedAction(){var s=Advanced(1);Assert.That(store.Save(s.ExportSnapshot(),Valid,out _),Is.True);Assert.That(store.TryLoad(Valid,out var snapshot,out _),Is.True);Assert.That(Session.TryRestore(snapshot,out var restored,out _),Is.True);Assert.That(restored.State.Energy,Is.EqualTo(59));Assert.That(restored.State.RootY,Is.EqualTo(2));Assert.That(restored.State.PendingItemId,Is.Zero);Assert.That(restored.State.Items.Length,Is.EqualTo(5));}
    [Test]public void InterruptedSaveLeavesOldCompleteChain(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.BeforeCommit=()=>{throw new IOException("injected before commit");};Assert.That(store.Save(Advanced(2).ExportSnapshot(),Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.Energy,Is.EqualTo(59));}
    [Test]public void InvalidNewWalletNeverReplacesLegalOwnership(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);var bad=Advanced(2).ExportSnapshot();bad.Wallet=-1;Assert.That(store.Save(bad,Valid,out _),Is.False);Assert.That(store.TryLoad(Valid,out var s,out _),Is.True);Assert.That(s.Wallet,Is.Zero);Assert.That(s.Energy,Is.EqualTo(59));}
    [Test]public void RecoveryThenSavePreservesOnlyValidOldChain(){store.Save(Advanced(1).ExportSnapshot(),Valid,out _);store.Save(Advanced(2).ExportSnapshot(),Valid,out _);var primary=Path.Combine(directory,"session.json");File.WriteAllText(primary,"broken");Assert.That(store.TryLoad(Valid,out var old,out _),Is.True);Assert.That(old.Energy,Is.EqualTo(59));Assert.That(store.Save(Advanced(3).ExportSnapshot(),Valid,out _),Is.True);File.WriteAllText(primary,"broken new");Assert.That(store.TryLoad(Valid,out old,out _),Is.True);Assert.That(old.Energy,Is.EqualTo(59));}
}}
