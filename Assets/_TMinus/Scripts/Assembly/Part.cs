using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>
    /// A buildable part (procedural or from a Blender FBX). On Awake it registers named children:
    /// SNAP_* (snap nodes), BOLT_## (bolt holes), PORT_* (ports), PIPE_* (pipe ends) and INT_*
    /// (internals that only show in X-ray).
    /// </summary>
    public sealed class Part : MonoBehaviour
    {
        public string partId;
        /// <summary>Height this part adds to a stack (0 for parts that wrap others, e.g. fairing halves).</summary>
        public float stackLength;
        /// <summary>How far engine bells hang below SNAP_Bottom.</summary>
        public float bellDepth;
        /// <summary>Outer shells fade in X-ray so the internals show through.</summary>
        public bool isShell;
        public float diameterM;

        public readonly List<Transform> SnapNodes = new List<Transform>();
        public readonly List<Transform> BoltHoles = new List<Transform>();
        public readonly List<Transform> Ports = new List<Transform>();
        public readonly List<Transform> PipeEnds = new List<Transform>();
        public readonly List<GameObject> Internals = new List<GameObject>();

        Renderer[] _renderers;
        PartDef _def;
        bool _registered;

        public PartDef Def
        {
            get
            {
                if (_def == null && GameState.Instance != null) _def = GameState.Instance.Data.GetPart(partId);
                return _def;
            }
            set => _def = value;
        }

        public Renderer[] Renderers => _renderers ??= GetComponentsInChildren<Renderer>(true);

        void Awake() => RegisterSockets();

        /// <summary>Scan children by name. Safe to call again after adding children.</summary>
        public void RegisterSockets()
        {
            SnapNodes.Clear(); BoltHoles.Clear(); Ports.Clear(); PipeEnds.Clear(); Internals.Clear();
            _renderers = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                var n = t.name;
                if (n.StartsWith("SNAP_")) SnapNodes.Add(t);
                else if (n.StartsWith("BOLT_")) BoltHoles.Add(t);
                else if (n.StartsWith("PORT_")) Ports.Add(t);
                else if (n.StartsWith("PIPE_")) PipeEnds.Add(t);
                else if (n.StartsWith("INT_")) Internals.Add(t.gameObject);
            }
            _registered = true;
        }

        public Transform GetSnap(string name)
        {
            if (!_registered) RegisterSockets();
            return SnapNodes.Find(s => s.name == name);
        }

        public Bounds WorldBounds(bool includeInternals = false)
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in Renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!includeInternals && r.name.StartsWith("INT_")) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public void SetInternalsVisible(bool visible)
        {
            foreach (var go in Internals) if (go != null) go.SetActive(visible);
        }
    }
}
