using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TMinus.Core.Data
{
    // Typed records for Resources/Data/*.json. Any JSON field not declared here is kept
    // in `extra`, so adding a field to the data never breaks loading.

    public abstract class DefBase
    {
        public string id;
        public string name;
        public string description;
        public string source;
        public bool illustrative;
        public bool needsReview;

        [JsonExtensionData] public IDictionary<string, JToken> extra = new Dictionary<string, JToken>();
    }

    // ---------------------------------------------------------------- parts.json

    /// <summary>A connector on a part. The matching child transform is named "PORT_" + id.</summary>
    public sealed class PortDef
    {
        public string id;       // e.g. "PWR_OUT_1"
        public string type;     // PWR_OUT, PWR_IN, BATT, SOLAR_OUT, SOLAR_IN, DATA, RF, PYRO_OUT, PYRO_IN, ARM
        public string label;
        public double maxPowerW; // outputs only
        public bool bus;         // accepts several cables (OBC data bus)
    }

    public sealed class PartDef : DefBase
    {
        /// <summary>frame, eps, battery, obc, radio, antenna, adcs, camera, solar, switch, pin,
        /// rocket_stage, interstage, avionics_ring, payload_adapter, fairing, avionics_device, saturn_stage</summary>
        public string category;
        /// <summary>cubesat, pathfinder, avionics or saturn_v.</summary>
        public string assembly;
        /// <summary>Where it goes: slot, face_long, face_end, inline, pin, frame, stack, ring.</summary>
        public string mount;
        public double massKg;
        public double costK;
        public double powerIdleW;
        public double powerActiveW;
        public double lengthM;
        public double diameterM;
        public string codexId;
        public bool inspectOnly;
        public List<PortDef> ports = new List<PortDef>();
        public List<string> requires = new List<string>();
        public Dictionary<string, double> specs = new Dictionary<string, double>();

        public double Spec(string key, double fallback = 0) =>
            specs != null && specs.TryGetValue(key, out var v) ? v : fallback;

        public PortDef GetPort(string portId) => ports?.Find(p => p.id == portId);

        public bool HasPortType(string type) => ports != null && ports.Exists(p => p.type == type);
    }

    // ---------------------------------------------------------------- rockets.json

    public sealed class StageDef
    {
        public string id;
        public string name;
        public string partId;
        public double dryMassKg;
        public double propMassKg;
        public int engineCount;
        public string engineName;
        /// <summary>Stage total thrust (all engines), kN.</summary>
        public double thrustSlKN;
        public double thrustVacKN;
        public double ispSl;
        public double ispVac;
        public double burnTimeS;
        public List<double> burnsS = new List<double>();
        public double diameterM;
        public double lengthM;
        public string propellants;
        /// <summary>Pyro channel that separates this stage from the next (0 = none).</summary>
        public int separationChannel;
        /// <summary>Pyro channel that ignites this stage (0 = ignited by the ground).</summary>
        public int ignitionChannel;
        public string source;
        public bool illustrative;
        public bool needsReview;
        public string notes;

        public double GrossMassKg => dryMassKg + propMassKg;
    }

    /// <summary>Non-propulsive mass: interstage, avionics, adapter, fairing, lumped masses.</summary>
    public sealed class RocketComponentDef
    {
        public string id;
        public string name;
        public string partId;
        public double massKg;
        public double lengthM;
        public double diameterM;
        /// <summary>Jettisoned at this altitude (fairing). 0 = not jettisoned by altitude.</summary>
        public double jettisonAltitudeKm;
        /// <summary>Dropped together with this stage id. Empty = rides to orbit.</summary>
        public string dropWithStage;
        public string source;
        public bool illustrative;
    }

    /// <summary>
    /// Autopilot profile for one rocket (audit: Moonshot). The defaults are the profile Pathfinder was tuned with. A heavy rocket whose upper
    /// stage cannot lift its own weight (Saturn V's S-II, thrust-to-weight below 1) needs altitudeHold: tilt the thrust up just enough to cancel sinking.
    /// </summary>
    public sealed class GuidanceDef
    {
        public double turnSpanM = 44000;     // the gravity turn is spread over this much height above 1 km
        public double turnEndM = 45000;      // above this the thrust follows the horizon (or altitude hold takes over)
        public double turnDeg = 60;          // how far the turn tips the rocket from vertical, on top of the pitch kick
        public bool altitudeHold;
        public double maxClimb = 0.8;        // largest share of thrust the altitude hold may point upward
        public double vrGain = 0.02;         // damping on the climb rate
    }

    public sealed class RocketDef : DefBase
    {
        public bool real;
        public bool buildable;
        public double heightM;
        public double launchCostMUSD;
        public double payloadLeoKg;
        public double payloadTliKg;
        public double payloadTargetAltKm;
        /// <summary>Payload used for the liftoff-mass figure on library cards.</summary>
        public double referencePayloadKg;
        public double liftoffMassMinKg;
        public double liftoffMassMaxKg;
        public List<StageDef> stages = new List<StageDef>();
        public List<RocketComponentDef> components = new List<RocketComponentDef>();
        /// <summary>How the autopilot flies this rocket. Null = the original Pathfinder profile.</summary>
        public GuidanceDef guidance;
        /// <summary>Assembly parts (parts.json ids) in stack order, bottom first.</summary>
        public List<string> partIds = new List<string>();

        public double DryAndPropMassKg
        {
            get
            {
                double m = 0;
                foreach (var s in stages) m += s.GrossMassKg;
                foreach (var c in components) m += c.massKg;
                return m;
            }
        }

        public double LiftoffMassKg(double payloadKg) => DryAndPropMassKg + payloadKg;

        public StageDef GetStage(string stageId) => stages.Find(s => s.id == stageId);
    }

    // ---------------------------------------------------------------- missions.json

    public sealed class SuccessCriterion
    {
        public string id;
        public string text;
        public string metric;
        public double threshold;
    }

    public sealed class RandomEventDef
    {
        public string id;
        public int day;
        public string type;     // solar_storm, ground_outage, battery_ageing
        public string effect;
        public double value;
        public string codexId;
    }

    public sealed class RewardImageDef
    {
        public string date;
        public string title;
        public string resource; // Resources path, filled in when the images are added
        public string sourceUrl;
    }

    public sealed class ScoringWeights
    {
        public double success = 40, science = 25, budget = 10, margins = 10, decisionQuality = 10, collaboration = 5;
    }

    public sealed class MissionDef : DefBase
    {
        public string priority;     // P0 / P1
        public string difficulty;
        public string objective;
        public bool locked;
        public bool startHere;
        public List<string> rocketOptions = new List<string>();
        public string payloadType;  // cubesat_3u, apollo, sounding
        public double budgetMUSD;   // 0 = no budget (sandbox)
        public double massLimitKg;
        public List<double> orbitOptionsKm = new List<double>();
        public double defaultOrbitKm;
        public List<string> launchSites = new List<string>();
        public int durationDays;
        public int groundContactsPerDay;
        public double contactMinutes;
        public int imagingPassesPerDay;
        public int imagesPerPass;
        public int seed;
        public double targetApogeeKm;
        public double parkingOrbitKm;
        public double requiredDeltaVLeftMs;
        public List<SuccessCriterion> successCriteria = new List<SuccessCriterion>();
        public List<RandomEventDef> randomEvents = new List<RandomEventDef>();
        public ScoringWeights scoring = new ScoringWeights();
        public List<RewardImageDef> rewardImages = new List<RewardImageDef>();
        public string rewardCredit;
        public string storyCard;
        /// <summary>Optional: who sponsored this mission pack. Shown on the mission card (audit F19). Data only.</summary>
        public string sponsor;
        public List<string> codexIds = new List<string>();

        // Failure Lab jobs (Prompt 15): new jobs need data only, no code.
        public string clientNote;                                       // short note on the job card
        public int rewardPoints;
        public List<string> seededFaults = new List<string>();          // "bolts_missing:<jointId>:<n>", "unplugged:<linkId>", "wrong_port:<linkId>:<port>"
        public List<JobChoiceDef> consoleChoices = new List<JobChoiceDef>(); // scripted console jobs (Apollo 12)
        public string lesson;                                           // the real-world lesson in the job debrief
    }

    public sealed class JobChoiceDef
    {
        public string id;
        public string label;
        public int correctAtStep;   // 1 or 2 = the right call at that step; 0 = never right
        public string why;          // shown when picked (right or wrong)
    }

    // ---------------------------------------------------------------- codex.json

    public sealed class CodexEntry : DefBase
    {
        public string title;
        [JsonProperty("short")] public string shortText;
        [JsonProperty("long")] public string longText;
        public string sourceUrl;
    }
}
