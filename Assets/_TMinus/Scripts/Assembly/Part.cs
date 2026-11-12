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
        /// <summary>Unique id inside one rocket/build (the assembly graph node id).</summary>
        public string instanceId;
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

        readonly Dictionary<Renderer, Material[]> _beforeOverride = new Dictionary<Renderer, Material[]>();

        /// <summary>Show the whole part in one material (drag ghost). ClearOverrideMaterial restores it.</summary>
        public void SetOverrideMaterial(Material m)
        {
            foreach (var r in Renderers)
            {
                if (r == null || r.name.StartsWith("INT_")) continue;
                if (!_beforeOverride.ContainsKey(r)) _beforeOverride[r] = r.sharedMaterials;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
            }
        }

        public void ClearOverrideMaterial()
        {
            foreach (var kv in _beforeOverride) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            _beforeOverride.Clear();
        }

        /// <summary>Brief emission pulse (green flash on a good connection).</summary>
        public void Flash(Color color, float seconds = 0.45f)
        {
            if (isActiveAndEnabled) StartCoroutine(FlashRoutine(color, seconds));
        }

        System.Collections.IEnumerator FlashRoutine(Color color, float seconds)
        {
            var mpb = new MaterialPropertyBlock();
            int id = Shader.PropertyToID("_EmissionColor");
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                mpb.SetColor(id, color * (1 - t / seconds) * 2f);
                foreach (var r in Renderers) if (r != null) r.SetPropertyBlock(mpb);
                yield return null;
            }
            mpb.SetColor(id, Color.black);
            foreach (var r in Renderers) if (r != null) r.SetPropertyBlock(mpb);
        }
    }
}
