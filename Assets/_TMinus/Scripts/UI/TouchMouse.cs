using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace TMinus.UI
{
    /// <summary>
    /// Touch support without touching every tool (audit F17): the game's 3D tools read the mouse, so touches are turned into the
    /// mouse's events on a virtual mouse device. One finger = left button (click, drag, hold to torque). Two fingers = right button
    /// (orbit the camera), and pinching zooms. Experimental: only tried by reading the code, never on a real screen.
    /// </summary>
    public sealed class TouchMouse : MonoBehaviour
    {
        const string DeviceName = "TouchMouse";
        const float PinchPixelsPerNotch = 40f;

        Mouse _mouse;
        Vector2 _lastPos, _lastMid;
        int _lastCount;
        float _lastPinch;
        readonly List<TouchControl> _active = new List<TouchControl>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[TouchMouse]");
            DontDestroyOnLoad(go);
            go.AddComponent<TouchMouse>();
        }

        void Update()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;                                  // no touch screen: nothing to do (the real mouse works as before)
            if (_mouse == null) _mouse = InputSystem.GetDevice<Mouse>(DeviceName) ?? InputSystem.AddDevice<Mouse>(DeviceName);

            _active.Clear();
            foreach (var t in screen.touches) if (t.press.isPressed) _active.Add(t);
            int n = _active.Count;
            if (n == 0 && _lastCount == 0) return;

            var state = new MouseState();
            Vector2 pos = _lastPos;
            float scroll = 0f;
            if (n == 1)
            {
                pos = _active[0].position.ReadValue();
                state = state.WithButton(MouseButton.Left, true);
            }
            else if (n >= 2)
            {
                Vector2 a = _active[0].position.ReadValue(), b = _active[1].position.ReadValue();
                pos = (a + b) * 0.5f;
                float pinch = Vector2.Distance(a, b);
                if (_lastCount >= 2) scroll = (pinch - _lastPinch) / PinchPixelsPerNotch * 120f;   // the orbit camera reads 120 per wheel notch
                _lastPinch = pinch;
                state = state.WithButton(MouseButton.Right, true);
            }
            state.position = pos;
            state.delta = n > 0 && _lastCount > 0 ? pos - _lastPos : Vector2.zero;
            state.scroll = new Vector2(0f, scroll);
            InputSystem.QueueStateEvent(_mouse, state);
            _lastPos = pos;
            _lastCount = n;
        }
    }
}
