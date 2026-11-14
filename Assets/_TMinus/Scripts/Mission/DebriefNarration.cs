using System.Collections.Generic;
using System.Linq;
using TMinus.Core;

namespace TMinus.Mission
{
    /// <summary>
    /// The three-sentence summary at the top of the debrief (audit F22): what happened, why it mattered, what to try next. This offline
    /// version is built from the game's own facts (cause ids, score lines, Top Fixes) so it is always right; when HOUSTON's AI is on it
    /// is replaced by the AI's wording of the same facts.
    /// </summary>
    public static class DebriefNarration
    {
        public static string Offline(string missionName, FlightRecord f, OpsReport ops, MissionScore score, IList<GoalResult> goals)
        {
            string fix = score.fixes.Count > 0 ? score.fixes[0] : null;
            int met = goals?.Count(g => g.met) ?? 0, total = goals?.Count ?? 0;

            if (f == null || !f.flown)
                return "No flight was flown, so there is nothing to debrief yet. Build, check the pad list and launch.";

            if (!f.orbitAchieved)
            {
                string what = f.outcome == "Aborted" ? $"The launch was aborted ({f.failureCode})" : $"The rocket was lost {f.flightTimeS:0} seconds after liftoff ({f.failureCode})";
                string cause = string.IsNullOrEmpty(f.failureCause) ? "" : $" The cause points to {f.failureCause}.";
                return $"{what}.{cause} A mission is lost at the weakest connection, which is why the checklist exists. " +
                       (fix != null ? "Next: " + fix : "Next: fix what the timeline names and fly again.");
            }

            if (!f.deployed)
                return $"{missionName} reached orbit, but the CubeSat never left the rocket. " +
                       "A satellite that is not released earns nothing. " + (fix != null ? "Next: " + fix : "Next: check the separation and fairing wiring.");

            if (ops != null && ops.wiringIssues.Count > 0)
                return $"{missionName} reached orbit and deployed, but a wiring fault limited the satellite: {ops.wiringIssues[0]} " +
                       (fix != null ? "Next: " + fix : "Next: fix that cable in the Integration Bay.");

            if (ops != null && !ops.survived)
                return $"{missionName} reached orbit, but the satellite stayed alive for only {ops.daysAlive} days. " +
                       "Power and battery margins decide how long a CubeSat lives. " + (fix != null ? "Next: " + fix : "Next: add power margin or battery.");

            string result = $"{missionName} succeeded: orbit, deployment, {ops?.daysAlive ?? 0} days alive and {ops?.imagesDownlinked ?? 0} images downlinked ({met} of {total} goals met, {score.stars} star{(score.stars == 1 ? "" : "s")}).";
            return result + (fix != null ? " To score higher: " + fix : " Try a harder challenge variant or a different orbit.");
        }

        /// <summary>The events and the score lines, as plain text for the AI to word. Facts only.</summary>
        public static string TimelineText(IEnumerable<LogEvent> events, double flightEndS)
        {
            return string.Join("\n", events.Select(e =>
            {
                string when = e.system == "operations" ? $"Day {System.Math.Max(0, (int)System.Math.Round((e.time - flightEndS) / 86400.0))}" : $"T+{e.time:0}s";
                return $"{when} {e.code}: {e.message}" + (string.IsNullOrEmpty(e.causeId) ? "" : $" (cause {e.causeId})");
            }));
        }

        public static string BreakdownText(MissionScore score) =>
            string.Join("; ", score.breakdown.Select(l => $"{l.label} {l.points:0.#}/{l.maximum:0.#}")) + $"; total {score.total:0.0}/100, {score.stars} stars";
    }
}
