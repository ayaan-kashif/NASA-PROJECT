using UnityEngine.SceneManagement;
using System;

namespace TMinus.Flight
{
    /// <summary>Static hooks for automated play-mode checks of the Flight scene.</summary>
    public static class FlightDebug
    {
        static FlightController C => FlightController.Instance;

        /// <summary>Reload the Flight scene so it rebuilds from the current GameState (after a demo build was applied).</summary>
        public static string Reload(string _) { SceneManager.LoadScene(Core.SceneNames.Flight); return "reloading"; }

        public static string Launch(string kick)
        {
            var c = RequireController();
            c.Launch(string.IsNullOrEmpty(kick) || kick == "x" ? 3 : double.Parse(kick));
            return Report(null);
        }

        public static string Countdown(string kick)
        {
            RequireController().StartCountdown(string.IsNullOrEmpty(kick) || kick == "x" ? 3 : double.Parse(kick));
            return "countdown started";
        }

        public static string Warp(string w) { var c = RequireController(); c.Warp = int.Parse(w); return "warp " + c.Warp; }

        public static string Camera(string mode) { RequireController().SetCamera(mode); return mode; }

        /// <summary>Runs the sim to the end immediately (no animation), for fast checks.</summary>
        public static string RunToEnd(string _)
        {
            var c = RequireController();
            for (int i = 0; i < 400000 && !c.Ended && c.Launched; i++) c.StepForDebug();
            c.RefreshDebugView();
            return Report(null);
        }

        /// <summary>Runs the sim (no animation) until the given flight time in seconds, then redraws.</summary>
        public static string RunTo(string seconds)
        {
            var c = RequireController(); double t = double.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < 400000 && !c.Ended && c.Launched && c.Sim.TimeS < t; i++) c.StepForDebug();
            c.RefreshDebugView();
            return Report(null);
        }

        public static string Report(string _)
        {
            if (C == null) return "no flight";
            var s = C.Sim;
            if (s == null) return $"flight controller not ready ({C.InitializationState}) · countdown=T-{C.CountdownRemaining:00} active={C.isActiveAndEnabled} timeScale={UnityEngine.Time.timeScale:0.##}";
            var r = Core.GameState.Instance.Flight;
            return $"phase={s.Phase} t={s.TimeS:0}s alt={FlightPhysics.Altitude(s.Position) / 1000:0.0}km stage={s.StageIndex} " +
                   $"fail={s.Outcome.failureCode}<-{s.Outcome.failureCause} maxQ={s.Outcome.maxQPa / 1000:0.0}kPa | " +
                   $"countdown=T-{C.CountdownRemaining:00} launched={C.Launched} active={C.isActiveAndEnabled} timeScale={UnityEngine.Time.timeScale:0.##} | " +
                   $"record: {r.outcome} orbit={r.orbitAchieved} deployed={r.deployed} {r.periapsisKm:0}x{r.apoapsisKm:0}km";
        }

        static FlightController RequireController()
        {
            var c = C;
            if (c == null) throw new InvalidOperationException("Flight scene has no active FlightController.");
            return c;
        }
    }
}
