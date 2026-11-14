using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Wiring
{
    /// <summary>
    /// Wiring on a bench (FlatSat). Click a port, a plug follows the mouse on a sagging cable; over a
    /// compatible port it snaps and glows green, over a wrong one it shows red with the reason. Click to
    /// connect, right-click a cable to unplug, R flips a power plug (polarity), Esc cancels.
    /// Multimeter mode: click two ports to test continuity. Used by the Integration Bay and the avionics bay.
    /// </summary>
    public sealed class WiringBench : MonoBehaviour
    {
        public Camera cam;
        public bool interactive = true;
        public bool multimeter;
        public string system = "Wiring";   // EventLog system name

        /// <summary>UI hooks (set by the scene).</summary>
        public Func<Vector2, bool> IsPointerOverUI;
        public Action<string, bool?, Vector2> ShowHint;
        public Action HideHint;
        public Action<string> Toast;

        public event Action Changed;
        public event Action<List<WiringFault>> FaultsChanged;

        public WiringGraph Graph { get; private set; }
        public List<WiringFault> Faults { get; private set; } = new List<WiringFault>();
        public IReadOnlyDictionary<string, BenchTile> Tiles => _tiles;

        readonly Dictionary<string, BenchTile> _tiles = new Dictionary<string, BenchTile>();
        readonly Dictionary<string, CableView> _cableViews = new Dictionary<string, CableView>();
        PortView _pending, _hoverPort, _meterFirst;
        CableView _live;
        bool _reversed;
        HashSet<string> _knownFaults = new HashSet<string>();

        public void Init(RulesDef rules)
        {
            Graph = new WiringGraph(rules);
            Graph.Connected += c => { DrawCable(c); OnChanged(); };
            Graph.Removed += c => { if (_cableViews.TryGetValue(c.id, out var v)) Destroy(v.gameObject); _cableViews.Remove(c.id); OnChanged(); };
        }

        // ------------------------------------------------------------------ devices + layout

        public BenchTile AddDevice(string instanceId, PartDef def)
        {
            if (_tiles.ContainsKey(instanceId)) return _tiles[instanceId];
            Graph.AddDevice(instanceId, def);
            var tile = BenchTile.Create(transform, instanceId, def);
            _tiles[instanceId] = tile;
            Layout();
            OnChanged();
            return tile;
        }

        public void RemoveDevice(string instanceId)
        {
            Graph.RemoveDevice(instanceId);
            if (_tiles.TryGetValue(instanceId, out var t)) Destroy(t.gameObject);
            _tiles.Remove(instanceId);
            Layout();
            OnChanged();
        }

        static readonly string[] CategoryOrder =
            { "solar", "battery", "switch", "eps", "obc", "adcs", "camera", "radio", "antenna", "avionics_device" };

        /// <summary>Tiles in rows on the bench, power on the left and data/radio on the right.</summary>
        public void Layout()
        {
            var tiles = _tiles.Values
                .OrderBy(t => { int i = Array.IndexOf(CategoryOrder, t.def.category); return i < 0 ? 99 : i; })
                .ThenBy(t => t.instanceId).ToList();
            int cols = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(tiles.Count * 1.6f)));
            const float gapX = 0.05f, rowDepth = 0.26f;
            int rows = Mathf.CeilToInt(tiles.Count / (float)cols);
            for (int r = 0; r < rows; r++)
            {
                var row = tiles.Skip(r * cols).Take(cols).ToList();
                float total = row.Sum(t => t.Size.x) + gapX * (row.Count - 1);
                float x = -total / 2;
                foreach (var t in row)
                {
                    t.transform.localPosition = new Vector3(x + t.Size.x / 2, 0, (rows - 1) * rowDepth / 2 - r * rowDepth);
                    x += t.Size.x + gapX;
                }
            }
            foreach (var c in Graph.Cables) UpdateCable(c);
        }

        public PortView FindPort(string address)
        {
            var dev = WiringGraph.DeviceOf(address);
            return _tiles.TryGetValue(dev, out var t) ? t.Port(WiringGraph.PortIdOf(address)) : null;
        }

        public Bounds BenchBounds()
        {
            var b = new Bounds(transform.position, Vector3.one * 0.2f);
            foreach (var t in _tiles.Values) b.Encapsulate(new Bounds(t.transform.position, new Vector3(t.Size.x, 0.05f, t.Size.y)));
            return b;
        }

        // ------------------------------------------------------------------ cables

        void DrawCable(WiringGraph.Cable c)
        {
            var v = CableView.Create(transform, c.type, false);
            v.cableId = c.id;
            _cableViews[c.id] = v;
            UpdateCable(c);
        }

        void UpdateCable(WiringGraph.Cable c)
        {
            if (!_cableViews.TryGetValue(c.id, out var v)) return;
            var a = FindPort(c.a); var b = FindPort(c.b);
            if (a != null && b != null) v.SetEnds(a.PlugPoint, b.PlugPoint);
        }

        public WiringGraph.Cable Connect(string a, string b, bool reversed)
        {
            var c = Graph.Connect(a, b, reversed, out var reason);
            if (c == null)
            {
                Toast?.Invoke(reason);
                AudioFx.Play("error");
                return null;
            }
            GameState.Instance?.Events.Add(system, "CableConnected", $"{Graph.Describe(a)} -> {Graph.Describe(b)} ({c.type}{(c.reversed ? ", reversed" : "")})", c.id);
            if (c.shortCircuit)
            {
                AudioFx.Play("spark");
                Spark(FindPort(b)?.PlugPoint ?? transform.position);
                Toast?.Invoke($"Sparks! Short circuit: two power outputs wired together. Right-click the cable to remove it.");
            }
            else AudioFx.Play("plug");
            // Ctrl+Z unplugs it again; Ctrl+Y plugs it back (audit F7).
            bool rev = c.reversed;
            UndoStack.Push("cable " + Graph.Describe(a) + " to " + Graph.Describe(b), () => DisconnectBetween(a, b), () => Connect(a, b, rev));
            return c;
        }

        void DisconnectBetween(string a, string b)
        {
            var c = Graph.Cables.FirstOrDefault(x => (x.a == a && x.b == b) || (x.a == b && x.b == a));
            if (c != null) Disconnect(c.id);
        }

        public void Disconnect(string cableId)
        {
            var c = Graph.Cables.FirstOrDefault(x => x.id == cableId);
            if (c == null) return;
            string endA = c.a, endB = c.b; bool wasReversed = c.reversed;
            Graph.Disconnect(cableId);
            UndoStack.Push("unplug " + Graph.Describe(endA) + " from " + Graph.Describe(endB), () => Connect(endA, endB, wasReversed), () => DisconnectBetween(endA, endB));
            AudioFx.Play("click");
            GameState.Instance?.Events.Add(system, "CableRemoved", $"{Graph.Describe(c.a)} -x- {Graph.Describe(c.b)}", cableId);
        }

        void OnChanged()
        {
            Faults = WiringValidator.Validate(Graph);
            var keys = new HashSet<string>(Faults.Select(f => f.code + "|" + f.device + "|" + f.port));
            foreach (var f in Faults)
            {
                var key = f.code + "|" + f.device + "|" + f.port;
                if (!_knownFaults.Contains(key) && (f.code == "short_circuit" || f.code == "reversed_polarity" || f.code == "rail_overload" || f.code == "battery_bypasses_eps"))
                    GameState.Instance?.Events.Add(system, "FaultDetected", f.message, f.port ?? f.device, f.severity);
            }
            _knownFaults = keys;
            FaultsChanged?.Invoke(Faults);
            Changed?.Invoke();
        }

        void Spark(Vector3 at)
        {
            var go = new GameObject("Sparks");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f; main.loop = false; main.startLifetime = 0.35f; main.startSpeed = 0.6f;
            main.startSize = 0.008f; main.startColor = new Color(1f, 0.85f, 0.3f); main.gravityModifier = 0.6f;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, 60) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.005f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialLibrary.Get(MaterialLibrary.CableUnlit);
            ps.Play();
            Destroy(go, 1.5f);
        }

        // ------------------------------------------------------------------ interaction

        void Update()
        {
            if (!interactive || cam == null || Graph == null) { CancelPending(); return; }
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool overUI = IsPointerOverUI != null && IsPointerOverUI(pos);

            var port = overUI ? null : RaycastPort(pos);
            if (port != _hoverPort)
            {
                _hoverPort?.SetGlow(Color.black);
                _hoverPort = port;
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame) { CancelPending(); _meterFirst = null; HideHint?.Invoke(); }
            if (kb != null && kb.rKey.wasPressedThisFrame && _pending != null) { _reversed = !_reversed; AudioFx.Play("click"); }

            if (_pending != null) UpdatePending(pos, port);
            else UpdateIdle(pos, port, overUI);

            if (overUI) return;
            if (mouse.leftButton.wasPressedThisFrame) Click(port);
            if (mouse.rightButton.wasPressedThisFrame && _pending == null)
            {
                var cable = NearestCable(pos, 10f);
                if (cable != null) Disconnect(cable.cableId);
            }
        }

        void UpdateIdle(Vector2 pos, PortView port, bool overUI)
        {
            if (port != null)
            {
                port.SetGlow(new Color(0.3f, 0.3f, 0.3f));
                int n = Graph.CablesAt(port.address).Count;
                string tool = multimeter ? (_meterFirst == null ? "Multimeter: click the first port" : "Multimeter: click the second port") : "Click to start a cable";
                ShowHint?.Invoke($"{Graph.Describe(port.address)} · {port.def.type}{(port.def.maxPowerW > 0 ? $" {port.def.maxPowerW:0} W" : "")}{(port.def.bus ? " · bus" : "")} · {(n == 0 ? "free" : n + " cable" + (n > 1 ? "s" : ""))}\n{tool}", null, pos);
                return;
            }
            var cable = overUI ? null : NearestCable(pos, 8f);
            if (cable != null)
            {
                var c = Graph.Cables.First(x => x.id == cable.cableId);
                ShowHint?.Invoke($"{WiringGraph.ShortLabel(Graph.Rules.GetCableType(c.type)?.label)}: {Graph.Describe(c.a)} → {Graph.Describe(c.b)}{(c.reversed ? " (REVERSED)" : "")}\nRight-click to unplug", c.shortCircuit || c.reversed ? false : (bool?)null, pos);
            }
            else HideHint?.Invoke();
        }

        void UpdatePending(Vector2 pos, PortView port)
        {
            Vector3 end;
            string type = null;
            bool valid = false;
            string reason = null;
            if (port != null && port != _pending)
            {
                valid = Graph.CanConnect(_pending.address, port.address, out type, out reason, out bool shortCircuit);
                end = port.PlugPoint;
                port.SetGlow(valid ? (shortCircuit ? new Color(0.8f, 0.4f, 0) : new Color(0.1f, 0.8f, 0.3f)) : new Color(0.9f, 0.1f, 0.1f));
                if (valid && shortCircuit) reason = "Warning: this joins two outputs (short circuit)";
            }
            else
            {
                var ray = cam.ScreenPointToRay(pos);
                var plane = new Plane(Vector3.up, transform.position + Vector3.up * 0.03f);
                end = plane.Raycast(ray, out float d) ? ray.GetPoint(d) : _pending.PlugPoint;
            }
            string liveType = type ?? GuessType(_pending.def.type);
            _live.SetType(liveType, true);
            _live.SetEnds(_pending.PlugPoint, end);

            string pol = liveType == "power" ? (_reversed ? "\nPolarity: REVERSED (press R to flip back)" : "\nPress R to flip the plug") : "";
            if (port != null && port != _pending)
                ShowHint?.Invoke(valid ? $"Click to connect ({liveType}){(reason != null ? "\n" + reason : "")}{pol}" : reason, valid && reason == null, pos);
            else ShowHint?.Invoke($"Cable from {Graph.Describe(_pending.address)}{pol}\nClick a port · Esc cancels", null, pos);
        }

        string GuessType(string portType) =>
            Graph.Rules.cableTypes.FirstOrDefault(c => c.allowed.Any(p => p.from == portType || p.to == portType))?.id ?? "data";

        void Click(PortView port)
        {
            if (multimeter)
            {
                if (port == null) return;
                if (_meterFirst == null) { _meterFirst = port; AudioFx.Play("click"); return; }
                int n = Graph.PathLength(_meterFirst.address, port.address);
                AudioFx.Play(n > 0 ? "beep" : "error");
                Toast?.Invoke(n > 0 ? $"Beep! {Graph.Describe(_meterFirst.address)} and {Graph.Describe(port.address)} are connected via {n} cable{(n > 1 ? "s" : "")}."
                                    : $"Open circuit: no path between {Graph.Describe(_meterFirst.address)} and {Graph.Describe(port.address)}.");
                _meterFirst = null;
                return;
            }

            if (_pending == null)
            {
                if (port == null) return;
                _pending = port;
                _reversed = false;
                _live = CableView.Create(transform, GuessType(port.def.type), true);
                AudioFx.Play("click", 0.6f);
                return;
            }
            if (port == null || port == _pending) { CancelPending(); return; }
            var from = _pending.address; bool rev = _reversed;
            CancelPending();
            var c = Connect(from, port.address, rev);
            if (c != null) FindPort(port.address)?.SetGlow(Color.black);
        }

        void CancelPending()
        {
            if (_live != null) Destroy(_live.gameObject);
            _live = null;
            _pending = null;
        }

        PortView RaycastPort(Vector2 screen)
        {
            if (!Physics.Raycast(cam.ScreenPointToRay(screen), out var hit, 50f)) return null;
            var pv = hit.collider.GetComponent<PortView>();
            return pv != null && _tiles.ContainsKey(WiringGraph.DeviceOf(pv.address)) ? pv : null;
        }

        CableView NearestCable(Vector2 screen, float maxPixels)
        {
            CableView best = null; float bestD = maxPixels;
            foreach (var v in _cableViews.Values)
            {
                float d = v.ScreenDistance(cam, screen);
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        // ------------------------------------------------------------------ save / load

        public void SaveTo(List<CableLink> cables)
        {
            cables.Clear();
            foreach (var c in Graph.Cables)
                cables.Add(new CableLink { fromPort = c.a, toPort = c.b, cableType = c.type, reversed = c.reversed });
        }

        public void LoadFrom(List<CableLink> cables)
        {
            foreach (var l in cables) Graph.Connect(l.fromPort, l.toPort, l.reversed, out _);
        }
    }
}
