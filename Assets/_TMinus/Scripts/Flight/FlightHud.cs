using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Flight
{
    /// <summary>Mission-control HUD (UI Toolkit, built in code; styles in UI/USS/Flight.uss).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FlightHud : MonoBehaviour
    {
        [SerializeField] StyleSheet baseStyle;
        [SerializeField] StyleSheet style;

        VisualElement _root, _stats, _fuel, _feed, _charts, _prelaunch, _end, _los, _banner;
        Label _clock, _phase, _bannerText;
        LineChart _altChart, _speedChart;
        OrbitMap _map;
        readonly Dictionary<string, Label> _values = new Dictionary<string, Label>();
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        FlightController _ctrl;
        bool _telemetry;
        float _bannerTime;

        public void Init(FlightController ctrl, bool telemetryOk)
        {
            _ctrl = ctrl; _telemetry = telemetryOk;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            foreach (var s in new[] { baseStyle, style }) if (s != null && !_root.styleSheets.Contains(s)) _root.styleSheets.Add(s);

            // Top bar: clock, phase, warp, cameras, abort.
            var top = Box("flight-top", _root);
            _clock = L("T+ 00:00", "clock", top);
            _phase = L("ON THE PAD", "phase", top);
            foreach (var (w, label) in new[] { (1, "x1"), (2, "x2"), (4, "x4"), (100, "x100") })
            {
                int warp = w;
                _buttons["w" + w] = Btn(label, () => _ctrl.Warp = warp, "tool-btn toggle", top);
            }
            Sep(top);
            foreach (var cam in new[] { "chase", "ground", "map" })
            {
                var c = cam;
                _buttons["c" + c] = Btn(char.ToUpper(c[0]) + c.Substring(1), () => _ctrl.SetCamera(c), "tool-btn toggle", top);
            }
            Sep(top);
            _buttons["auto"] = Btn("Autopilot ON", ToggleAutopilot, "tool-btn on", top);
            Btn("Abort", () => _ctrl.Abort(), "tool-btn abort", top);

            // Right: telemetry numbers and propellant.
            _stats = Box("flight-stats", _root);
            L("TELEMETRY", "info-category", _stats);
            foreach (var key in new[] { "Altitude", "Surface speed", "Orbital speed", "Acceleration", "Dynamic pressure", "Mass", "Thrust/weight", "Delta-v left", "Apoapsis", "Periapsis", "Stage" })
            {
                var row = Box("stat-row", _stats);
                L(key, "stat-key", row);
                _values[key] = L("—", "stat-value", row);
            }
            L("PROPELLANT", "info-category", _stats).style.marginTop = 10;
            _fuel = Box("fuel", _stats);

            _feed = Box("flight-feed", _root);
            L("EVENTS", "info-category", _feed);

            _charts = Box("flight-charts", _root);
            _altChart = new LineChart("ALTITUDE (km)", new Color(1f, 0.48f, 0.1f));
            _speedChart = new LineChart("SPEED (m/s)", new Color(0.4f, 0.75f, 1f));
            _charts.Add(_altChart); _charts.Add(_speedChart);

            _map = new OrbitMap { style = { display = DisplayStyle.None } };
            _root.Add(_map);

            _los = Box("los", _root);
            L("LOS · NO TELEMETRY", "los-title", _los);
            L("The telemetry radio or its antenna isn't wired. The rocket still flies; nobody on the ground can see how.", "los-text", _los);
            _los.style.display = telemetryOk ? DisplayStyle.None : DisplayStyle.Flex;
            if (!telemetryOk) { _stats.style.display = DisplayStyle.None; _charts.style.display = DisplayStyle.None; }

            _banner = Box("banner-row", _root);
            _banner.pickingMode = PickingMode.Ignore;
            _bannerText = L("", "banner", _banner);
            _banner.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------ pre-launch + end

        public void ShowPrelaunch(double pitchKick, double twr)
        {
            _prelaunch = Box("modal", _root);
            var p = Box("pad-panel-small", _prelaunch);
            L("TERMINAL COUNT HOLD · T-0", "info-category", p);
            L($"Lift-off thrust-to-weight: {twr:0.00} {(twr >= 1 ? "✓" : "✗ (below 1: it can't lift off)")}", "check-title", p);
            L("Pitch kick: how far the rocket tips over a few seconds after lift-off. Too little and it climbs straight up wasting fuel; too much and it dives into thick air.", "check-detail", p);
            var slider = new Slider("Pitch kick (°)", 1, 6) { value = (float)pitchKick };
            slider.AddToClassList("kick-slider");
            var val = L($"{pitchKick:0.0}°", "check-title", p);
            slider.RegisterValueChangedCallback(e => val.text = $"{e.newValue:0.0}°");
            p.Add(slider);
            Btn("IGNITION  ›", () => _ctrl.Launch(slider.value), "card-btn accent big", p);
        }

        public void HidePrelaunch() { if (_prelaunch != null) _prelaunch.style.display = DisplayStyle.None; }

        public void ShowEnd(FlightRecord r)
        {
            _end = Box("end-row", _root);
            var p = Box("end-panel", _end);
            p.AddToClassList(r.orbitAchieved ? "good" : "bad");
            L(r.orbitAchieved ? "ORBIT ACHIEVED" : r.outcome == "Aborted" ? "ABORTED" : "MISSION LOST", "end-title", p);
            L(r.orbitAchieved ? $"{r.periapsisKm:0} × {r.apoapsisKm:0} km orbit · max-Q {r.maxQKPa:0.0} kPa · {(r.deployed ? "CubeSat deployed" : "CubeSat NOT deployed")}"
                              : $"{r.failureCode} at T+{r.flightTimeS:0} s · max-Q {r.maxQKPa:0.0} kPa", "check-detail", p);
            Btn("Continue to operations + debrief  ›", () => _ctrl.ContinueToDebrief(), "card-btn accent", p);
        }

        public void Banner(string text, bool good)
        {
            _bannerText.text = text;
            _bannerText.EnableInClassList("bad", !good);
            _banner.style.display = DisplayStyle.Flex;
            _bannerTime = 3.5f;
        }

        public void Feed(double t, string message, Severity sev)
        {
            var l = L($"T+{t:0}s  {message}", "feed-line", null);
            l.EnableInClassList("bad", sev >= Severity.Fault);
            l.EnableInClassList("warn", sev == Severity.Warning);
            _feed.Insert(1, l);
            while (_feed.childCount > 12) _feed.RemoveAt(_feed.childCount - 1);
        }

        public void SetCameraMode(string mode)
        {
            foreach (var m in new[] { "chase", "ground", "map" }) _buttons["c" + m].EnableInClassList("on", m == mode);
            if (_map != null) _map.style.display = mode == "map" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ToggleAutopilot()
        {
            var o = _ctrl.Sim.Options;
            o.autopilot = !o.autopilot;
            _buttons["auto"].text = o.autopilot ? "Autopilot ON" : "Manual (A/D, Shift/Ctrl)";
            _buttons["auto"].EnableInClassList("on", o.autopilot);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(TelemetrySample s, FlightSim sim)
        {
            if (_bannerTime > 0 && (_bannerTime -= Time.deltaTime) <= 0) _banner.style.display = DisplayStyle.None;
            foreach (var w in new[] { 1, 2, 4, 100 }) _buttons["w" + w].EnableInClassList("on", _ctrl.Warp == w);
            var ts = System.TimeSpan.FromSeconds(sim.TimeS);
            _clock.text = $"T+ {(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
            _phase.text = sim.Phase.ToString().ToUpperInvariant();

            _map.path = _ctrl.Path; _map.pos = sim.Position; _map.vel = sim.Velocity;
            if (s != null) _map.caption = $"Apoapsis {(s.apoapsisM - FlightConstants.EarthRadiusM) / 1000:0} km · Periapsis {(s.periapsisM - FlightConstants.EarthRadiusM) / 1000:0} km";
            if (_map.style.display == DisplayStyle.Flex) _map.Refresh();

            if (s == null || !_telemetry) return;
            Set("Altitude", $"{s.altitudeM / 1000:0.0} km");
            Set("Surface speed", $"{s.surfaceSpeedMs:0} m/s");
            Set("Orbital speed", $"{s.orbitalSpeedMs:0} m/s");
            Set("Acceleration", $"{s.accelerationG:0.00} g");
            Set("Dynamic pressure", $"{s.dynamicPressurePa / 1000:0.0} kPa");
            Set("Mass", $"{s.massKg:N0} kg");
            Set("Thrust/weight", $"{s.thrustToWeight:0.00}");
            Set("Delta-v left", $"{s.deltaVRemainingMs:N0} m/s");
            Set("Apoapsis", s.apoapsisM > 0 ? $"{(s.apoapsisM - FlightConstants.EarthRadiusM) / 1000:0} km" : "—");
            Set("Periapsis", $"{(s.periapsisM - FlightConstants.EarthRadiusM) / 1000:0} km");
            Set("Stage", $"{s.stageIndex + 1}");
            _altChart.Add((float)s.timeS, (float)(s.altitudeM / 1000), $"{s.altitudeM / 1000:0.0}");
            _speedChart.Add((float)s.timeS, (float)s.orbitalSpeedMs, $"{s.orbitalSpeedMs:0}");

            if (s.propellantPercent != null)
            {
                while (_fuel.childCount < s.propellantPercent.Length)
                {
                    var row = Box("fuel-row", _fuel);
                    L($"Stage {_fuel.childCount}", "stat-key", row);
                    var track = Box("budget-track", row);
                    Box("budget-fill green", track);
                }
                for (int i = 0; i < s.propellantPercent.Length; i++)
                    ((VisualElement)_fuel[i][1][0]).style.width = Length.Percent((float)s.propellantPercent[i]);
            }
        }

        void Set(string k, string v) { if (_values.TryGetValue(k, out var l)) l.text = v; }

        public bool IsPointerOverUI(Vector2 screen)
        {
            if (_root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = _root.panel.Pick(p);
            return picked != null && picked != _root;
        }

        // ------------------------------------------------------------------ helpers

        static VisualElement Box(string cls, VisualElement parent)
        {
            var v = new VisualElement();
            foreach (var c in cls.Split(' ')) v.AddToClassList(c);
            parent?.Add(v);
            return v;
        }

        static Label L(string text, string cls, VisualElement parent)
        {
            var l = new Label(text);
            foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        static Button Btn(string text, System.Action a, string cls, VisualElement parent)
        {
            var b = new Button(a) { text = text };
            foreach (var c in cls.Split(' ')) b.AddToClassList(c);
            parent?.Add(b);
            return b;
        }

        static void Sep(VisualElement parent) => Box("tool-sep", parent);
    }
}
