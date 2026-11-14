using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core.Data;

namespace TMinus.Core
{
    /// <summary>
    /// Cleans a design that came from outside (a pasted design code, a save file) before it is put into GameState (audit A-10).
    /// Unknown ids and out-of-range numbers are dropped or clamped; nothing here throws.
    /// </summary>
    public static class DesignValidator
    {
        /// <summary>Returns how many items were dropped or corrected. The bundle is edited in place.</summary>
        public static int Clean(DesignBundle b, DataRegistry data)
        {
            if (b == null || data == null) return 0;
            int fixedCount = 0;
            var rules = data.GetRules();

            b.spacecraft = b.spacecraft ?? new SpacecraftDesign();
            b.vehicle = b.vehicle ?? new VehicleDesign();
            var sc = b.spacecraft; var v = b.vehicle;
            sc.parts = sc.parts ?? new List<PlacedPart>();
            sc.cables = sc.cables ?? new List<CableLink>();
            v.parts = v.parts ?? new List<PlacedPart>();
            v.bolts = v.bolts ?? new List<BoltRecord>();
            v.jointWarps = v.jointWarps ?? new List<JointWarp>();
            v.avionicsCables = v.avionicsCables ?? new List<CableLink>();

            // Mission: must exist and be playable.
            var mission = string.IsNullOrEmpty(b.missionId) ? null : data.GetMission(b.missionId);
            if (mission == null || mission.locked) { if (!string.IsNullOrEmpty(b.missionId)) fixedCount++; b.missionId = "floodwatch_1"; }

            // Rocket and launch site.
            if (data.GetRocket(v.rocketId) == null) { v.rocketId = "pathfinder"; fixedCount++; }
            if (rules.GetLaunchSite(v.launchSiteId) == null) { v.launchSiteId = rules.launchSites.Count > 0 ? rules.launchSites[0].id : "sonmiani"; fixedCount++; }

            // Numbers.
            double orbit = Clamp(sc.orbitAltitudeKm, 150, 2000);
            if (Math.Abs(orbit - sc.orbitAltitudeKm) > 1e-9) { sc.orbitAltitudeKm = orbit; fixedCount++; }
            double kick = Clamp(v.pitchKickDeg, 0, 10);
            if (Math.Abs(kick - v.pitchKickDeg) > 1e-9) { v.pitchKickDeg = kick; fixedCount++; }

            // CubeSat parts: known part ids, unique instance ids.
            var seen = new HashSet<string>();
            fixedCount += sc.parts.RemoveAll(p => p == null || string.IsNullOrEmpty(p.instanceId) || data.GetPart(p.partId) == null || !seen.Add(p.instanceId));
            var instances = new HashSet<string>(sc.parts.Select(p => p.instanceId));
            fixedCount += sc.cables.RemoveAll(c => !PortOk(c, instances));

            // Rocket parts: only structure is checked here (the payload uses a non-catalogue id on purpose).
            fixedCount += v.parts.RemoveAll(p => p == null || string.IsNullOrEmpty(p.instanceId) || string.IsNullOrEmpty(p.partId));

            // Bolts: known joint, index inside the joint, sane torque.
            var joints = rules.boltJoints.ToDictionary(j => j.id, j => j.boltCount);
            fixedCount += v.bolts.RemoveAll(x => x == null || !joints.TryGetValue(x.jointId ?? "", out int n) || x.boltIndex < 0 || x.boltIndex >= n);
            foreach (var x in v.bolts)
                if (double.IsNaN(x.torqueNm) || x.torqueNm < 0 || x.torqueNm > 1000) { x.torqueNm = Clamp(double.IsNaN(x.torqueNm) ? 0 : x.torqueNm, 0, 1000); fixedCount++; }
            fixedCount += v.jointWarps.RemoveAll(w => w == null || !joints.ContainsKey(w.jointId ?? ""));
            foreach (var w in v.jointWarps) if (w.count < 0 || w.count > 99) { w.count = (int)Clamp(w.count, 0, 99); fixedCount++; }

            // Avionics cables: both ends must be avionics devices.
            var avionics = new HashSet<string>(data.Parts.Where(p => p.assembly == "avionics").Select(p => p.id));
            fixedCount += v.avionicsCables.RemoveAll(c => !PortOk(c, avionics));
            return fixedCount;
        }

        static bool PortOk(CableLink c, ISet<string> devices) =>
            c != null && Device(c.fromPort) != null && Device(c.toPort) != null && devices.Contains(Device(c.fromPort)) && devices.Contains(Device(c.toPort));

        static string Device(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            int i = address.IndexOf('/');
            return i > 0 ? address.Substring(0, i) : null;
        }

        static double Clamp(double x, double min, double max) => double.IsNaN(x) ? min : Math.Max(min, Math.Min(max, x));
    }
}
