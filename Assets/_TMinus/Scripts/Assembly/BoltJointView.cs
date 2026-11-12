using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>One bolt hole + bolt on a joint flange. Its collider is what the torque wrench clicks.</summary>
    public sealed class BoltVisual : MonoBehaviour
    {
        public BoltJointView view;
        public int index;
    }

    /// <summary>
    /// Draws the bolts of one joint (on the BOLT_## holes) and keeps its BoltJointState.
    /// Colour + height + label show the state: hole only (missing), raised yellow (inserted),
    /// orange (under-torqued), green (torqued), red (over-torqued / stripped).
    /// </summary>
    public sealed class BoltJointView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static readonly Color Yellow = new Color(1f, 0.82f, 0.2f);
        public static readonly Color Orange = new Color(1f, 0.5f, 0.12f);
        public static readonly Color Green = new Color(0.25f, 0.9f, 0.4f);
        public static readonly Color Red = new Color(1f, 0.2f, 0.2f);

        public BoltJointDef Def { get; private set; }
        public BoltJointState State { get; private set; }
        public Part Owner { get; private set; }

        readonly List<Transform> _holes = new List<Transform>();
        readonly List<GameObject> _heads = new List<GameObject>();
        readonly List<Renderer> _rings = new List<Renderer>();
        MaterialPropertyBlock _mpb;
        int _highlight = -1, _coach = -1;
        float _size;

        public static BoltJointView Ensure(Part owner, BoltJointDef def, VehicleDesign saved)
        {
            var root = owner.transform.Find("Joint_" + def.id);
            if (root == null) return null;
            var view = root.GetComponent<BoltJointView>() ?? root.gameObject.AddComponent<BoltJointView>();
            if (view.State == null) view.Init(owner, def, saved);
            return view;
        }

        void Init(Part owner, BoltJointDef def, VehicleDesign saved)
        {
            Owner = owner;
            Def = def;
            State = BoltJointState.LoadFrom(def, saved);
            _mpb = new MaterialPropertyBlock();
            _size = Mathf.Max(0.018f, owner.diameterM * 0.022f);

            for (int i = 0; i < def.boltCount; i++)
            {
                var hole = transform.Find($"BOLT_{i + 1:00}");
                if (hole == null) { Debug.LogWarning($"[Bolts] {def.id} is missing BOLT_{i + 1:00}"); continue; }
                _holes.Add(hole);

                var ring = new GameObject("HoleRing");
                ring.transform.SetParent(hole, false);
                ring.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Torus(_size * 0.9f, _size * 0.18f, 16, 6);
                var rr = ring.AddComponent<MeshRenderer>();
                rr.sharedMaterial = MaterialLibrary.Get(MaterialLibrary.BandBlack);
                _rings.Add(rr);

                var head = new GameObject("BoltHead");
                head.transform.SetParent(hole, false);
                head.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Lathe(
                    new[] { new Vector2(_size * 0.4f, -_size * 1.4f), new Vector2(_size * 0.4f, 0), new Vector2(_size, 0), new Vector2(_size, _size * 0.7f) },
                    6, capTop: true, name: "hexbolt");
                head.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(MaterialLibrary.MetalGrey);
                _heads.Add(head);

                var col = hole.gameObject.AddComponent<SphereCollider>();
                col.radius = _size * 2.2f;
                col.center = new Vector3(0, _size * 0.5f, 0);
                var bv = hole.gameObject.AddComponent<BoltVisual>();
                bv.view = this;
                bv.index = i;
            }
            Refresh();
        }

        public Vector3 BoltWorld(int i) => i >= 0 && i < _holes.Count ? _holes[i].position : transform.position;

        public Bounds FlangeBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var h in _holes) b.Encapsulate(h.position);
            b.Expand(new Vector3(_size * 12, _size * 12, _size * 12));
            return b;
        }

        public void SetHighlight(int i) { if (_highlight != i) { _highlight = i; Refresh(); } }
        public void SetCoach(int i) { if (_coach != i) { _coach = i; Refresh(); } }

        public static Color ColorOf(BoltState s)
        {
            switch (s)
            {
                case BoltState.Inserted: return Yellow;
                case BoltState.UnderTorqued: return Orange;
                case BoltState.Torqued: return Green;
                case BoltState.OverTorqued: return Red;
                default: return new Color(0.2f, 0.2f, 0.22f);
            }
        }

        public void Refresh()
        {
            if (State == null) return;
            for (int i = 0; i < _heads.Count; i++)
            {
                var s = State.StateOf(i);
                bool present = s != BoltState.Missing;
                var head = _heads[i];
                head.SetActive(present);
                // Inserted bolts stand proud of the flange; torque pulls them down flush.
                float k = present ? 1f - Mathf.Clamp01((float)(State.torque[i] / State.Target)) : 0;
                head.transform.localPosition = new Vector3(0, _size * 0.9f * k, 0);

                _mpb.Clear();
                _mpb.SetColor(BaseColorId, ColorOf(s));
                _mpb.SetColor(EmissionId, i == _highlight ? Color.white * 0.35f : Color.black);
                head.GetComponent<Renderer>().SetPropertyBlock(_mpb);

                _mpb.Clear();
                var ringColor = i == _coach ? new Color(0.2f, 1f, 0.5f) : i == _highlight ? Color.white : new Color(0.06f, 0.06f, 0.07f);
                _mpb.SetColor(BaseColorId, ringColor);
                _mpb.SetColor(EmissionId, i == _coach ? new Color(0.1f, 0.8f, 0.3f) : Color.black);
                _rings[i].SetPropertyBlock(_mpb);
            }
        }

        void Update()
        {
            if (_coach < 0 || _coach >= _rings.Count) return;
            float pulse = 1f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            _rings[_coach].transform.localScale = Vector3.one * pulse;
        }

        public void Save(VehicleDesign design) => State.SaveTo(design);
    }
}
