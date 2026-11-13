using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Procedural surface textures for the look pass (no paid or downloaded assets): panel seams + rivets,
    /// spray-foam bumps, brushed metal, concrete tiles, circuit boards, launch-site ground. Saved as PNGs.
    /// </summary>
    public static class LookTextures
    {
        public const string Dir = "Assets/_TMinus/Art/Textures";

        const float T = 512f; // texture size: noise wraps over it so tiles meet without seams

        /// <summary>Tileable fBm: four offset samples blended bilinearly across the tile.</summary>
        static float Noise(int x, int y, float sx, float sy, int octaves = 4)
        {
            float u = x / T, v = y / T;
            return Raw(x / sx, y / sy, octaves) * (1 - u) * (1 - v) + Raw((x - T) / sx, y / sy, octaves) * u * (1 - v)
                 + Raw(x / sx, (y - T) / sy, octaves) * (1 - u) * v + Raw((x - T) / sx, (y - T) / sy, octaves) * u * v;
        }

        static float Raw(float x, float y, int octaves)
        {
            float a = 0, amp = 0.5f, f = 1;
            for (int i = 0; i < octaves; i++) { a += amp * Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 9.1f); amp *= 0.5f; f *= 2; }
            return a;
        }

        /// <summary>Writes a PNG and sets the importer (normal map or sRGB colour, repeat wrap). Returns the asset path.</summary>
        static string Save(string name, int size, Func<int, int, Color> pixel, bool normal)
        {
            Directory.CreateDirectory(Dir);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = pixel(x, y);
            tex.SetPixels(px); tex.Apply();
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return path;
        }

        /// <summary>Normal map from a height function (tangent space, +Y up in the texture).</summary>
        static string SaveNormal(string name, int size, Func<int, int, float> height, float strength)
        {
            return Save(name, size, (x, y) =>
            {
                float hL = height((x - 1 + size) % size, y), hR = height((x + 1) % size, y);
                float hD = height(x, (y - 1 + size) % size), hU = height(x, (y + 1) % size);
                var n = new Vector3((hL - hR) * strength, (hD - hU) * strength, 1).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }, true);
        }

        // ---------------------------------------------------------------- patterns (heights 0..1)

        /// <summary>Riveted skin panels: 2 x 2 panels per tile, seams sunk, rivets raised along each seam.</summary>
        static float PanelHeight(int x, int y, int size)
        {
            int cell = size / 2;
            int px = x % cell, py = y % cell;
            float seam = Mathf.Min(Mathf.Min(px, cell - 1 - px), Mathf.Min(py, cell - 1 - py));
            float h = seam < 1.5f ? 0f : 1f;
            // Rivets: small domes 6 px in from each seam, every 16 px.
            foreach (var d in new[] { 6, cell - 7 })
            {
                float rx = Mathf.Abs(px - d), ry = Mathf.Abs(py - d);
                float along = Mathf.Abs(((py + 8) % 16) - 8), alongX = Mathf.Abs(((px + 8) % 16) - 8);
                float r1 = Mathf.Sqrt(rx * rx + along * along), r2 = Mathf.Sqrt(ry * ry + alongX * alongX);
                float r = Mathf.Min(r1, r2);
                if (r < 2.2f) h = 1f + 0.6f * (1f - r / 2.2f);
            }
            return h;
        }

        static float TraceHeight(int x, int y, int size)
        {
            // Copper traces on a 16 px grid with random on/off segments, pads at junctions.
            int g = 16;
            int cx = x / g, cy = y / g, lx = x % g, ly = y % g;
            uint hash = (uint)(cx * 73856093 ^ cy * 19349663);
            bool hTrace = (hash & 1) == 1 && Mathf.Abs(ly - g / 2) < 2;
            bool vTrace = (hash & 2) == 2 && Mathf.Abs(lx - g / 2) < 2;
            bool pad = (hash & 12) == 12 && Mathf.Abs(lx - g / 2) < 4 && Mathf.Abs(ly - g / 2) < 4;
            return pad ? 1f : (hTrace || vTrace) ? 0.7f : 0f;
        }

        public static void BuildAll()
        {
            const int S = 512;
            // Painted / bare skin panels (albedo multiplies the material colour, so one map serves every paint).
            Save("T_Panels_A", S, (x, y) =>
            {
                float h = PanelHeight(x, y, S);
                float grime = 0.93f + 0.07f * Noise(x, y, 90f, 90f);
                float v = (h < 0.5f ? 0.62f : 1f) * grime;
                return new Color(v, v, v, 1);
            }, false);
            SaveNormal("T_Panels_N", S, (x, y) => PanelHeight(x, y, S), 2.2f);

            // Spray-on foam insulation (Saturn/Falcon style orange): lumpy, irregular.
            Save("T_Foam_A", S, (x, y) => { float v = 0.82f + 0.18f * Noise(x, y, 40f, 40f, 5); return new Color(v, v * 0.97f, v * 0.94f, 1); }, false);
            SaveNormal("T_Foam_N", S, (x, y) => Noise(x, y, 22f, 22f, 5), 6f);

            // Brushed metal: long streaks along X.
            SaveNormal("T_Brushed_N", S, (x, y) => Noise(x, y, 260f, 1.6f, 3), 1.6f);
            Save("T_Brushed_A", S, (x, y) => { float v = 0.86f + 0.14f * Noise(x, y, 260f, 1.6f, 3); return new Color(v, v, v, 1); }, false);

            // Concrete floor: 2 x 2 slabs, stains, sunk joints.
            Func<int, int, float> slab = (x, y) => (x % (S / 2) < 3 || y % (S / 2) < 3) ? 0f : 1f;
            Save("T_Concrete_A", S, (x, y) =>
            {
                float n = 0.78f + 0.22f * Noise(x, y, 60f, 60f, 5) - 0.08f * Mathf.Clamp01(Noise(x, y, 200f, 200f, 2) - 0.45f) * 4;
                if (slab(x, y) < 0.5f) n *= 0.55f;
                return new Color(n, n, n * 1.02f, 1);
            }, false);
            SaveNormal("T_Concrete_N", S, (x, y) => slab(x, y) * 0.8f + 0.2f * Noise(x, y, 6f, 6f, 2), 3f);

            // Circuit board: green solder mask, copper traces and pads, silkscreen dots.
            Save("T_PCB_A", S, (x, y) =>
            {
                float t = TraceHeight(x, y, S);
                if (t > 0.9f) return new Color(0.85f, 0.72f, 0.35f, 1);           // gold pad
                if (t > 0.5f) return new Color(0.45f, 0.75f, 0.45f, 1);          // trace under mask
                return new Color(0.75f, 0.9f, 0.78f, 1) * (0.92f + 0.08f * Noise(x, y, 30f, 30f));
            }, false);
            SaveNormal("T_PCB_N", S, (x, y) => TraceHeight(x, y, S), 2.5f);

            // Soft round glow for flame and explosion particles (alpha falls off smoothly to the edge).
            string glow = Save("T_Glow", 128, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 63.5f;
                float a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.2f);
                return new Color(1, 1, 1, a);
            }, false);
            var gi = (TextureImporter)AssetImporter.GetAtPath(glow);
            gi.alphaIsTransparency = true; gi.wrapMode = TextureWrapMode.Clamp; gi.SaveAndReimport();

            // Launch-site ground: scrub grass and sand.
            Save("T_Ground_A", S, (x, y) =>
            {
                float n = Noise(x, y, 70f, 70f, 5), d = Noise(x, y, 9f, 9f, 3);
                var grass = Color.Lerp(new Color(0.30f, 0.37f, 0.20f), new Color(0.42f, 0.45f, 0.26f), d);
                var sand = Color.Lerp(new Color(0.62f, 0.56f, 0.42f), new Color(0.70f, 0.64f, 0.50f), d);
                return Color.Lerp(grass, sand, Mathf.SmoothStep(0, 1, (n - 0.42f) * 5f));
            }, false);
            SaveNormal("T_Ground_N", S, (x, y) => Noise(x, y, 5f, 5f, 3), 2f);
        }
    }
}
