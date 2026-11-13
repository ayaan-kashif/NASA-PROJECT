using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Wiring;

namespace TMinus.Houston
{
    public static partial class AssemblyAdvisor
    {
        /// <summary>
        /// Prioritised faults for the current scope. Bay: CubeSat wiring + budgets. VAB: mounted joints + avionics.
        /// Avionics bay: ring wiring, Safe/Arm must be SAFE. Pad (or no scene): everything, incl. RBF pin + ARMED.
        /// </summary>
        public static List<ScanFault> Scan(AdvisorContext ctx)
        {
            var faults = new List<ScanFault>();
            if (ctx == null) return faults;
            var scope = ctx.Scope;
            bool pad = scope == AdvisorScope.Pad;

            if (scope == AdvisorScope.CubeSat || pad) ScanCubeSat(ctx, faults);
            if (scope == AdvisorScope.Rocket || pad) ScanJoints(ctx, faults);
            if (scope == AdvisorScope.Rocket || scope == AdvisorScope.Avionics || pad) ScanAvionics(ctx, faults);

            if (pad)
            {
                if (ctx.RbfPinInserted)
                    faults.Add(new ScanFault { severity = FaultSeverity.Warning, what = "Remove-Before-Flight pin is still in: the satellite would never switch on.", where = "rbf_pin", why = "rbf_pin_in", codexId = "rbf_pin", fixAction = "pull_pin" });
                if (ctx.Vehicle != null && !ctx.Vehicle.armed)
                    faults.Add(new ScanFault { severity = FaultSeverity.Warning, what = "Safe/Arm plug is SAFE: arm it at the pad, after the pad is cleared.", where = "av_safe_arm", why = "not_armed", codexId = "safe_arm", fixAction = "arm" });
            }
            else if (ctx.Vehicle != null && ctx.Vehicle.armed && (scope == AdvisorScope.Rocket || scope == AdvisorScope.Avionics))
            {
                faults.Add(new ScanFault { severity = FaultSeverity.Blocker, what = "Safe/Arm is ARMED inside the VAB: pyro circuits are live. Switch it to SAFE.", where = "av_safe_arm", why = "armed_in_vab", codexId = "safe_arm", fixAction = "safe" });
            }

            faults.Sort((a, b) => b.severity.CompareTo(a.severity));
            return faults;
        }

        static void ScanCubeSat(AdvisorContext ctx, List<ScanFault> faults)
        {
            if (ctx.Wiring != null)
                foreach (var wf in WiringValidator.Validate(ctx.Wiring))
                    faults.Add(new ScanFault { severity = MapSeverity(wf.severity), what = wf.message, where = wf.port ?? wf.device, why = wf.code, codexId = wf.codexId, fixAction = "wire" });

            if (ctx.SpacecraftReport != null)
                foreach (var issue in ctx.SpacecraftReport.issues)
                    faults.Add(new ScanFault { severity = MapSeverity(issue.severity), what = issue.message, where = issue.code, why = issue.code, codexId = issue.codexId, fixAction = "budget" });
        }

        static void ScanJoints(AdvisorContext ctx, List<ScanFault> faults)
        {
            if (ctx.BoltJoints == null) return;
            foreach (var joint in ctx.BoltJoints)
            {
                if (!JointMounted(joint, ctx)) continue; // nothing to bolt until both parts are stacked
                var s = joint.GetStatus();
                string id = joint.def.id;
                if (s.missing > 0)
                    faults.Add(new ScanFault { severity = FaultSeverity.Blocker, what = $"{s.missing} of {s.boltCount} bolts missing on {id}.", where = id, why = "bolts_missing", codexId = "star_pattern", fixAction = "bolt" });
                if (s.sequenceErrors > 0)
                    faults.Add(new ScanFault { severity = FaultSeverity.Warning, what = $"Joint {id} warped: bolts were fully torqued out of sequence (integrity {s.integrity * 100:0}%).", where = id, why = "joint_warped", codexId = "star_pattern", fixAction = "rebolt" });
                if (s.over > 0)
                    faults.Add(new ScanFault { severity = FaultSeverity.Warning, what = $"{s.over} over-torqued (stripped) bolt{(s.over > 1 ? "s" : "")} on {id}: remove and replace.", where = id, why = "bolts_overtorqued", codexId = "torque", fixAction = "rebolt" });
                int loose = s.under + s.inserted;
                if (loose > 0)
                    faults.Add(new ScanFault { severity = FaultSeverity.Warning, what = $"{loose} bolt{(loose > 1 ? "s" : "")} on {id} not yet at full torque (pass {joint.CurrentPass()}/3).", where = id, why = "bolts_not_torqued", codexId = "torque", fixAction = "bolt" });
            }
        }

        static void ScanAvionics(AdvisorContext ctx, List<ScanFault> faults)
        {
            if (ctx.AvionicsWiring != null)
                foreach (var wf in WiringValidator.Validate(ctx.AvionicsWiring))
                    faults.Add(new ScanFault { severity = MapSeverity(wf.severity), what = "Avionics: " + wf.message, where = wf.port ?? wf.device, why = wf.code, codexId = wf.codexId, fixAction = "wire_avionics" });

            if (ctx.AvionicsReport == null) return;
            foreach (var c in ctx.AvionicsReport.consequences)
            {
                var link = ctx.Rules?.avionicsWiring.FirstOrDefault(l => l.id == c.linkId);
                faults.Add(new ScanFault
                {
                    severity = FaultSeverity.Blocker,
                    what = c.message,
                    where = link != null ? link.from : c.linkId,
                    why = c.consequence,
                    codexId = ConsequenceCodex(c.consequence),
                    fixAction = "wire_avionics"
                });
            }
        }
    }
}
