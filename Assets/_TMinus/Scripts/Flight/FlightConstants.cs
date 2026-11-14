using System;
using TMinus.Core.Data;

namespace TMinus.Flight
{
    public static class FlightConstants
    {
        public const double Mu = 3.986004418e14;
        public const double EarthRadiusM = 6371000;
        public const double EarthRotationRadS = 7.2921159e-5;
        public const double G0 = 9.80665;
        public const double SeaLevelPressurePa = 101325;
        public const double ScaleHeightM = 8500;
        public const double AtmosphereLimitM = 150000;
        public const double OrbitalCutoffMinAltitudeM = 100000;
        public const double Dt = 0.05;
        public const double SpeedOfSoundMs = 340;
        public const double StageCd = 0.4;
        public const double TransonicCd = 0.6;
    }

    public static class FlightVehicleFactory
    {
        public static FlightVehicle From(RocketDef rocket, RulesDef rules)
        {
            if (rocket == null) throw new ArgumentNullException(nameof(rocket));
            var v = new FlightVehicle { id = rocket.id };
            if (rocket.guidance != null)
            {
                v.turnSpanM = rocket.guidance.turnSpanM; v.turnEndM = rocket.guidance.turnEndM; v.turnDeg = rocket.guidance.turnDeg;
                v.altitudeHold = rocket.guidance.altitudeHold; v.maxClimb = rocket.guidance.maxClimb; v.vrGain = rocket.guidance.vrGain;
            }
            foreach (var s in rocket.stages) v.stages.Add(FlightStage.From(s));
            foreach (var c in rocket.components)
                v.components.Add(new FlightComponent { id = c.id, dropWithStage = c.dropWithStage, massKg = c.massKg,
                    diameterM = c.diameterM, jettisonAltitudeKm = c.jettisonAltitudeKm });
            if (rules != null)
            {
                for (int i = 0; i + 1 < v.stages.Count; i++)
                {
                    string lower = v.stages[i].partId, upper = v.stages[i + 1].partId;
                    var joint = rules.boltJoints.Find(j => j.lowerPartId == lower && j.upperPartId == upper);
                    if (joint == null) joint = rules.boltJoints.Find(j => j.lowerPartId == lower || j.upperPartId == upper);
                    if (joint != null) v.separationJoints[v.stages[i].id] = joint.id;
                }
            }
            return v;
        }
    }
}
