using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>Generated environment maps: a studio reflection cubemap, a star field and a procedural Earth.</summary>
    public static class LookCubemaps
    {
        public const string LookDir = "Assets/_TMinus/Resources/Looks";

        static Vector3 FaceDir(CubemapFace f, float u, float v)
        {
            // u, v in [-1, 1], v down (cubemap convention).
            switch (f)
            {
                case CubemapFace.PositiveX: return new Vector3(1, -v, -u).normalized;
                case CubemapFace.NegativeX: return new Vector3(-1, -v, u).normalized;
                case CubemapFace.PositiveY: return new Vector3(u, 1, v).normalized;
                case CubemapFace.NegativeY: return new Vector3(u, -1, -v).normalized;
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1).normalized;
                default: return new Vector3(-u, -v, -1).normalized;
            }
        }

        static Cubemap SaveCube(string name, int size, TextureFormat fmt, Func<Vector3, Color> sample)
        {
            Directory.CreateDirectory(LookDir);
            string path = $"{LookDir}/{name}.cubemap";
            var cube = new Cubemap(size, fmt, true);
            var px = new Color[size * size];
            foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        px[y * size + x] = sample(FaceDir(f, (x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1));
                cube.SetPixels(px, f);
            }
            cube.Apply(true);
            var old = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (old != null) { EditorUtility.CopySerialized(cube, old); EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(cube); return old; }
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        static float Box(Vector3 d, Vector3 centre, float halfW, float halfH)
        {
            centre.Normalize();
            var right = Vector3.Cross(Vector3.up, centre).normalized; if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            var up = Vector3.Cross(centre, right);
            float along = Vector3.Dot(d, centre); if (along <= 0) return 0;
            float x = Vector3.Dot(d, right) / along, y = Vector3.Dot(d, up) / along;
            // Soft-edged rectangle (a studio soft box).
            return Mathf.Clamp01((halfW - Mathf.Abs(x)) * 12) * Mathf.Clamp01((halfH - Mathf.Abs(y)) * 12);
        }

        /// <summary>Studio/hangar reflections: dark floor, soft horizon, two big soft boxes and cool ceiling lights (HDR).</summary>
        public static Cubemap StudioReflection() => SaveCube("StudioReflection", 128, TextureFormat.RGBAHalf, d =>
        {
            float t = d.y;
            Color c = t < 0 ? Color.Lerp(new Color(0.16f, 0.16f, 0.18f), new Color(0.05f, 0.05f, 0.06f), -t)
                            : Color.Lerp(new Color(0.32f, 0.35f, 0.42f), new Color(0.55f, 0.6f, 0.72f), t);
            c += new Color(1f, 0.97f, 0.92f) * 5f * Box(d, new Vector3(0.6f, 0.75f, -0.4f), 0.45f, 0.25f);
            c += new Color(0.85f, 0.9f, 1f) * 2.5f * Box(d, new Vector3(-0.8f, 0.35f, 0.5f), 0.3f, 0.5f);
            c += new Color(1f, 0.6f, 0.3f) * 0.8f * Box(d, new Vector3(0.1f, 0.1f, 1f), 0.6f, 0.06f); // warm strip
            if (t > 0.85f) c += new Color(0.9f, 0.95f, 1f) * 2f * Mathf.Clamp01(Mathf.Cos(d.x * 40) * Mathf.Cos(d.z * 40) * 4 - 3);
            c.a = 1; return c;
        });

        /// <summary>Star field with a faint Milky Way band (seeded, so rebuilds are identical).</summary>
        public static Cubemap Stars()
        {
            var band = new Vector3(0.3f, 0.9f, 0.3f).normalized;
            return SaveCube("Stars", 512, TextureFormat.RGBA32, d =>
            {
                float milky = Mathf.Exp(-Mathf.Pow(Vector3.Dot(d, band) * 6f, 2)) * (0.5f + 0.5f * Mathf.PerlinNoise(d.x * 9 + 3, d.z * 9 + d.y * 4));
                var c = new Color(0.012f, 0.014f, 0.025f) + new Color(0.07f, 0.065f, 0.08f) * milky;
                return c;
            });
        }

        /// <summary>Paints the stars into the star cubemap as small dots (done after the base pass).</summary>
        public static void PaintStars(Cubemap cube)
        {
            var rnd = new System.Random(1969);
            int size = cube.width;
            foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                var px = cube.GetPixels(f);
                for (int i = 0; i < 1500; i++)
                {
                    int x = rnd.Next(size), y = rnd.Next(size);
                    float m = Mathf.Pow((float)rnd.NextDouble(), 5f);
                    float b = 0.25f + 0.75f * m;
                    var tint = rnd.NextDouble() < 0.2 ? new Color(1f, 0.85f, 0.7f) : rnd.NextDouble() < 0.3 ? new Color(0.75f, 0.85f, 1f) : Color.white;
                    px[y * size + x] = tint * b;
                    if (m > 0.93f)
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int xx = Mathf.Clamp(x + dx, 0, size - 1), yy = Mathf.Clamp(y + dy, 0, size - 1);
                            { var o = px[yy * size + xx]; var g = tint * b * 0.4f; px[yy * size + xx] = new Color(Mathf.Max(o.r, g.r), Mathf.Max(o.g, g.g), Mathf.Max(o.b, g.b), 1); }
                        }
                }
                cube.SetPixels(px, f);
            }
            cube.Apply(true);
            EditorUtility.SetDirty(cube);
        }

        // ---------------------------------------------------------------- Earth (equirectangular)

        static float Fbm(Vector3 p, int oct)
        {
            float a = 0, amp = 0.5f, f = 1;
            for (int i = 0; i < oct; i++)
            {
                // Triplanar-ish 3D noise from three 2D slices: no seam at the date line, no pinch at the poles.
                float n = (Mathf.PerlinNoise(p.x * f + 11, p.y * f + 7) + Mathf.PerlinNoise(p.y * f + 3, p.z * f + 19) + Mathf.PerlinNoise(p.z * f + 23, p.x * f + 5)) / 3f;
                a += amp * n; amp *= 0.5f; f *= 2.03f;
            }
            return a;
        }

        public static void Earth(string path, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = (y + 0.5f) / h * Mathf.PI - Mathf.PI / 2, aLat = Mathf.Abs(lat) * Mathf.Rad2Deg;
                for (int x = 0; x < w; x++)
                {
                    float lon = (x + 0.5f) / w * Mathf.PI * 2;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * 2.2f;
                    var warp = new Vector3(Fbm(p + new Vector3(4, 1, 2), 3), Fbm(p + new Vector3(8, 3, 5), 3), Fbm(p + new Vector3(1, 9, 7), 3));
                    float land = Fbm(p + warp * 1.6f, 6) - 0.04f * Mathf.Clamp01((aLat - 60) / 25f);
                    float sea = 0.515f;
                    Color c;
                    if (land < sea)
                    {
                        float depth = Mathf.Clamp01((sea - land) * 9);
                        c = Color.Lerp(new Color(0.09f, 0.33f, 0.45f), new Color(0.02f, 0.08f, 0.22f), Mathf.Sqrt(depth));
                    }
                    else
                    {
                        float hgt = Mathf.Clamp01((land - sea) * 5);
                        float band = Mathf.Clamp01(1 - Mathf.Abs(aLat - 24) / 16f);
                        float dry = Mathf.Clamp01(band * 1.4f * (Fbm(p * 1.3f + warp + Vector3.one * 30, 4) - 0.3f) * 3.2f);
                        Color green = Color.Lerp(new Color(0.16f, 0.30f, 0.12f), new Color(0.30f, 0.36f, 0.18f), aLat / 60f);
                        Color desert = new Color(0.72f, 0.60f, 0.42f);
                        c = Color.Lerp(green, desert, Mathf.Clamp01(dry));
                        c = Color.Lerp(c, new Color(0.45f, 0.40f, 0.34f), Mathf.Clamp01(hgt - 0.5f) * 1.6f); // mountains
                        if (aLat > 58) c = Color.Lerp(c, new Color(0.55f, 0.55f, 0.5f), Mathf.Clamp01((aLat - 58) / 10f)); // tundra
                    }
                    if (aLat > 72 - 4 * Fbm(p * 3, 2)) c = new Color(0.93f, 0.95f, 0.98f); // ice caps
                    // Clouds: swirled bands, more at the ITCZ and mid-latitudes.
                    float cl = Fbm(p * 1.7f + warp * 2.5f + new Vector3(50, 0, 0), 5);
                    float belt = 0.75f + 0.25f * Mathf.Cos(lat * 6);
                    float cloud = Mathf.Clamp01((cl * belt - 0.37f) * 3.5f);
                    c = Color.Lerp(c, new Color(0.96f, 0.97f, 1f), cloud * 0.85f);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px); tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToJPG(92));
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096; imp.anisoLevel = 4; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
    }
}
