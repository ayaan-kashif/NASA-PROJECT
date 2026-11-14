using TMinus.Core;
using TMinus.Flight;
using TMinus.Spacecraft;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// Judge Mode (Prompt 18): a scripted ~3-minute FloodWatch-1 story through the real scenes, offline.
    /// 1 Bay: one fault (antenna cable missing), HOUSTON points to it, Space fixes it.
    /// 2 VAB: joint 1 bolted out of order with 3 bolts missing; HOUSTON flags it; launch anyway.
    /// 3 Flight: the joint fails at max-Q.  4 Debrief explains why.
    /// 5 Fix and fly again: orbit, deploy, 30 days of operations.  6 3-star debrief.
    /// Space = next step. F9 or the main-menu button starts it. Esc exits.
    /// </summary>
    public sealed class JudgeMode : MonoBehaviour
    {
        public static JudgeMode Active { get; private set; }

        int _step;
        float _timer = -1, _autoT;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly bool AutoPlay = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-judgeauto") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart() { if (AutoPlay && Active == null) Begin(); }
#endif
        Label _caption, _counter;
        VisualElement _box;

        static readonly string[] Captions =
        {
            "1/6 · Integration Bay. A nearly finished FloodWatch-1 CubeSat. HOUSTON's scan finds the one fault: the radio's antenna cable is missing. Press Space to plug it in.",
            "1/6 · Antenna cable connected. The wiring check is clean. Press Space to go to the rocket.",
            "2/6 · VAB. Joint 1 (stage 1 to interstage) was tightened clockwise in one pass and 3 bolts are missing. HOUSTON flags it. We launch anyway. Press Space.",
            "3/6 · Flight. Watch dynamic pressure climb towards max-Q...",
            "4/6 · Debrief. The causal timeline traces the loss to joint j_s1_interstage. Press Space to fix the joint and fly again.",
            "5/6 · Fixed: every joint torqued in star order over three passes. Second launch...",
            "6/6 · Orbit, deployment, 30 days of operations. The satellite photographs the 2022 Sindh floods. Press Space to finish.",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            DemoHooks.StartJudgeMode = Begin;
            var go = new GameObject("[JudgeModeHotkey]");
            DontDestroyOnLoad(go);
            go.AddComponent<JudgeHotkey>();
        }

        public static void Begin()
        {
            if (Active != null) return;
            var go = new GameObject("[JudgeMode]");
            DontDestroyOnLoad(go);
            FlightController.AutoAdvance = true;   // the scripted story moves on by itself after each flight
            Active = go.AddComponent<JudgeMode>();
        }

        void Start()
        {
            var doc = UiDocs.Create("[JudgeMode UI]", transform, 900);
            if (doc != null)
            {
                var root = doc.rootVisualElement;
                root.pickingMode = PickingMode.Ignore;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.bottom = 24;
                row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center;
                _box = new VisualElement();
                var s = _box.style;
                s.width = 1100; s.paddingLeft = 22; s.paddingRight = 22; s.paddingTop = 12; s.paddingBottom = 14;
                s.backgroundColor = new Color(0.98f, 0.48f, 0.1f, 0.95f);
                s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 14;
                _counter = new Label("JUDGE MODE  ·  Space: next  ·  Esc: exit");
                _counter.style.color = new Color(0.1f, 0.05f, 0); _counter.style.fontSize = 14; _counter.style.unityFontStyleAndWeight = FontStyle.Bold;
                _caption = new Label();
                _caption.style.color = Color.white; _caption.style.fontSize = 22; _caption.style.whiteSpace = WhiteSpace.Normal;
                _caption.style.unityFontStyleAndWeight = FontStyle.Bold;
                _box.Add(_counter); _box.Add(_caption);
                row.Add(_box); root.Add(row);
                _row = row;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            SaveService.BeginScript(GameState.Instance);   // the player's own build comes back when the demo ends
            DemoBuilds.Apply(GameState.Instance, missingRf: true, badJoint: true, padReady: true);
            GameState.Instance.Events.Clear();
            Go(0);
            SceneFlow.Load(SceneNames.IntegrationBay);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            FlightController.AutoAdvance = false;
            if (Active == this) { Active = null; SaveService.EndScript(GameState.Instance); }
        }

        void Go(int step)
        {
            _step = step;
            if (_caption != null) _caption.text = Captions[Mathf.Clamp(step, 0, Captions.Length - 1)];
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _timer = 0.8f; // let the scene's Start() run first

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _step != 1) { Exit(); return; }
            if (kb != null && kb.spaceKey.wasPressedThisFrame) Next();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Development builds with -judgeauto: move on by itself every 14 s (performance runs, no keyboard needed).
            if (AutoPlay && (_autoT += Time.unscaledDeltaTime) > 14f) { _autoT = 0; Next(); }
#endif
            if (_timer >= 0 && (_timer -= Time.unscaledDeltaTime) < 0) { _timer = -1; OnSceneReady(SceneManager.GetActiveScene().name); }

            // Flight 2: after the dramatic part (max-Q, staging) skip the long coast to orbit.
            var f = FlightController.Instance;
            if (_step == 5 && f != null && f.Launched && !f.Ended && f.Sim != null && f.Sim.TimeS > 130)
                f.FastForwardToEnd();
        }

        VisualElement _row;

        void PlaceOverlay(string scene)
        {
            if (_row == null) return;
            bool top = scene == SceneNames.Debrief;
            _row.style.top = top ? 16 : StyleKeyword.Auto;
            _row.style.bottom = top ? StyleKeyword.Auto : 24;
        }

        void OnSceneReady(string scene)
        {
            PlaceOverlay(scene);
            switch (scene)
            {
                case SceneNames.IntegrationBay when _step == 0:
                    BayController.Instance?.SetMode("wire");
                    HoustonBus.Ask("radio_sband", "IntegrationBay");
                    break;
                case SceneNames.VAB when _step == 2:
                    HoustonBus.Ask("pf_interstage", "VAB");
                    break;
                case SceneNames.Flight when _step == 3 || _step == 5:
                    var f = FlightController.Instance;
                    if (f == null) break;
                    f.Launch(3);
                    f.Warp = 4;
                    break;
                case SceneNames.Debrief when _step == 3:
                    Go(4);
                    break;
                case SceneNames.Debrief when _step == 5:
                    Go(6);
                    break;
            }
        }

        public void Next()
        {
            switch (_step)
            {
                case 0:
                    var bay = BayController.Instance;
                    if (bay != null && !bay.bench.Graph.IsConnected("radio_sband/RF")) bay.bench.Connect("radio_sband/RF", "ant_patch/RF", false);
                    Go(1);
                    break;
                case 1: Go(2); SceneFlow.Load(SceneNames.VAB); break;
                case 2: Go(3); SceneFlow.Load(SceneNames.Flight); break;
                case 3:
                    var f = FlightController.Instance;
                    if (f != null && f.Ended) SceneFlow.Load(SceneNames.Debrief);
                    break;
                case 4:
                    DemoBuilds.Apply(GameState.Instance, padReady: true);
                    Go(5);
                    SceneFlow.Load(SceneNames.Flight);
                    break;
                case 5:
                    var f2 = FlightController.Instance;
                    if (f2 != null && f2.Ended) SceneFlow.Load(SceneNames.Debrief);
                    else if (f2 != null && f2.Launched) f2.FastForwardToEnd();
                    break;
                case 6: Exit(); SceneFlow.Load(SceneNames.MainMenu); break;
            }
        }

        public void Exit() => Destroy(gameObject);

        public int Step => _step;
    }

    /// <summary>Global F9 shortcut for Judge Mode.</summary>
    sealed class JudgeHotkey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) JudgeMode.Begin();
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace TMinus.Mission
{
    /// <summary>Hooks for automated checks of Judge Mode.</summary>
    public static class JudgeDebug
    {
        public static string Start(string _) { JudgeMode.Begin(); return "started"; }
        public static string Next(string _) { JudgeMode.Active?.Next(); return State(null); }
        public static string State(string _)
        {
            var f = TMinus.Flight.FlightController.Instance;
            var r = GameState.Instance.Flight;
            return $"step={JudgeMode.Active?.Step} scene={SceneManager.GetActiveScene().name} flight={(f?.Sim != null ? f.Sim.Phase + " t=" + f.Sim.TimeS.ToString("0") : "-")} record={r.outcome} fail={r.failureCode}<-{r.failureCause}";
        }
    }
}
#endif
