using System;
using System.Collections.Generic;
using System.Linq;

namespace TMinus.Assembly
{
    /// <summary>
    /// Which part sits on which. Nodes are part instances; edges are snap connections
    /// (lower, socket, upper, joint). Validates order, diameter and free sockets; supports undo/redo.
    /// </summary>
    public sealed class AssemblyGraph
    {
        public const string StandId = "STAND";

        public sealed class Edge
        {
            public string lower, socket, upper, jointId;
            public override string ToString() => $"{lower}.{socket}<-{upper}" + (jointId != null ? $"[{jointId}]" : "");
        }

        readonly AssemblyRules _rules;
        readonly Dictionary<string, string> _partOf = new Dictionary<string, string>(); // instance -> part id
        readonly List<Edge> _edges = new List<Edge>();
        readonly Stack<(bool attach, Edge edge)> _undo = new Stack<(bool, Edge)>();
        readonly Stack<(bool attach, Edge edge)> _redo = new Stack<(bool, Edge)>();

        /// <summary>Prompt 5 hook: false while a joint still has bolts in it.</summary>
        public Func<string, bool> JointBoltsClear = _ => true;

        public event Action<Edge> PartAttached;
        public event Action<Edge> PartDetached;
        public event Action GraphChanged;

        public AssemblyGraph(AssemblyRules rules)
        {
            _rules = rules;
            _partOf[StandId] = StandId;
        }

        public AssemblyRules Rules => _rules;
        public IReadOnlyList<Edge> Edges => _edges;
        public IEnumerable<string> Instances => _partOf.Keys.Where(k => k != StandId);
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void AddNode(string instanceId, string partId) => _partOf[instanceId] = partId;

        public string PartIdOf(string instanceId) => _partOf.TryGetValue(instanceId, out var p) ? p : null;

        public Edge EdgeBelow(string instanceId) => _edges.Find(e => e.upper == instanceId);

        public bool IsAttached(string instanceId) => instanceId == StandId || EdgeBelow(instanceId) != null;

        public Edge EdgeAt(string lower, string socket) => _edges.Find(e => e.lower == lower && e.socket == socket);

        /// <summary>Everything resting on this part, directly or indirectly.</summary>
        public List<string> Dependents(string instanceId)
        {
            var result = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(instanceId);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                foreach (var e in _edges.Where(e => e.lower == id))
                {
                    result.Add(e.upper);
                    queue.Enqueue(e.upper);
                }
            }
            return result;
        }

        /// <summary>Every (lower instance, socket) on the rocket that is still empty.</summary>
        public IEnumerable<(string lower, string socket)> FreeSockets()
        {
            foreach (var kv in _partOf)
            {
                if (!IsAttached(kv.Key)) continue;
                foreach (var s in _rules.SocketsOf(kv.Value))
                    if (EdgeAt(kv.Key, s) == null) yield return (kv.Key, s);
            }
        }

        public bool CanAttach(string upper, string lower, string socket, out string reason)
        {
            reason = null;
            var upperPart = PartIdOf(upper);
            var lowerPart = PartIdOf(lower);
            if (upperPart == null || lowerPart == null) { reason = "Unknown part."; return false; }
            if (IsAttached(upper)) { reason = $"{_rules.Name(upperPart)} is already on the rocket."; return false; }
            if (!IsAttached(lower)) { reason = $"{_rules.Name(lowerPart)} isn't on the rocket yet."; return false; }
            if (!_rules.SocketsOf(lowerPart).Contains(socket)) { reason = $"{_rules.Name(lowerPart)} has no {socket} mount."; return false; }
            if (EdgeAt(lower, socket) != null) { reason = $"That mount on {_rules.Name(lowerPart)} is already taken."; return false; }

            var needed = _rules.SocketFor(upperPart);
            if (needed != socket)
            {
                reason = $"{_rules.Name(upperPart)} doesn't mount there.";
                return false;
            }

            if (lower != StandId && _rules.DiameterMismatch(upperPart, lowerPart, out var du, out var dl))
            {
                reason = $"Diameter mismatch: {dl:0.0#} m vs {du:0.0#} m. An adapter is needed.";
                return false;
            }

            if (socket == AssemblyRules.Top)
            {
                var expected = lower == StandId ? FirstStackPart() : _rules.ExpectedOnTop(lowerPart);
                if (expected != upperPart)
                {
                    int iu = _rules.IndexOf(upperPart), il = _rules.IndexOf(lowerPart);
                    if (lower == StandId)
                        reason = $"Wrong order: {_rules.Name(expected)} goes on the stand first.";
                    else if (iu < il)
                        reason = $"Wrong order: {_rules.Name(upperPart)} goes below {_rules.Name(lowerPart)}.";
                    else if (expected == null)
                        reason = $"Nothing stacks on top of {_rules.Name(lowerPart)}.";
                    else
                        reason = $"Wrong order: {_rules.Name(expected)} goes on {_rules.Name(lowerPart)} first.";
                    return false;
                }
            }
            return true;
        }

        string FirstStackPart() => _rules.Order.FirstOrDefault(p => _rules.SocketFor(p) == AssemblyRules.Top);

        public bool TryAttach(string upper, string lower, string socket, out string reason)
        {
            if (!CanAttach(upper, lower, socket, out reason)) return false;
            var edge = new Edge
            {
                lower = lower, socket = socket, upper = upper,
                jointId = _rules.JointFor(PartIdOf(lower), PartIdOf(upper))
            };
            DoAttach(edge);
            _undo.Push((true, edge));
            _redo.Clear();
            return true;
        }

        public bool CanDetach(string instanceId, out string reason)
        {
            reason = null;
            var edge = EdgeBelow(instanceId);
            if (edge == null) { reason = "It isn't attached."; return false; }
            var deps = _edges.Where(e => e.lower == instanceId).Select(e => _rules.Name(PartIdOf(e.upper))).ToList();
            if (deps.Count > 0) { reason = "Remove first: " + string.Join(", ", deps) + "."; return false; }
            if (edge.jointId != null && !JointBoltsClear(edge.jointId))
            {
                reason = $"Unbolt it first: joint {edge.jointId} still has bolts in.";
                return false;
            }
            return true;
        }

        public bool TryDetach(string instanceId, out string reason)
        {
            if (!CanDetach(instanceId, out reason)) return false;
            var edge = EdgeBelow(instanceId);
            DoDetach(edge);
            _undo.Push((false, edge));
            _redo.Clear();
            return true;
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var (attach, edge) = _undo.Pop();
            if (attach) DoDetach(edge); else DoAttach(edge);
            _redo.Push((attach, edge));
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            var (attach, edge) = _redo.Pop();
            if (attach) DoAttach(edge); else DoDetach(edge);
            _undo.Push((attach, edge));
            return true;
        }

        void DoAttach(Edge e)
        {
            _edges.Add(e);
            PartAttached?.Invoke(e);
            GraphChanged?.Invoke();
        }

        void DoDetach(Edge e)
        {
            _edges.Remove(e);
            PartDetached?.Invoke(e);
            GraphChanged?.Invoke();
        }

        public void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
        }

        /// <summary>Order-independent text form of the graph (used by tests and saves).</summary>
        public string Snapshot() => string.Join(";", _edges.Select(e => e.ToString()).OrderBy(s => s));

        public bool IsComplete() => _partOf.Keys.Where(k => k != StandId).All(IsAttached);
    }
}
