using System.Collections.Generic;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>Procedural meshes. Everything is a lathe (profile spun around Y), origin at the bottom.</summary>
    public static class MeshBuilder
    {
        /// <summary>
        /// Spin a profile (x = radius, y = height, bottom to top) around the Y axis.
        /// Hard edges between profile segments; smooth around the circle.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 32, float sweepDeg = 360f,
                                 float startDeg = 0f, bool capBottom = false, bool capTop = false,
                                 bool doubleSided = false, string name = "lathe")
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float sweep = sweepDeg * Mathf.Deg2Rad, start = startDeg * Mathf.Deg2Rad;

            for (int i = 0; i < profile.Count - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-10f) continue;
                Vector2 n2 = new Vector2(d.y, -d.x).normalized; // outward for an upward profile
                int baseIndex = v.Count;
                for (int j = 0; j <= segments; j++)
                {
                    float ang = start + sweep * j / segments;
                    float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    var normal = new Vector3(n2.x * c, n2.y, n2.x * s);
                    v.Add(new Vector3(a.x * c, a.y, a.x * s)); n.Add(normal); uv.Add(new Vector2((float)j / segments, a.y));
                    v.Add(new Vector3(b.x * c, b.y, b.x * s)); n.Add(normal); uv.Add(new Vector2((float)j / segments, b.y));
                }
                for (int j = 0; j < segments; j++)
                {
                    int i0 = baseIndex + j * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    AddQuad(v, t, i0, i2, i3, i1, n[i0]);
                }
            }

            if (capBottom) AddCap(v, n, uv, t, profile[0], segments, start, sweep, Vector3.down);
            if (capTop) AddCap(v, n, uv, t, profile[profile.Count - 1], segments, start, sweep, Vector3.up);

            if (doubleSided)
            {
                int count = v.Count, triCount = t.Count;
                for (int i = 0; i < count; i++) { v.Add(v[i]); n.Add(-n[i]); uv.Add(uv[i]); }
                for (int i = 0; i < triCount; i += 3)
                {
                    t.Add(t[i] + count); t.Add(t[i + 2] + count); t.Add(t[i + 1] + count);
                }
            }

            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents(); // normal maps (panel seams, rivets) need tangents
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Two triangles with winding chosen so the face normal matches `normal`.</summary>
        static void AddQuad(List<Vector3> v, List<int> t, int a, int b, int c, int d, Vector3 normal)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (face.sqrMagnitude < 1e-12f) face = Vector3.Cross(v[c] - v[a], v[d] - v[a]);
            if (Vector3.Dot(face, normal) >= 0)
            {
                t.Add(a); t.Add(b); t.Add(c);
                t.Add(a); t.Add(c); t.Add(d);
            }
            else
            {
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(a); t.Add(d); t.Add(c);
            }
        }

        static void AddCap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                           Vector2 p, int segments, float start, float sweep, Vector3 normal)
        {
            if (p.x < 1e-5f) return;
            int center = v.Count;
            v.Add(new Vector3(0, p.y, 0)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float ang = start + sweep * j / segments;
                v.Add(new Vector3(p.x * Mathf.Cos(ang), p.y, p.x * Mathf.Sin(ang)));
                n.Add(normal);
                uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang)));
            }
            for (int j = 0; j < segments; j++)
            {
                int a = center, b = center + 1 + j, c = center + 2 + j;
                var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(face, normal) >= 0) { t.Add(a); t.Add(b); t.Add(c); }
                else { t.Add(a); t.Add(c); t.Add(b); }
            }
        }

        public static Mesh Cylinder(float radius, float height, int segments = 32, bool caps = true) =>
            Lathe(new[] { new Vector2(radius, 0), new Vector2(radius, height) }, segments, capBottom: caps, capTop: caps, name: "cylinder");

        public static Mesh Frustum(float r0, float r1, float height, int segments = 32, bool caps = true) =>
            Lathe(new[] { new Vector2(r0, 0), new Vector2(r1, height) }, segments, capBottom: caps, capTop: caps, name: "frustum");

        /// <summary>Engine bell, exit at y = 0, throat and chamber above.</summary>
        public static Mesh EngineBell(float exitRadius, float length, int segments = 24)
        {
            var p = new List<Vector2>();
            float throat = exitRadius * 0.35f, bellLen = length * 0.75f;
            for (int i = 0; i <= 8; i++)
            {
                float k = i / 8f;                       // 0 at exit, 1 at throat
                float r = Mathf.Lerp(exitRadius, throat, Mathf.Pow(k, 0.6f));
                p.Add(new Vector2(r, bellLen * k));
            }
            p.Add(new Vector2(throat * 1.6f, bellLen + length * 0.08f));
            p.Add(new Vector2(throat * 1.6f, length));
            return Lathe(p, segments, capTop: true, doubleSided: true, name: "bell");
        }

        /// <summary>Ogive nose (cylinder base then a curved taper to a point).</summary>
        public static Mesh Ogive(float radius, float cylinderLength, float noseLength, int segments = 32,
                                 float sweepDeg = 360f, float startDeg = 0f)
        {
            var p = new List<Vector2> { new Vector2(radius, 0), new Vector2(radius, cylinderLength) };
            for (int i = 1; i <= 10; i++)
            {
                float k = i / 10f;
                p.Add(new Vector2(radius * Mathf.Sqrt(1 - k * k), cylinderLength + noseLength * k));
            }
            return Lathe(p, segments, sweepDeg, startDeg, doubleSided: true, name: "ogive");
        }

        /// <summary>Ring with a round cross-section (torus).</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int segments = 40, int sides = 12)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= sides; i++)
            {
                float a = -Mathf.PI / 2 + 2 * Mathf.PI * i / sides; // counter-clockwise so normals face out
                p.Add(new Vector2(majorRadius + minorRadius * Mathf.Cos(a), minorRadius + minorRadius * Mathf.Sin(a)));
            }
            return Lathe(p, segments, name: "torus");
        }
    }
}
