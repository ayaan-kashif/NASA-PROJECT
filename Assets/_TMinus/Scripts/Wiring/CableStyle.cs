using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;

namespace TMinus.Wiring
{
    /// <summary>
    /// Look of each cable and port type. Colour is never the only signal: every cable type also
    /// has its own pattern (solid pair, dashes, rings, stripes) and width.
    /// </summary>
    public static class CableStyle
    {
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        public static Color PortColor(string portType)
        {
            switch (portType)
            {
                case "PWR_OUT": case "PWR_IN": return new Color(0.9f, 0.22f, 0.2f);
                case "BATT": return new Color(0.95f, 0.45f, 0.15f);
                case "SOLAR_OUT": case "SOLAR_IN": return new Color(1f, 0.75f, 0.15f);
                case "DATA": return new Color(0.2f, 0.55f, 1f);
                case "RF": return new Color(0.7f, 0.72f, 0.75f);
                case "PYRO_OUT": case "PYRO_IN": return new Color(1f, 0.88f, 0.1f);
                case "ARM": return new Color(0.7f, 0.35f, 1f);
                default: return Color.white;
            }
        }

        public static string PortIcon(string portType)
        {
            switch (portType)
            {
                case "PWR_OUT": return "+ out"; case "PWR_IN": return "+ in";
                case "BATT": return "BAT"; case "SOLAR_OUT": case "SOLAR_IN": return "SUN";
                case "DATA": return "DAT"; case "RF": return "RF";
                case "PYRO_OUT": case "PYRO_IN": return "PYRO"; case "ARM": return "ARM";
                default: return portType;
            }
        }

        public static float Width(string cableType)
        {
            switch (cableType)
            {
                case "rf": return 0.012f;
                case "power": return 0.007f;
                case "pyro": return 0.008f;
                default: return 0.006f;
            }
        }

        /// <summary>Unlit material with the type's pattern texture (tiled along the line).</summary>
        public static Material Material(string cableType, bool ghost = false)
        {
            string key = cableType + (ghost ? "_ghost" : "");
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            var baseMat = MaterialLibrary.Get(MaterialLibrary.CableUnlit);
            m = new Material(baseMat) { name = "Cable_" + key, mainTexture = Pattern(cableType) };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Pattern(cableType));
            var tint = ghost ? new Color(1, 1, 1, 0.55f) : Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            Mats[key] = m;
            return m;
        }

        static Texture2D Pattern(string type)
        {
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "cable_" + type };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    switch (type)
                    {
                        case "power": c = y < h / 2 ? new Color(0.85f, 0.12f, 0.1f) : new Color(0.08f, 0.08f, 0.08f); break; // red/black pair
                        case "data": c = x < w * 0.6f ? new Color(0.15f, 0.45f, 1f) : new Color(0.75f, 0.85f, 1f); break;  // dashes
                        case "rf": c = x % 16 < 3 ? new Color(0.35f, 0.36f, 0.4f) : new Color(0.72f, 0.73f, 0.76f); break; // rings
                        case "pyro": c = ((x + y) / 4) % 2 == 0 ? new Color(1f, 0.85f, 0.05f) : new Color(0.06f, 0.06f, 0.06f); break; // stripes
                        default: c = Color.white; break;
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
