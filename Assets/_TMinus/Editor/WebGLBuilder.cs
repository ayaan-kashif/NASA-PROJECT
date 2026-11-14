using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Tools > T-Minus > Build WebGL: a browser build in Builds/WebGL (index.html at the root), set up for plain
    /// static hosts such as GitHub Pages or itch.io (gzip + decompression fallback, so no server headers needed).
    /// Summary in Logs/build-webgl.txt.
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutDir = "Builds/WebGL";

        [MenuItem("Tools/T-Minus/Build WebGL", priority = 201)]
        public static void Build()
        {
            MaterialBuilder.EnsureGlowKeywords();
            PlayerSettings.productName = "T-MINUS";
            PlayerSettings.companyName = "T-MINUS Team";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // works where the host can't send Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            // An engine package's generated bindings.cpp uses Emscripten's embind, which isn't linked by default. Only the Unity AI packages need it,
            // so a release build made without them (tools/build_release.sh) skips it (audit A-11).
            bool aiPackages = File.Exists("Packages/manifest.json") && File.ReadAllText("Packages/manifest.json").Contains("com.unity.ai.");
#pragma warning disable 618
            if (aiPackages && !(PlayerSettings.WebGL.emscriptenArgs ?? "").Contains("-lembind"))
                PlayerSettings.WebGL.emscriptenArgs = ((PlayerSettings.WebGL.emscriptenArgs ?? "") + " -lembind").Trim();
#pragma warning restore 618

            // Keep URP's post-processing shaders: when they were stripped, the web build drew a black 3D view.
            var urpGlobal = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            if (urpGlobal != null)
            {
                var so = new SerializedObject(urpGlobal);
                // The committed asset is the single source of truth (audit A-34): if someone turned stripping back on, say so and stop
                // instead of quietly editing project settings as a side effect of a build.
                var it = so.GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.Boolean && it.name.StartsWith("m_StripUnusedPostProcessing") && it.boolValue)
                    {
                        Debug.LogError("[T-Minus] Post-processing shader stripping is ON in UniversalRenderPipelineGlobalSettings. The web build would show a black 3D view. " +
                                       "Turn it off (Project Settings > Graphics > URP Global Settings > Shader Stripping) and build again.");
                        return;
                    }
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = OutDir, target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.None
            });
            var s = report.summary;
            string text = $"result={s.result} errors={s.totalErrors} size={s.totalSize / (1024 * 1024)} MB time={s.totalTime.TotalSeconds:0}s\noutput={Path.GetFullPath(OutDir)}\n";
            foreach (var step in report.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    text += $"ERROR [{step.name}] {m.content}\n";
            Directory.CreateDirectory("Logs");
            // Compare with the previous build so a size jump (for example after changing shader stripping, audit A-35 / I16) is visible at once.
            long mb = (long)(s.totalSize / (1024 * 1024));
            const string sizeFile = "Logs/build-webgl-size.txt";
            if (File.Exists(sizeFile) && long.TryParse(File.ReadAllText(sizeFile).Trim(), out long previousMb))
            {
                long diff = mb - previousMb;
                text += $"size compared with the previous build: {previousMb} MB -> {mb} MB ({(diff >= 0 ? "+" : "")}{diff} MB)\n";
                if (diff > 5) text += "WARNING: the build grew by more than 5 MB. If you just changed shader stripping, decide whether the cost is worth it.\n";
            }
            File.WriteAllText(sizeFile, mb.ToString());
            File.WriteAllText("Logs/build-webgl.txt", text);
            Debug.Log("[T-Minus] WebGL build: " + text);
        }
    }
}
