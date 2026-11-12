using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TMinus.Core.Data
{
    /// <summary>rules.json: cables, required wiring, assembly order, bolt joints, pyro channels.</summary>
    public sealed class RulesDef
    {
        public List<CableTypeDef> cableTypes = new List<CableTypeDef>();
        public List<PortRuleDef> forbidden = new List<PortRuleDef>();
        public List<WarningRuleDef> warnings = new List<WarningRuleDef>();
        public List<RequiredConnectionDef> requiredConnections = new List<RequiredConnectionDef>();
        public List<InlineRuleDef> inlineRules = new List<InlineRuleDef>();
        /// <summary>"cubesat" / "pathfinder" -> ordered steps (part ids or categories).</summary>
        public Dictionary<string, List<string>> assemblyOrder = new Dictionary<string, List<string>>();
        public List<BoltJointDef> boltJoints = new List<BoltJointDef>();
        public List<PyroChannelDef> pyroChannels = new List<PyroChannelDef>();
        public SafeArmDef safeArm = new SafeArmDef();
        public List<AvionicsLinkDef> avionicsWiring = new List<AvionicsLinkDef>();
        public List<LaunchSiteDef> launchSites = new List<LaunchSiteDef>();

        [JsonIgnore] public JObject raw = new JObject();
        [JsonIgnore] public bool IsEmpty => raw == null || !raw.HasValues;

        public CableTypeDef GetCableType(string id) => cableTypes.Find(c => c.id == id);
        public BoltJointDef GetJoint(string id) => boltJoints.Find(j => j.id == id);
        public LaunchSiteDef GetLaunchSite(string id) => launchSites.Find(s => s.id == id);

        /// <summary>The cable type that may join these two port types (either direction), or null.</summary>
        public CableTypeDef CableFor(string fromType, string toType)
        {
            foreach (var c in cableTypes)
                foreach (var pair in c.allowed)
                    if ((pair.from == fromType && pair.to == toType) || (pair.from == toType && pair.to == fromType))
                        return c;
            return null;
        }
    }

    public sealed class PortPairDef
    {
        public string from;
        public string to;
    }

    public sealed class CableTypeDef
    {
        public string id;          // power, data, rf, pyro
        public string label;
        public string color;       // hex
        public string pattern;     // solid, dashed, ringed, striped (never colour alone)
        public double maxLengthM;
        public bool polarized;
        public List<PortPairDef> allowed = new List<PortPairDef>();
        public string codexId;
    }

    public sealed class PortRuleDef
    {
        public string code;
        public string from;
        public string to;
        public string message;
        public string severity;
        public string codexId;
    }

    public sealed class WarningRuleDef
    {
        public string code;
        public string message;
        public string severity;
        public string codexId;
    }

    /// <summary>Every part of this category needs these port types connected.</summary>
    public sealed class RequiredConnectionDef
    {
        public string category;
        public List<string> portTypes = new List<string>();
        /// <summary>At least this many of portTypes must be connected (0 = all of them).</summary>
        public int minConnected;
        public string message;
        public string codexId;
    }

    /// <summary>A part that must sit electrically between two others (deployment switch).</summary>
    public sealed class InlineRuleDef
    {
        public string partId;
        public string fromCategory;
        public string toCategory;
        public string message;
        public string codexId;
    }

    public sealed class BoltJointDef
    {
        public string id;
        public string lowerPartId;
        public string upperPartId;
        public int boltCount;
        public string size;
        public double torqueNm;
        public double tolerancePct;
        public double maxQkPa;
        public string codexId;
    }

    public sealed class PyroChannelDef
    {
        public int channel;
        public string function;    // s1_ignition, stage_separation, s2_ignition, fairing_separation
        public string targetPartId;
        public string failureIfUnwired;
    }

    public sealed class SafeArmDef
    {
        public string partId;
        public string vabState = "SAFE";
        public string padState = "ARMED";
        public string message;
    }

    /// <summary>A required avionics cable and what happens in flight without it.</summary>
    public sealed class AvionicsLinkDef
    {
        public string id;
        public string from;        // "partId/PORT_ID"
        public string to;
        public string cableType;
        public string consequence; // code read by FlightSim
        public string message;
    }

    public sealed class LaunchSiteDef
    {
        public string id;
        public string name;
        public double latitudeDeg;
        public double longitudeDeg;
        public string country;
    }
}
