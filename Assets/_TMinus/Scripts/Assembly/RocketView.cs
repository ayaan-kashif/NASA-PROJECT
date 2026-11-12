using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>
    /// A rocket in the VAB. Inspect mode: the full stack. Assembly mode (buildable rockets):
    /// an AssemblyGraph decides what is stacked; loose parts lie on the floor in front of the stand.
    /// Also animates the exploded layout and the X-ray look.
    /// </summary>
    public sealed class RocketView : MonoBehaviour
    {
        public RocketDef Rocket { get; private set; }
        public AssemblyGraph Graph { get; private set; }
        public readonly List<Part> Parts = new List<Part>();
        public bool Exploded { get; private set; }
        public bool XRay { get; private set; }
        public float Height { get; private set; }
        /// <summary>Part being dragged by the player: not moved by the layout animation.</summary>
        public Part Dragging;

        readonly Dictionary<Part, Pose> _target = new Dictionary<Part, Pose>();
        readonly Dictionary<Part, Vector3> _exploded = new Dictionary<Part, Vector3>();
        readonly Dictionary<Renderer, Material[]> _originalMats = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<string, Part> _byInstance = new Dictionary<string, Part>();
        float _deckTop = 1.5f;

        public static RocketView Spawn(RocketDef rocket, DataRegistry data, Transform parent, bool forAssembly,
                                       float deckTop = 1.5f, VehicleDesign saved = null)
        {
            var go = new GameObject("Rocket_" + rocket.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RocketView>();
            view._deckTop = deckTop;
            view.Build(rocket, data, forAssembly && rocket.buildable, saved);
            return view;
        }

        public Part Find(string instanceId) => instanceId != null && _byInstance.TryGetValue(instanceId, out var p) ? p : null;

        void Build(RocketDef rocket, DataRegistry data, bool assembly, VehicleDesign saved)
        {
            Rocket = rocket;
            var ids = new List<string>();
            if (rocket.buildable)
            {
                var rules = AssemblyRules.FromData(data, rocket.id);
                ids.AddRange(rules.Order);
                Graph = new AssemblyGraph(rules);
            }
            else ids.AddRange(rocket.partIds);

            foreach (var id in ids)
            {
                string factoryId = id == AssemblyRules.PayloadPartId ? ProceduralPartFactory.PayloadCubeSat : id;
                var part = ProceduralPartFactory.Create(factoryId, data, transform).GetComponent<Part>();
                part.instanceId = id;
                part.name = id;
                Parts.Add(part);
                _byInstance[id] = part;
                Graph?.AddNode(id, id);
            }

            if (Graph != null)
            {
                if (!assembly) AttachAllInOrder();
                else if (saved != null) Restore(saved);
                Graph.ClearHistory();
                Graph.GraphChanged += Relayout;
            }

            Height = 0;
            foreach (var p in Parts) Height += p.stackLength;
            Height = Mathf.Max(Height, 1f);
            Relayout();
            foreach (var p in Parts) { p.transform.localPosition = _target[p].position; p.transform.localRotation = _target[p].rotation; }
        }

        void AttachAllInOrder()
        {
            string below = AssemblyGraph.StandId;
            foreach (var id in Graph.Rules.Order)
            {
                var socket = Graph.Rules.SocketFor(id);
                string lower = socket == AssemblyRules.Top ? below : "pf_payload_adapter";
                if (Graph.TryAttach(id, lower, socket, out _) && socket == AssemblyRules.Top) below = id;
            }
        }

        void Restore(VehicleDesign saved)
        {
            foreach (var placed in saved.parts)
            {
                var slash = placed.mountId?.IndexOf('/') ?? -1;
                if (slash < 0) continue;
                Graph.TryAttach(placed.instanceId, placed.mountId.Substring(0, slash), placed.mountId.Substring(slash + 1), out _);
            }
        }

        /// <summary>Write the current graph into a VehicleDesign (attach order is kept).</summary>
        public void SaveTo(VehicleDesign design)
        {
            design.rocketId = Rocket.id;
            design.parts.Clear();
            if (Graph == null) return;
            foreach (var e in Graph.Edges)
                design.parts.Add(new PlacedPart { instanceId = e.upper, partId = Graph.PartIdOf(e.upper), mountId = e.lower + "/" + e.socket });
        }

        // ------------------------------------------------------------------ layout

        public void Relayout()
        {
            var adapterPos = Vector3.zero;
            if (Graph == null)
            {
                float y = 0;
                foreach (var p in Parts)
                {
                    if (p == Parts[0]) y = p.bellDepth;
                    _target[p] = new Pose(new Vector3(0, y, 0), Quaternion.identity);
                    y += p.stackLength;
                }
            }
            else
            {
                // Walk the stack up from the stand.
                string lower = AssemblyGraph.StandId;
                float y = 0;
                var edge = Graph.EdgeAt(lower, AssemblyRules.Top);
                bool first = true;
                while (edge != null)
                {
                    var p = Find(edge.upper);
                    if (first) { y = p.bellDepth; first = false; }
                    _target[p] = new Pose(new Vector3(0, y, 0), Quaternion.identity);
                    if (p.Def != null && p.Def.category == "payload_adapter") adapterPos = new Vector3(0, y, 0);
                    y += p.stackLength;
                    lower = edge.upper;
                    edge = Graph.EdgeAt(lower, AssemblyRules.Top);
                }
                foreach (var e in Graph.Edges)
                {
                    if (e.socket == AssemblyRules.Top) continue;
                    var adapter = Find(e.lower);
                    var basePos = _target.TryGetValue(adapter, out var ap) ? ap.position : adapterPos;
                    var pos = e.socket == AssemblyRules.Payload ? basePos + Vector3.up * (adapter.stackLength + 0.02f) : basePos;
                    _target[Find(e.upper)] = new Pose(pos, Quaternion.identity);
                }
                LayOutLooseParts();
            }
            ComputeExploded();
        }

        /// <summary>Unattached parts lie on the floor in a row in front of the stand.</summary>
        void LayOutLooseParts()
        {
            float x = 0;
            var loose = new List<Part>();
            foreach (var p in Parts) if (!Graph.IsAttached(p.instanceId)) loose.Add(p);
            float total = 0;
            foreach (var p in loose) total += Mathf.Max(p.stackLength, p.diameterM) + 1.2f;
            x = -total / 2;
            float z = -Mathf.Max(6f, Height * 0.35f);
            foreach (var p in loose)
            {
                bool small = p.stackLength < 0.6f || (p.Def != null && p.Def.category == "fairing");
                float len = Mathf.Max(p.stackLength, small ? p.diameterM : 0.1f);
                Pose pose;
                if (small)
                    pose = new Pose(new Vector3(x + len / 2, -_deckTop, z), Quaternion.identity);
                else // lying down, nose towards +x
                    pose = new Pose(new Vector3(x + p.bellDepth, -_deckTop + p.diameterM / 2, z), Quaternion.Euler(0, 0, -90));
                _target[p] = pose;
                x += len + p.bellDepth + 1.2f;
            }
        }

        /// <summary>World position where `upper`'s origin goes when attached at (lower, socket).</summary>
        public Vector3 SocketWorld(string lower, string socket, Part upper)
        {
            Vector3 local;
            if (lower == AssemblyGraph.StandId) local = Vector3.up * (upper != null ? upper.bellDepth : 0);
            else
            {
                var lp = Find(lower);
                var basePos = _target.TryGetValue(lp, out var pose) ? pose.position : lp.transform.localPosition;
                local = socket == AssemblyRules.Top ? basePos + Vector3.up * lp.stackLength
                      : socket == AssemblyRules.Payload ? basePos + Vector3.up * (lp.stackLength + 0.02f)
                      : basePos;
            }
            return transform.TransformPoint(local);
        }

        void ComputeExploded()
        {
            float gap = Mathf.Max(Height, 5f) * 0.06f;
            int i = 0;
            foreach (var p in Parts)
            {
                var basePos = _target[p].position;
                bool attached = Graph == null || Graph.IsAttached(p.instanceId);
                var lift = attached ? Vector3.up * (gap * i) : Vector3.zero;
                if (attached && p.Def != null && p.Def.category == "fairing")
                    _exploded[p] = basePos + lift + Vector3.right * (p.partId.EndsWith("left") ? -1 : 1) * p.diameterM * 1.1f;
                else _exploded[p] = basePos + lift;
                if (attached) i++;
            }
        }

        public void SetExploded(bool on) => Exploded = on;

        public void SetXRay(bool on)
        {
            XRay = on;
            var ghost = MaterialLibrary.Get(MaterialLibrary.XRay);
            foreach (var p in Parts)
            {
                p.SetInternalsVisible(on);
                if (!p.isShell) continue;
                foreach (var r in p.Renderers)
                {
                    if (r == null || r.name.StartsWith("INT_")) continue;
                    if (on)
                    {
                        if (!_originalMats.ContainsKey(r)) _originalMats[r] = r.sharedMaterials;
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int k = 0; k < mats.Length; k++) mats[k] = ghost;
                        r.sharedMaterials = mats;
                    }
                    else if (_originalMats.TryGetValue(r, out var orig)) r.sharedMaterials = orig;
                }
            }
        }

        public Vector3 LabelAnchor(Part p)
        {
            var b = p.WorldBounds();
            return new Vector3(b.max.x, b.center.y, b.center.z);
        }

        public Bounds WorldBounds(bool attachedOnly = false)
        {
            var b = new Bounds(transform.position + Vector3.up * Height * 0.5f, Vector3.one * 0.1f);
            foreach (var p in Parts)
                if (!attachedOnly || Graph == null || Graph.IsAttached(p.instanceId)) b.Encapsulate(p.WorldBounds());
            return b;
        }

        void Update()
        {
            float k = 1 - Mathf.Exp(-Time.deltaTime * 6f);
            foreach (var p in Parts)
            {
                // Skip the dragged part and parts released from the stack (e.g. a dropped stage in flight).
                if (p == null || p == Dragging || p.transform.parent != transform || !_target.TryGetValue(p, out var pose)) continue;
                var pos = Exploded ? _exploded[p] : pose.position;
                p.transform.localPosition = Vector3.Lerp(p.transform.localPosition, pos, k);
                p.transform.localRotation = Quaternion.Slerp(p.transform.localRotation, pose.rotation, k);
            }
        }
    }
}
