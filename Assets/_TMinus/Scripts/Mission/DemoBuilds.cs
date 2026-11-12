using System.Linq;
using TMinus.Core;

namespace TMinus.Mission
{
    /// <summary>
    /// Ready-made FloodWatch-1 builds for testing and Judge Mode (Prompt 18): a flight-ready S-band CubeSat on a
    /// Pathfinder stacked, bolted in star order and fully wired. Options inject the classic faults.
    /// </summary>
    public static class DemoBuilds
    {
        public static readonly string[] CubeSatParts =
            { "eps", "batt_20", "dep_switch", "obc", "radio_sband", "ant_patch", "adcs_mag", "cam_wide", "solar_body", "rbf_pin" };

        public static readonly string[][] CubeSatCables =
        {
            new[] { "solar_body/SOLAR_OUT_1", "eps/SOLAR_IN_1" }, new[] { "batt_20/BATT", "dep_switch/BATT_IN" },
            new[] { "dep_switch/BATT_OUT", "eps/BATT" }, new[] { "eps/PWR_OUT_1", "obc/PWR_IN" },
            new[] { "eps/PWR_OUT_2", "radio_sband/PWR_IN" }, new[] { "eps/PWR_OUT_3", "adcs_mag/PWR_IN" },
            new[] { "eps/PWR_OUT_4", "cam_wide/PWR_IN" }, new[] { "eps/DATA", "obc/DATA_1" },
            new[] { "radio_sband/DATA", "obc/DATA_2" }, new[] { "adcs_mag/DATA", "obc/DATA_3" },
            new[] { "cam_wide/DATA", "obc/DATA_4" }, new[] { "radio_sband/RF", "ant_patch/RF" },
        };

        /// <param name="missingRf">Leave the antenna cable out (Judge Mode step a).</param>
        /// <param name="badJoint">Bolt joint 1 clockwise in one pass with 3 bolts missing (Judge Mode step b).</param>
        /// <param name="noSeparation">Leave pyro channel 2 unwired.</param>
        public static void Apply(GameState s, bool missingRf = false, bool badJoint = false, bool noSeparation = false)
        {
            var rules = s.Data.GetRules();
            s.CurrentMissionId = "floodwatch_1";

            var sc = new SpacecraftDesign { orbitAltitudeKm = 550 };
            foreach (var p in CubeSatParts) sc.parts.Add(new PlacedPart { instanceId = p, partId = p, mountId = Mount(p) });
            foreach (var c in CubeSatCables)
                if (!(missingRf && c[0] == "radio_sband/RF")) sc.cables.Add(new CableLink { fromPort = c[0], toPort = c[1] });
            s.Spacecraft = sc;

            var v = new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" };
            string below = "STAND";
            foreach (var id in rules.assemblyOrder["pathfinder"])
            {
                string socket = id == "payload" ? "payload" : id.StartsWith("pf_fairing") ? (id.EndsWith("left") ? "fairing_left" : "fairing_right") : "top";
                string lower = socket == "top" ? below : "pf_payload_adapter";
                v.parts.Add(new PlacedPart { instanceId = id, partId = id, mountId = lower + "/" + socket });
                if (socket == "top") below = id;
            }
            foreach (var j in rules.boltJoints)
            {
                var st = new BoltJointState(j);
                bool bad = badJoint && j.id == "j_s1_interstage";
                for (int i = 0; i < j.boltCount; i++) if (!(bad && i >= j.boltCount - 3)) st.Insert(i);
                if (bad) for (int i = 0; i < j.boltCount - 3; i++) st.ApplyTorque(i, st.Target);
                else for (int pass = 1; pass <= 3; pass++) foreach (var i in BoltMath.StarOrder(j.boltCount)) st.ApplyTorque(i, st.PassTargetNm(pass));
                st.SaveTo(v);
            }
            foreach (var l in rules.avionicsWiring)
                if (!(noSeparation && l.id == "w_pyro2")) v.avionicsCables.Add(new CableLink { fromPort = l.from, toPort = l.to, cableType = l.cableType });
            s.Vehicle = v;
            s.Launch = new LaunchRecord();
        }

        static string Mount(string partId)
        {
            switch (partId)
            {
                case "eps": return "U1A"; case "batt_20": return "U1B"; case "obc": return "U2A"; case "radio_sband": return "U2B";
                case "adcs_mag": return "U3A"; case "ant_patch": return "TOP"; case "cam_wide": return "BOTTOM";
                case "solar_body": return "-Z"; case "dep_switch": return "RAIL"; case "rbf_pin": return "PIN";
                default: return "U3B";
            }
        }
    }

    /// <summary>Static hooks for automated checks of the briefing / pad.</summary>
    public static class BriefingDebug
    {
        static BriefingUI C => BriefingUI.Instance;
        public static string Missions(string _) { C.ShowMissionSelect(); return "ok"; }
        public static string Brief(string id) { GameState.Instance.CurrentMissionId = id; C.ShowBriefing(); return "ok"; }
        public static string Demo(string variant)
        {
            DemoBuilds.Apply(GameState.Instance, missingRf: variant.Contains("rf"), badJoint: variant.Contains("joint"), noSeparation: variant.Contains("sep"));
            return "applied " + variant;
        }
        public static string Checklist(string _)
        {
            C.ShowChecklist();
            var items = PrelaunchChecklist.Build(GameState.Instance);
            return string.Join(" | ", items.Where(i => !i.pass).Select(i => i.id));
        }
        public static string PadDone(string _)
        {
            GameState.Instance.Spacecraft.rbfPinPulled = true;
            GameState.Instance.Vehicle.armed = true;
            C.ShowChecklist();
            return "pad actions done";
        }
    }
}
