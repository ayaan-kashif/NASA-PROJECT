using System;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Mission
{
    public sealed class DesignComparison
    {
        public SpacecraftReport current, candidate;
        public string missionName;
    }

    /// <summary>Decode a shared TM1 code and compare both designs with the same mission budget model.</summary>
    public static class DesignCompare
    {
        public static bool TryCompare(GameState state, string code, out DesignComparison comparison, out string error)
        {
            comparison = null;
            if (state?.Data == null) { error = "Mission data is not ready."; return false; }
            if (!DesignCode.TryDecode(code, out var bundle, out error)) return false;
            DesignValidator.Clean(bundle, state.Data);
            var mission = state.Data.GetMission(state.CurrentMissionId) ?? state.Data.GetMission(bundle.missionId);
            var currentRocket = state.Data.GetRocket(state.Vehicle?.rocketId);
            var candidateRocket = state.Data.GetRocket(bundle.vehicle?.rocketId) ?? currentRocket;
            comparison = new DesignComparison
            {
                current = SpacecraftBudget.Compute(state.Spacecraft, state.Data, mission, currentRocket),
                candidate = SpacecraftBudget.Compute(bundle.spacecraft, state.Data, mission, candidateRocket),
                missionName = mission?.name ?? state.CurrentMissionId
            };
            error = null;
            return true;
        }

        public static string Metric(string label, double current, double candidate, string unit, string lowerIsBetter = null)
        {
            double delta = candidate - current;
            string sign = delta > 1e-8 ? "+" : "";
            // "lower" = smaller is better (mass, cost, GSD); anything else = bigger is better (margins, images).
            string direction = Math.Abs(delta) < 1e-8 ? " (same)" : (delta < 0) == (lowerIsBetter == "lower") ? " (improved)" : " (worse)";
            return $"{label}: {current:0.##} → {candidate:0.##}{unit}  ({sign}{delta:0.##}{unit}){direction}";
        }

        public static string Status(BudgetStatus status) => status == BudgetStatus.Green ? "GREEN" : status == BudgetStatus.Yellow ? "WATCH" : "RED";
    }
}
