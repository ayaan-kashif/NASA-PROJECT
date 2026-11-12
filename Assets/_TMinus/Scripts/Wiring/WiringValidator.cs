using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Wiring
{
    public sealed class WiringFault
    {
        public string code;
        public Severity severity;
        public string message;
        public string device;   // instance id
        public string port;     // address, if one port is to blame
        public string cableId;
        public string codexId;
        public override string ToString() => $"[{severity}] {code}: {message}";
    }

    /// <summary>
    /// Checks a WiringGraph against rules.json: short circuits, reversed polarity, rail overload,
    /// required connections per device, battery bypassing the EPS, and the deployment switch in line.
    /// </summary>
    public static class WiringValidator
    {
        public static List<WiringFault> Validate(WiringGraph g)
        {
            var faults = new List<WiringFault>();
            var rules = g.Rules;

            foreach (var c in g.Cables)
            {
                if (c.shortCircuit)
                    faults.Add(new WiringFault
                    {
                        code = "short_circuit", severity = Severity.Blocker, cableId = c.id, port = c.a, device = WiringGraph.DeviceOf(c.a), codexId = "eps",
                        message = $"Short circuit: {g.Describe(c.a)} is wired to {g.Describe(c.b)}. Two outputs fight each other."
                    });
                else if (c.reversed)
                    faults.Add(new WiringFault
                    {
                        code = "reversed_polarity", severity = Severity.Blocker, cableId = c.id, port = c.b, device = WiringGraph.DeviceOf(c.b), codexId = "eps",
                        message = $"Reversed polarity between {g.Describe(c.a)} and {g.Describe(c.b)}: red and black are swapped."
                    });
            }

            CheckRailLoads(g, faults);
            CheckRequired(g, rules, faults);
            CheckBatteryPath(g, rules, faults);

            return faults.OrderByDescending(f => f.severity).ToList();
        }

        static void CheckRailLoads(WiringGraph g, List<WiringFault> faults)
        {
            foreach (var (inst, def) in g.Devices.Select(kv => (kv.Key, kv.Value)))
            {
                foreach (var port in def.ports.Where(p => p.type == "PWR_OUT" && p.maxPowerW > 0))
                {
                    var addr = WiringGraph.Address(inst, port.id);
                    double load = 0;
                    var loads = new List<string>();
                    foreach (var peer in g.PeersOf(addr))
                    {
                        var p = g.Port(peer);
                        if (p == null || p.type != "PWR_IN") continue;
                        var d = g.Device(WiringGraph.DeviceOf(peer));
                        double w = System.Math.Max(d.powerIdleW, d.powerActiveW);
                        load += w;
                        loads.Add($"{d.name} {w:0.#} W");
                    }
                    if (load > port.maxPowerW + 1e-9)
                        faults.Add(new WiringFault
                        {
                            code = "rail_overload", severity = Severity.Fault, port = addr, device = inst, codexId = "eps",
                            message = $"{def.name} {port.label} is rated {port.maxPowerW:0} W but feeds {load:0.#} W ({string.Join(", ", loads)}). Spread the loads over more outputs."
                        });
                }
            }
        }

        static void CheckRequired(WiringGraph g, RulesDef rules, List<WiringFault> faults)
        {
            foreach (var (inst, def) in g.Devices.Select(kv => (kv.Key, kv.Value)))
            {
                foreach (var rc in rules.requiredConnections.Where(r => r.category == def.category))
                {
                    var missing = new List<string>();
                    int connected = 0;
                    foreach (var type in rc.portTypes)
                    {
                        bool any = def.ports.Any(p => p.type == type && g.IsConnected(WiringGraph.Address(inst, p.id)));
                        if (any) connected++; else missing.Add(Friendly(type));
                    }
                    bool ok = rc.minConnected > 0 ? connected >= rc.minConnected : missing.Count == 0;
                    if (ok) continue;
                    var firstMissingPort = def.ports.FirstOrDefault(p => rc.portTypes.Contains(p.type) && !g.IsConnected(WiringGraph.Address(inst, p.id)));
                    faults.Add(new WiringFault
                    {
                        code = "missing_connection", severity = Severity.Blocker, device = inst, codexId = rc.codexId,
                        port = firstMissingPort != null ? WiringGraph.Address(inst, firstMissingPort.id) : null,
                        message = $"{def.name}: {rc.message} Not connected: {string.Join(", ", missing)}."
                    });
                }
            }
        }

        static void CheckBatteryPath(WiringGraph g, RulesDef rules, List<WiringFault> faults)
        {
            var devices = g.Devices.ToList();
            foreach (var (inst, def) in devices.Where(kv => kv.Value.category == "battery").Select(kv => (kv.Key, kv.Value)))
            {
                foreach (var port in def.ports.Where(p => p.type == "BATT"))
                {
                    var addr = WiringGraph.Address(inst, port.id);
                    foreach (var peer in g.PeersOf(addr))
                    {
                        var peerDev = g.Device(WiringGraph.DeviceOf(peer));
                        var peerPort = g.Port(peer);
                        if (peerPort?.type == "PWR_IN")
                        {
                            var w = rules.warnings.FirstOrDefault(x => x.code == "battery_bypasses_eps");
                            faults.Add(new WiringFault
                            {
                                code = "battery_bypasses_eps", severity = Severity.Warning, device = inst, port = peer, codexId = "eps",
                                message = w?.message ?? $"Battery wired straight to {peerDev?.name}: it bypasses the EPS."
                            });
                        }
                        else if (peerDev?.category == "eps")
                        {
                            var inline = rules.inlineRules.FirstOrDefault(r => r.fromCategory == "battery" && r.toCategory == "eps");
                            faults.Add(new WiringFault
                            {
                                code = "switch_not_inline", severity = Severity.Blocker, device = inst, port = peer, codexId = inline?.codexId ?? "deployment_switch",
                                message = (inline?.message ?? "The deployment switch must sit between the battery and the EPS.") + " Right now the battery goes straight into the EPS, so the satellite would switch on inside the dispenser."
                            });
                        }
                    }
                }
            }

            foreach (var inline in rules.inlineRules)
            {
                bool hasFrom = devices.Any(d => d.Value.category == inline.fromCategory);
                bool hasTo = devices.Any(d => d.Value.category == inline.toCategory);
                if (!hasFrom || !hasTo) continue;
                var sw = devices.FirstOrDefault(d => d.Value.id == inline.partId);
                if (sw.Key == null)
                {
                    faults.Add(new WiringFault { code = "switch_missing", severity = Severity.Blocker, codexId = inline.codexId, message = "No deployment switch fitted. " + inline.message });
                    continue;
                }
                var sides = sw.Value.ports.Select(p => g.PeersOf(WiringGraph.Address(sw.Key, p.id)).Select(x => g.Device(WiringGraph.DeviceOf(x))?.category).ToList()).ToList();
                bool toFrom = sides.Any(s => s.Contains(inline.fromCategory));
                bool toTo = sides.Any(s => s.Contains(inline.toCategory));
                bool alreadyReported = faults.Any(f => f.code == "switch_not_inline");
                if ((!toFrom || !toTo) && !alreadyReported)
                    faults.Add(new WiringFault
                    {
                        code = "switch_not_inline", severity = Severity.Blocker, device = sw.Key, codexId = inline.codexId,
                        message = $"{sw.Value.name}: {inline.message} It needs one cable to the battery and one to the EPS."
                    });
            }
        }

        public static string Friendly(string portType)
        {
            switch (portType)
            {
                case "PWR_IN": return "power";
                case "DATA": return "data";
                case "RF": return "antenna (RF)";
                case "SOLAR_IN": return "solar panel";
                case "BATT": return "battery";
                case "PYRO_IN": return "pyro";
                default: return portType.ToLowerInvariant();
            }
        }
    }
}
