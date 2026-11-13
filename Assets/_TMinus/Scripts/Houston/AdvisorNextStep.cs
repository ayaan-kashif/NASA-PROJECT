using System.Linq;
using TMinus.Core;
using TMinus.Wiring;

namespace TMinus.Houston
{
    public static partial class AssemblyAdvisor
    {
        /// <summary>The next action for the current scope, in dependency order, skipping finished steps.</summary>
        public static Recommendation NextStep(AdvisorContext ctx)
        {
            if (ctx == null) return Rec("No build information yet.", "none", null, null);
            switch (ctx.Scope)
            {
                case AdvisorScope.CubeSat:
                    return CubeSatStep(ctx) ?? BlockerStep(ctx) ?? Rec("The CubeSat is built and wired. Next: build the rocket in the VAB.", "cubesat_ready", null, "eps");
                case AdvisorScope.Rocket:
                    return RocketStep(ctx) ?? AvionicsStep(ctx) ?? BlockerStep(ctx) ?? Rec("The rocket is stacked, bolted and wired. Next: go to the pad.", "rocket_ready", null, "staging");
                case AdvisorScope.Avionics:
                    return ArmedStep(ctx) ?? AvionicsStep(ctx) ?? BlockerStep(ctx) ?? Rec("All 11 avionics cables are in place. Back to the VAB.", "avionics_ready", null, "pyro_separation");
                default:
                    return CubeSatStep(ctx) ?? RocketStep(ctx) ?? AvionicsStep(ctx) ?? BlockerStep(ctx) ?? PadStep(ctx)
                           ?? Rec("Build complete! All checks pass and the pad is ready: run the GO/NO-GO poll.", "ready_for_launch", null, "staging");
            }
        }

        static Recommendation Rec(string text, string reason, string obj, string codex) =>
            new Recommendation { action = "NextStep", text = text, reasonCode = reason, objectRef = obj, codexId = codex };

        static Recommendation BlockerStep(AdvisorContext ctx)
        {
            var b = Scan(ctx).FirstOrDefault(f => f.severity == FaultSeverity.Blocker);
            return b == null ? null : Rec("Fix: " + b.what, "fix_blocker", b.where, b.codexId);
        }

        // ---------------------------------------------------------------- CubeSat: parts, then cables

        static Recommendation CubeSatStep(AdvisorContext ctx)
        {
            if (ctx.CubeSatAssemblyOrder == null) return null;
            foreach (var step in ctx.CubeSatAssemblyOrder)
            {
                if (step == "frame_3u" || step == "rbf_pin") continue; // the frame is the base; the pin is a pad step
                if (!IsCubeSatStepPlaced(step, ctx)) return CubeSatInstall(step, ctx);
            }
            if (ctx.Wiring == null) return null;
            var wf = WiringValidator.Validate(ctx.Wiring).FirstOrDefault(f => f.severity >= Severity.Fault);
            return wf == null ? null : Rec(wf.message, "wiring_needed", wf.port ?? wf.device, wf.codexId);
        }

        static bool IsCubeSatStepPlaced(string step, AdvisorContext ctx)
        {
            if (ctx.CubeSatParts == null) return false;
            foreach (var p in ctx.CubeSatParts)
            {
                if (p.partId == step) return true;
                var def = ctx.Data?.GetPart(p.partId);
                if (def != null && def.category == step) return true;
            }
            return false;
        }

        static Recommendation CubeSatInstall(string step, AdvisorContext ctx)
        {
            // Category steps get a sensible default part; a specific part id is used as is.
            string partId;
            string text;
            switch (step)
            {
                case "eps": partId = "eps"; text = "Install the EPS (electrical power system) board first: everything else draws power through it."; break;
                case "battery": partId = "batt_20"; text = "Install a battery: it carries the satellite through every eclipse."; break;
                case "dep_switch": partId = "dep_switch"; text = "Install the deployment switch: it keeps the satellite off inside the dispenser."; break;
                case "obc": partId = "obc"; text = "Install the flight computer (OBC): it stores images and runs the schedule."; break;
                case "radio": partId = "radio_uhf"; text = "Install a radio to send data home (UHF is cheap, S-band is about 100 times faster)."; break;
                case "antenna": partId = "ant_uhf"; text = "Install an antenna on an end face (pick the one that matches your radio)."; break;
                case "adcs": partId = "adcs_mag"; text = "Install an ADCS so the satellite can point its camera."; break;
                case "camera": partId = "cam_wide"; text = "Install the camera payload on an end face."; break;
                case "solar": partId = "solar_body"; text = "Attach solar panels to a long face: no panels, no power."; break;
                default: partId = step; text = $"Install the {ctx.Data?.GetPart(step)?.name ?? step}."; break;
            }
            var def = ctx.Data?.GetPart(partId);
            var mount = def != null ? ctx.FreeMountFor?.Invoke(def) : null;
            if (mount != null) text += $" It will go in {MountName(mount)}.";
            return Rec(text, "assembly_order", partId, def?.codexId ?? "eps");
        }

        // ---------------------------------------------------------------- Rocket: stack, then bolts

        static Recommendation RocketStep(AdvisorContext ctx)
        {
            if (ctx.Graph != null && !ctx.Graph.IsComplete())
            {
                foreach (var partId in ctx.Graph.Rules.Order)
                    if (!ctx.Graph.IsAttached(partId))
                        return Rec($"Stack the {ctx.Graph.Rules.Name(partId)} next (bottom first).", "assembly_order", partId, "staging");
            }
            if (ctx.Graph == null && ctx.Vehicle != null && ctx.Rules != null &&
                ctx.Rules.assemblyOrder.TryGetValue(ctx.Vehicle.rocketId ?? "", out var order) && ctx.Vehicle.parts.Count < order.Count)
                return Rec($"Stack the rocket in the VAB ({ctx.Vehicle.parts.Count} of {order.Count} parts mounted).", "assembly_order", null, "staging");
            if (ctx.BoltJoints == null) return null;
            foreach (var joint in ctx.BoltJoints)
            {
                if (!JointMounted(joint, ctx)) continue;
                int next = joint.NextBolt();
                if (next < 0) continue;
                int pass = joint.CurrentPass();
                bool missing = !joint.present[next];
                return Rec(missing ? $"Fit bolt {next + 1} on {joint.def.id} (star order {BoltMath.StarOrderText(joint.Count)})."
                                   : $"Torque bolt {next + 1} on {joint.def.id} to {joint.PassTargetNm(pass):0.#} N·m (pass {pass}/3, star order).",
                           "bolt_needed", joint.def.id, "star_pattern");
            }
            return null;
        }

        // ---------------------------------------------------------------- Avionics + pad

        static Recommendation ArmedStep(AdvisorContext ctx) =>
            ctx.Vehicle != null && ctx.Vehicle.armed
                ? Rec("Switch Safe/Arm to SAFE before touching any cable: the pyro circuits are live.", "safe_first", "av_safe_arm", "safe_arm")
                : null;

        static Recommendation AvionicsStep(AdvisorContext ctx)
        {
            var c = ctx.AvionicsReport?.consequences.FirstOrDefault();
            if (c == null) return null;
            var link = ctx.Rules?.avionicsWiring.FirstOrDefault(l => l.id == c.linkId);
            var g = ctx.AvionicsWiring;
            string route = link == null ? c.linkId
                : g != null ? $"{g.Describe(link.from)} → {g.Describe(link.to)}" : $"{link.from} → {link.to}";
            string where = ctx.Scope == AdvisorScope.Avionics ? "" : "Open the Avionics bay and ";
            return Rec($"{where}connect {route}. Without it: {c.message}", "avionics_wiring", link?.from ?? c.linkId, ConsequenceCodex(c.consequence));
        }

        static Recommendation PadStep(AdvisorContext ctx)
        {
            if (ctx.RbfPinInserted) return Rec("Pull the Remove-Before-Flight pin (pad checklist).", "rbf_pin", "rbf_pin", "rbf_pin");
            if (ctx.Vehicle != null && !ctx.Vehicle.armed) return Rec("Arm the Safe/Arm plug now that the pad is clear.", "safe_arm", "av_safe_arm", "safe_arm");
            return null;
        }
    }
}
