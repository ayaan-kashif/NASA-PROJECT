using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core.Data;

namespace TMinus.Wiring
{
    /// <summary>
    /// Devices and the cables between their ports. A port address is "instanceId/PORT_ID".
    /// Used twice: the CubeSat (Integration Bay) and the rocket's avionics ring (Prompt 8).
    /// </summary>
    public sealed class WiringGraph
    {
        public sealed class Cable
        {
            public string id;
            public string a, b;      // port addresses
            public string type;      // power, data, rf, pyro
            public bool reversed;    // power only: red and black swapped
            public bool shortCircuit;
            public override string ToString() => $"{a} -{type}{(reversed ? "(rev)" : "")}- {b}";
        }

        readonly RulesDef _rules;
        readonly Dictionary<string, PartDef> _devices = new Dictionary<string, PartDef>();
        readonly List<Cable> _cables = new List<Cable>();
        int _nextId = 1;

        public event Action<Cable> Connected;
        public event Action<Cable> Removed;

        public WiringGraph(RulesDef rules) { _rules = rules; }

        public RulesDef Rules => _rules;
        public IReadOnlyList<Cable> Cables => _cables;
        public IEnumerable<KeyValuePair<string, PartDef>> Devices => _devices;

        public void AddDevice(string instanceId, PartDef def) => _devices[instanceId] = def;

        public void RemoveDevice(string instanceId)
        {
            foreach (var c in _cables.Where(c => DeviceOf(c.a) == instanceId || DeviceOf(c.b) == instanceId).ToList())
                Disconnect(c.id);
            _devices.Remove(instanceId);
        }

        public PartDef Device(string instanceId) => instanceId != null && _devices.TryGetValue(instanceId, out var d) ? d : null;

        public static string DeviceOf(string address) => address?.Split('/')[0];
        public static string PortIdOf(string address) { var i = address?.IndexOf('/') ?? -1; return i < 0 ? null : address.Substring(i + 1); }
        public static string Address(string instanceId, string portId) => instanceId + "/" + portId;

        public PortDef Port(string address) => Device(DeviceOf(address))?.GetPort(PortIdOf(address));

        public string Describe(string address)
        {
            var d = Device(DeviceOf(address));
            var p = Port(address);
            return $"{d?.name ?? DeviceOf(address)} {p?.label ?? PortIdOf(address)}";
        }

        public List<Cable> CablesAt(string address) => _cables.Where(c => c.a == address || c.b == address).ToList();

        public bool IsConnected(string address) => _cables.Any(c => c.a == address || c.b == address);

        /// <summary>Other end(s) of the cables plugged into this port.</summary>
        public IEnumerable<string> PeersOf(string address)
        {
            foreach (var c in _cables)
            {
                if (c.a == address) yield return c.b;
                else if (c.b == address) yield return c.a;
            }
        }

        public bool CanConnect(string a, string b, out string cableType, out string reason, out bool shortCircuit)
        {
            cableType = null; reason = null; shortCircuit = false;
            var pa = Port(a); var pb = Port(b);
            if (pa == null || pb == null) { reason = "Unknown port."; return false; }
            if (a == b) { reason = "Pick a different port."; return false; }
            if (DeviceOf(a) == DeviceOf(b)) { reason = "A board can't be wired to itself."; return false; }

            var cable = _rules.CableFor(pa.type, pb.type);
            if (pa.type == "PWR_OUT" && pb.type == "PWR_OUT")
            {
                cable = _rules.GetCableType("power");
                shortCircuit = true;
            }
            if (cable == null)
            {
                var from = _rules.cableTypes.FirstOrDefault(c => c.allowed.Any(x => x.from == pa.type || x.to == pa.type));
                reason = from != null ? $"{ShortLabel(from.label)} can't plug into a {pb.type} port." : $"{pa.type} and {pb.type} don't connect.";
                return false;
            }
            if (!pa.bus && IsConnected(a)) { reason = $"{Describe(a)} already has a cable."; return false; }
            if (!pb.bus && IsConnected(b)) { reason = $"{Describe(b)} already has a cable."; return false; }
            if (_cables.Any(c => (c.a == a && c.b == b) || (c.a == b && c.b == a))) { reason = "Those ports are already connected."; return false; }

            cableType = cable.id;
            return true;
        }

        /// <summary>"RF coax (grey)" -> "RF coax".</summary>
        public static string ShortLabel(string label)
        {
            var i = label?.IndexOf(" (") ?? -1;
            return i > 0 ? label.Substring(0, i) : label;
        }

        public Cable Connect(string a, string b, bool reversed, out string reason)
        {
            if (!CanConnect(a, b, out var type, out reason, out var shortCircuit)) return null;
            var cable = new Cable { id = "c" + _nextId++, a = a, b = b, type = type, reversed = reversed && type == "power", shortCircuit = shortCircuit };
            _cables.Add(cable);
            Connected?.Invoke(cable);
            return cable;
        }

        public bool Disconnect(string cableId)
        {
            var c = _cables.Find(x => x.id == cableId);
            if (c == null) return false;
            _cables.Remove(c);
            Removed?.Invoke(c);
            return true;
        }

        /// <summary>Multimeter: number of cables on the shortest path between two ports, or -1 (open circuit).</summary>
        public int PathLength(string from, string to)
        {
            if (from == to) return 0;
            var dist = new Dictionary<string, int> { [from] = 0 };
            var q = new Queue<string>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                // Bus ports on one board share the same bus: moving between them costs no cable.
                var port = Port(cur);
                if (port != null && port.bus)
                {
                    var dev = Device(DeviceOf(cur));
                    foreach (var sib in dev.ports.Where(p => p.bus && p.type == port.type))
                    {
                        var addr = Address(DeviceOf(cur), sib.id);
                        if (dist.ContainsKey(addr)) continue;
                        dist[addr] = dist[cur];
                        if (addr == to) return dist[addr];
                        q.Enqueue(addr);
                    }
                }
                foreach (var next in PeersOf(cur))
                {
                    if (dist.ContainsKey(next)) continue;
                    dist[next] = dist[cur] + 1;
                    if (next == to) return dist[next];
                    q.Enqueue(next);
                }
            }
            return -1;
        }

        public void Clear()
        {
            foreach (var c in _cables.ToList()) Disconnect(c.id);
        }
    }
}
