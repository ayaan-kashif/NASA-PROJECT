using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TMinus.UI
{
    /// <summary>Ctrl+Z undoes and Ctrl+Y (or Ctrl+Shift+Z) redoes in the Integration Bay and the Avionics Bay (audit F7). The VAB has its own for stacking.</summary>
    public sealed class UndoHotkeys : MonoBehaviour
    {
        static readonly HashSet<string> Rooms = new HashSet<string> { SceneNames.IntegrationBay, SceneNames.AvionicsBay };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[UndoHotkeys]");
            DontDestroyOnLoad(go);
            go.AddComponent<UndoHotkeys>();
            SceneManager.sceneLoaded += (scene, mode) => UndoStack.Clear();   // a list from another room would undo the wrong thing
        }

        void Update()
        {
            if (!Rooms.Contains(SceneManager.GetActiveScene().name)) return;
            var kb = Keyboard.current;
            if (kb == null || !(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) return;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            string done = null;
            bool redo = kb.yKey.wasPressedThisFrame || (shift && kb.zKey.wasPressedThisFrame);
            if (redo) done = UndoStack.Redo();
            else if (kb.zKey.wasPressedThisFrame) done = UndoStack.Undo();
            else return;
            if (done == null) return;
            AudioFx.Play("click");
            GameState.Instance?.Events.Add("Undo", redo ? "Redo" : "Undo", (redo ? "Redid: " : "Undid: ") + done);
        }
    }
}
