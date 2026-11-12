using System.Collections.Generic;
using TMinus.Core;

namespace TMinus.Assembly
{
    /// <summary>
    /// Stacking rules for one buildable rocket, taken from rules.json (assemblyOrder, boltJoints)
    /// and parts.json (names, categories, diameters). Pure C#, so tests can build it by hand.
    /// </summary>
    public sealed class AssemblyRules
    {
        public const string Top = "top", Payload = "payload", FairingLeft = "fairing_left", FairingRight = "fairing_right";
        public const string PayloadPartId = "payload";

        public sealed class PartInfo
        {
            public string name;
            public string category;
            public double diameterM;
        }

        /// <summary>Assembly order, bottom first (part ids; "payload" for the CubeSat).</summary>
        public readonly List<string> Order;
        public readonly Dictionary<string, PartInfo> Parts;
        /// <summary>(lowerPartId, upperPartId) -> joint id.</summary>
        public readonly Dictionary<(string, string), string> Joints;
        public double DiameterTolerance = 0.05;

        public AssemblyRules(List<string> order, Dictionary<string, PartInfo> parts,
                             Dictionary<(string, string), string> joints)
        {
            Order = order;
            Parts = parts;
            Joints = joints ?? new Dictionary<(string, string), string>();
        }

        public static AssemblyRules FromData(DataRegistry data, string rocketId)
        {
            var rules = data.GetRules();
            var order = rules.assemblyOrder.TryGetValue(rocketId, out var o) ? new List<string>(o) : new List<string>();
            var parts = new Dictionary<string, PartInfo>();
            foreach (var id in order)
            {
                var def = data.GetPart(id);
                parts[id] = def != null
                    ? new PartInfo { name = def.name, category = def.category, diameterM = def.diameterM }
                    : new PartInfo { name = id == PayloadPartId ? "CubeSat payload" : id, category = id };
            }
            var joints = new Dictionary<(string, string), string>();
            foreach (var j in rules.boltJoints) joints[(j.lowerPartId, j.upperPartId)] = j.id;
            return new AssemblyRules(order, parts, joints);
        }

        public string Name(string partId) =>
            partId == AssemblyGraph.StandId ? "the launch stand" :
            Parts.TryGetValue(partId, out var p) ? p.name : partId;

        public string Category(string partId) => Parts.TryGetValue(partId, out var p) ? p.category : "";

        public int IndexOf(string partId) => partId == AssemblyGraph.StandId ? -1 : Order.IndexOf(partId);

        /// <summary>Which socket a part needs to attach to.</summary>
        public string SocketFor(string partId)
        {
            if (partId == PayloadPartId) return Payload;
            var cat = Category(partId);
            if (cat == "fairing") return partId.EndsWith("left") ? FairingLeft : FairingRight;
            return Top;
        }

        /// <summary>Sockets a part offers to the parts above it.</summary>
        public IEnumerable<string> SocketsOf(string partId)
        {
            if (partId == AssemblyGraph.StandId) { yield return Top; yield break; }
            var cat = Category(partId);
            if (cat == "payload_adapter")
            {
                yield return Payload; yield return FairingLeft; yield return FairingRight;
                yield break;
            }
            if (cat == "fairing" || partId == PayloadPartId) yield break;
            yield return Top;
        }

        /// <summary>The stack part that must go directly on top of this one (null if none).</summary>
        public string ExpectedOnTop(string lowerPartId)
        {
            for (int i = IndexOf(lowerPartId) + 1; i < Order.Count; i++)
                if (SocketFor(Order[i]) == Top) return Order[i];
            return null;
        }

        public string JointFor(string lowerPartId, string upperPartId)
        {
            if (Joints.TryGetValue((lowerPartId, upperPartId), out var j)) return j;
            // Fairing halves bolt to the adapter, which sits on the avionics ring.
            return null;
        }

        public bool DiameterMismatch(string a, string b, out double da, out double db)
        {
            da = Parts.TryGetValue(a, out var pa) ? pa.diameterM : 0;
            db = Parts.TryGetValue(b, out var pb) ? pb.diameterM : 0;
            if (da <= 0 || db <= 0) return false;
            if (Category(a) == "payload_adapter" || Category(b) == "payload_adapter") return false; // adapters change diameter by design
            return System.Math.Abs(da - db) / System.Math.Max(da, db) > DiameterTolerance;
        }
    }
}
