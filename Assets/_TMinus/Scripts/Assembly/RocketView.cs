using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>
    /// A whole rocket on the stand: builds the stack from rockets.json partIds (bottom first),
    /// and animates Assembled / Exploded layouts plus the X-ray look.
    /// </summary>
    public sealed class RocketView : MonoBehaviour
    {
        public RocketDef Rocket { get; private set; }
        public readonly List<Part> Parts = new List<Part>();
        public bool Exploded { get; private set; }
        public bool XRay { get; private set; }
        public float Height { get; private set; }

        readonly Dictionary<Part, Vector3> _assembled = new Dictionary<Part, Vector3>();
        readonly Dictionary<Part, Vector3> _exploded = new Dictionary<Part, Vector3>();
        readonly Dictionary<Renderer, Material[]> _originalMats = new Dictionary<Renderer, Material[]>();

        public static RocketView Spawn(RocketDef rocket, DataRegistry data, Transform parent)
        {
            var go = new GameObject("Rocket_" + rocket.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RocketView>();
            view.Build(rocket, data);
            return view;
        }

        void Build(RocketDef rocket, DataRegistry data)
        {
            Rocket = rocket;
            float y = 0, fairingBase = 0;
            int index = 0;

            var ids = new List<string>(rocket.partIds);
            int adapter = ids.IndexOf("pf_payload_adapter");
            if (adapter >= 0) ids.Insert(adapter + 1, ProceduralPartFactory.PayloadCubeSat);

            foreach (var id in ids)
            {
                var part = ProceduralPartFactory.Create(id, data, transform).GetComponent<Part>();
                if (index == 0) y = part.bellDepth; // engines stand on the platform

                Vector3 pos;
                bool fairing = part.Def != null && part.Def.category == "fairing";
                if (fairing) pos = new Vector3(0, fairingBase, 0);
                else if (id == ProceduralPartFactory.PayloadCubeSat) pos = new Vector3(0, y + 0.02f, 0);
                else
                {
                    if (id == "pf_payload_adapter") fairingBase = y;
                    pos = new Vector3(0, y, 0);
                    y += part.stackLength;
                }

                part.transform.localPosition = pos;
                _assembled[part] = pos;
                Parts.Add(part);
                index++;
            }

            Height = Mathf.Max(y, (float)rocket.heightM * 0.5f);
            ComputeExploded();
        }

        void ComputeExploded()
        {
            float gap = Height * 0.06f;
            int i = 0;
            foreach (var p in Parts)
            {
                var basePos = _assembled[p];
                var lift = Vector3.up * (gap * i);
                if (p.Def != null && p.Def.category == "fairing")
                {
                    float side = p.partId.EndsWith("left") ? -1 : 1;
                    _exploded[p] = basePos + lift + Vector3.right * side * p.diameterM * 1.1f;
                }
                else _exploded[p] = basePos + lift;
                i++;
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

        /// <summary>Label anchor for the exploded-view leader lines (right edge, mid height).</summary>
        public Vector3 LabelAnchor(Part p)
        {
            var b = p.WorldBounds();
            return new Vector3(b.max.x, b.center.y, b.center.z);
        }

        public Bounds WorldBounds()
        {
            var b = new Bounds(transform.position + Vector3.up * Height * 0.5f, Vector3.one * 0.1f);
            foreach (var p in Parts) b.Encapsulate(p.WorldBounds());
            return b;
        }

        void Update()
        {
            float k = 1 - Mathf.Exp(-Time.deltaTime * 6f);
            foreach (var p in Parts)
            {
                if (p == null) continue;
                var target = Exploded ? _exploded[p] : _assembled[p];
                p.transform.localPosition = Vector3.Lerp(p.transform.localPosition, target, k);
            }
        }
    }
}
