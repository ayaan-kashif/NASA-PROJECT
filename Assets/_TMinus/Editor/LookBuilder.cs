using System.IO;
using TMinus.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Tools > T-Minus > Build Look (idempotent): textured materials, studio reflections, sky + star skyboxes,
    /// procedural Earth, atmosphere glow, ground, post-processing profiles and 4x MSAA. Runtime: Core/SceneLook.
    /// </summary>
    public static class LookBuilder
    {
        const string Tex = LookTextures.Dir;
        const string Looks = LookCubemaps.LookDir;

        [MenuItem("Tools/T-Minus/Build Look", priority = 11)]
        public static void Build()
        {
            MaterialBuilder.Build();
            LookTextures.BuildAll();
            AssetDatabase.Refresh();

            Surface(MaterialLibrary.BodyWhite, "Panels", new Vector2(4, 0.6f), 0.6f, 0.5f, 0f);
            Surface(MaterialLibrary.BandBlack, "Panels", new Vector2(4, 0.6f), 0.4f, 0.35f, 0f);
            Surface(MaterialLibrary.InsulationOrange, "Foam", new Vector2(3, 1.2f), 0.9f, 0.18f, 0f);
            Surface(MaterialLibrary.MetalGrey, "Brushed", new Vector2(2, 2), 0.25f, 0.55f, 0.85f);
            Surface(MaterialLibrary.EngineDark, "Brushed", new Vector2(2, 2), 0.3f, 0.5f, 0.8f);
            Surface(MaterialLibrary.Concrete, "Concrete", new Vector2(10, 10), 0.6f, 0.15f, 0f);
            Surface(MaterialLibrary.Wall, "Panels", new Vector2(8, 4), 0.35f, 0.3f, 0.1f);
            Surface(MaterialLibrary.GantryRed, "Brushed", new Vector2(1, 4), 0.25f, 0.38f, 0.3f);
            var board = Surface(MaterialLibrary.BoardGreen, "PCB", new Vector2(1, 1), 0.5f, 0.62f, 0.1f);
            board.SetColor("_BaseColor", new Color(0.28f, 0.58f, 0.36f));

            Directory.CreateDirectory(Looks);
            LookCubemaps.StudioReflection();
            LookCubemaps.PaintStars(LookCubemaps.Stars());
            LookCubemaps.Earth("Assets/_TMinus/Resources/Earth/EarthProcedural.jpg", 2048, 1024);

            // NASA Blue Marble (if present): full 4096 px, wraps east-west.
            if (AssetImporter.GetAtPath("Assets/_TMinus/Resources/Earth/BlueMarble.jpg") is TextureImporter bm)
            { bm.maxTextureSize = 4096; bm.wrapModeU = TextureWrapMode.Repeat; bm.wrapModeV = TextureWrapMode.Clamp; bm.anisoLevel = 4; bm.SaveAndReimport(); }

            var sky = Mat("Sky_Earth", "Skybox/Procedural");
            sky.SetFloat("_SunDisk", 2); sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_SunSizeConvergence", 6);
            sky.SetFloat("_AtmosphereThickness", 1.05f); sky.SetColor("_SkyTint", new Color(0.48f, 0.52f, 0.6f));
            sky.SetColor("_GroundColor", new Color(0.36f, 0.38f, 0.33f)); sky.SetFloat("_Exposure", 1.25f);
            var stars = Mat("Sky_Stars", "Skybox/Cubemap");
            stars.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>($"{Looks}/Stars.cubemap"));
            stars.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f)); stars.SetFloat("_Exposure", 1.6f);
            var atmo = Mat("Atmosphere", "TMinus/AtmosphereRim");
            atmo.SetColor("_Color", new Color(0.32f, 0.58f, 1f)); atmo.SetFloat("_Power", 7f); atmo.SetFloat("_Intensity", 1.8f);
            var ground = Mat("Ground", "Universal Render Pipeline/Lit");
            Textures(ground, "Ground", Vector2.one, 0.7f, 0.12f, 0f);

            Profile("Look_Interior", contrast: 14, saturation: 8, exposure: 0.15f, bloom: 0.55f, vignette: 0.24f, temperature: -4);
            Profile("Look_Flight", contrast: 10, saturation: 14, exposure: 0f, bloom: 0.9f, vignette: 0.16f, temperature: 0);

            // The PC quality level's pipeline asset (there is no project-wide default asset here).
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pc != null) { pc.msaaSampleCount = 4; EditorUtility.SetDirty(pc); }

            AssetDatabase.SaveAssets();
            MaterialBuilder.EnsureGlowKeywords();
            Debug.Log("[T-Minus] Look built: textured materials, reflections, skies, Earth, post-processing, MSAA 4x.");
        }

        static Material Surface(string libName, string tex, Vector2 tiling, float bump, float smooth, float metal)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_TMinus/Art/Materials/{libName}.mat");
            Textures(mat, tex, tiling, bump, smooth, metal);
            return mat;
        }

        static void Textures(Material mat, string tex, Vector2 tiling, float bump, float smooth, float metal)
        {
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/T_{tex}_A.png"));
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/T_{tex}_N.png");
            mat.SetTexture("_BumpMap", n); mat.SetFloat("_BumpScale", bump);
            if (n != null) mat.EnableKeyword("_NORMALMAP");
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(mat);
        }

        static Material Mat(string name, string shaderName)
        {
            string path = $"{Looks}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Profile(string name, float contrast, float saturation, float exposure, float bloom, float vignette, float temperature)
        {
            string path = $"{Looks}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(contrast); ca.saturation.Override(saturation); ca.postExposure.Override(exposure);
            var b = p.Add<Bloom>(true); b.intensity.Override(bloom); b.threshold.Override(0.95f); b.scatter.Override(0.7f);
            b.highQualityFiltering.Override(true);
            var v = p.Add<Vignette>(true); v.intensity.Override(vignette); v.smoothness.Override(0.45f);
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(temperature);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f)); smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.97f, 0f));
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
        }
    }
}
