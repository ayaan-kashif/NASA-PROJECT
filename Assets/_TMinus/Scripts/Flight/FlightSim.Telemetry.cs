using System;

namespace TMinus.Flight
{
    public sealed partial class FlightSim
    {
        TelemetrySample Sample(GuidanceCommand command)
        {
            double altitude = FlightPhysics.Altitude(_position);
            Vec2d rel = _velocity - FlightPhysics.AirVelocity(_position, Options.latitudeDeg);
            var orbit = OrbitElements.From(_position, _velocity);
            double q = FlightPhysics.DynamicPressure(_position, _velocity, Options.latitudeDeg);
            double thrust = CurrentThrust(command.throttle, altitude);
            var fuel = new double[_fuel.Length];
            for (int i = 0; i < fuel.Length; i++) fuel[i] = Vehicle.stages[i].propMassKg > 0 ? 100 * _fuel[i] / Vehicle.stages[i].propMassKg : 0;
            return new TelemetrySample
            {
                timeS = _time, altitudeM = altitude, downrangeM = Downrange(), surfaceSpeedMs = _velocity.Magnitude,
                orbitalSpeedMs = rel.Magnitude, accelerationG = thrust / Math.Max(1, _mass) / FlightConstants.G0,
                dynamicPressurePa = q, massKg = _mass, throttle = command.throttle, thrustN = thrust,
                thrustToWeight = thrust / Math.Max(1, _mass * FlightConstants.G0), deltaVRemainingMs = DeltaVRemaining(),
                apoapsisM = orbit.apoapsisM, periapsisM = orbit.periapsisM, stageIndex = _stageIndex,
                propellantPercent = fuel, phase = Phase
            };
        }

        double CurrentThrust(double throttle, double altitude)
        {
            if (_stageIndex < 0 || _stageIndex >= Vehicle.stages.Count || !_stageIgnited) return 0;
            var s = Vehicle.stages[_stageIndex];
            double isp = s.ispVac - (s.ispVac - s.ispSeaLevel) * FlightPhysics.Pressure(altitude) / FlightConstants.SeaLevelPressurePa;
            return s.thrustVacN * isp / Math.Max(1, s.ispVac) * Math.Max(0, Math.Min(1, throttle));
        }

        double DeltaVRemaining()
        {
            double m = _mass, sum = 0;
            for (int i = Math.Max(0, _stageIndex); i < Vehicle.stages.Count; i++)
            {
                if (!_stageAttached[i]) continue;
                var s = Vehicle.stages[i];
                double prop = _fuel[i];
                if (prop <= 0 || m <= prop) { m -= s.dryMassKg; continue; }
                sum += s.ispVac * FlightConstants.G0 * Math.Log(m / (m - prop));
                m -= prop + s.dryMassKg;
                foreach (var c in Vehicle.components) if (c.dropWithStage == s.id) m -= c.massKg;
            }
            return sum;
        }

        double Downrange()
        {
            double angle = Math.Atan2(_position.y, _position.x);
            double delta = angle - _startAngle - _rotationRate * _time;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta < -Math.PI) delta += 2 * Math.PI;
            return delta * FlightConstants.EarthRadiusM;
        }
    }
}
