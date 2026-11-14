using System;
using System.Collections.Generic;

namespace TMinus.Core
{
    public enum CrewRole { FlightDirector, PropulsionStructures, SpacecraftComms, ScienceBudget }

    public enum HoustonMode { On, HintsOnly, Off }

    [Serializable]
    public sealed class CrewMember
    {
        public string name;
        public CrewRole role;
    }

    /// <summary>A part placed in a design. mountId = slot, face or snap node it sits on.</summary>
    [Serializable]
    public sealed class PlacedPart
    {
        public string instanceId;
        public string partId;
        public string mountId;
    }

    /// <summary>A cable between two ports, addressed as "instanceId/PORT_NAME".</summary>
    [Serializable]
    public sealed class CableLink
    {
        public string fromPort;
        public string toPort;
        public string cableType;
        public bool reversed;
    }

    /// <summary>The CubeSat as built in the Integration Bay.</summary>
    [Serializable]
    public sealed class SpacecraftDesign
    {
        public List<PlacedPart> parts = new List<PlacedPart>();
        public List<CableLink> cables = new List<CableLink>();
        public bool rbfPinPulled;
        public double orbitAltitudeKm = 550;
    }

    /// <summary>Torque applied to one bolt of one joint.</summary>
    [Serializable]
    public sealed class BoltRecord
    {
        public string jointId;
        public int boltIndex;
        public double torqueNm;
        public int sequence;
    }

    /// <summary>How many times a joint was warped by tightening bolts out of sequence.</summary>
    [Serializable]
    public sealed class JointWarp
    {
        public string jointId;
        public int count;
    }

    /// <summary>The rocket as assembled in the VAB.</summary>
    [Serializable]
    public sealed class VehicleDesign
    {
        public string rocketId = "pathfinder";
        public List<PlacedPart> parts = new List<PlacedPart>();
        public List<BoltRecord> bolts = new List<BoltRecord>();
        public List<JointWarp> jointWarps = new List<JointWarp>();
        public List<CableLink> avionicsCables = new List<CableLink>();
        public bool armed;
        public double pitchKickDeg = 3;
        public string launchSiteId = "sonmiani";
    }

    /// <summary>What the crew knew and decided at launch (decision-quality score, debrief).</summary>
    [Serializable]
    public sealed class LaunchRecord
    {
        /// <summary>Checklist item ids that were FAIL when the countdown started.</summary>
        public List<string> knownFailures = new List<string>();
        /// <summary>Role -> "GO" / "NO-GO" in the final poll.</summary>
        public List<string> poll = new List<string>();
        public int noGoRounds;
        public bool launched;
        /// <summary>GO / NO-GO votes teammates sent from other computers (audit F25). A vote only counts for the design it was made for.</summary>
        public List<CrewVote> remoteVotes = new List<CrewVote>();
    }

    /// <summary>One telemetry sample kept for the debrief (1 Hz).</summary>
    [Serializable]
    public struct TelemetryPoint
    {
        public float t, altitudeKm, speedMs, qKPa;
    }

    /// <summary>Result of the last flight (Flight scene -> Operations / Debrief).</summary>
    [Serializable]
    public sealed class FlightRecord
    {
        public bool flown;
        public bool orbitAchieved;
        public bool deployed;
        public string outcome;        // Orbit / Failed / Aborted
        public string failureCode;    // joint_failure, stuck_stage, ignition_failure, impact, ...
        public string failureCause;   // cause id (cable link id or joint id)
        public double maxQKPa, maxQTimeS;
        public double apoapsisKm, periapsisKm;
        public double flightTimeS;
        /// <summary>True for a flight with no CubeSat to operate (Moonshot): the debrief judges the flight alone.</summary>
        public bool flightOnly;
        /// <summary>Propellant left in the stages that are still attached, as delta-v (m/s), when the flight ended.</summary>
        public double deltaVLeftMs;
        public List<TelemetryPoint> telemetry = new List<TelemetryPoint>();
    }

    [Serializable]
    public sealed class GameSettings
    {
        public HoustonMode houstonMode = HoustonMode.On;
        public string language = "en";
        /// <summary>Hints used per ladder level: [0]=text nudge, [1]=highlight, [2]=show me.</summary>
        public int[] hintsUsed = new int[3];
    }
}
