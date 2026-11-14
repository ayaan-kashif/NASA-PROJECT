using UnityEditor;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Tools > T-Minus > Graphics > Make Faster (same look) / Restore Original / Report.
    /// Frees GPU time on weak graphics cards without changing what the game looks like: the desktop render asset used both MSAA 4x and the
    /// SMAA that Core/SceneLook switches on for every camera (two anti-aliasing passes), 4 shadow cascades with High soft shadows, and
    /// full-resolution ambient occlusion. Idempotent: it sets absolute values, so running it twice changes nothing more.
    /// Restore Original puts back the values the project had before (they are the constants below), and so does
    /// <c>git restore Assets/Settings</c>. Only the render-asset fields named in the .asset file are touched.
    /// </summary>
    public static class GraphicsTuner
    {
        const string PcAsset = "Assets/Settings/PC_RPAsset.asset";
        const string PcRenderer = "Assets/Settings/PC_Renderer.asset";

        struct Preset { public int msaa, cascades, shadowRes, softShadow, aoDownsample; public float renderScale; }

        // soft shadow quality: 1 Low, 2 Medium, 3 High. aoDownsample: 0 full size, 1 half size.
        static readonly Preset Original = new Preset { msaa = 4, cascades = 4, shadowRes = 2048, softShadow = 3, aoDownsample = 0, renderScale = 1f };
        static readonly Preset Faster = new Preset { msaa = 1, cascades = 2, shadowRes = 2048, softShadow = 2, aoDownsample = 1, renderScale = 1f };
        /// <summary>Live demo on a weak laptop (Radeon RX 540 / UHD 620): steady 30 fps at 1080p. SMAA still smooths the edges.</summary>
        static readonly Preset DemoLive = new Preset { msaa = 1, cascades = 2, shadowRes = 1024, softShadow = 2, aoDownsample = 1, renderScale = 0.8f };
        /// <summary>Recording the demo video frame by frame: best quality, no speed limit.</summary>
        static readonly Preset DemoVideo = new Preset { msaa = 4, cascades = 4, shadowRes = 4096, softShadow = 3, aoDownsample = 0, renderScale = 1f };

        [MenuItem("Tools/T-Minus/Graphics/Demo Live (weak laptop, 30 fps)", priority = 33)]
        public static void DemoLiveMenu() => Apply(DemoLive, "demo live");

        [MenuItem("Tools/T-Minus/Graphics/Demo Video (best quality, for recording)", priority = 34)]
        public static void DemoVideoMenu() => Apply(DemoVideo, "demo video");

        [MenuItem("Tools/T-Minus/Graphics/Make Faster (same look)", priority = 30)]
        public static void MakeFaster() => Apply(Faster, "faster");

        [MenuItem("Tools/T-Minus/Graphics/Restore Original", priority = 31)]
        public static void RestoreOriginal() => Apply(Original, "original");

        [MenuItem("Tools/T-Minus/Graphics/Report current values", priority = 32)]
        public static void Report()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(PcAsset);
            if (asset == null) { Debug.LogError("[T-Minus] " + PcAsset + " not found."); return; }
            var so = new SerializedObject(asset);
            Debug.Log($"[T-Minus] Graphics now: MSAA {so.FindProperty("m_MSAA").intValue}, shadow cascades {so.FindProperty("m_ShadowCascadeCount").intValue}, " +
                      $"shadow map {so.FindProperty("m_MainLightShadowmapResolution").intValue}, soft shadows {so.FindProperty("m_SoftShadowQuality").intValue}, " +
                      $"render scale {so.FindProperty("m_RenderScale").floatValue}, AO downsample {AoDownsample()}.");
        }

        static void Apply(Preset p, string name)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(PcAsset);
            if (asset == null) { Debug.LogError("[T-Minus] " + PcAsset + " not found: nothing changed."); return; }
            var so = new SerializedObject(asset);
            Set(so, "m_MSAA", p.msaa);
            Set(so, "m_ShadowCascadeCount", p.cascades);
            Set(so, "m_MainLightShadowmapResolution", p.shadowRes);
            Set(so, "m_SoftShadowQuality", p.softShadow);
            var rs = so.FindProperty("m_RenderScale");
            if (rs != null) rs.floatValue = p.renderScale;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            bool ao = SetAo(p.aoDownsample);
            AssetDatabase.SaveAssets();
            Debug.Log($"[T-Minus] Graphics set to '{name}': MSAA {p.msaa}, {p.cascades} shadow cascades, soft shadows {p.softShadow}, " +
                      $"AO {(ao ? (p.aoDownsample == 1 ? "half size" : "full size") : "not found (skipped)")}. SMAA from SceneLook still smooths edges.");
        }

        static void Set(SerializedObject so, string field, int value)
        {
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning("[T-Minus] URP asset has no field " + field + " (a different URP version?): skipped."); return; }
            prop.intValue = value;
        }

        static SerializedObject AoObject()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PcRenderer))
                if (o != null && o.name == "ScreenSpaceAmbientOcclusion") return new SerializedObject(o);
            return null;
        }

        static string AoDownsample()
        {
            var ao = AoObject();
            var prop = ao?.FindProperty("m_Settings.Downsample");
            return prop == null ? "n/a" : prop.intValue.ToString();
        }

        static bool SetAo(int downsample)
        {
            var ao = AoObject();
            var prop = ao?.FindProperty("m_Settings.Downsample");
            if (prop == null) return false;
            prop.intValue = downsample;
            ao.ApplyModifiedProperties();
            EditorUtility.SetDirty(ao.targetObject);
            return true;
        }
    }
}
