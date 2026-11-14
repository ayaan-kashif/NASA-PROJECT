using System;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Flight
{
    public sealed partial class FlightController
    {
        bool _initialized;
        bool _initializing;
        int _countdownRemaining;
        double _countdownTimer;
        double _pendingPitchKick;
        internal int CountdownRemaining => _countdownRemaining;
        internal string InitializationState => _initialized ? "ready" : _initializing ? "initializing" : "pending";

        public void StartCountdown(double pitchKickDeg)
        {
            EnsureInitialized();
            if (Launched || Ended || _countdownRemaining > 0) return;
            _pendingPitchKick = pitchKickDeg;
            _countdownRemaining = 10;
            _countdownTimer = 1;
            hud.CountdownStarted();
            hud.Feed(Sim.TimeS, "Countdown started · terminal checks green", Severity.Info);
        }

        void TickCountdown()
        {
            if (_countdownRemaining <= 0) return;
            _countdownTimer -= Time.unscaledDeltaTime;
            while (_countdownTimer <= 0 && _countdownRemaining > 0)
            {
                _countdownRemaining--;
                hud.Countdown(_countdownRemaining);
                hud.Feed(Sim.TimeS, _countdownRemaining > 0 ? $"T-{_countdownRemaining:00}" : "T-0 · release hold-downs", Severity.Info);
                _countdownTimer += 1;
                if (_countdownRemaining == 0) Launch(_pendingPitchKick);
            }
        }

        void CancelCountdown()
        {
            _countdownRemaining = 0;
            _countdownTimer = 0;
            hud.CancelCountdown();
        }

        void EnsureInitialized()
        {
            if (_initialized || _initializing) return;
            _initializing = true;
            try
            {
                Instance = this;
                view ??= FindAnyObjectByType<FlightView>();
                hud ??= FindAnyObjectByType<FlightHud>();
                orbit ??= FindAnyObjectByType<OrbitCamera>();
                if (view == null || hud == null || orbit == null)
                    throw new InvalidOperationException("Flight scene is incomplete: it needs FlightView, FlightHud and OrbitCamera.");

                var data = S.Data;
                var rules = data.GetRules();
                _rocket = data.GetRocket(S.Vehicle.rocketId) ?? data.GetRocket("pathfinder");
                if (_rocket == null) throw new InvalidOperationException("Flight could not load the selected rocket or Pathfinder fallback.");
                S.Vehicle.rocketId = _rocket.id;
                _avionics = _rocket.buildable ? AvionicsReport.Compute(S.Vehicle, rules) : AvionicsReport.Perfect();
                var joints = _rocket.buildable ? BoltJointState.AllStatuses(rules, S.Vehicle).Select(j => (IJointStatus)j).ToList() : null;

                S.Events.RemoveSystem("flight"); // a new flight replaces the last one's events (Fly again)
                Sim = FlightRuntime.Create(data, S.Vehicle, _avionics, joints, S.Events, S.CurrentMissionId, CarriedMassKg(data));
                if (S.Spacecraft.orbitAltitudeKm > 0 && _rocket.buildable) Sim.Options.targetAltitudeKm = S.Spacecraft.orbitAltitudeKm;
                Sim.Options.pitchKickDeg = S.Vehicle.pitchKickDeg > 0 ? S.Vehicle.pitchKickDeg : 3;

                if (_engine == null) _engine = gameObject.AddComponent<EngineSound>();
                _cam = orbit.GetComponent<Camera>();
                if (_cam == null) throw new InvalidOperationException("Flight OrbitCamera must be attached to a Camera.");
                view.cam = _cam;
                view.Build(_rocket, data, Sim.Position, Sim.Options.latitudeDeg, Sim.Options.longitudeDeg);
                S.Events.Added -= OnEvent;
                S.Events.Added += OnEvent;

                hud.Init(this, _avionics.TelemetryOk);
                hud.ShowPrelaunch(Sim.Options.pitchKickDeg, TwrAtLiftoff());
                orbit.IsPointerOverUI = hud.IsPointerOverUI;
                orbit.yaw = 20; orbit.pitch = 5;
                orbit.Frame(view.RocketBounds(), true);
                SetCamera("ground");
                _initialized = true;
            }
            finally { _initializing = false; }
        }

        /// <summary>
        /// What a buildable rocket really carries: the player's CubeSat. It used to be a fixed 150 kg whatever was built (audit A-14). tools/sim
        /// shows Pathfinder still reaches orbit with a few kilograms, and max-Q rises only from 40.6 to 41.4 kPa (the joint limit is 52.8).
        /// A rocket that is not built by the player carries its reference payload.
        /// </summary>
        double? CarriedMassKg(DataRegistry data)
        {
            if (_rocket == null || !_rocket.buildable) return null;
            var report = SpacecraftBudget.Compute(S.Spacecraft, data, data.GetMission(S.CurrentMissionId), _rocket);
            return report.massKg;
        }

        internal void StepForDebug()
        {
            EnsureInitialized();
            Sim.Step();
            AfterStep();
        }

        internal void RefreshDebugView()
        {
            var last = Sim.Outcome.telemetry.Count > 0 ? Sim.Outcome.telemetry[Sim.Outcome.telemetry.Count - 1] : null;
            double maxThrust = MaxThrustN();
            view.Sync(Sim.Position, Sim.Velocity, Sim.TimeS, last != null ? last.thrustN / maxThrust : 0, last?.altitudeM ?? 0);
            hud.ForceTextRefresh();
            hud.Tick(last, Sim);
            UpdateCamera(last);
        }

        void LateUpdate()
        {
            if (!_initialized || _cam == null) return;
            // OrbitCamera adjusts clips for close-up modeling; flight must also draw Earth's full radius.
            _cam.farClipPlane = 2e4f;
            _cam.nearClipPlane = CameraMode == "ground" ? 1f : 0.3f;
        }
    }
}
