using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TMinus.UI
{
    /// <summary>
    /// The project uses the new Input System only. One persistent EventSystem with the
    /// Input System UI module routes mouse and keyboard to UI Toolkit in every scene.
    /// Scenes must not add their own EventSystem.
    /// </summary>
    public static class InputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("[EventSystem]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
