using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class UndoStackTests
    {
        [SetUp] public void Fresh() => UndoStack.Clear();
        [TearDown] public void Clean() => UndoStack.Clear();

        [Test]
        public void UndoThenRedoRunsTheActionsInOrder()
        {
            int value = 0;
            UndoStack.Push("add 1", () => value -= 1, () => value += 1);
            value += 1;
            UndoStack.Push("add 10", () => value -= 10, () => value += 10);
            value += 10;

            Assert.AreEqual("add 10", UndoStack.Undo());
            Assert.AreEqual(1, value);
            Assert.AreEqual("add 1", UndoStack.Undo());
            Assert.AreEqual(0, value);
            Assert.IsNull(UndoStack.Undo(), "nothing left to undo");

            Assert.AreEqual("add 1", UndoStack.Redo());
            Assert.AreEqual(1, value);
            Assert.AreEqual("add 10", UndoStack.Redo());
            Assert.AreEqual(11, value);
            Assert.IsNull(UndoStack.Redo());
        }

        [Test]
        public void ANewActionClearsTheRedoList()
        {
            UndoStack.Push("a", () => { }, () => { });
            UndoStack.Undo();
            Assert.AreEqual(1, UndoStack.RedoCount);
            UndoStack.Push("b", () => { }, () => { });
            Assert.AreEqual(0, UndoStack.RedoCount);
        }

        [Test]
        public void ActionsRunByUndoDoNotRecordThemselves()
        {
            int pushes = 0;
            UndoStack.Push("outer", () => { UndoStack.Push("inner", () => { }, () => { }); pushes++; }, () => { });
            UndoStack.Undo();
            Assert.AreEqual(1, pushes);
            Assert.AreEqual(0, UndoStack.UndoCount, "the inner push during undo must be ignored");
            Assert.IsFalse(UndoStack.Applying);
        }

        [Test]
        public void TheListIsCapped()
        {
            for (int i = 0; i < 150; i++) UndoStack.Push("x" + i, () => { }, () => { });
            Assert.AreEqual(100, UndoStack.UndoCount);
        }
    }
}
