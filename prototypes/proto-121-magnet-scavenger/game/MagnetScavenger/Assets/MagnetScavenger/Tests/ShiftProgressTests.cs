using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrototypeKit;

namespace MagnetShift.Tests
{
    public sealed class ShiftProgressTests
    {
        string directory;
        CheckpointStore<ShiftProgress> store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "magnet-shift-tests", Guid.NewGuid().ToString("N"));
            store = new CheckpointStore<ShiftProgress>(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        static ShiftGame Play(ShiftProgress progress, int index, string history)
        {
            progress.current = index;
            Assert.IsTrue(ShiftGame.TryRestore(ShiftLevels.All[index], history, out var game));
            progress.Record(game);
            return game;
        }

        [Test]
        public void WinningThenUndoingKeepsBestRecordAndUnlocksTheNextOrder()
        {
            var progress = ShiftProgress.Fresh();
            Assert.IsFalse(progress.Open(1));
            var game = Play(progress, 0, "PPU");
            Assert.IsTrue(game.Undo());
            progress.Record(game);
            Assert.AreEqual("PP", progress.histories[0]);
            Assert.AreEqual("PPU", progress.records[0]);
            Assert.IsTrue(progress.Open(1));
            Assert.IsFalse(progress.Open(2));
            Assert.IsTrue(ShiftProgress.Valid(progress));
            Play(progress, 0, "LRPPU");
            Assert.AreEqual("PPU", progress.records[0]);
            Assert.IsTrue(ShiftProgress.Valid(progress));
        }

        [Test]
        public void ValidationRejectsSkippedOrdersAndUnprovenOrDamagedRecords()
        {
            var save = ShiftProgress.Fresh();
            save.current = 1;
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.current = 0;
            save.histories[1] = "P";
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.histories[1] = "";
            save.records[0] = "P";
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.records[0] = null;
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.records[0] = "";
            save.histories[0] = "PPU";
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.records[0] = "PPU";
            save.histories[0] = "PX";
            Assert.IsFalse(ShiftProgress.Valid(save));
            save.histories[0] = "";
            save.schema = 0;
            Assert.IsFalse(ShiftProgress.Valid(save));
        }

        [Test]
        public void DiskRoundTripRestoresSettingsOrderChainPositionAndUndo()
        {
            var progress = ShiftProgress.Fresh();
            Play(progress, 0, "PPU");
            var before = Play(progress, 1, "PRP");
            progress.muted = true;
            Assert.IsTrue(store.Save(progress, ShiftProgress.Valid, out var saved), saved);

            var reopened = new CheckpointStore<ShiftProgress>(directory);
            Assert.IsTrue(reopened.TryLoad(ShiftProgress.Valid, out var loaded, out var read), read);
            Assert.IsTrue(loaded.muted);
            Assert.AreEqual(1, loaded.current);
            CollectionAssert.AreEqual(progress.records, loaded.records);
            CollectionAssert.AreEqual(progress.histories, loaded.histories);
            Assert.IsTrue(ShiftGame.TryRestore(ShiftLevels.All[loaded.current], loaded.histories[loaded.current], out var after));
            Assert.AreEqual(before.CarX, after.CarX);
            Assert.AreEqual(before.Load, after.Load);
            Assert.AreEqual(before.Remaining, after.Remaining);
            Assert.AreEqual(before.Heading, after.Heading);
            CollectionAssert.AreEqual(before.ChainCells, after.ChainCells);
            CollectionAssert.AreEqual(before.Bin.Select(p => p.Id), after.Bin.Select(p => p.Id));
            Assert.IsTrue(after.Undo());
            Assert.AreEqual("PR", after.History);
        }

        [Test]
        public void DamagedPrimaryRecoversBackupAndDoesNotOverwriteItOnNextSave()
        {
            var progress = ShiftProgress.Fresh();
            Play(progress, 0, "PPU");
            Assert.IsTrue(store.Save(progress, ShiftProgress.Valid, out _));
            Play(progress, 1, "P");
            Assert.IsTrue(store.Save(progress, ShiftProgress.Valid, out _));
            string primary = Path.Combine(directory, "session.json");
            File.WriteAllText(primary, "{truncated");
            Assert.IsTrue(store.TryLoad(ShiftProgress.Valid, out var recovered, out var message));
            StringAssert.Contains("备份", message);
            Assert.AreEqual(0, recovered.current);
            Assert.AreEqual("PPU", recovered.records[0]);

            Play(recovered, 1, "PR");
            Assert.IsTrue(store.Save(recovered, ShiftProgress.Valid, out _));
            File.WriteAllText(primary, "broken again");
            Assert.IsTrue(store.TryLoad(ShiftProgress.Valid, out recovered, out _));
            Assert.AreEqual(0, recovered.current);
            Assert.AreEqual("", recovered.histories[1]);
        }

        [Test]
        public void ValidChecksumCannotUnlockAnOrderWithoutAWinningReplay()
        {
            var save = ShiftProgress.Fresh();
            Play(save, 0, "P");
            Assert.IsTrue(store.Save(save, ShiftProgress.Valid, out _));
            save.current = 2;
            save.records[1] = "P";
            // Use the envelope-only overload to simulate a writer that skipped gameplay validation.
            Assert.IsTrue(store.Save(save, out _));
            Assert.IsTrue(store.TryLoad(ShiftProgress.Valid, out var restored, out var message));
            StringAssert.Contains("备份", message);
            Assert.AreEqual(0, restored.current);
            Assert.AreEqual("P", restored.histories[0]);
            Assert.IsFalse(restored.Open(1));
        }
    }
}
