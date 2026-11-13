using TMinus.Assembly;
using TMinus.Core;
using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Flight
{
    /// <summary>
    /// True-scale (metres) launch site: land, pad, tower and lightning masts. The globe is drawn at 1/1000 scale,
    /// which is fine from altitude but makes the rocket barely move near the ground, so below
    /// <see cref="MaxAltitudeM"/> this local ground is shown instead, placed from the pad's real (rotating) position.
    /// </summary>
    public sealed class FlightPad
    {
        public const double MaxAltitudeM = 3000;
        const float LandRadius = 15000f;

        public Transform Root { get; private set; }
        Vec2d _pad0;
        double _omega;
        float _baseY;

        public static FlightPad Build(Transform parent, Vec2d startPos, double omega, float baseY)
        {
            var p = new FlightPad { _pad0 = startPos, _omega = omega, _baseY = baseY };
            p.Root = new GameObject("PadArea").transform;
            p.Root.SetParent(parent, false);

            Disc(p.Root, "Land", LandRadius, new Color(0.36f, 0.42f, 0.27f), -0.05f);
            Disc(p.Root, "Scrub", 900, new Color(0.47f, 0.45f, 0.33f), -0.03f);
            Disc(p.Root, "PadConcrete", 45, new Color(0.58f, 0.58f, 0.57f), 0f);
            Box(p.Root, "Road", new Vector3(8, 0.04f, 900), new Vector3(0, 0.01f, -470), new Color(0.3f, 0.3f, 0.31f));
            Box(p.Root, "FlameTrench", new Vector3(10, 0.05f, 30), new Vector3(0, 0.02f, 25), new Color(0.18f, 0.17f, 0.17f));
            var tower = Box(p.Root, "Tower", new Vector3(3, 27, 3), new Vector3(21, 13.5f, 10), Color.white);
            tower.GetComponent<Renderer>().sharedMaterial = M.Get(M.GantryRed);
            foreach (var x in new[] { -60f, 60f })
                Box(p.Root, "LightningMast", new Vector3(0.8f, 60, 0.8f), new Vector3(x, 30, 50), new Color(0.7f, 0.7f, 0.72f));
            Box(p.Root, "WaterTower", new Vector3(10, 30, 10), new Vector3(-90, 15, -40), new Color(0.85f, 0.85f, 0.83f));
            Box(p.Root, "Hangar", new Vector3(60, 25, 40), new Vector3(140, 12.5f, -380), new Color(0.75f, 0.76f, 0.78f));
            return p;
        }

        /// <summary>Place the site relative to the rocket (which stays at the origin) and hide it once we are high up.</summary>
        public void Sync(Vec2d rocketPos, double time, double altitudeM, Quaternion toUnity)
        {
            bool show = altitudeM < MaxAltitudeM;
            if (Root.gameObject.activeSelf != show) Root.gameObject.SetActive(show);
            if (!show) return;
            double a = _omega * time, c = System.Math.Cos(a), s = System.Math.Sin(a);
            var pad = new Vec2d(_pad0.x * c - _pad0.y * s, _pad0.x * s + _pad0.y * c);
            var d = pad - rocketPos;
            var up = (toUnity * new Vector3((float)pad.Normalized.x, (float)pad.Normalized.y, 0)).normalized;
            Root.position = toUnity * new Vector3((float)d.x, (float)d.y, 0) + up * _baseY;
            Root.rotation = Quaternion.FromToRotation(Vector3.up, up);
        }

        static void Disc(Transform parent, string name, float radius, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(radius, 0) }, 96, doubleSided: true);
            go.AddComponent<MeshRenderer>().sharedMaterial = Tint(color);
        }

        static GameObject Box(Transform parent, string name, Vector3 size, Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = size; go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = Tint(color);
            return go;
        }

        static Material Tint(Color color)
        {
            var mat = new Material(M.Get(M.Concrete)) { color = color };
            mat.SetColor("_BaseColor", color);
            return mat;
        }
    }
}
