using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.UI
{
    /// <summary>
    /// Pause menu and controls card for every gameplay room (audit F4 + F6). Press P or click the Menu button: the game
    /// freezes, and you can read the controls for this room, change the volume, restart the room or go to the main menu.
    /// F1 opens the controls directly. It is created at start-up, so no scene had to be edited.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly HashSet<string> Rooms = new HashSet<string>
        {
            SceneNames.MissionBriefing, SceneNames.IntegrationBay, SceneNames.VAB, SceneNames.AvionicsBay, SceneNames.Flight, SceneNames.Debrief
        };

        static string Everywhere => Strings.Get("pause.everywhere", "P  pause menu   ·   F1  controls   ·   H  HOUSTON   ·   N  next step   ·   Shift+S  scan the build   ·   F9  Judge Mode demo");

        static readonly Dictionary<string, string[]> Controls = new Dictionary<string, string[]>
        {
            [SceneNames.MissionBriefing] = new[]
            {
                "Click a mission card to read its briefing.",
                "Choose orbit, rocket and launch site; set 1 to 4 players (roles merge for fewer players).",
                "Build the spacecraft, build the rocket, then open the pre-launch checklist.",
                "Checklist: fix every FAIL (Fix button) or launch anyway and learn why. Then the GO / NO-GO poll.",
            },
            [SceneNames.IntegrationBay] = new[]
            {
                "Build mode: click a part in the list to install it in the 3U frame. Click the RBF pin to pull it.",
                "Wire mode: click a port, then another port to run a cable. R flips a power plug. Right-click a cable to unplug it. Esc cancels.",
                "Multimeter: click two ports to test whether they are connected.",
                "Ctrl+Z undoes and Ctrl+Y redoes: installing or removing a part, plugging or unplugging a cable.",
                "Right-drag orbits the camera, middle-drag pans, scroll zooms.",
            },
            [SceneNames.VAB] = new[]
            {
                "Assemble: drag parts from the floor or the parts list onto the stack, bottom first. Drag a part off the rocket to remove it.",
                "Ctrl+Z / Ctrl+Y undo and redo stacking.   F  focus the selected part.   Esc  deselect.",
                "Bolting: pick a joint, click a hole to fit a bolt, hold the mouse button to torque it. Follow the star order and the 3 passes.",
                "Right-click a bolt to remove it.   Right-drag orbit, middle-drag pan, scroll zoom.",
            },
            [SceneNames.AvionicsBay] = new[]
            {
                "Click a port, then another port to run a cable. Right-click a cable to unplug it. Esc cancels.",
                "Keep Safe/Arm on SAFE while you work. Arm it at the pad, from the pre-launch checklist.",
                "Ctrl+Z undoes and Ctrl+Y redoes plugging or unplugging a cable.",
            },
            [SceneNames.Flight] = new[]
            {
                "Choose the pitch kick, then START COUNTDOWN. The autopilot flies the ascent.",
                "C  cycle the camera (chase, ground, map).   1 2 3 4  time warp x1, x2, x4, x100.",
                "Autopilot off: A / D pitch, Left Shift / Left Ctrl throttle.   Abort ends the flight.",
            },
            [SceneNames.Debrief] = new[]
            {
                "Read the mission goals, the score breakdown and the causal timeline: every failure names its cause.",
                "Redesign goes back to the briefing, Fly again launches the same design, Save design code copies a code you can share.",
            },
        };

        static PauseMenu _instance;
        VisualElement _root, _button, _overlay, _card;
        bool _open, _showControls;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[PauseMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PauseMenu>();
        }

        void Awake()
        {
            var doc = UiDocs.Create("[PauseMenu UI]", transform, 950);
            if (doc == null) return;
            _root = doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            _button = new Button(Open) { text = Strings.Get("pause.button", "☰  Menu (P)") };
            var b = _button.style;
            b.position = Position.Absolute; b.left = 14; b.bottom = 14;
            b.fontSize = 16; b.color = Color.white;
            b.paddingLeft = 12; b.paddingRight = 12; b.paddingTop = 6; b.paddingBottom = 6;
            b.backgroundColor = new Color(0.04f, 0.07f, 0.16f, 0.82f);
            b.borderTopWidth = b.borderBottomWidth = b.borderLeftWidth = b.borderRightWidth = 1;
            b.borderTopColor = b.borderBottomColor = b.borderLeftColor = b.borderRightColor = new Color(1f, 1f, 1f, 0.25f);
            b.borderTopLeftRadius = b.borderTopRightRadius = b.borderBottomLeftRadius = b.borderBottomRightRadius = 8;
            _root.Add(_button);

            _overlay = new VisualElement();
            var o = _overlay.style;
            o.position = Position.Absolute; o.left = 0; o.right = 0; o.top = 0; o.bottom = 0;
            o.backgroundColor = new Color(0.01f, 0.02f, 0.06f, 0.82f);
            o.alignItems = Align.Center; o.justifyContent = Justify.Center;
            o.display = DisplayStyle.None;
            _card = new VisualElement();
            var c = _card.style;
            c.width = Length.Percent(56); c.maxWidth = 980; c.minWidth = 520;
            c.backgroundColor = new Color(0.06f, 0.09f, 0.18f, 1f);
            c.paddingLeft = 32; c.paddingRight = 32; c.paddingTop = 24; c.paddingBottom = 24;
            c.borderTopWidth = 4; c.borderTopColor = new Color(1f, 0.48f, 0.1f);
            c.borderBottomLeftRadius = c.borderBottomRightRadius = 12;
            _overlay.Add(_card);
            _root.Add(_overlay);

            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh(SceneManager.GetActiveScene().name);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_open) { Time.timeScale = 1f; AudioListener.pause = false; }
            if (_instance == this) _instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Close();
            Refresh(scene.name);
        }

        void Refresh(string scene)
        {
            if (_button != null) _button.style.display = Rooms.Contains(scene) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Update()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!Rooms.Contains(scene) || _root == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (_open && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (_root.panel?.focusController?.focusedElement is TextField) return;   // typing in HOUSTON's box or a name field
            if (kb.pKey.wasPressedThisFrame) { if (_open) Close(); else Open(); }
            if (kb.f1Key.wasPressedThisFrame) { if (_open && _showControls) Close(); else { _showControls = true; Open(); } }
        }

        void Open()
        {
            if (_overlay == null) return;
            _open = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            _overlay.style.display = DisplayStyle.Flex;
            Build();
        }

        void Close()
        {
            if (!_open && (_overlay == null || _overlay.style.display != DisplayStyle.Flex)) return;
            _open = false; _showControls = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ content

        void Build()
        {
            _card.Clear();
            string scene = SceneManager.GetActiveScene().name;
            Add(_showControls ? Strings.Get("pause.controls", "CONTROLS") : Strings.Get("pause.paused", "PAUSED"), 34, true, Color.white, 6);
            Add(Nice(scene), 16, false, new Color(1f, 0.6f, 0.25f), 16);

            if (_showControls)
            {
                if (Controls.TryGetValue(scene, out var lines)) foreach (var l in lines) Add("•  " + l, 18, false, Color.white, 8);
                Add(Everywhere, 15, false, new Color(0.7f, 0.78f, 0.9f), 16);
                _card.Add(MakeButton(Strings.Get("pause.back", "Back"), () => { _showControls = false; Build(); }, false));
                return;
            }

            var volume = new Slider(Strings.Get("pause.volume", "Volume"), 0f, 1f) { value = SettingsStore.Volume };
            volume.style.marginBottom = 14; volume.style.color = Color.white;
            volume.RegisterValueChangedCallback(e => { var s = GameState.Instance; if (s != null) SettingsStore.Save(s.Settings, e.newValue, Screen.fullScreen); });
            _card.Add(volume);
            _card.Add(MakeButton(Strings.Get("pause.resume", "Resume"), Close, true));
            _card.Add(MakeButton(Strings.Get("pause.controlsButton", "Controls"), () => { _showControls = true; Build(); }, false));
            _card.Add(MakeButton(Strings.Get("pause.restart", "Restart this room"), () => { string s = scene; Close(); SceneFlow.Load(s); }, false));
            _card.Add(MakeButton(Strings.Get("pause.mainMenu", "Main menu"), () => { SaveService.SaveIfChanged(GameState.Instance); Close(); SceneFlow.Load(SceneNames.MainMenu); }, false));
            Add(Strings.Get("pause.saved", "Your build is saved automatically."), 14, false, new Color(0.7f, 0.78f, 0.9f), 0);
        }

        static string Nice(string scene)
        {
            switch (scene)
            {
                case SceneNames.MissionBriefing: return "Mission briefing and pad";
                case SceneNames.IntegrationBay: return "Integration Bay (CubeSat)";
                case SceneNames.VAB: return "Vehicle Assembly Building (rocket)";
                case SceneNames.AvionicsBay: return "Avionics Bay";
                case SceneNames.Flight: return "Flight";
                case SceneNames.Debrief: return "Debrief";
                default: return scene;
            }
        }

        void Add(string text, int size, bool bold, Color color, int marginBottom)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.color = color; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = marginBottom;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            _card.Add(l);
        }

        static Button MakeButton(string text, System.Action onClick, bool accent)
        {
            var b = new Button(onClick) { text = text };
            var s = b.style;
            s.fontSize = 20; s.color = Color.white; s.marginBottom = 10;
            s.paddingTop = 10; s.paddingBottom = 10;
            s.backgroundColor = accent ? new Color(1f, 0.48f, 0.1f) : new Color(1f, 1f, 1f, 0.12f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 0;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 8;
            return b;
        }
    }
}
