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
            // An engine package's generated bindings.cpp uses Emscripten's embind, which isn't linked by default.
#pragma warning disable 618
            if (!(PlayerSettings.WebGL.emscriptenArgs ?? "").Contains("-lembind"))
                PlayerSettings.WebGL.emscriptenArgs = ((PlayerSettings.WebGL.emscriptenArgs ?? "") + " -lembind").Trim();
#pragma warning restore 618

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
            File.WriteAllText("Logs/build-webgl.txt", text);
            Debug.Log("[T-Minus] WebGL build: " + text);
        }
    }
}
