using System;
using System.Collections.Generic;
using TMinus.Core.Data;

namespace TMinus.Flight
{
    public struct Vec2d
    {
        public double x, y;
        public Vec2d(double x, double y) { this.x = x; this.y = y; }
        public double SqrMagnitude => x * x + y * y;
        public double Magnitude => Math.Sqrt(SqrMagnitude);
        public Vec2d Normalized => Magnitude > 0 ? this / Magnitude : new Vec2d();
        public double Dot(Vec2d other) => x * other.x + y * other.y;
        public static Vec2d operator +(Vec2d a, Vec2d b) => new Vec2d(a.x + b.x, a.y + b.y);
        public static Vec2d operator -(Vec2d a, Vec2d b) => new Vec2d(a.x - b.x, a.y - b.y);
        public static Vec2d operator *(Vec2d a, double b) => new Vec2d(a.x * b, a.y * b);
        public static Vec2d operator /(Vec2d a, double b) => new Vec2d(a.x / b, a.y / b);
        public static double Dot(Vec2d a, Vec2d b) => a.x * b.x + a.y * b.y;
        public static Vec2d Rotate(Vec2d v, double radians) => new Vec2d(v.x * Math.Cos(radians) - v.y * Math.Sin(radians), v.x * Math.Sin(radians) + v.y * Math.Cos(radians));
    }

    public sealed class FlightStage
    {
        public string id, partId;
        public double dryMassKg, propMassKg, thrustVacN, ispVac, ispSeaLevel, diameterM;
        public int ignitionChannel, separationChannel;
        public double GrossMassKg => dryMassKg + propMassKg;

        public static FlightStage From(StageDef d) => new FlightStage
        {
            id = d.id, partId = d.partId, dryMassKg = d.dryMassKg, propMassKg = d.propMassKg,
            thrustVacN = d.thrustVacKN * 1000, ispVac = d.ispVac, ispSeaLevel = d.ispSl,
            diameterM = d.diameterM, ignitionChannel = d.ignitionChannel, separationChannel = d.separationChannel
        };
    }

    public sealed class FlightComponent
    {
        public string id, dropWithStage;
        public double massKg, diameterM, jettisonAltitudeKm;
    }

    public sealed class FlightVehicle
    {
        public string id;
        public readonly List<FlightStage> stages = new List<FlightStage>();
        public readonly List<FlightComponent> components = new List<FlightComponent>();
        public readonly Dictionary<string, string> separationJoints = new Dictionary<string, string>();
        public double MaxDiameterM => Math.Max(0.01, Math.Max(MaxStageDiameter(), MaxComponentDiameter()));
        double MaxStageDiameter() { double d = 0; foreach (var s in stages) d = Math.Max(d, s.diameterM); return d; }
        double MaxComponentDiameter() { double d = 0; foreach (var c in components) d = Math.Max(d, c.diameterM); return d; }
        public double InitialMassKg(double payloadKg)
        {
            double m = payloadKg;
            foreach (var s in stages) m += s.GrossMassKg;
            foreach (var c in components) m += c.massKg;
            return m;
        }
    }

    public sealed class LaunchOptions
    {
        public double payloadMassKg = 150;
        public double targetAltitudeKm = 550;
        public double latitudeDeg = 25.2;
        public double longitudeDeg;
        public double pitchKickDeg = 3;
        public bool autopilot = true;
        public double initialAltitudeKm;
        public bool startInCircularOrbit;
    }

    public enum FlightPhase { Pad, Ascent, Coast, Circularize, Orbit, Failed, Aborted }

    public struct OrbitElements
    {
        public double energy, semiMajorAxisM, eccentricity, apoapsisM, periapsisM;
        public bool bound;
        public static OrbitElements From(Vec2d position, Vec2d velocity)
        {
            double r = position.Magnitude, v2 = velocity.SqrMagnitude;
            if (r <= 0) return new OrbitElements();
            double e = v2 * 0.5 - FlightConstants.Mu / r;
            double a = Math.Abs(e) < 1e-12 ? double.PositiveInfinity : -FlightConstants.Mu / (2 * e);
            double h = position.x * velocity.y - position.y * velocity.x;
            double eccSq = 1 + 2 * e * h * h / (FlightConstants.Mu * FlightConstants.Mu);
            double ecc = Math.Sqrt(Math.Max(0, eccSq));
            bool bound = e < 0 && !double.IsInfinity(a) && !double.IsNaN(a);
            return new OrbitElements
            {
                energy = e, semiMajorAxisM = a, eccentricity = ecc, bound = bound,
                apoapsisM = bound ? a * (1 + ecc) : double.PositiveInfinity,
                periapsisM = bound ? a * (1 - ecc) : double.NegativeInfinity
            };
        }
    }

    public sealed class TelemetrySample
    {
        public double timeS, altitudeM, downrangeM, surfaceSpeedMs, orbitalSpeedMs, accelerationG;
        public double dynamicPressurePa, massKg, throttle, thrustN, thrustToWeight, deltaVRemainingMs;
        public double apoapsisM, periapsisM;
        public int stageIndex;
        public double[] propellantPercent;
        public FlightPhase phase;
    }

    public sealed class FlightOutcome
    {
        public FlightPhase phase;
        public string failureCode, failureCause;
        public double maxQPa, maxQTimeS;
        public readonly List<TelemetrySample> telemetry = new List<TelemetrySample>();
        public bool OrbitAchieved => phase == FlightPhase.Orbit;
    }
}
