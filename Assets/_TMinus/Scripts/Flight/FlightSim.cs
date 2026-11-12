using System;
using System.Collections.Generic;
using TMinus.Core;

namespace TMinus.Flight
{
    /// <summary>Pure C# two-dimensional flight solver. The Unity layer only feeds controls and draws samples.</summary>
    public sealed partial class FlightSim
    {
        readonly double[] _fuel;
        readonly bool[] _stageAttached;
        readonly bool[] _burnoutHandled;
        readonly bool[] _componentAttached;
        readonly Dictionary<string, IJointStatus> _joints = new Dictionary<string, IJointStatus>();
        readonly IAvionicsReport _avionics;
        readonly EventLog _events;
        readonly FlightOutcome _outcome = new FlightOutcome();
        readonly double _startAngle;
        readonly double _rotationRate;
        bool _pitchKicked, _fairingChecked, _guidanceFailureLogged;
        bool _stageIgnited = true;
        double _mass, _time, _maxQ, _guidanceErrorDeg;
        Vec2d _position, _velocity;
        int _stageIndex;

        public FlightVehicle Vehicle { get; }
        public LaunchOptions Options { get; }
        public FlightPhase Phase { get; private set; } = FlightPhase.Pad;
        public Vec2d Position => _position;
        public Vec2d Velocity => _velocity;
        public double MassKg => _mass;
        public double TimeS => _time;
        public int StageIndex => _stageIndex;
        public bool PitchKicked => _pitchKicked;
        public double GuidanceErrorDeg => _guidanceErrorDeg;
        public double ManualThrottle { get; private set; } = 1;
        public double ManualPitchDeg { get; private set; } = 90;
        public bool RecordTelemetry { get; set; } = true;
        public FlightOutcome Outcome => _outcome;

        public FlightSim(FlightVehicle vehicle, LaunchOptions options, IAvionicsReport avionics = null,
                         IEnumerable<IJointStatus> joints = null, EventLog events = null)
        {
            Vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            Options = options ?? new LaunchOptions();
            _avionics = avionics ?? new PerfectAvionicsReport();
            _events = events;
            _fuel = new double[vehicle.stages.Count];
            _stageAttached = new bool[vehicle.stages.Count];
            _burnoutHandled = new bool[vehicle.stages.Count];
            for (int i = 0; i < vehicle.stages.Count; i++)
            {
                _fuel[i] = vehicle.stages[i].propMassKg;
                _stageAttached[i] = true;
            }
            _componentAttached = new bool[vehicle.components.Count];
            for (int i = 0; i < _componentAttached.Length; i++) _componentAttached[i] = true;
            if (joints != null) foreach (var joint in joints) if (joint != null) _joints[joint.JointId] = joint;
            _startAngle = Options.longitudeDeg * Math.PI / 180.0;
            _rotationRate = FlightConstants.EarthRotationRadS * Math.Cos(Options.latitudeDeg * Math.PI / 180.0);
            double startRadius = FlightConstants.EarthRadiusM + Options.initialAltitudeKm * 1000;
            _position = new Vec2d(startRadius * Math.Cos(_startAngle), startRadius * Math.Sin(_startAngle));
            var east = new Vec2d(-Math.Sin(_startAngle), Math.Cos(_startAngle));
            _velocity = east * (Options.startInCircularOrbit ? Math.Sqrt(FlightConstants.Mu / startRadius) : _rotationRate * startRadius);
            _mass = vehicle.InitialMassKg(Options.payloadMassKg);
            if (Options.initialAltitudeKm > 0 || Options.startInCircularOrbit) Phase = FlightPhase.Coast;
            _outcome.phase = Phase;
            if (vehicle.stages.Count == 0 || _mass <= 0) Fail("invalid_vehicle", "No usable rocket stage is configured.", vehicle.id);
            else if (!_avionics.S1IgnitionOk)
            {
                _stageIgnited = false;
                Fail("ignition_failure", "Stage 1 did not ignite; launch aborted.", Cause("no_ignition"));
                Phase = FlightPhase.Aborted;
                _outcome.phase = Phase;
            }
            else Log("launch", "Stage 1 ignition confirmed.", null, Severity.Info);
        }

        public void SetManualControl(double throttle, double pitchDeg)
        {
            ManualThrottle = Clamp(throttle, 0, 1);
            ManualPitchDeg = Clamp(pitchDeg, 0, 180);
        }

        internal void SetPhase(FlightPhase phase) { Phase = phase; _outcome.phase = phase; }
        internal void MarkPitchKick() { _pitchKicked = true; }

        /// <summary>Advance wall-clock seconds at a clamped warp, always stepping with 0.05 s integration.</summary>
        public void Advance(double realSeconds, int requestedWarp)
        {
            int maxWarp = FlightPhysics.Altitude(_position) < FlightConstants.AtmosphereLimitM ? 4 : 100;
            int warp = requestedWarp >= 100 ? 100 : requestedWarp >= 4 ? 4 : requestedWarp >= 2 ? 2 : 1;
            warp = Math.Min(warp, maxWarp);
            int count = (int)Math.Floor(Math.Max(0, realSeconds) * warp / FlightConstants.Dt + 1e-9);
            for (int i = 0; i < count && !Terminal(); i++) Step();
        }

        public void RunForSimSeconds(double seconds)
        {
            double end = _time + Math.Max(0, seconds);
            while (_time + 1e-9 < end && !Terminal()) Step();
        }

        public void Step()
        {
            if (Terminal()) return;
            if (_stageIndex >= 0 && _stageIndex < _fuel.Length && _fuel[_stageIndex] <= 1e-9 && !_burnoutHandled[_stageIndex])
                HandleBurnout();
            if (Terminal()) return;

            GuidanceCommand command = Options.autopilot ? FlightGuidance.Autopilot(this) : FlightGuidance.Manual(this);
            if (!_avionics.GuidanceOk)
            {
                _guidanceErrorDeg += 0.5 * FlightConstants.Dt;
                if (!_guidanceFailureLogged)
                {
                    _guidanceFailureLogged = true;
                    Log("guidance_drift", "IMU guidance is unavailable; pitch error is growing by 0.5 degrees per second.", Cause("guidance_drift"), Severity.Fault);
                }
                command.thrustDirection = Vec2d.Rotate(command.thrustDirection, _guidanceErrorDeg * Math.PI / 180.0).Normalized;
            }
            if (!_stageIgnited || _stageIndex < 0 || _stageIndex >= Vehicle.stages.Count || _fuel[_stageIndex] <= 0)
                command.throttle = 0;
            Integrate(FlightConstants.Dt, command);
            HoldDownOnPad();
            _time += FlightConstants.Dt;
            double h = FlightPhysics.Altitude(_position);
            double q = FlightPhysics.DynamicPressure(_position, _velocity, Options.latitudeDeg);
            if (q > _maxQ) { _maxQ = q; _outcome.maxQPa = q; _outcome.maxQTimeS = _time; }
            CheckJointFailure(q);
            CheckFairing(h);
            if (h < -10 && _time > 2) Fail("impact", "The vehicle fell back to Earth.", null);
            if (_stageIndex >= 0 && _stageIndex < _fuel.Length && _fuel[_stageIndex] <= 1e-9 && !_burnoutHandled[_stageIndex])
                HandleBurnout();
            if (RecordTelemetry) _outcome.telemetry.Add(Sample(command));
            _outcome.phase = Phase;
        }

        static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));

        void HoldDownOnPad()
        {
            if (Phase != FlightPhase.Pad) return;
            Vec2d radial = _position.Normalized;
            _position = radial * FlightConstants.EarthRadiusM;
            _velocity -= radial * _velocity.Dot(radial);
        }

        bool Terminal() => Phase == FlightPhase.Orbit || Phase == FlightPhase.Failed || Phase == FlightPhase.Aborted;
        string Cause(string consequence) => _avionics.CauseId(consequence);

        void HandleBurnout()
        {
            int spent = _stageIndex;
            _burnoutHandled[spent] = true;
            Log("stage_cutoff", $"{Vehicle.stages[spent].id} propellant depleted.", Vehicle.stages[spent].id, Severity.Info);
            if (spent + 1 >= Vehicle.stages.Count) return;
            if (!_avionics.StageSeparationOk)
            {
                Fail("stuck_stage", "Stage separation command failed; the upper stage remains attached.", Cause("stuck_stage"));
                return;
            }
            string jointId = Vehicle.separationJoints.TryGetValue(Vehicle.stages[spent].id, out var id) ? id : Vehicle.stages[spent].id;
            if (_joints.TryGetValue(jointId, out var joint) && _maxQ / 1000.0 > joint.FailureQkPa)
            {
                Fail("joint_failure", $"{jointId} failed under dynamic pressure.", jointId);
                return;
            }
            DropStage(spent);
            Log("stage_separation", $"{Vehicle.stages[spent].id} separated.", jointId, Severity.Info);
            _stageIndex++;
            _stageIgnited = _avionics.S2IgnitionOk;
            if (!_stageIgnited)
                Log("s2_no_thrust", "Stage 2 igniter is not wired; the upper stage will coast.", Cause("s2_no_thrust"), Severity.Fault);
            else Log("stage_ignition", $"{Vehicle.stages[_stageIndex].id} ignition confirmed.", Vehicle.stages[_stageIndex].id, Severity.Info);
        }

        void DropStage(int index)
        {
            if (!_stageAttached[index]) return;
            _stageAttached[index] = false;
            _mass = Math.Max(0, _mass - Vehicle.stages[index].dryMassKg - _fuel[index]);
            for (int i = 0; i < Vehicle.components.Count; i++)
                if (_componentAttached[i] && Vehicle.components[i].dropWithStage == Vehicle.stages[index].id)
                {
                    _mass -= Vehicle.components[i].massKg;
                    _componentAttached[i] = false;
                }
        }

        void CheckJointFailure(double qPa)
        {
            double qKpa = qPa / 1000.0;
            foreach (var joint in _joints.Values)
                if (qKpa > joint.FailureQkPa)
                {
                    Fail("joint_failure", $"{joint.JointId} failed above its {joint.FailureQkPa:0.0} kPa limit.", joint.JointId);
                    return;
                }
        }

        bool _fairingStuckLogged;

        void CheckFairing(double altitudeM)
        {
            if (_fairingChecked || altitudeM < 110000) return;
            _fairingChecked = true;
            bool hasFairing = false;
            for (int i = 0; i < Vehicle.components.Count; i++)
                if (_componentAttached[i] && Vehicle.components[i].jettisonAltitudeKm >= 110) hasFairing = true;
            if (!hasFairing) return;
            if (!_avionics.FairingSeparationOk)
            {
                if (_fairingStuckLogged) return;
                _fairingStuckLogged = true;
                Log("fairing_stuck", "Fairing separation failed; the fairing stays attached.", Cause("fairing_stuck"), Severity.Fault);
                return;
            }
            for (int i = 0; i < Vehicle.components.Count; i++)
                if (_componentAttached[i] && Vehicle.components[i].jettisonAltitudeKm >= 110)
                {
                    _mass -= Vehicle.components[i].massKg;
                    _componentAttached[i] = false;
                }
            Log("fairing_jettison", "Fairing released above 110 km.", null, Severity.Info);
        }

        void Integrate(double dt, GuidanceCommand command)
        {
            var y = new Motion { p = _position, v = _velocity, mass = _mass,
                prop = _stageIndex >= 0 && _stageIndex < _fuel.Length ? _fuel[_stageIndex] : 0 };
            var k1 = Derivative(y, command, dt);
            var k2 = Derivative(y + k1 * (dt * 0.5), command, dt);
            var k3 = Derivative(y + k2 * (dt * 0.5), command, dt);
            var k4 = Derivative(y + k3 * dt, command, dt);
            var next = y + (k1 + k2 * 2 + k3 * 2 + k4) * (dt / 6.0);
            double newProp = Clamp(next.prop, 0, y.prop);
            double consumed = y.prop - newProp;
            _position = next.p;
            _velocity = next.v;
            _mass = Math.Max(1, y.mass - consumed);
            if (_stageIndex >= 0 && _stageIndex < _fuel.Length) _fuel[_stageIndex] = newProp;
        }

        Motion Derivative(Motion y, GuidanceCommand command, double dt)
        {
            double r = Math.Max(1, y.p.Magnitude), altitude = r - FlightConstants.EarthRadiusM;
            Vec2d gravity = y.p * (-FlightConstants.Mu / (r * r * r));
            Vec2d rel = y.v - FlightPhysics.AirVelocity(y.p, Options.latitudeDeg);
            double rho = FlightPhysics.Density(altitude), speed = rel.Magnitude;
            double area = Math.PI * Vehicle.MaxDiameterM * Vehicle.MaxDiameterM / 4.0;
            double drag = 0.5 * rho * speed * speed * FlightPhysics.DragCoefficient(speed) * area;
            Vec2d dragAccel = y.mass > 0 && speed > 0 ? rel * (-drag / (speed * y.mass)) : new Vec2d();
            double thrust = 0, mdot = 0;
            if (_stageIndex >= 0 && _stageIndex < Vehicle.stages.Count && _stageIgnited && y.prop > 0 && command.throttle > 0)
            {
                var stage = Vehicle.stages[_stageIndex];
                double nominal = stage.thrustVacN / (FlightConstants.G0 * stage.ispVac) * command.throttle;
                mdot = Math.Min(nominal, y.prop / dt);
                double isp = stage.ispVac - (stage.ispVac - stage.ispSeaLevel) * FlightPhysics.Pressure(altitude) / FlightConstants.SeaLevelPressurePa;
                thrust = mdot * FlightConstants.G0 * isp;
            }
            Vec2d accel = gravity + dragAccel + command.thrustDirection * (thrust / Math.Max(1, y.mass));
            return new Motion { p = y.v, v = accel, mass = -mdot, prop = -mdot, thrust = thrust, mdot = mdot };
        }

        void Fail(string code, string message, string cause)
        {
            _outcome.failureCode = code;
            _outcome.failureCause = cause;
            if (Phase != FlightPhase.Aborted) SetPhase(FlightPhase.Failed);
            Log(code, message, cause, Severity.Fault);
        }

        void Log(string code, string message, string cause, Severity severity)
        {
            _events?.AddAt(_time, "flight", code, message, cause, severity);
        }

        struct Motion
        {
            public Vec2d p, v;
            public double mass, prop, thrust, mdot;
            public static Motion operator +(Motion a, Motion b) => new Motion
            { p = a.p + b.p, v = a.v + b.v, mass = a.mass + b.mass, prop = a.prop + b.prop };
            public static Motion operator *(Motion a, double s) => new Motion
            { p = a.p * s, v = a.v * s, mass = a.mass * s, prop = a.prop * s };
        }
    }
}
