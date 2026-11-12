using System.Linq;
using UnityEngine;

namespace TMinus.Spacecraft
{
    /// <summary>Static hooks for automated play-mode checks of the Integration Bay.</summary>
    public static class BayDebug
    {
        static BayController C => BayController.Instance;

        public static readonly string[] DefaultParts =
            { "eps", "batt_20", "dep_switch", "obc", "radio_uhf", "ant_uhf", "adcs_mag", "cam_wide", "solar_body" };

        public static readonly string[][] DefaultCables =
        {
            new[] { "solar_body/SOLAR_OUT_1", "eps/SOLAR_IN_1" },
            new[] { "batt_20/BATT", "dep_switch/BATT_IN" },
            new[] { "dep_switch/BATT_OUT", "eps/BATT" },
            new[] { "eps/PWR_OUT_1", "obc/PWR_IN" },
            new[] { "eps/PWR_OUT_2", "radio_uhf/PWR_IN" },
            new[] { "eps/PWR_OUT_3", "adcs_mag/PWR_IN" },
            new[] { "eps/PWR_OUT_4", "cam_wide/PWR_IN" },
            new[] { "eps/DATA", "obc/DATA_1" },
            new[] { "radio_uhf/DATA", "obc/DATA_2" },
            new[] { "adcs_mag/DATA", "obc/DATA_3" },
            new[] { "cam_wide/DATA", "obc/DATA_4" },
            new[] { "radio_uhf/RF", "ant_uhf/RF" },
        };

        public static string InstallDefault(string _)
        {
            foreach (var p in DefaultParts) C.Install(p);
            return C.Summary();
        }

        /// <summary>"all" wires the default build; "norf" skips the antenna cable; "short" adds a short circuit test.</summary>
        public static string WireDefault(string variant)
        {
            foreach (var pair in DefaultCables)
            {
                if (variant == "norf" && pair[0] == "radio_uhf/RF") continue;
                C.bench.Connect(pair[0], pair[1], false);
            }
            return C.Summary();
        }

        public static string TryConnect(string args)
        {
            var a = args.Split(' ');
            var c = C.bench.Connect(a[0], a[1], a.Length > 2 && a[2] == "rev");
            return c != null ? "connected " + c.type : "refused";
        }

        public static string Mode(string m) { C.SetMode(m); return m; }

        public static string Meter(string args)
        {
            var a = args.Split(' ');
            int n = C.bench.Graph.PathLength(a[0], a[1]);
            return n > 0 ? $"connected via {n}" : "open circuit";
        }

        public static string Pin(string _) { C.TogglePin(); return GameStateText(); }

        static string GameStateText() => "rbfPulled=" + Core.GameState.Instance.Spacecraft.rbfPinPulled;

        public static string Report(string _) => C.Summary();

        public static string Swap(string args)
        {
            var a = args.Split(' ');
            C.Remove(a[0]);
            C.Install(a[1]);
            return C.Summary();
        }

        public static string Orbit(string km) { Core.GameState.Instance.Spacecraft.orbitAltitudeKm = double.Parse(km); C.SendMessage("RefreshBudgets"); return C.Summary(); }

        /// <summary>Hover the live cable from port a over port b to show the hint, for screenshots.</summary>
        public static string Faults(string _) =>
            string.Join("\n", C.bench.Faults.Select(f => f.ToString()));
    }
}
