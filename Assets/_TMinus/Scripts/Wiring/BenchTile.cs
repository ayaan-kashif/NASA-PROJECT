using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Wiring
{
    /// <summary>A connector on a bench tile. Its address is "instanceId/PORT_ID".</summary>
    public sealed class PortView : MonoBehaviour
    {
        public string address;
        public PortDef def;
        public BenchTile tile;
        Renderer _r;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;

        public Vector3 PlugPoint => transform.position + transform.forward * 0.012f + Vector3.up * 0.004f;

        public void Init()
        {
            _r = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            SetGlow(Color.black);
        }

        public void SetGlow(Color glow)
        {
            if (_r == null) return;
            _mpb.Clear();
            _mpb.SetColor(BaseColorId, CableStyle.PortColor(def.type));
            _mpb.SetColor(EmissionId, glow);
            _r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// One device laid flat on the bench: a board-coloured tile with its connectors along the front
    /// edge. Port child objects are named PORT_<id>, the same convention Blender models use.
    /// </summary>
    public sealed class BenchTile : MonoBehaviour
    {
        public string instanceId;
        public PartDef def;
        public readonly List<PortView> Ports = new List<PortView>();
        public Vector2 Size { get; private set; }

        public const float PortPitch = 0.05f;

        public static BenchTile Create(Transform parent, string instanceId, PartDef def)
        {
            var go = new GameObject("Tile_" + instanceId);
            go.transform.SetParent(parent, false);
            var tile = go.AddComponent<BenchTile>();
            tile.instanceId = instanceId;
            tile.def = def;
            tile.Build();
            return tile;
        }

        void Build()
        {
            int n = Mathf.Max(1, def.ports.Count);
            float width = Mathf.Max(0.22f, n * PortPitch + 0.05f);
            const float depth = 0.16f;
            Size = new Vector2(width, depth);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(transform, false);
            board.transform.localScale = new Vector3(width, 0.012f, depth);
            board.transform.localPosition = new Vector3(0, 0.006f, 0);
            board.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(BoardMaterial(def.category));

            // A few "chips" so boards read as electronics.
            var rng = new System.Random(def.id.GetHashCode());
            int chips = def.category == "solar" || def.category == "antenna" ? 0 : 2 + rng.Next(3);
            for (int i = 0; i < chips; i++)
            {
                var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chip.name = "Chip";
                Destroy(chip.GetComponent<Collider>());
                chip.transform.SetParent(transform, false);
                float s = 0.025f + (float)rng.NextDouble() * 0.03f;
                chip.transform.localScale = new Vector3(s, 0.008f, s);
                chip.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * (width - 0.06f), 0.016f, 0.02f + (float)rng.NextDouble() * 0.04f);
                chip.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(MaterialLibrary.EngineDark);
            }

            for (int i = 0; i < def.ports.Count; i++)
            {
                var p = def.ports[i];
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "PORT_" + p.id;
                c.transform.SetParent(transform, false);
                c.transform.localScale = new Vector3(0.028f, 0.016f, 0.02f);
                c.transform.localPosition = new Vector3(-width / 2 + 0.035f + i * PortPitch, 0.02f, -depth / 2 + 0.012f);
                c.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                c.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(MaterialLibrary.BodyWhite);
                var col = c.GetComponent<BoxCollider>();
                col.size = new Vector3(1.6f, 2.5f, 2.5f);
                var pv = c.AddComponent<PortView>();
                pv.address = WiringGraph.Address(instanceId, p.id);
                pv.def = p;
                pv.tile = this;
                pv.Init();
                Ports.Add(pv);
            }
        }

        public PortView Port(string portId) => Ports.Find(p => p.def.id == portId);

        public Vector3 LabelAnchor => transform.position + new Vector3(0, 0.03f, Size.y * 0.5f);

        static string BoardMaterial(string category)
        {
            switch (category)
            {
                case "battery": return MaterialLibrary.Wall;
                case "solar": return MaterialLibrary.BandBlack;
                case "antenna": return MaterialLibrary.MetalGrey;
                case "camera": return MaterialLibrary.EngineDark;
                case "switch": case "pin": return MaterialLibrary.InsulationOrange;
                default: return MaterialLibrary.BoardGreen;
            }
        }
    }
}
