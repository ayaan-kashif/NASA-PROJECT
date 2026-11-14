using System;
using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Flight
{
    /// <summary>Thin bridge from shared game data and build reports into the pure flight solver.</summary>
    public static class FlightRuntime
    {
        public static FlightSim Create(DataRegistry data, VehicleDesign vehicle, IAvionicsReport avionics,
                                       IEnumerable<IJointStatus> joints, EventLog events, string missionId = null, double? payloadKg = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            var rocket = data.GetRocket(vehicle.rocketId);
            if (rocket == null) throw new InvalidOperationException($"Unknown rocket '{vehicle.rocketId}'.");
            var rules = data.GetRules();
            var site = rules?.GetLaunchSite(vehicle.launchSiteId);
            var mission = data.GetMission(string.IsNullOrEmpty(missionId) ? "floodwatch_1" : missionId) ?? data.GetMission("floodwatch_1");
            double target = mission != null && mission.parkingOrbitKm > 0 ? mission.parkingOrbitKm
                          : mission != null && mission.defaultOrbitKm > 0 ? mission.defaultOrbitKm : rocket.payloadTargetAltKm;
            var options = new LaunchOptions
            {
                // The mass the rocket really carries (the player's CubeSat). Tests and the library card use the rocket's reference payload.
                payloadMassKg = payloadKg ?? rocket.referencePayloadKg,
                targetAltitudeKm = target,
                latitudeDeg = site?.latitudeDeg ?? 0,
                longitudeDeg = site?.longitudeDeg ?? 0,
                pitchKickDeg = vehicle.pitchKickDeg,
                autopilot = true,
                suborbital = mission != null && mission.targetApogeeKm > 0
            };
            return new FlightSim(FlightVehicleFactory.From(rocket, rules), options,
                avionics ?? new PerfectAvionicsReport(), joints, events);
        }
    }
}
