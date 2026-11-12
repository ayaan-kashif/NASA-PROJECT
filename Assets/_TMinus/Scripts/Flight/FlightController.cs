using System.Collections.Generic;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Flight
{
    /// <summary>
    /// Flight scene (Prompt 11): builds FlightSim from the player's real build (bolts, avionics), holds on the
    /// pad for the pitch-kick choice, checks thrust-to-weight at T-0, flies with time warp, turns sim events into
    /// animations and HUD callouts, then stores a FlightRecord and goes to the Debrief.
    /// </summary>
    public sealed class FlightController : MonoBehaviour
    {
        public FlightView view;
        public FlightHud hud;
        public OrbitCamera orbit;

        public static FlightController Instance { get; private set; }
        public FlightSim Sim { get; private set; }
        public int Warp { get; set; } = 1;
        public string CameraMode { get; private set; } = "ground";
        public bool Launched { get; private set; }
        public bool Ended { get; private set; }
        public readonly List<Vec2d> Path = new List<Vec2d>();

        AvionicsReport _avionics;
        RocketDef _rocket;
        double _maxQ, _lastPathT = -1;
        bool _maxQCalled, _deployed;
        FlightPhase _lastPhase = FlightPhase.Pad;
        float _endTimer = -1;
        Camera _cam;
        GameState S => GameState.Instance;

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (S != null) S.Events.Added -= OnEvent;
        }

        void Start()
        {
            var data = S.Data;
            var rules = data.GetRules();
            _rocket = data.GetRocket(S.Vehicle.rocketId) ?? data.GetRocket("pathfinder");
            S.Vehicle.rocketId = _rocket.id;
            _avionics = _rocket.buildable ? AvionicsReport.Compute(S.Vehicle, rules) : AvionicsReport.Perfect();
            var joints = _rocket.buildable ? BoltJointState.AllStatuses(rules, S.Vehicle).Select(j => (IJointStatus)j).ToList() : null;

            Sim = FlightRuntime.Create(data, S.Vehicle, _avionics, joints, S.Events);
            if (S.Spacecraft.orbitAltitudeKm > 0 && _rocket.buildable) Sim.Options.targetAltitudeKm = S.Spacecraft.orbitAltitudeKm;
            Sim.Options.pitchKickDeg = S.Vehicle.pitchKickDeg > 0 ? S.Vehicle.pitchKickDeg : 3;

            _cam = orbit.GetComponent<Camera>();
            view.cam = _cam;
            view.Build(_rocket, data, Sim.Position, Sim.Options.latitudeDeg);
            S.Events.Added += OnEvent;

            hud.Init(this, _avionics.TelemetryOk);
            hud.ShowPrelaunch(Sim.Options.pitchKickDeg, TwrAtLiftoff());
            orbit.IsPointerOverUI = hud.IsPointerOverUI;
            orbit.yaw = 20; orbit.pitch = 5;
            orbit.Frame(view.RocketBounds(), true);
            SetCamera("ground");
        }

        public double TwrAtLiftoff()
        {
            var s = Sim.Vehicle.stages[0];
            double thrustSl = s.thrustVacN * (s.ispVac > 0 ? s.ispSeaLevel / s.ispVac : 1);
            return thrustSl / (Sim.MassKg * FlightConstants.G0);
        }

        /// <summary>T-0: check lift-off thrust, then release the clamps.</summary>
        public void Launch(double pitchKickDeg)
        {
            if (Launched || Ended) return;
            Sim.Options.pitchKickDeg = pitchKickDeg;
            S.Vehicle.pitchKickDeg = pitchKickDeg;
            hud.HidePrelaunch();
            double twr = TwrAtLiftoff();
            if (twr < 1)
            {
                S.Events.Add("flight", "abort_low_twr", $"Not enough thrust to lift off: TWR {twr:0.00}", null, Severity.Fault);
                hud.Banner($"ABORT: not enough thrust to lift off (TWR {twr:0.00})", false);
                Finish("Aborted", "abort_low_twr", null);
                return;
            }
            Launched = true;
            AudioFx.Play("snap", 1f, 0.5f);
            hud.Banner("IGNITION · LIFTOFF", true);
        }

        void Update()
        {
            HandleKeys();
            if (Launched && !Ended)
            {
                Sim.Advance(Time.deltaTime, Warp);
                AfterStep();
            }
            var last = Sim.Outcome.telemetry.Count > 0 ? Sim.Outcome.telemetry[Sim.Outcome.telemetry.Count - 1] : null;
            double maxThrust = Sim.Vehicle.stages.Count > 0 ? Sim.Vehicle.stages.Max(s => s.thrustVacN) : 1;
            view.Sync(Sim.Position, Sim.Velocity, Sim.TimeS, last != null ? last.thrustN / maxThrust : 0, last?.altitudeM ?? 0);
            hud.Tick(last, Sim);
            UpdateCamera(last);

            if (_endTimer >= 0 && (_endTimer -= Time.deltaTime) < 0) { _endTimer = -1; SceneFlow.Load(SceneNames.Debrief); }
        }

        void AfterStep()
        {
            var last = Sim.Outcome.telemetry.Count > 0 ? Sim.Outcome.telemetry[Sim.Outcome.telemetry.Count - 1] : null;
            if (last != null)
            {
                if (last.dynamicPressurePa > _maxQ) _maxQ = last.dynamicPressurePa;
                else if (!_maxQCalled && _maxQ > 5000 && last.dynamicPressurePa < _maxQ * 0.97)
                {
                    _maxQCalled = true;
                    hud.Banner($"MAX-Q · {_maxQ / 1000:0.0} kPa", true);
                    hud.Feed(Sim.TimeS, $"Max-Q passed: {_maxQ / 1000:0.0} kPa", Severity.Info);
                }
            }
            if (Sim.TimeS - _lastPathT >= 1) { _lastPathT = Sim.TimeS; Path.Add(Sim.Position); }

            if (Sim.Phase != _lastPhase)
            {
                _lastPhase = Sim.Phase;
                if (Sim.Phase == FlightPhase.Orbit) OnOrbit();
                else if (Sim.Phase == FlightPhase.Failed || Sim.Phase == FlightPhase.Aborted)
                    Finish(Sim.Phase.ToString(), Sim.Outcome.failureCode, Sim.Outcome.failureCause);
            }
        }

        void OnOrbit()
        {
            hud.Banner("ORBIT ACHIEVED", true);
            S.Events.AddAt(Sim.TimeS, "flight", "orbit_achieved", "Orbit achieved.", null, Severity.Info);
            bool canDeploy = _avionics.FairingSeparationOk;
            if (canDeploy)
            {
                var sat = view.PartsWhere(p => p.instanceId == "payload").ToList();
                view.Release(sat, Sim.Position, Sim.Velocity, Vector3.up * 1.5f);
                _deployed = sat.Count > 0;
                S.Events.AddAt(Sim.TimeS + 1, "flight", "cubesat_deployed", "CubeSat sprang out of the dispenser.", null, Severity.Info);
                hud.Feed(Sim.TimeS, "CubeSat deployed", Severity.Info);
            }
            else hud.Feed(Sim.TimeS, "CubeSat can't deploy: the fairing is still closed.", Severity.Fault);
            Finish("Orbit", null, null);
        }

        void OnEvent(LogEvent e)
        {
            if (e.system != "flight") return;
            hud.Feed(e.time, e.message, e.severity);
            switch (e.code)
            {
                case "stage_separation":
                {
                    int spent = Mathf.Max(0, Sim.StageIndex); // the sim logs separation before moving to the next stage
                    var stage = _rocket.stages.Count > spent ? _rocket.stages[spent] : null;
                    var ids = new HashSet<string>();
                    if (stage != null) ids.Add(stage.partId);
                    foreach (var c in _rocket.components) if (stage != null && c.dropWithStage == stage.id && c.partId != null) ids.Add(c.partId);
                    view.Release(view.PartsWhere(p => ids.Contains(p.partId)), Sim.Position, Sim.Velocity, Vector3.down * 6);
                    hud.Banner("STAGE SEPARATION", true);
                    AudioFx.Play("snap");
                    break;
                }
                case "stage_ignition": hud.Banner("STAGE 2 IGNITION", true); break;
                case "fairing_jettison":
                    view.Release(view.PartsWhere(p => p.Def != null && p.Def.category == "fairing" && p.partId.EndsWith("left")), Sim.Position, Sim.Velocity, Vector3.left * 8);
                    view.Release(view.PartsWhere(p => p.Def != null && p.Def.category == "fairing"), Sim.Position, Sim.Velocity, Vector3.right * 8);
                    hud.Banner("FAIRING SEPARATION", true);
                    break;
                case "joint_failure":
                    view.Explode();
                    hud.Banner($"VEHICLE LOST: {e.message}", false);
                    AudioFx.Play("spark");
                    break;
                case "stuck_stage": hud.Banner("STAGE SEPARATION FAILED", false); break;
                case "ignition_failure": hud.Banner("NO IGNITION · ABORT", false); break;
                case "s2_no_thrust": hud.Banner("STAGE 2 DID NOT LIGHT", false); break;
                case "guidance_drift": hud.Banner("GUIDANCE LOST: NO IMU", false); break;
            }
        }

        void Finish(string outcome, string code, string cause)
        {
            if (Ended) return;
            Ended = true;
            var last = Sim.Outcome.telemetry.Count > 0 ? Sim.Outcome.telemetry[Sim.Outcome.telemetry.Count - 1] : null;
            var rec = new FlightRecord
            {
                flown = true, outcome = outcome, orbitAchieved = outcome == "Orbit", deployed = _deployed,
                failureCode = code, failureCause = cause,
                maxQKPa = Sim.Outcome.maxQPa / 1000, maxQTimeS = Sim.Outcome.maxQTimeS,
                apoapsisKm = last != null ? (last.apoapsisM - FlightConstants.EarthRadiusM) / 1000 : 0,
                periapsisKm = last != null ? (last.periapsisM - FlightConstants.EarthRadiusM) / 1000 : 0,
                flightTimeS = Sim.TimeS
            };
            double next = 0;
            foreach (var t in Sim.Outcome.telemetry)
            {
                if (t.timeS < next) continue;
                next = t.timeS + 1;
                rec.telemetry.Add(new TelemetryPoint { t = (float)t.timeS, altitudeKm = (float)(t.altitudeM / 1000), speedMs = (float)t.orbitalSpeedMs, qKPa = (float)(t.dynamicPressurePa / 1000) });
            }
            S.Flight = rec;
            hud.ShowEnd(rec);
            _endTimer = outcome == "Orbit" ? 9f : 7f;
        }

        public void ContinueToDebrief() => SceneFlow.Load(SceneNames.Debrief);

        public void Abort()
        {
            if (!Launched || Ended) return;
            S.Events.AddAt(Sim.TimeS, "flight", "manual_abort", "Flight Director called an abort.", null, Severity.Warning);
            hud.Banner("ABORT", false);
            Finish("Aborted", "manual_abort", null);
        }

        // ------------------------------------------------------------------ cameras + keys

        public void SetCamera(string mode)
        {
            CameraMode = mode;
            orbit.enabled = mode == "chase";
            hud.SetCameraMode(mode);
        }

        void UpdateCamera(TelemetrySample last)
        {
            if (CameraMode == "ground" && Launched && Sim.TimeS > 20) SetCamera("chase");
            if (CameraMode == "ground")
            {
                // A camera on the ground 400 m from the pad, looking at the rocket.
                var pad = view.earthRoot.Find("PadArea");
                if (pad != null)
                {
                    _cam.transform.position = pad.TransformPoint(new Vector3(-250, 6, -300));
                    _cam.transform.LookAt(view.RocketBounds().center, pad.up);
                }
            }
            else if (CameraMode == "chase")
            {
                var b = view.RocketBounds();
                orbit.target = b.center;
                orbit.minDistance = b.extents.magnitude * 1.2f;
                orbit.maxDistance = Mathf.Max(5000, b.extents.magnitude * 50);
                if (orbit.distance < orbit.minDistance) orbit.distance = orbit.minDistance;
            }
            float alt = (float)(last?.altitudeM ?? 0);
            _cam.backgroundColor = Color.Lerp(new Color(0.45f, 0.65f, 0.95f), new Color(0.005f, 0.008f, 0.02f), Mathf.Clamp01(alt / 70000f));
            _cam.farClipPlane = 4e7f;
            _cam.nearClipPlane = CameraMode == "ground" ? 1f : 0.3f;
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.cKey.wasPressedThisFrame) SetCamera(CameraMode == "chase" ? "ground" : CameraMode == "ground" ? "map" : "chase");
            if (kb.digit1Key.wasPressedThisFrame) Warp = 1;
            if (kb.digit2Key.wasPressedThisFrame) Warp = 2;
            if (kb.digit3Key.wasPressedThisFrame) Warp = 4;
            if (kb.digit4Key.wasPressedThisFrame) Warp = 100;
            if (!Sim.Options.autopilot)
            {
                double pitch = Sim.ManualPitchDeg, thr = Sim.ManualThrottle;
                if (kb.aKey.isPressed) pitch += 20 * Time.deltaTime;
                if (kb.dKey.isPressed) pitch -= 20 * Time.deltaTime;
                if (kb.leftShiftKey.isPressed) thr += Time.deltaTime;
                if (kb.leftCtrlKey.isPressed) thr -= Time.deltaTime;
                Sim.SetManualControl(thr, pitch);
            }
        }
    }
}
