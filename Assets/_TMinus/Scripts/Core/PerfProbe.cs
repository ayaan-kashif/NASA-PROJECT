#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Frame-time probe for the demo checks (development and editor builds only). PerfProbe.Measure 5 averages the frame time
    /// over 5 seconds of real time and reports fps, the worst frame and the GPU Unity is running on.
    /// </summary>
    public static class PerfProbe
    {
        /// <summary>
        /// Development builds started with -perfprobe time every room as it loads (5 s each) and log the result, so one
        /// Judge Mode run (F9) measures the whole game. Nothing happens in normal builds or without the argument.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoOnSceneLoad()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perfprobe") < 0) return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Measure("5");
            Measure("5");
        }

        public static string GpuName => SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceVendor + ")";

        public static string Measure(string seconds)
        {
            float s = float.TryParse(seconds, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 5f;
            var go = new GameObject("[PerfProbe]") { hideFlags = HideFlags.HideAndDontSave };
            var probe = go.AddComponent<Runner>();
            probe.Begin(s);
            return "measuring " + s + " s on " + GpuName;
        }

        public static string Last => Runner.LastReport;

        sealed class Runner : MonoBehaviour
        {
            public static string LastReport = "(none yet)";
            float _left, _total, _worst; int _frames;

            public void Begin(float seconds) { _left = seconds; _total = seconds; _worst = 0; _frames = 0; }

            void Update()
            {
                if (_left <= 0) return;
                float dt = Time.unscaledDeltaTime;
                _left -= dt; _frames++;
                _worst = Mathf.Max(_worst, dt);
                if (_left > 0) return;
                float avg = (_total) / Mathf.Max(1, _frames);
                var sb = new StringBuilder();
                sb.Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name).Append(": ")
                  .Append((1f / avg).ToString("0.0")).Append(" fps, avg ").Append((avg * 1000).ToString("0.0"))
                  .Append(" ms, worst ").Append((_worst * 1000).ToString("0.0")).Append(" ms, ")
                  .Append(_frames).Append(" frames, GPU ").Append(GpuName)
                  .Append(", screen ").Append(Screen.width).Append('x').Append(Screen.height);
                LastReport = sb.ToString();
                Debug.Log("[PerfProbe] " + LastReport);
                Destroy(gameObject);
            }
        }
    }
}
#endif
