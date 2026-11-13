using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;

namespace TMinus.Houston
{
    /// <summary>
    /// Deterministic, offline advisor (Prompt 13). Reads only the game's own data and rules, so its
    /// answers are always consistent with the validators. Split across files:
    /// AssemblyAdvisor (WhereDoesThisGo, BoltCoach), AdvisorNextStep, AdvisorScan.
    /// </summary>
    public static partial class AssemblyAdvisor
    {
        // ------------------------------------------------------------------ WhereDoesThisGo

        /// <summary>Ranked valid targets for a held item (part id or port address "instance/PORT").</summary>
        public static List<Recommendation> WhereDoesThisGo(string heldItem, AdvisorContext ctx)
        {
            var results = new List<Recommendation>();
            if (ctx == null || string.IsNullOrEmpty(heldItem)) return results;
            string item = heldItem.Trim();

            // 1. A cable end: every free compatible port, required connections first.
            if (item.Contains("/"))
            {
                var g = ctx.Wiring ?? ctx.AvionicsWiring;
                var fromPort = g?.Port(item);
                if (fromPort != null)
                {
                    foreach (var (inst, def) in g.Devices)
                        foreach (var port in def.ports)
                        {
                            var addr = WiringGraph.Address(inst, port.id);
                            if (addr == item || !g.CanConnect(item, addr, out _, out _, out bool shortCircuit) || shortCircuit) continue;
                            bool required = IsRequiredConnection(item, addr, g, ctx);
                            results.Add(new Recommendation
                            {
                                action = "WhereDoesThisGo",
                                text = $"Connect to {g.Describe(addr)}" + (required ? " (required)" : ""),
                                reasonCode = required ? "required_connection" : "valid_target",
                                objectRef = addr,
                                codexId = PortCodex(fromPort.type)
                            });
                        }
                    results.Sort((a, b) => (b.reasonCode == "required_connection").CompareTo(a.reasonCode == "required_connection"));
                    return results;
                }
            }

            var partDef = ctx.Data?.GetPart(item);

            // 2. Battery: its power path is battery -> deployment switch -> EPS BATT port.
            if (partDef != null && partDef.category == "battery")
                results.Add(new Recommendation
                {
                    action = "WhereDoesThisGo",
                    text = "Battery power goes to the EPS BATT port, through the deployment switch (battery -> switch -> EPS)",
                    reasonCode = "required_connection",
                    objectRef = "eps/BATT",
                    codexId = "eps"
                });

            // 3. A rocket part: every free mount where it can legally attach now.
            if (ctx.Graph != null && ctx.Graph.PartIdOf(item) != null)
            {
                foreach (var (lower, socket) in ctx.Graph.FreeSockets())
                    if (ctx.Graph.CanAttach(item, lower, socket, out _))
                        results.Add(new Recommendation
                        {
                            action = "WhereDoesThisGo",
                            text = $"Mount on {ctx.Graph.Rules.Name(ctx.Graph.PartIdOf(lower))} ({socket})",
                            reasonCode = "valid_target",
                            objectRef = $"{lower}/{socket}",
                            codexId = "staging"
                        });
                if (results.Count == 0 && ctx.Graph.FreeSockets().Any())
                {
                    var (l, s) = ctx.Graph.FreeSockets().First();
                    ctx.Graph.CanAttach(item, l, s, out var reason);
                    results.Add(new Recommendation { action = "WhereDoesThisGo", text = "Not yet: " + reason, reasonCode = "wrong_order", objectRef = item, codexId = "staging" });
                }
            }

            // 4. A CubeSat part: the mount it would go to (live bay if open).
            if (partDef != null && partDef.assembly == "cubesat" && partDef.mount != "frame")
            {
                string mount = ctx.FreeMountFor?.Invoke(partDef);
                results.Add(new Recommendation
                {
                    action = "WhereDoesThisGo",
                    text = mount != null ? $"Install the {partDef.name} in {MountName(mount)}"
                                         : $"Install the {partDef.name} on {DefaultMountName(partDef.mount)}",
                    reasonCode = "valid_mount",
                    objectRef = mount ?? partDef.mount,
                    codexId = partDef.codexId ?? "eps"
                });
            }
            return results;
        }

        static string MountName(string mount)
        {
            switch (mount)
            {
                case "TOP": return "the top face";
                case "BOTTOM": return "the bottom face";
                case "RAIL": return "the rail foot";
                case "PIN": return "the pin socket";
                default: return mount.StartsWith("U") ? "slot " + mount : "face " + mount;
            }
        }

        static string DefaultMountName(string mountType)
        {
            switch (mountType)
            {
                case "slot": return "a free board slot (U1-U3)";
                case "face_long": return "a long side face";
                case "face_end": return "an end face";
                case "inline": return "the rail foot";
                default: return mountType;
            }
        }

        // ------------------------------------------------------------------ BoltCoach

        /// <summary>Next bolt number (1-based), current pass, target torque and the star sequence.</summary>
        public static BoltCoachInfo BoltCoach(BoltJointState joint)
        {
            if (joint == null) return null;
            int next = joint.NextBolt();
            int pass = joint.CurrentPass();
            return new BoltCoachInfo
            {
                jointId = joint.def.id,
                nextBoltNumber = next >= 0 ? next + 1 : -1,
                currentPass = Math.Min(pass, 3),
                targetTorqueNm = joint.PassTargetNm(pass),
                starOrderText = BoltMath.StarOrderText(joint.Count)
            };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Is this connection one of the target device's required connections (by port type)?</summary>
        static bool IsRequiredConnection(string fromAddr, string toAddr, WiringGraph g, AdvisorContext ctx)
        {
            if (ctx.Rules == null) return false;
            foreach (var addr in new[] { fromAddr, toAddr })
            {
                var dev = g.Device(WiringGraph.DeviceOf(addr));
                var port = g.Port(addr);
                if (dev == null || port == null) continue;
                if (ctx.Rules.requiredConnections.Any(rc => rc.category == dev.category && rc.portTypes.Contains(port.type)))
                    return true;
            }
            return ctx.Rules.avionicsWiring.Any(l => (l.from == fromAddr && l.to == toAddr) || (l.from == toAddr && l.to == fromAddr));
        }

        /// <summary>Is the joint's pair of parts actually mounted (live graph, else the saved vehicle)?</summary>
        static bool JointMounted(BoltJointState joint, AdvisorContext ctx)
        {
            if (ctx.Graph != null)
                return ctx.Graph.PartIdOf(joint.def.lowerPartId) != null && ctx.Graph.IsAttached(joint.def.lowerPartId) &&
                       ctx.Graph.PartIdOf(joint.def.upperPartId) != null && ctx.Graph.IsAttached(joint.def.upperPartId);
            if (ctx.Vehicle != null && ctx.Vehicle.parts.Count > 0)
                return ctx.Vehicle.parts.Any(p => p.instanceId == joint.def.lowerPartId) && ctx.Vehicle.parts.Any(p => p.instanceId == joint.def.upperPartId);
            // No stack information: a joint that already has bolts in it was clearly assembled.
            return joint.HasAnyBolt();
        }

        static FaultSeverity MapSeverity(Severity s) =>
            s >= Severity.Fault ? FaultSeverity.Blocker : s == Severity.Warning ? FaultSeverity.Warning : FaultSeverity.Tip;

        static string PortCodex(string portType)
        {
            switch (portType)
            {
                case "DATA": return "data_budget";
                case "RF": return "downlink";
                case "PYRO_OUT": case "PYRO_IN": return "pyro_separation";
                case "ARM": return "safe_arm";
                default: return "eps";
            }
        }

        static string ConsequenceCodex(string consequence)
        {
            switch (consequence)
            {
                case "stuck_stage": case "fairing_stuck": case "no_ignition": case "s2_no_thrust": return "pyro_separation";
                case "guidance_drift": return "gravity_turn";
                case "no_telemetry": return "telemetry";
                default: return "staging";
            }
        }
    }
}
