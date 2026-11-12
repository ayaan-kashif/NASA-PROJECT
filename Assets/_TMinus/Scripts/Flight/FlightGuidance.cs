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

            if (sim.Phase == FlightPhase.Pad) sim.SetPhase(FlightPhase.Ascent);
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
                    double turn = Math.Max(0, Math.Min(1, (h - 1000) / 44000.0));
                    double pitchFromVertical = (sim.Options.pitchKickDeg + turn * 60) * Math.PI / 180.0;
                    direction = h >= 45000 ? east : radial * Math.Cos(pitchFromVertical) + east * Math.Sin(pitchFromVertical);
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
