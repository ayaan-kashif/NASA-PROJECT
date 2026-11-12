using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;

namespace TMinus.Mission
{
    public sealed class ChecklistItem
    {
        public string id;
        public CrewRole role;
        public string title;
        public bool pass;
        public string detail;
        /// <summary>Scene that fixes it (Fix button), or null.</summary>
        public string fixScene;
        /// <summary>Pad action done right here instead: "pull_rbf" or "arm".</summary>
        public string action;
        public string codexId;
        public override string ToString() => $"[{(pass ? "PASS" : "FAIL")}] {id} ({role}): {detail}";
    }

    /// <summary>
    /// Pre-launch checklist (Prompt 9), filled from the real build: SpacecraftReport, the CubeSat wiring,
    /// AssemblyGraph state (Vehicle.parts), BoltJoint status, AvionicsReport and payload mass vs capacity.
    /// </summary>
    public static class PrelaunchChecklist
    {
        /// <summary>Plain inputs so tests can build a checklist without a running game.</summary>
        public sealed class Input
        {
            public DataRegistry Data;
            public string CurrentMissionId;
            public SpacecraftDesign Spacecraft;
            public VehicleDesign Vehicle;
        }

        public static Input From(GameState g) =>
            new Input { Data = g.Data, CurrentMissionId = g.CurrentMissionId, Spacecraft = g.Spacecraft, Vehicle = g.Vehicle };

        public static List<ChecklistItem> Build(GameState g) => Build(From(g));

        public static List<ChecklistItem> Build(Input s)
        {
            var data = s.Data;
            var rules = data.GetRules();
            var mission = data.GetMission(s.CurrentMissionId);
            var rocket = data.GetRocket(s.Vehicle.rocketId);
            var report = SpacecraftBudget.Compute(s.Spacecraft, data, mission, rocket);
            var items = new List<ChecklistItem>();

            // ---------------- Spacecraft & Comms
            var missingParts = report.issues.Where(i => i.code.StartsWith("no_")).Select(i => i.message).ToList();
            foreach (var cat in new[] { "eps", "obc" })
                if (!s.Spacecraft.parts.Any(p => data.GetPart(p.partId)?.category == cat))
                    missingParts.Add($"No {(cat == "eps" ? "EPS board" : "flight computer")}.");
            items.Add(Item("sc_parts", CrewRole.SpacecraftComms, "CubeSat has every subsystem", missingParts.Count == 0,
                missingParts.Count == 0 ? "Power, computer, radio + antenna, ADCS, camera and panels fitted." : string.Join(" ", missingParts),
                SceneNames.IntegrationBay, "eps"));

            var faults = SpacecraftWiringFaults(s);
            int bad = faults.Count(f => f.severity >= Severity.Fault);
            items.Add(Item("sc_wiring", CrewRole.SpacecraftComms, "CubeSat wiring check", bad == 0,
                bad == 0 ? $"{s.Spacecraft.cables.Count} cables, no faults." : $"{bad} wiring fault(s): {faults.First(f => f.severity >= Severity.Fault).message}",
                SceneNames.IntegrationBay, "eps"));

            bool powerOk = report.power != BudgetStatus.Red && report.battery != BudgetStatus.Red;
            items.Add(Item("sc_power", CrewRole.SpacecraftComms, "Power and battery margins", powerOk,
                $"Uses {report.consumptionW:0.00} W of {report.generationW:0.00} W; battery needs {report.requiredBatteryWh:0.0} of {report.batteryWh:0} Wh.",
                SceneNames.IntegrationBay, "eclipse"));

            items.Add(Item("sc_data", CrewRole.SpacecraftComms, "Data budget meets the image goal", report.data == BudgetStatus.Green,
                report.imageGoal > 0 ? $"About {report.imagesDownlinkedMission:0} of {report.imageGoal} images will reach the ground." : $"{report.downlinkPerDayMB:0.0} MB/day downlink.",
                SceneNames.IntegrationBay, "downlink"));

            items.Add(new ChecklistItem
            {
                id = "pad_rbf", role = CrewRole.SpacecraftComms, title = "Remove-Before-Flight pin pulled", pass = s.Spacecraft.rbfPinPulled,
                detail = s.Spacecraft.rbfPinPulled ? "Pin removed: the satellite will wake up after deployment." : "Still inserted: the satellite would never switch on.",
                action = s.Spacecraft.rbfPinPulled ? null : "pull_rbf", codexId = "rbf_pin"
            });

            // ---------------- Propulsion & Structures
            if (rocket != null && rocket.buildable)
            {
                int needed = rules.assemblyOrder.TryGetValue(rocket.id, out var order) ? order.Count : 0;
                int mounted = s.Vehicle.parts.Count;
                items.Add(Item("rk_stack", CrewRole.PropulsionStructures, "Rocket fully stacked", needed > 0 && mounted >= needed,
                    $"{mounted} of {needed} parts mounted.", SceneNames.VAB, "staging"));

                var joints = BoltJointState.AllStatuses(rules, s.Vehicle);
                var weak = joints.Where(j => j.integrity < 0.999).ToList();
                items.Add(Item("rk_joints", CrewRole.PropulsionStructures, "Every joint torqued in star order", weak.Count == 0,
                    weak.Count == 0 ? $"All {joints.Count} joints at 100% integrity."
                        : string.Join(" ", weak.Select(j => $"{j.jointId}: {j.integrity * 100:0}% ({j.torqued}/{j.boltCount} torqued{(j.missing > 0 ? $", {j.missing} missing" : "")}{(j.sequenceErrors > 0 ? ", warped" : "")}).")),
                    SceneNames.VAB, "star_pattern"));

                var av = AvionicsReport.Compute(s.Vehicle, rules);
                items.Add(Item("rk_avionics", CrewRole.PropulsionStructures, "Avionics ring wired", av.AllOk,
                    av.AllOk ? $"All {av.requiredLinks} cables in place." : $"{av.consequences.Count} missing: {av.consequences[0].message}",
                    SceneNames.AvionicsBay, "pyro_separation"));

                double payloadKg = report.massKg;
                items.Add(Item("rk_payload", CrewRole.PropulsionStructures, "Payload within rocket capacity", payloadKg <= rocket.payloadLeoKg,
                    $"{payloadKg:0.00} kg payload, {rocket.name} lifts {rocket.payloadLeoKg:0} kg to orbit.", SceneNames.VAB, "delta_v"));
            }

            // ---------------- Science & Budget
            items.Add(Item("sb_budget", CrewRole.ScienceBudget, "Within mission budget", report.cost != BudgetStatus.Red,
                $"{report.totalCostM:0.000} M$ of {report.budgetM:0.00} M$.", SceneNames.IntegrationBay, null));
            items.Add(Item("sb_mass", CrewRole.ScienceBudget, "Within the 3U mass limit", report.mass != BudgetStatus.Red,
                $"{report.massKg:0.00} kg of {report.massLimitKg:0.0} kg.", SceneNames.IntegrationBay, null));
            items.Add(Item("sb_pointing", CrewRole.ScienceBudget, "Camera can point accurately enough", report.pointingQuality >= 1,
                report.adcsId == null ? "No ADCS fitted." : $"Image quality {report.pointingQuality:0.0}; resolution ≈ {report.gsdM:0.#} m/pixel at {report.altitudeKm:0} km.",
                SceneNames.IntegrationBay, "adcs"));

            // ---------------- Flight Director
            var site = rules.GetLaunchSite(s.Vehicle.launchSiteId);
            items.Add(Item("fd_site", CrewRole.FlightDirector, "Launch site chosen", site != null,
                site != null ? $"{site.name} ({site.latitudeDeg:0.0}°N): Earth's spin gives {EarthBoostMs(site.latitudeDeg):0} m/s eastward." : "Pick Sonmiani or Cape Canaveral.",
                SceneNames.MissionBriefing, "launching_east"));
            items.Add(new ChecklistItem
            {
                id = "pad_arm", role = CrewRole.FlightDirector, title = "Safe/Arm plug ARMED (pad clear)", pass = s.Vehicle.armed,
                detail = s.Vehicle.armed ? "Pyro circuits live. Ready for terminal count." : "Still SAFE: nothing can ignite or separate.",
                action = s.Vehicle.armed ? null : "arm", codexId = "safe_arm"
            });
            return items;
        }

        static ChecklistItem Item(string id, CrewRole role, string title, bool pass, string detail, string fix, string codex) =>
            new ChecklistItem { id = id, role = role, title = title, pass = pass, detail = detail, fixScene = pass ? null : fix, codexId = codex };

        /// <summary>Eastward speed of the ground: ω × R × cos(latitude).</summary>
        public static double EarthBoostMs(double latitudeDeg) =>
            7.2921159e-5 * 6371000.0 * System.Math.Cos(latitudeDeg * System.Math.PI / 180.0);

        public static List<WiringFault> SpacecraftWiringFaults(Input s)
        {
            var g = new WiringGraph(s.Data.GetRules());
            foreach (var p in s.Spacecraft.parts)
            {
                var def = s.Data.GetPart(p.partId);
                if (def != null && def.ports.Count > 0) g.AddDevice(p.instanceId, def);
            }
            foreach (var c in s.Spacecraft.cables) g.Connect(c.fromPort, c.toPort, c.reversed, out _);
            return WiringValidator.Validate(g);
        }

        /// <summary>Roles for 1-4 players; with fewer players, roles merge.</summary>
        public static List<CrewMember> AssignRoles(IList<string> players)
        {
            var roles = new[] { CrewRole.FlightDirector, CrewRole.PropulsionStructures, CrewRole.SpacecraftComms, CrewRole.ScienceBudget };
            int n = System.Math.Max(1, System.Math.Min(4, players.Count));
            // 1: all to P1. 2: P1 = FD + Science, P2 = Propulsion + Spacecraft. 3: P1 = FD + Science. 4: one each.
            int[] owner = n == 1 ? new[] { 0, 0, 0, 0 } : n == 2 ? new[] { 0, 1, 1, 0 } : n == 3 ? new[] { 0, 1, 2, 0 } : new[] { 0, 1, 2, 3 };
            var crew = new List<CrewMember>();
            for (int i = 0; i < roles.Length; i++)
                crew.Add(new CrewMember { name = players.Count > owner[i] ? players[owner[i]] : $"Player {owner[i] + 1}", role = roles[i] });
            return crew;
        }

        public static string RoleName(CrewRole r)
        {
            switch (r)
            {
                case CrewRole.FlightDirector: return "Flight Director";
                case CrewRole.PropulsionStructures: return "Propulsion & Structures";
                case CrewRole.SpacecraftComms: return "Spacecraft & Comms";
                default: return "Science & Budget";
            }
        }
    }
}
