using System;

namespace TMinus.Flight
{
    public static class FlightPhysics
    {
        public static double Altitude(Vec2d position) => position.Magnitude - FlightConstants.EarthRadiusM;

        public static double Density(double altitudeM) => altitudeM >= FlightConstants.AtmosphereLimitM || altitudeM < 0
            ? 0 : 1.225 * Math.Exp(-altitudeM / FlightConstants.ScaleHeightM);

        public static double Pressure(double altitudeM) => altitudeM >= FlightConstants.AtmosphereLimitM || altitudeM < 0
            ? 0 : FlightConstants.SeaLevelPressurePa * Math.Exp(-altitudeM / FlightConstants.ScaleHeightM);

        public static Vec2d AirVelocity(Vec2d position, double latitudeDeg)
        {
            double w = FlightConstants.EarthRotationRadS * Math.Cos(latitudeDeg * Math.PI / 180.0);
            return new Vec2d(-w * position.y, w * position.x);
        }

        public static double DynamicPressure(Vec2d position, Vec2d velocity, double latitudeDeg)
        {
            double rho = Density(Altitude(position));
            Vec2d relative = velocity - AirVelocity(position, latitudeDeg);
            return 0.5 * rho * relative.SqrMagnitude;
        }

        public static double DragCoefficient(double relativeSpeedMs)
        {
            double mach = relativeSpeedMs / FlightConstants.SpeedOfSoundMs;
            return mach >= 0.8 && mach <= 1.2 ? FlightConstants.TransonicCd : FlightConstants.StageCd;
        }

        public static double DragForceN(Vec2d position, Vec2d velocity, double latitudeDeg, double diameterM)
        {
            double rho = Density(Altitude(position));
            Vec2d rel = velocity - AirVelocity(position, latitudeDeg);
            return 0.5 * rho * rel.SqrMagnitude * DragCoefficient(rel.Magnitude) * Math.PI * diameterM * diameterM / 4;
        }

        public static double RocketEquation(double ispSeconds, double initialMassKg, double finalMassKg)
        {
            if (ispSeconds <= 0 || initialMassKg <= finalMassKg || finalMassKg <= 0) return 0;
            return ispSeconds * FlightConstants.G0 * Math.Log(initialMassKg / finalMassKg);
        }
    }
}
