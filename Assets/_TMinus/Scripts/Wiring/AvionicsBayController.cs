using System.Collections.Generic;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Wiring
{
    /// <summary>
    /// Close-up "Bay view" of the Pathfinder avionics ring (Prompt 8). Same wiring bench as the
    /// CubeSat. Cables are saved to GameState.Vehicle.avionicsCables; AvionicsReport turns missing
    /// cables into flight consequences. Safe/Arm must stay SAFE here: changing wiring while ARMED is a fault.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AvionicsBayController : MonoBehaviour
    {
        public OrbitCamera orbit;
        public WiringBench bench;
        [SerializeField] StyleSheet baseStyle;
        [SerializeField] StyleSheet style;

        public static AvionicsBayController Instance { get; private set; }
        public AvionicsReport Report { get; private set; }

        VisualElement _root, _faultList, _faults, _labels;
        Label _count, _toast, _hint;
        Button _arm, _meter;
        IVisualElementScheduledItem _toastHide;
        readonly Dictionary<string, Label> _tileLabels = new Dictionary<string, Label>();
        Camera _cam;

        VehicleDesign Vehicle => GameState.Instance.Vehicle;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (var s in new[] { baseStyle, style }) if (s != null && !_root.styleSheets.Contains(s)) _root.styleSheets.Add(s);
            _faultList = _root.Q("fault-list");
            _faults = _root.Q("faults");
            _labels = _root.Q("labels-layer");
            _count = _root.Q<Label>("fault-count");
            _toast = _root.Q<Label>("toast");
            _arm = _root.Q<Button>("btn-arm");
            _meter = _root.Q<Button>("btn-meter");
            _root.Q<Button>("btn-back").clicked += () => SceneFlow.Load(SceneNames.VAB);
            _root.Q<Button>("btn-reset").clicked += Frame;
            _arm.clicked += ToggleArm;
            _meter.clicked += () => { bench.multimeter = !bench.multimeter; _meter.EnableInClassList("on", bench.multimeter); };
            _hint = new Label { pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("drag-hint");
            _hint.style.display = DisplayStyle.None;
            _root.Add(_hint);
        }

        void Start()
        {
            var data = GameState.Instance.Data;
            _cam = orbit.GetComponent<Camera>();
            bench.cam = _cam;
            bench.system = "Avionics";
            bench.Init(data.GetRules());
            bench.IsPointerOverUI = IsPointerOverUI;
            bench.ShowHint = ShowHint;
            bench.HideHint = () => _hint.style.display = DisplayStyle.None;
            bench.Toast = m => Toast(m, 4000);
            orbit.IsPointerOverUI = IsPointerOverUI;

            foreach (var p in data.Parts.Where(p => p.assembly == "avionics").OrderBy(p => p.id))
                bench.AddDevice(p.id, p);
            bench.LoadFrom(Vehicle.avionicsCables);

            bench.Graph.Connected += _ => CheckArmedWork("connected a cable");
            bench.Graph.Removed += _ => CheckArmedWork("removed a cable");
            bench.Changed += () => { bench.SaveTo(Vehicle.avionicsCables); Refresh(); };

            RefreshArm();
            Refresh();
            Frame();
            if (Vehicle.avionicsCables.Count == 0)
                Toast("Wire the ring: battery -> PDU -> computer, IMU, telemetry; then the four pyro channels.", 6000);
        }

        void Frame()
        {
            orbit.yaw = 0; orbit.pitch = 60;
            orbit.Frame(bench.BenchBounds(), true);
        }

        // ------------------------------------------------------------------ Safe/Arm

        public void ToggleArm()
        {
            Vehicle.armed = !Vehicle.armed;
            AudioFx.Play(Vehicle.armed ? "error" : "click");
            if (Vehicle.armed)
            {
                Toast("ARMED inside the VAB! Pyro circuits are live. Arm only at the pad, after everyone has left.", 5000);
                GameState.Instance.Events.Add("Avionics", "ArmedInVab", "Safe/Arm switched to ARMED in the VAB", "av_safe_arm", Severity.Warning);
            }
            else Toast("Safe/Arm on SAFE. The pyro circuits are blocked.");
            RefreshArm();
            Refresh();
        }

        void RefreshArm()
        {
            _arm.text = Vehicle.armed ? "Safe/Arm: ARMED ⚠" : "Safe/Arm: SAFE ✓";
            _arm.EnableInClassList("arm-armed", Vehicle.armed);
            _arm.EnableInClassList("arm-safe", !Vehicle.armed);
        }

        void CheckArmedWork(string what)
        {
            if (!Vehicle.armed) return;
            Toast($"Safety violation: you {what} while ARMED. Switch Safe/Arm to SAFE first.", 5000);
            GameState.Instance.Events.Add("Avionics", "WorkWhileArmed", $"Wiring changed while ARMED ({what})", "av_safe_arm", Severity.Fault);
            AudioFx.Play("error");
        }

        // ------------------------------------------------------------------ checks

        void Refresh()
        {
            Report = AvionicsReport.Compute(Vehicle, GameState.Instance.Data.GetRules());
            _faultList.Clear();
            var faults = bench.Faults;
            int issues = Report.consequences.Count + faults.Count + (Vehicle.armed ? 1 : 0);
            _count.text = $"{Report.wiredLinks}/{Report.requiredLinks} cables" + (issues == 0 ? "  ✓" : "");
            _faults.EnableInClassList("clean", issues == 0);

            if (Vehicle.armed) Item("blocker", "✗ SAFETY", "Safe/Arm is ARMED. It must be SAFE in the VAB.");
            foreach (var f in faults)
                Item(f.severity >= Severity.Blocker ? "blocker" : "fault-sev", f.severity >= Severity.Blocker ? "✗ WIRING" : "! WIRING", f.message, f.port);
            foreach (var c in Report.consequences)
            {
                var link = GameState.Instance.Data.GetRules().avionicsWiring.First(l => l.id == c.linkId);
                Item("blocker", "✗ IN FLIGHT", $"{bench.Graph.Describe(link.from)} → {bench.Graph.Describe(link.to)} missing. {c.message}", link.from);
            }
            if (issues == 0)
            {
                var ok = new Label("All 11 cables in place. Every stage will light, separate and report home.");
                ok.AddToClassList("fault-ok");
                _faultList.Add(ok);
            }
        }

        void Item(string cls, string tag, string message, string port = null)
        {
            var item = new VisualElement();
            item.AddToClassList("fault"); item.AddToClassList(cls);
            var t = new Label(tag); t.AddToClassList("fault-tag");
            var m = new Label(message); m.AddToClassList("fault-msg");
            item.Add(t); item.Add(m);
            if (port != null)
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    var pv = bench.FindPort(port);
                    if (pv != null) { orbit.Focus(new Bounds(pv.transform.position, Vector3.one * 0.3f)); pv.SetGlow(new Color(1f, 0.4f, 0.1f)); }
                });
            _faultList.Add(item);
        }

        // ------------------------------------------------------------------ UI helpers

        void Update()
        {
            if (_cam == null || _labels == null) return;
            foreach (var t in bench.Tiles.Values)
            {
                if (!_tileLabels.TryGetValue(t.instanceId, out var l))
                {
                    l = new Label(t.def.name) { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("tile-label");
                    l.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
                    _labels.Add(l);
                    _tileLabels[t.instanceId] = l;
                }
                var sp = _cam.WorldToScreenPoint(t.LabelAnchor);
                var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
                l.style.left = p.x; l.style.top = p.y;
            }
        }

        public void Toast(string message, int ms = 3000)
        {
            _toast.text = message;
            _toast.RemoveFromClassList("hidden");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("hidden")).StartingIn(ms);
        }

        void ShowHint(string text, bool? valid, Vector2 screen)
        {
            _hint.text = text;
            _hint.EnableInClassList("valid", valid == true);
            _hint.EnableInClassList("invalid", valid == false);
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            _hint.style.left = p.x + 22; _hint.style.top = p.y + 18;
            _hint.style.display = DisplayStyle.Flex;
        }

        bool IsPointerOverUI(Vector2 screen)
        {
            if (_root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = _root.panel.Pick(p);
            return picked != null && picked != _root;
        }
    }

    /// <summary>Static hooks for automated checks of the avionics bay.</summary>
    public static class AvionicsDebug
    {
        static AvionicsBayController C => AvionicsBayController.Instance;

        /// <summary>"all" wires everything; "nosep" leaves out pyro channel 2.</summary>
        public static string Wire(string variant)
        {
            foreach (var l in GameState.Instance.Data.GetRules().avionicsWiring)
            {
                if (variant == "nosep" && l.id == "w_pyro2") continue;
                C.bench.Connect(l.from, l.to, false);
            }
            return C.Report.ToString();
        }

        public static string Arm(string _) { C.ToggleArm(); return C.Report.ToString(); }
        public static string Report(string _) => C.Report.ToString();
    }
}
