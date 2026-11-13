using System.IO;
using TMinus.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build Materials: URP Lit materials + Resources/TMinusMaterials library.</summary>
    public static class MaterialBuilder
    {
        const string Dir = "Assets/_TMinus/Art/Materials";
        const string LibraryPath = "Assets/_TMinus/Resources/TMinusMaterials.asset";

        static readonly string[] Names =
        {
            MaterialLibrary.BodyWhite, MaterialLibrary.BandBlack, MaterialLibrary.InsulationOrange,
            MaterialLibrary.MetalGrey, MaterialLibrary.EngineDark, MaterialLibrary.Concrete,
            MaterialLibrary.Wall, MaterialLibrary.GantryRed, MaterialLibrary.BoardGreen,
            MaterialLibrary.GhostValid, MaterialLibrary.GhostInvalid, MaterialLibrary.XRay, MaterialLibrary.CableUnlit
        };

        /// <summary>
        /// URP's material validation keeps stripping _EMISSION from these (even with a non-black emission colour),
        /// and a build without it drops the hover/selection glow variant. Re-assert it before saving or building.
        /// </summary>
        public static void EnsureGlowKeywords()
        {
            foreach (var name in Names)
            {
                if (MaterialLibrary.IsTransparent(name) || name == MaterialLibrary.CableUnlit) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{name}.mat");
                if (m == null || m.IsKeywordEnabled("_EMISSION")) continue;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/T-Minus/Build Materials", priority = 10)]
        public static MaterialLibrary Build()
        {
            Directory.CreateDirectory(Dir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[T-Minus] URP Lit shader not found.");
                return null;
            }

            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.materials.Clear();

            foreach (var name in Names)
            {
                var path = $"{Dir}/{name}.mat";
                var wanted = name == MaterialLibrary.CableUnlit ? Shader.Find("Universal Render Pipeline/Unlit") : shader;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(wanted) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != wanted) mat.shader = wanted;
                Configure(mat, name);
                EditorUtility.SetDirty(mat);
                lib.materials.Add(mat);
            }

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        static void Configure(Material m, string name)
        {
            var c = MaterialLibrary.DefaultColor(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", name == MaterialLibrary.MetalGrey || name == MaterialLibrary.EngineDark ? 0.55f : 0.3f);
            m.SetFloat("_Metallic", name == MaterialLibrary.MetalGrey || name == MaterialLibrary.EngineDark ? 0.6f : 0f);

            // Emission on, so hover/selection can tint via property blocks. Near-black (1/255), not black:
            // URP turns _EMISSION off for pure black, and the player build would then strip the glow variant.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.004f, 0.004f, 0.004f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.enableInstancing = true;

            if (MaterialLibrary.IsTransparent(name))
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat("_Surface", 0);
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetFloat("_ZWrite", 1);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = -1;
            }
        }
    }
}
