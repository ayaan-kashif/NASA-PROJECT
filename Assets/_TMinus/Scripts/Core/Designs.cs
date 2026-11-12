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

    [Serializable]
    public sealed class GameSettings
    {
        public HoustonMode houstonMode = HoustonMode.On;
        public string language = "en";
        /// <summary>Hints used per ladder level: [0]=text nudge, [1]=highlight, [2]=show me.</summary>
        public int[] hintsUsed = new int[3];
    }
}
