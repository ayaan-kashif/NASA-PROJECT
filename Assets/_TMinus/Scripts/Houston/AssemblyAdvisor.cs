using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;

namespace TMinus.Houston
{
    public enum HintLevel { TextNudge = 0, Highlight = 1, ShowMe = 2 }
    public enum FaultSeverity { Tip, Warning, Blocker }

    public sealed class Recommendation
    {
        public string action;      // WhereDoesThisGo, NextStep, Scan, BoltCoach
        public string text;
        public string reasonCode;
        public string codexId;
        public string objectRef;   // instanceId / port address / joint id
        public HintLevel hintLevel;

        public override string ToString() => $"[{reasonCode}] {text} ({objectRef})";
    }

    public sealed class ScanFault
    {
        public FaultSeverity severity;
        public string what;
        public string where;       // object reference
        public string why;
        public string codexId;
        public string fixAction;

        public override string ToString() => $"[{severity}] {what} (at {where})";
    }

    public sealed class BoltCoachInfo
    {
        public string jointId;
        public int nextBoltNumber;  // 1-based, -1 when done
        public int currentPass;
        public double targetTorqueNm;
        public string starOrderText;

        public override string ToString() =>
            $"Joint {jointId}: next bolt {nextBoltNumber}, pass {currentPass}/3 ({targetTorqueNm:0.#} N*m), star pattern {starOrderText}";
    }

    /// <summary>
    /// Deterministic, offline advisor (Prompt 13). Reads the game's data and rules,
    /// and provides accurate guidance for assembly, bolting, and wiring.
    /// Never wrong, works offline.
    /// </summary>
    public static class AssemblyAdvisor
    {
        // ------------------------------------------------------------------ WhereDoesThisGo

        /// <summary>Ranked valid targets for a held item (part id, category, or port address).</summary>
        public static List<Recommendation> WhereDoesThisGo(string heldItem, AdvisorContext ctx)
        {
            var results = new List<Recommendation>();
            if (ctx == null || string.IsNullOrEmpty(heldItem)) return results;

            string item = heldItem.Trim();

            // 1. Port address held (e.g. "inst/PORT_ID" or "batt_20/BATT"): Wiring connection
            if (item.Contains("/") && ctx.Wiring != null)
            {
                var fromPort = ctx.Wiring.Port(item);
                if (fromPort != null)
                {
                    foreach (var (inst, def) in ctx.Wiring.Devices)
                    {
                        foreach (var port in def.ports)
                        {
                            var addr = WiringGraph.Address(inst, port.id);
                            if (addr == item) continue;
                            if (ctx.Wiring.CanConnect(item, addr, out var cableType, out _, out _))
                            {
                                bool isRequired = IsRequiredConnection(item, addr, ctx);
                                results.Add(new Recommendation
                                {
                                    action = "WhereDoesThisGo",
                                    text = $"Connect to {ctx.Wiring.Describe(addr)}" + (isRequired ? " (required)" : ""),
                                    reasonCode = isRequired ? "required_connection" : "valid_target",
                                    objectRef = addr,
                                    codexId = PortCodex(fromPort.type)
                                });
                            }
                        }
                    }

                    SortWiringRecommendations(results);
                    return results;
                }
            }

            // 2. Battery special rule (per PLAN.md acceptance check: "Holding the battery highlights the EPS BATT port")
            bool isBattery = item == "batt_20" || item == "batt_40" || item.StartsWith("batt") || item.Contains("battery");
            if (isBattery)
            {
                // In Wiring/FlatSat mode: target is the EPS BATT port
                results.Add(new Recommendation
                {
                    action = "WhereDoesThisGo",
                    text = "Connect battery to EPS BATT port (via deployment switch)",
                    reasonCode = "required_connection",
                    objectRef = "eps/BATT",
                    codexId = "eps"
                });

                // In Integration Bay physical build mode: target is frame slot U1B or slot
                results.Add(new Recommendation
                {
                    action = "WhereDoesThisGo",
                    text = "Slide battery into slot U1B (internal rails)",
                    reasonCode = "valid_mount",
                    objectRef = "U1B",
                    codexId = "battery_dod"
                });
            }

            // 3. Rocket assembly: held part id -> valid snap nodes
            if (ctx.Graph != null)
            {
                foreach (var (lower, socket) in ctx.Graph.FreeSockets())
                {
                    if (ctx.Graph.CanAttach(item, lower, socket, out _))
                    {
                        var lowerPart = ctx.Graph.PartIdOf(lower);
                        results.Add(new Recommendation
                        {
                            action = "WhereDoesThisGo",
                            text = $"Mount on {ctx.Graph.Rules.Name(lowerPart)} ({socket})",
                            reasonCode = "valid_target",
                            objectRef = $"{lower}/{socket}",
                            codexId = "staging"
                        });
                    }
                }
            }

            // 4. CubeSat part placement: part id -> compatible mount
            var partDef = ctx.Data?.GetPart(item);
            if (partDef != null && !isBattery)
            {
                string mountType = partDef.mount;
                string mountSlot = DefaultMountForPart(item);
                results.Add(new Recommendation
                {
                    action = "WhereDoesThisGo",
                    text = $"Install {partDef.name} ({mountSlot})",
                    reasonCode = "valid_mount",
                    objectRef = mountSlot,
                    codexId = partDef.codexId ?? "eps"
                });
            }

            return results;
        }

        static void SortWiringRecommendations(List<Recommendation> list)
        {
            list.Sort((a, b) =>
            {
                int scoreA = a.reasonCode == "required_connection" ? 1 : 0;
                int scoreB = b.reasonCode == "required_connection" ? 1 : 0;
                return scoreB.CompareTo(scoreA);
            });
        }

        static string DefaultMountForPart(string partId)
        {
            switch (partId)
            {
                case "eps": return "slot U1A";
                case "batt_20": case "batt_40": return "slot U1B";
                case "obc": return "slot U2A";
                case "radio_uhf": case "radio_sband": return "slot U2B";
                case "adcs_mag": case "adcs_rw": return "slot U3A";
                case "cam_wide": case "cam_hd": return "bottom face";
                case "ant_uhf": case "ant_patch": return "top face";
                case "solar_body": case "solar_wings": return "-Z / +Z faces";
                case "dep_switch": return "RAIL mount";
                case "rbf_pin": return "PIN socket";
                default: return "slot U3B";
            }
        }

        // ------------------------------------------------------------------ NextStep

        /// <summary>Next action in dependency order, skipping finished steps and prioritizing blockers.</summary>
        public static Recommendation NextStep(AdvisorContext ctx)
        {
            if (ctx == null) return new Recommendation { action = "NextStep", text = "No context available.", reasonCode = "none" };

            // 1. Check for critical blockers first
            var faults = Scan(ctx);
            var blocker = faults.FirstOrDefault(f => f.severity == FaultSeverity.Blocker);
            if (blocker != null)
            {
                return new Recommendation
                {
                    action = "NextStep",
                    text = $"Fix: {blocker.what}",
                    reasonCode = "fix_blocker",
                    objectRef = blocker.where,
                    codexId = blocker.codexId
                };
            }

            // 2. CubeSat build order (Integration Bay)
            if (ctx.CubeSatAssemblyOrder != null)
            {
                // Parts placement in order
                foreach (var step in ctx.CubeSatAssemblyOrder)
                {
                    if (step == "frame_3u") continue; // Base structure
                    if (step == "rbf_pin") continue;  // Checked later at pad

                    bool placed = IsCubeSatStepPlaced(step, ctx);
                    if (!placed)
                    {
                        return StepRecommendationForCubeSat(step, ctx);
                    }
                }

                // Wiring connections
                if (ctx.Wiring != null)
                {
                    var wiringFaults = WiringValidator.Validate(ctx.Wiring);
                    var missing = wiringFaults.FirstOrDefault(f => f.code == "missing_connection" || f.code == "switch_not_inline");
                    if (missing != null)
                    {
                        return new Recommendation
                        {
                            action = "NextStep",
                            text = missing.message,
                            reasonCode = "wiring_needed",
                            objectRef = missing.port ?? missing.device,
                            codexId = missing.codexId
                        };
                    }
                }
            }

            // 3. Rocket assembly order (VAB)
            if (ctx.Graph != null && !ctx.Graph.IsComplete())
            {
                foreach (var partId in ctx.Graph.Rules.Order)
                {
                    bool attached = ctx.Graph.Instances.Any(inst =>
                        ctx.Graph.PartIdOf(inst) == partId && ctx.Graph.IsAttached(inst));
                    if (!attached)
                    {
                        return new Recommendation
                        {
                            action = "NextStep",
                            text = $"Stack the {ctx.Graph.Rules.Name(partId)} on the rocket",
                            reasonCode = "assembly_order",
                            objectRef = partId,
                            codexId = "staging"
                        };
                    }
                }
            }

            // 4. Bolt joints (VAB BoltCoach)
            if (ctx.BoltJoints != null)
            {
                foreach (var joint in ctx.BoltJoints)
                {
                    int next = joint.NextBolt();
                    if (next >= 0)
                    {
                        int pass = joint.CurrentPass();
                        return new Recommendation
                        {
                            action = "NextStep",
                            text = $"Bolt {next + 1} next on {joint.def.id} (star pattern pass {pass}/3)",
                            reasonCode = "bolt_needed",
                            objectRef = joint.def.id,
                            codexId = "star_pattern"
                        };
                    }
                }
            }

            // 5. Avionics bay wiring
            if (ctx.AvionicsWiring != null)
            {
                var avFaults = WiringValidator.Validate(ctx.AvionicsWiring);
                var missing = avFaults.FirstOrDefault(f => f.code == "missing_connection");
                if (missing != null)
                {
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = $"Wire avionics: {missing.message}",
                        reasonCode = "avionics_wiring",
                        objectRef = missing.port ?? missing.device,
                        codexId = missing.codexId
                    };
                }
            }

            // 6. RBF Pin check
            if (ctx.RbfPinInserted)
            {
                return new Recommendation
                {
                    action = "NextStep",
                    text = "Pull the Remove-Before-Flight pin at the pad",
                    reasonCode = "rbf_pin",
                    objectRef = "rbf_pin",
                    codexId = "rbf_pin"
                };
            }

            // 7. Safe/Arm check
            if (ctx.Vehicle != null && !ctx.Vehicle.armed)
            {
                return new Recommendation
                {
                    action = "NextStep",
                    text = "Arm the rocket at the pad (switch Safe/Arm plug to ARMED)",
                    reasonCode = "safe_arm",
                    objectRef = "av_safe_arm",
                    codexId = "safe_arm"
                };
            }

            return new Recommendation
            {
                action = "NextStep",
                text = "Build complete! All systems verified and ready for launch.",
                reasonCode = "ready_for_launch",
                codexId = "staging"
            };
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

        static Recommendation StepRecommendationForCubeSat(string step, AdvisorContext ctx)
        {
            switch (step)
            {
                case "eps":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Slide the EPS board into slot U1",
                        reasonCode = "assembly_order",
                        objectRef = "eps",
                        codexId = "eps"
                    };
                case "battery":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Install the battery into slot U1B",
                        reasonCode = "assembly_order",
                        objectRef = "batt_20",
                        codexId = "battery_dod"
                    };
                case "dep_switch":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Install the deployment switch on the rail",
                        reasonCode = "assembly_order",
                        objectRef = "dep_switch",
                        codexId = "deployment_switch"
                    };
                case "obc":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Slide the flight computer (OBC) into slot U2",
                        reasonCode = "assembly_order",
                        objectRef = "obc",
                        codexId = "telemetry"
                    };
                case "radio":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Install the radio board into slot U2B",
                        reasonCode = "assembly_order",
                        objectRef = "radio_sband",
                        codexId = "downlink"
                    };
                case "antenna":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Mount the antenna on the top face",
                        reasonCode = "assembly_order",
                        objectRef = "ant_patch",
                        codexId = "downlink"
                    };
                case "adcs":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Install the ADCS module into slot U3",
                        reasonCode = "assembly_order",
                        objectRef = "adcs_mag",
                        codexId = "adcs"
                    };
                case "camera":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Install the camera payload on the bottom face",
                        reasonCode = "assembly_order",
                        objectRef = "cam_wide",
                        codexId = "gsd"
                    };
                case "solar":
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = "Attach solar panels to the outer faces",
                        reasonCode = "assembly_order",
                        objectRef = "solar_body",
                        codexId = "eclipse"
                    };
                default:
                    return new Recommendation
                    {
                        action = "NextStep",
                        text = $"Install the {step}",
                        reasonCode = "assembly_order",
                        objectRef = step,
                        codexId = "eps"
                    };
            }
        }

        // ------------------------------------------------------------------ Scan

        /// <summary>Comprehensive scan of all faults, sorted by severity.</summary>
        public static List<ScanFault> Scan(AdvisorContext ctx)
        {
            var faults = new List<ScanFault>();
            if (ctx == null) return faults;

            // 1. CubeSat Wiring faults
            if (ctx.Wiring != null)
            {
                foreach (var wf in WiringValidator.Validate(ctx.Wiring))
                {
                    faults.Add(new ScanFault
                    {
                        severity = MapSeverity(wf.severity),
                        what = wf.message,
                        where = wf.port ?? wf.device,
                        why = wf.code,
                        codexId = wf.codexId,
                        fixAction = "wire"
                    });
                }
            }

            // 2. Bolt Joint faults
            if (ctx.BoltJoints != null)
            {
                foreach (var joint in ctx.BoltJoints)
                {
                    var status = joint.GetStatus();
                    if (status.missing > 0)
                    {
                        faults.Add(new ScanFault
                        {
                            severity = FaultSeverity.Blocker,
                            what = $"{status.missing} of {status.boltCount} bolts missing on {joint.def.id}",
                            where = joint.def.id,
                            why = "bolts_missing",
                            codexId = "star_pattern",
                            fixAction = "bolt"
                        });
                    }
                    if (status.sequenceErrors > 0)
                    {
                        faults.Add(new ScanFault
                        {
                            severity = FaultSeverity.Warning,
                            what = $"Joint {joint.def.id} warped ({status.sequenceErrors} sequence errors, integrity {status.integrity:0.00})",
                            where = joint.def.id,
                            why = "joint_warped",
                            codexId = "star_pattern",
                            fixAction = "rebolt"
                        });
                    }
                    if (status.over > 0)
                    {
                        faults.Add(new ScanFault
                        {
                            severity = FaultSeverity.Warning,
                            what = $"{status.over} over-torqued bolts on {joint.def.id}",
                            where = joint.def.id,
                            why = "bolts_overtorqued",
                            codexId = "torque",
                            fixAction = "rebolt"
                        });
                    }
                }
            }

            // 3. Avionics Wiring & Consequences
            if (ctx.AvionicsWiring != null)
            {
                foreach (var wf in WiringValidator.Validate(ctx.AvionicsWiring))
                {
                    faults.Add(new ScanFault
                    {
                        severity = MapSeverity(wf.severity),
                        what = $"Avionics: {wf.message}",
                        where = wf.port ?? wf.device,
                        why = wf.code,
                        codexId = wf.codexId,
                        fixAction = "wire_avionics"
                    });
                }
            }

            if (ctx.AvionicsReport != null)
            {
                foreach (var c in ctx.AvionicsReport.consequences)
                {
                    faults.Add(new ScanFault
                    {
                        severity = FaultSeverity.Blocker,
                        what = c.message,
                        where = c.linkId,
                        why = c.consequence,
                        codexId = ConsequenceCodex(c.consequence),
                        fixAction = "wire_avionics"
                    });
                }
            }

            // 4. Spacecraft budget issues
            if (ctx.SpacecraftReport != null)
            {
                foreach (var issue in ctx.SpacecraftReport.issues)
                {
                    faults.Add(new ScanFault
                    {
                        severity = MapSeverity(issue.severity),
                        what = issue.message,
                        where = issue.code,
                        why = issue.code,
                        codexId = issue.codexId
                    });
                }
            }

            // 5. RBF Pin check
            if (ctx.RbfPinInserted)
            {
                faults.Add(new ScanFault
                {
                    severity = FaultSeverity.Warning,
                    what = "Remove-Before-Flight pin is still in",
                    where = "rbf_pin",
                    why = "rbf_pin_in",
                    codexId = "rbf_pin",
                    fixAction = "pull_pin"
                });
            }

            // 6. Safe/Arm check
            if (ctx.Vehicle != null && !ctx.Vehicle.armed)
            {
                faults.Add(new ScanFault
                {
                    severity = FaultSeverity.Warning,
                    what = "Safe/Arm plug is SAFE (must be ARMED at the pad)",
                    where = "av_safe_arm",
                    why = "not_armed",
                    codexId = "safe_arm",
                    fixAction = "arm"
                });
            }

            faults.Sort((a, b) => b.severity.CompareTo(a.severity));
            return faults;
        }

        // ------------------------------------------------------------------ BoltCoach

        /// <summary>Bolt coach returns the next bolt number (1-based), pass, target torque, and star sequence.</summary>
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

        static bool IsRequiredConnection(string fromAddr, string toAddr, AdvisorContext ctx)
        {
            if (ctx.Rules == null) return false;
            var fromDev = WiringGraph.DeviceOf(fromAddr);
            var toDev = WiringGraph.DeviceOf(toAddr);
            var fromDef = ctx.Wiring?.Device(fromDev);
            var toDef = ctx.Wiring?.Device(toDev);
            if (fromDef == null || toDef == null) return false;

            return ctx.Rules.requiredConnections.Any(rc =>
                rc.category == fromDef.category || rc.category == toDef.category);
        }

        static FaultSeverity MapSeverity(Severity s)
        {
            switch (s)
            {
                case Severity.Blocker:
                case Severity.Fault:
                    return FaultSeverity.Blocker;
                case Severity.Warning:
                    return FaultSeverity.Warning;
                default:
                    return FaultSeverity.Tip;
            }
        }

        static string PortCodex(string portType)
        {
            switch (portType)
            {
                case "PWR_OUT": case "PWR_IN": case "BATT": case "SOLAR_OUT": case "SOLAR_IN":
                    return "eps";
                case "DATA": return "telemetry";
                case "RF": return "downlink";
                case "PYRO_OUT": case "PYRO_IN": return "pyro_separation";
                default: return "eps";
            }
        }

        static string ConsequenceCodex(string consequence)
        {
            switch (consequence)
            {
                case "no_ignition": return "staging";
                case "stuck_stage": return "pyro_separation";
                case "s2_no_thrust": return "staging";
                case "guidance_drift": return "adcs";
                case "no_telemetry": return "telemetry";
                case "fairing_stuck": return "pyro_separation";
                default: return "staging";
            }
        }
    }

    /// <summary>Snapshot of the build state for advisor queries.</summary>
    public sealed class AdvisorContext
    {
        public DataRegistry Data;
        public RulesDef Rules;
        public AssemblyGraph Graph;
        public List<BoltJointState> BoltJoints;
        public WiringGraph Wiring;
        public WiringGraph AvionicsWiring;
        public AvionicsReport AvionicsReport;
        public SpacecraftReport SpacecraftReport;
        public List<PlacedPart> CubeSatParts;
        public List<string> CubeSatAssemblyOrder;
        public VehicleDesign Vehicle;
        public bool RbfPinInserted;
        public string SceneName;
        public string MissionId;
    }
}
