using System;

namespace TMinus.Flight
{
    public struct GuidanceCommand
    {
        public double throttle;
        public Vec2d thrustDirection;
    }

    internal static class FlightGuidance
    {
        public static GuidanceCommand Autopilot(FlightSim sim)
        {
            var state = sim.Position;
            Vec2d radial = state.Normalized;
            Vec2d east = new Vec2d(-radial.y, radial.x);
            Vec2d air = FlightPhysics.AirVelocity(state, sim.Options.latitudeDeg);
            Vec2d relative = sim.Velocity - air;
            Vec2d direction = radial;
            double h = FlightPhysics.Altitude(state);
            double relativeSpeed = relative.Magnitude;
            double targetRadius = FlightConstants.EarthRadiusM + sim.Options.targetAltitudeKm * 1000;
            double throttle = 1;
            if (sim.Options.suborbital) return Suborbital(sim, radial, east, h);

            if (sim.Phase == FlightPhase.Pad)
            {
                var firstStage = sim.Vehicle.stages[sim.StageIndex];
                double seaLevelThrust = firstStage.thrustVacN * firstStage.ispSeaLevel / Math.Max(1, firstStage.ispVac);
                if (seaLevelThrust * sim.ManualThrottle > sim.MassKg * FlightConstants.G0)
                    sim.SetPhase(FlightPhase.Ascent);
                else
                    direction = radial; // Hold-down clamps keep a sub-TWR vehicle on the pad.
            }
            if (sim.Phase == FlightPhase.Ascent)
            {
                var orbit = OrbitElements.From(state, sim.Velocity);
                if (h >= FlightConstants.OrbitalCutoffMinAltitudeM && orbit.bound && orbit.apoapsisM >= targetRadius && relative.Dot(radial) > 0)
                {
                    sim.SetPhase(FlightPhase.Coast);
                    throttle = 0;
                }
                else if (relativeSpeed < 100 && h < 1000)
                    direction = radial;
                else
                {
                    if (!sim.PitchKicked) sim.MarkPitchKick();
                    var v = sim.Vehicle;
                    double turn = Math.Max(0, Math.Min(1, (h - 1000) / v.turnSpanM));
                    double pitchFromVertical = (sim.Options.pitchKickDeg + turn * v.turnDeg) * Math.PI / 180.0;
                    direction = h >= v.turnEndM ? east : radial * Math.Cos(pitchFromVertical) + east * Math.Sin(pitchFromVertical);
                    if (v.altitudeHold && h >= v.turnEndM) direction = AltitudeHold(sim, radial, east, relative, h, v);
                }
            }

            if (sim.Phase == FlightPhase.Coast)
            {
                throttle = 0;
                if (relative.Dot(radial) <= 0)
                {
                    sim.SetPhase(FlightPhase.Circularize);
                    throttle = 1;
                    direction = sim.Velocity.Normalized;
                }
            }
            else if (sim.Phase == FlightPhase.Circularize)
            {
                direction = sim.Velocity.Normalized;
                var orbit = OrbitElements.From(state, sim.Velocity);
                if (orbit.bound && orbit.periapsisM >= targetRadius - 10000)
                {
                    sim.SetPhase(FlightPhase.Orbit);
                    throttle = 0;
                }
            }

            if (sim.Phase == FlightPhase.Orbit || sim.Phase == FlightPhase.Failed || sim.Phase == FlightPhase.Aborted)
                throttle = 0;
            return new GuidanceCommand { throttle = throttle, thrustDirection = direction.Normalized };
        }

        /// <summary>A sounding rocket: lift off once thrust beats weight, straight up for 300 m, then a fixed small tilt towards the east.</summary>
        static GuidanceCommand Suborbital(FlightSim sim, Vec2d radial, Vec2d east, double h)
        {
            if (sim.Phase == FlightPhase.Pad)
            {
                var first = sim.Vehicle.stages[sim.StageIndex];
                if (first.thrustVacN * first.ispSeaLevel / Math.Max(1, first.ispVac) * sim.ManualThrottle > sim.MassKg * FlightConstants.G0)
                    sim.SetPhase(FlightPhase.Ascent);
            }
            double kick = sim.Options.pitchKickDeg * Math.PI / 180.0;
            if (!sim.PitchKicked && h >= 300) sim.MarkPitchKick();
            var direction = h < 300 ? radial : radial * Math.Cos(kick) + east * Math.Sin(kick);
            bool done = sim.Phase == FlightPhase.Failed || sim.Phase == FlightPhase.Aborted || sim.Phase == FlightPhase.Apogee;
            return new GuidanceCommand { throttle = done ? 0 : 1, thrustDirection = direction.Normalized };
        }

        /// <summary>
        /// For a stage whose thrust cannot hold its weight: tilt the thrust up from the horizon just enough to cancel the sinking
        /// (gravity minus the centrifugal lift of the sideways speed, minus a little damping on the climb rate).
        /// </summary>
        static Vec2d AltitudeHold(FlightSim sim, Vec2d radial, Vec2d east, Vec2d relative, double h, FlightVehicle v)
        {
            int index = Math.Max(0, Math.Min(sim.StageIndex, v.stages.Count - 1));
            var s = v.stages[index];
            double isp = s.ispVac - (s.ispVac - s.ispSeaLevel) * FlightPhysics.Pressure(h) / FlightConstants.SeaLevelPressurePa;
            double accel = s.thrustVacN * isp / Math.Max(1, s.ispVac) / Math.Max(1, sim.MassKg);
            double r = sim.Position.Magnitude;
            double g = FlightConstants.Mu / (r * r);
            double sideways = relative.Dot(east), climb = relative.Dot(radial);
            double need = g - sideways * sideways / r - v.vrGain * climb;   // upward acceleration still needed
            double sin = Math.Max(0, Math.Min(v.maxClimb, need / accel));
            if (accel < 1.5 * g || sin > 0.05) return radial * sin + east * Math.Sqrt(1 - sin * sin);
            return east;
        }

        public static GuidanceCommand Manual(FlightSim sim)
        {
            Vec2d radial = sim.Position.Normalized;
            Vec2d tangent = new Vec2d(-radial.y, radial.x);
            double a = sim.ManualPitchDeg * Math.PI / 180.0;
            var forward = radial * Math.Sin(a) + tangent * Math.Cos(a);
            return new GuidanceCommand { throttle = sim.ManualThrottle, thrustDirection = forward.Normalized };
        }
    }
}
