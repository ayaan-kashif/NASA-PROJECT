using System;
using System.Collections.Generic;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;

namespace TMinus.Houston
{
    public enum HintLevel { TextNudge = 0, Highlight = 1, ShowMe = 2 }
    public enum FaultSeverity { Tip, Warning, Blocker }

    /// <summary>Which part of the build HOUSTON looks at, from the active scene.</summary>
    public enum AdvisorScope { CubeSat, Rocket, Avionics, Pad }

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
        /// <summary>Live Integration Bay: the mount a part would actually go to (null when not in the bay).</summary>
        public Func<PartDef, string> FreeMountFor;

        /// <summary>Scene decides the scope. No scene (tests, flight, debrief) = full pre-launch check.</summary>
        public AdvisorScope Scope
        {
            get
            {
                switch (SceneName)
                {
                    case SceneNames.IntegrationBay: return AdvisorScope.CubeSat;
                    case SceneNames.VAB: return AdvisorScope.Rocket;
                    case SceneNames.AvionicsBay: return AdvisorScope.Avionics;
                    default: return AdvisorScope.Pad;
                }
            }
        }
    }
}
