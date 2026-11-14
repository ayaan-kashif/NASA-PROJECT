using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build Windows: Windows x64 player in Builds/Windows/T-MINUS.exe (git-ignored). Summary in Logs/build-report.txt.</summary>
    public static class WindowsBuilder
    {
        public const string OutPath = "Builds/Windows/T-MINUS.exe";

        /// <summary>Development build for performance checks: same content, plus the frame-time probe (-perfprobe).</summary>
        [MenuItem("Tools/T-Minus/Build Windows (development, for performance tests)", priority = 202)]
        public static void BuildDevelopment() => Build("Builds/WindowsDev/T-MINUS.exe", BuildOptions.Development);

        [MenuItem("Tools/T-Minus/Build Windows", priority = 200)]
        public static void Build() => Build(OutPath, BuildOptions.None);

        static void Build(string outPath, BuildOptions extra)
        {
            MaterialBuilder.EnsureGlowKeywords();
            PlayerSettings.productName = "T-MINUS";
            PlayerSettings.companyName = "T-MINUS Team";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = extra
            };
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string text = $"result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize / (1024 * 1024)} MB time={s.totalTime.TotalSeconds:0}s\n" +
                          $"output={Path.GetFullPath(outPath)}\nscenes={string.Join(", ", scenes)}\n";
            foreach (var step in report.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    text += $"ERROR [{step.name}] {m.content}\n";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/build-report.txt", text);
            Debug.Log("[T-Minus] Windows build: " + text);
        }
    }
}
