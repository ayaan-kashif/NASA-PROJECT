using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Mission
{
    public static class DebriefDebug
    {
        public static OpsReport LastOps { get; private set; }
        public static MissionScore LastScore { get; private set; }
        static FlightRecord _forFlight;
        public static void ResetRun() { LastOps = null; LastScore = null; _forFlight = null; }
        /// <summary>True when the cached result belongs to the current flight. Every flight gets a new FlightRecord,
        /// so a second flight (Judge Mode's fix-and-fly, or Redesign) never shows the first flight's operations.</summary>
        public static bool IsCurrent => LastOps != null && ReferenceEquals(_forFlight, GameState.Instance?.Flight);
        public static void Run() { if (!IsCurrent) RunMission(); SceneFlow.Load(SceneNames.Debrief); }
        public static OpsReport RunMission()
        {
            var s = GameState.Instance;
            if (s == null) { Debug.LogError("[DebriefDebug] GameState missing."); return null; }
            var mission = s.Data.GetMission(s.CurrentMissionId);
            var rocket = s.Data.GetRocket(s.Vehicle.rocketId);
            var budget = SpacecraftBudget.Compute(s.Spacecraft, s.Data, mission, rocket);
            LastOps = OpsSimulation.Run(s, budget);
            _forFlight = s.Flight;
            LastScore = MissionScoring.Compute(s, LastOps, budget);
            Debug.Log($"[DebriefDebug] {LastScore.stars} stars, {LastScore.total:0.0}/100; orbit={s.Flight.orbitAchieved}, deployed={s.Flight.deployed}, alive={LastOps.daysAlive}d, images={LastOps.imagesDownlinked}, final SOC={LastOps.finalSoc:P0}");
            return LastOps;
        }
        public static string RunOps(string _) { var r = RunMission(); return r == null ? "no report" : $"{LastScore.stars} stars; {LastScore.total:0.0}/100; alive {r.daysAlive}d; captured {r.imagesTaken}; downlinked {r.imagesDownlinked}; SOC {r.finalSoc:P0}; power cause={r.powerFailureCause}"; }
        public static string Show(string _) { Run(); return "Debrief loaded"; }
        public static string SetupDemo(string variant)
        {
            var s = GameState.Instance;
            DemoBuilds.Apply(s, noSeparation: (variant ?? "").Contains("sep"));
            s.Spacecraft.rbfPinPulled = true;
            if ((variant ?? "").Contains("hd"))
            {
                var camera = s.Spacecraft.parts.FirstOrDefault(p => s.Data.GetPart(p.partId)?.category == "camera");
                if (camera != null) camera.partId = "cam_hd";
            }
            s.Flight = new FlightRecord(); s.Events.Clear();
            return "prepared " + variant;
        }
    }
}
