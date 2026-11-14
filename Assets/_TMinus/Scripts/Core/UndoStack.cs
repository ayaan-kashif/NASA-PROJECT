using System;
using System.Collections.Generic;

namespace TMinus.Core
{
    /// <summary>
    /// One shared undo and redo list for the CubeSat and avionics workshops (audit F7): installing or removing a part, plugging or
    /// unplugging a cable. (Stacking the rocket keeps its own Ctrl+Z in the VAB; torque cannot be undone, just like a real wrench.)
    /// Actions that are being undone or redone do not push new entries.
    /// </summary>
    public static class UndoStack
    {
        sealed class Entry { public string label; public Action undo, redo; }

        const int Max = 100;
        static readonly List<Entry> Undos = new List<Entry>();
        static readonly List<Entry> Redos = new List<Entry>();

        /// <summary>True while an undo or redo is running, so the actions it triggers are not recorded again.</summary>
        public static bool Applying { get; private set; }

        public static int UndoCount => Undos.Count;
        public static int RedoCount => Redos.Count;

        public static void Push(string label, Action undo, Action redo)
        {
            if (Applying || undo == null || redo == null) return;
            Undos.Add(new Entry { label = label, undo = undo, redo = redo });
            if (Undos.Count > Max) Undos.RemoveAt(0);
            Redos.Clear();
        }

        /// <summary>Undoes the last action. Returns its description, or null when there is nothing to undo.</summary>
        public static string Undo()
        {
            if (Undos.Count == 0) return null;
            var e = Undos[Undos.Count - 1];
            Undos.RemoveAt(Undos.Count - 1);
            Run(e.undo);
            Redos.Add(e);
            return e.label;
        }

        public static string Redo()
        {
            if (Redos.Count == 0) return null;
            var e = Redos[Redos.Count - 1];
            Redos.RemoveAt(Redos.Count - 1);
            Run(e.redo);
            Undos.Add(e);
            return e.label;
        }

        public static void Clear() { Undos.Clear(); Redos.Clear(); }

        static void Run(Action a)
        {
            Applying = true;
            try { a(); }
            finally { Applying = false; }
        }
    }
}
