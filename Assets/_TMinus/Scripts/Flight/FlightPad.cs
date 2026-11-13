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

            var groundMat = Resources.Load<Material>("Looks/Ground");
            Quad(p.Root, "Land", LandRadius * 2, -0.06f, groundMat != null ? groundMat : Tint(new Color(0.36f, 0.42f, 0.27f)), 1f / 140f);
            Quad(p.Root, "PadConcrete", 90, 0f, M.Get(M.Concrete), 1f / 90f);
            Box(p.Root, "Road", new Vector3(8, 0.04f, 900), new Vector3(0, 0.01f, -495), new Color(0.22f, 0.22f, 0.23f));
            Box(p.Root, "FlameTrench", new Vector3(10, 0.05f, 30), new Vector3(0, 0.02f, 25), new Color(0.12f, 0.11f, 0.11f));
            Truss(p.Root, new Vector3(16, 0, 9), 30f, 4f, M.Get(M.GantryRed));
            foreach (var x in new[] { -60f, 60f })
            {
                Box(p.Root, "LightningMast", new Vector3(0.7f, 64, 0.7f), new Vector3(x, 32, 50), new Color(0.78f, 0.78f, 0.8f));
                Box(p.Root, "MastTip", new Vector3(0.25f, 6, 0.25f), new Vector3(x, 67, 50), new Color(0.6f, 0.6f, 0.62f));
            }
            Box(p.Root, "WaterTowerLegs", new Vector3(8, 22, 8), new Vector3(-90, 11, -40), new Color(0.55f, 0.56f, 0.58f));
            Box(p.Root, "WaterTank", new Vector3(14, 10, 14), new Vector3(-90, 27, -40), new Color(0.9f, 0.9f, 0.88f));
            Box(p.Root, "Hangar", new Vector3(60, 25, 40), new Vector3(140, 12.5f, -380), new Color(0.8f, 0.81f, 0.83f));
            Box(p.Root, "HangarDoor", new Vector3(40, 18, 0.5f), new Vector3(140, 9, -359.7f), new Color(0.35f, 0.38f, 0.45f));
            Box(p.Root, "Blockhouse", new Vector3(18, 6, 12), new Vector3(-120, 3, 160), new Color(0.7f, 0.7f, 0.68f));
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

        /// <summary>Flat ground quad with real-world UVs (uvPerMetre), so textures keep a believable scale.</summary>
        static void Quad(Transform parent, string name, float size, float y, Material mat, float uvPerMetre)
        {
            float h = size / 2, u = size * uvPerMetre;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[] { Vector2.zero, new Vector2(0, u), new Vector2(u, u), new Vector2(u, 0) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Open lattice launch tower: four legs, a ring every 3 m, X-bracing, and a service arm at the top.</summary>
        static void Truss(Transform parent, Vector3 basePos, float height, float width, Material mat)
        {
            var root = new GameObject("Tower").transform;
            root.SetParent(parent, false); root.localPosition = basePos;
            float w = width / 2;
            void Bar(Vector3 a, Vector3 b, float t)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = (a + b) / 2;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
                go.transform.localScale = new Vector3(t, (b - a).magnitude, t);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            var corners = new[] { new Vector3(-w, 0, -w), new Vector3(w, 0, -w), new Vector3(w, 0, w), new Vector3(-w, 0, w) };
            foreach (var c in corners) Bar(c, c + Vector3.up * height, 0.35f);
            for (float y = 0; y < height; y += 3f)
                for (int i = 0; i < 4; i++)
                {
                    var a = corners[i] + Vector3.up * y; var b = corners[(i + 1) % 4] + Vector3.up * y;
                    Bar(a, b, 0.18f);
                    if (y + 3f <= height) Bar(a, b + Vector3.up * 3f, 0.12f);
                }
            // Service arm reaching toward the rocket, with a walkway.
            Bar(new Vector3(-w, 14f, 0), new Vector3(-basePos.x + 1.2f, 14f, -basePos.z), 0.5f);
            Bar(new Vector3(-w, 12.8f, 0), new Vector3(-basePos.x + 1.6f, 12.8f, -basePos.z), 0.15f);
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
