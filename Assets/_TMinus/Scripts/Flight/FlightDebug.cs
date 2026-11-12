using UnityEngine.SceneManagement;

namespace TMinus.Flight
{
    /// <summary>Static hooks for automated play-mode checks of the Flight scene.</summary>
    public static class FlightDebug
    {
        static FlightController C => FlightController.Instance;

        /// <summary>Reload the Flight scene so it rebuilds from the current GameState (after a demo build was applied).</summary>
        public static string Reload(string _) { SceneManager.LoadScene(Core.SceneNames.Flight); return "reloading"; }

        public static string Launch(string kick) { C.Launch(string.IsNullOrEmpty(kick) || kick == "x" ? 3 : double.Parse(kick)); return Report(null); }

        public static string Warp(string w) { C.Warp = int.Parse(w); return "warp " + C.Warp; }

        public static string Camera(string mode) { C.SetCamera(mode); return mode; }

        /// <summary>Runs the sim to the end immediately (no animation), for fast checks.</summary>
        public static string RunToEnd(string _)
        {
            for (int i = 0; i < 400000 && !C.Ended && C.Launched; i++)
            {
                C.Sim.Step();
                if (C.Sim.Phase == FlightPhase.Orbit || C.Sim.Phase == FlightPhase.Failed || C.Sim.Phase == FlightPhase.Aborted) break;
            }
            return Report(null);
        }

        public static string Report(string _)
        {
            if (C == null) return "no flight";
            var s = C.Sim;
            var r = Core.GameState.Instance.Flight;
            return $"phase={s.Phase} t={s.TimeS:0}s alt={FlightPhysics.Altitude(s.Position) / 1000:0.0}km stage={s.StageIndex} " +
                   $"fail={s.Outcome.failureCode}<-{s.Outcome.failureCause} maxQ={s.Outcome.maxQPa / 1000:0.0}kPa | record: {r.outcome} orbit={r.orbitAchieved} deployed={r.deployed} {r.periapsisKm:0}x{r.apoapsisKm:0}km";
        }
    }
}
