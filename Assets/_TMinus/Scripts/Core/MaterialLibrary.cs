using System.Collections.Generic;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Shared URP materials by name (Body_White, Band_Black, Insulation_Orange, Metal_Grey, ...).
    /// The asset lives at Resources/TMinusMaterials (made by Tools > T-Minus > Build Materials).
    /// Blender FBX materials with the same names are remapped to these.
    /// </summary>
    [CreateAssetMenu(menuName = "T-Minus/Material Library")]
    public sealed class MaterialLibrary : ScriptableObject
    {
        public const string ResourcePath = "TMinusMaterials";

        public const string BodyWhite = "Body_White";
        public const string BandBlack = "Band_Black";
        public const string InsulationOrange = "Insulation_Orange";
        public const string MetalGrey = "Metal_Grey";
        public const string EngineDark = "Engine_Dark";
        public const string Concrete = "Floor_Concrete";
        public const string Wall = "Hall_Wall";
        public const string GantryRed = "Gantry_Red";
        public const string BoardGreen = "Board_Green";
        public const string GhostValid = "Ghost_Valid";
        public const string GhostInvalid = "Ghost_Invalid";
        public const string XRay = "XRay_Shell";
        public const string CableUnlit = "Cable_Unlit";

        public List<Material> materials = new List<Material>();

        static MaterialLibrary _instance;
        static readonly Dictionary<string, Material> Fallbacks = new Dictionary<string, Material>();

        public static MaterialLibrary Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<MaterialLibrary>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>Material from the library only (null if the name isn't one of ours, e.g. Blender's "Material").</summary>
        public static Material Find(string name)
        {
            var lib = Instance;
            if (lib == null || string.IsNullOrEmpty(name)) return null;
            foreach (var m in lib.materials) if (m != null && m.name == name) return m;
            return null;
        }

        /// <summary>Material by name; falls back to a runtime URP Lit material so nothing renders pink.</summary>
        public static Material Get(string name)
        {
            var lib = Instance;
            if (lib != null)
                foreach (var m in lib.materials)
                    if (m != null && m.name == name) return m;

            if (Fallbacks.TryGetValue(name, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name, color = DefaultColor(name) };
            Fallbacks[name] = mat;
            return mat;
        }

        public static Color DefaultColor(string name)
        {
            switch (name)
            {
                case BodyWhite: return new Color(0.92f, 0.92f, 0.9f);
                case BandBlack: return new Color(0.06f, 0.06f, 0.07f);
                case InsulationOrange: return new Color(0.85f, 0.42f, 0.12f);
                case MetalGrey: return new Color(0.55f, 0.57f, 0.6f);
                case EngineDark: return new Color(0.18f, 0.17f, 0.17f);
                case Concrete: return new Color(0.42f, 0.43f, 0.45f);
                case Wall: return new Color(0.2f, 0.23f, 0.3f);
                case GantryRed: return new Color(0.62f, 0.16f, 0.12f);
                case BoardGreen: return new Color(0.08f, 0.38f, 0.2f);
                case GhostValid: return new Color(0.2f, 1f, 0.45f, 0.35f);
                case GhostInvalid: return new Color(1f, 0.25f, 0.2f, 0.35f);
                case XRay: return new Color(0.6f, 0.8f, 1f, 0.15f);
                case CableUnlit: return Color.white;
                default: return Color.magenta;
            }
        }

        public static bool IsTransparent(string name) =>
            name == GhostValid || name == GhostInvalid || name == XRay;
    }
}
