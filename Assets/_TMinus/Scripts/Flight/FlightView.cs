using System.Collections.Generic;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Flight
{
    /// <summary>
    /// Draws the flight with a floating origin: the rocket stays at (0,0,0) and the real-size Earth
    /// (radius 6,371 km) moves and turns around it. The 2D sim plane (x, y) maps to Unity (y-up) through
    /// a fixed rotation so the pad's "up" is Unity +Y. Dropped stages, fairing halves and the CubeSat keep
    /// their own inertial position so they fall behind the rocket.
    /// </summary>
    public sealed class FlightView : MonoBehaviour
    {
        public Transform earthRoot;
        public Light sun;
        public Camera cam;

        RocketView _rocket;
        FlightPad _pad;
        MeshRenderer _earthSphere;
        ParticleSystem _exhaust;
        readonly Quaternion _toUnity = Quaternion.Euler(0, 0, 90); // sim +X (pad up) -> Unity +Y
        Vec2d _simPos;
        double _omega, _simTime;
        Vector3 _padDirSim;

        sealed class Loose { public Transform t; public Vec2d pos, vel; public double t0; public Vector3 spin; }
        readonly List<Loose> _loose = new List<Loose>();

        public RocketView Rocket => _rocket;
        /// <summary>The true-scale launch site (null before Build). Only active below FlightPad.MaxAltitudeM.</summary>
        public Transform PadArea => _pad?.Root;

        public void Build(RocketDef rocket, DataRegistry data, Vec2d startPos, double latitudeDeg)
        {
            _omega = FlightConstants.EarthRotationRadS * System.Math.Cos(latitudeDeg * System.Math.PI / 180);
            _padDirSim = new Vector3((float)(startPos.x / startPos.Magnitude), (float)(startPos.y / startPos.Magnitude), 0);
            BuildEarth();
            _rocket = RocketView.Spawn(rocket, data, transform, false, 0);
            _rocket.name = "FlyingRocket";
            foreach (var p in _rocket.Parts) foreach (var c in p.GetComponentsInChildren<Collider>()) Destroy(c);
            _exhaust = MakeExhaust();
            _pad = FlightPad.Build(transform, startPos, _omega, LowestAttachedY());
            Sync(startPos, new Vec2d(), 0, 0, 0);
        }

        void BuildEarth()
        {
            float R = (float)(FlightConstants.EarthRadiusM * 0.001);
            var sphere = new GameObject("Earth");
            sphere.transform.SetParent(earthRoot, false);
            sphere.AddComponent<MeshFilter>().sharedMesh = UvSphere(R, 256, 128);
            var mat = new Material(M.Get(M.BodyWhite)) { name = "EarthMat" };
            mat.SetTexture("_BaseMap", EarthTexture());
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.35f);
            sphere.AddComponent<MeshRenderer>().sharedMaterial = mat;

            _earthSphere = sphere.GetComponent<MeshRenderer>();
        }

        ParticleSystem MakeExhaust()
        {
            var go = new GameObject("Exhaust");
            go.transform.SetParent(_rocket.transform, false);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0); // emit downwards
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.6f; main.startSpeed = 40; main.startSize = 1.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.45f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;
            var em = ps.emission; em.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 6; shape.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.4f), new GradientColorKey(Color.gray, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 2.5f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = M.Get(M.CableUnlit);
            return ps;
        }

        /// <summary>Move the world so the rocket stays at the origin; orient and light it.</summary>
        public void Sync(Vec2d pos, Vec2d vel, double time, double thrustFraction, double altitudeM)
        {
            _simPos = pos; _simTime = time;
            earthRoot.position = ToUnity((new Vec2d() - pos) * 0.001);
            earthRoot.rotation = Quaternion.AngleAxis((float)(_omega * time * Mathf.Rad2Deg), Vector3.forward);
            // Near the ground the 1/1000 globe would sit metres under the rocket: show the real-size pad instead.
            bool low = altitudeM < FlightPad.MaxAltitudeM;
            _pad?.Sync(pos, time, altitudeM, _toUnity);
            if (_earthSphere != null) _earthSphere.enabled = !low;
            RenderSettings.fog = low;
            if (low)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 2500; RenderSettings.fogEndDistance = 14000;
                RenderSettings.fogColor = new Color(0.62f, 0.75f, 0.92f);
            }

            // Nose along the air-relative velocity once moving, else straight up from the ground.
            var air = new Vec2d(-_omega * pos.y, _omega * pos.x);
            var rel = vel - air;
            var dir = rel.Magnitude > 20 ? rel.Normalized : pos.Normalized;
            var up = ToUnity(dir).normalized;
            _rocket.transform.rotation = Quaternion.Slerp(_rocket.transform.rotation, Quaternion.FromToRotation(Vector3.up, up), 0.2f);

            if (_exhaust != null)
            {
                var em = _exhaust.emission;
                em.rateOverTime = (float)(thrustFraction * 400);
                var shape = _exhaust.shape;
                // Plume widens as the air thins.
                shape.angle = Mathf.Lerp(6, 40, Mathf.Clamp01((float)(altitudeM / 60000)));
                _exhaust.transform.localPosition = new Vector3(0, LowestAttachedY() - 1f, 0);
            }

            foreach (var l in _loose)
            {
                if (l.t == null) continue;
                double dt = time - l.t0;
                var p = l.pos + l.vel * dt;
                l.t.position = ToUnity(p - pos);
                l.t.Rotate(l.spin * Time.deltaTime, Space.Self);
            }
        }

        Vector3 ToUnity(Vec2d v) => _toUnity * new Vector3((float)v.x, (float)v.y, 0);

        float LowestAttachedY()
        {
            float y = 0; bool any = false;
            foreach (var p in _rocket.Parts)
            {
                if (p == null || p.transform.parent != _rocket.transform) continue;
                float py = p.transform.localPosition.y - p.bellDepth;
                if (!any || py < y) { y = py; any = true; }
            }
            return y;
        }

        /// <summary>Let parts go: they keep the rocket's velocity minus a push and tumble away.</summary>
        public void Release(IEnumerable<Part> parts, Vec2d pos, Vec2d vel, Vector3 push)
        {
            var dir = (pos.Normalized);
            foreach (var p in parts.Where(p => p != null).ToList())
            {
                p.transform.SetParent(transform, true);
                var local = p.transform.position; // relative to the rocket at origin
                var offset = Quaternion.Inverse(_toUnity) * local;
                var pushSim = Quaternion.Inverse(_toUnity) * (_rocket.transform.rotation * push);
                _loose.Add(new Loose
                {
                    t = p.transform,
                    pos = pos + new Vec2d(offset.x, offset.y),
                    vel = vel + new Vec2d(pushSim.x, pushSim.y),
                    t0 = _simTime,
                    spin = Random.onUnitSphere * Random.Range(10, 40)
                });
            }
        }

        public IEnumerable<Part> PartsWhere(System.Func<Part, bool> f) => _rocket.Parts.Where(p => p != null && p.transform.parent == _rocket.transform && f(p));

        public void Explode()
        {
            var go = new GameObject("Breakup");
            go.transform.position = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.duration = 1; main.startLifetime = 2; main.startSpeed = 30; main.startSize = 2;
            main.startColor = new Color(1f, 0.6f, 0.2f);
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, 200) });
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = M.Get(M.CableUnlit);
            ps.Play();
            if (_exhaust != null) { var e = _exhaust.emission; e.rateOverTime = 0; }
            var all = _rocket.Parts.Where(p => p != null && p.transform.parent == _rocket.transform).ToList();
            for (int i = 0; i < all.Count; i++)
                Release(new[] { all[i] }, _simPos, new Vec2d(), Random.onUnitSphere * 8);
        }

        public Bounds RocketBounds()
        {
            var b = new Bounds(Vector3.zero, Vector3.one * 2);
            foreach (var p in _rocket.Parts)
                if (p != null && p.transform.parent == _rocket.transform) b.Encapsulate(p.WorldBounds());
            return b;
        }

        // ------------------------------------------------------------------ Earth mesh + texture

        static Mesh UvSphere(float r, int lon, int lat)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2 * Mathf.PI * x / lon;
                    var d = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                    v.Add(d * r); n.Add(d); uv.Add(new Vector2((float)x / lon, 1 - (float)y / lat));
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    t.Add(i); t.Add(i + 1); t.Add(i + lon + 1);
                    t.Add(i + 1); t.Add(i + lon + 2); t.Add(i + lon + 1);
                }
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = "EarthSphere" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Procedural Earth: oceans, land from layered noise, ice caps. Swap for NASA Blue Marble if added.</summary>
        static Texture2D EarthTexture()
        {
            var blue = Resources.Load<Texture2D>("Earth/BlueMarble");
            if (blue != null) return blue;
            const int w = 512, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "EarthProcedural", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, lat = (float)y / h;
                    float nse = Mathf.PerlinNoise(u * 6 + 3, lat * 3 + 1) * 0.6f + Mathf.PerlinNoise(u * 18, lat * 9) * 0.4f;
                    Color c = nse > 0.55f ? Color.Lerp(new Color(0.25f, 0.42f, 0.18f), new Color(0.62f, 0.55f, 0.36f), (nse - 0.55f) * 3)
                                          : Color.Lerp(new Color(0.03f, 0.12f, 0.35f), new Color(0.07f, 0.25f, 0.5f), nse);
                    if (lat < 0.07f || lat > 0.93f) c = new Color(0.92f, 0.95f, 0.98f);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
