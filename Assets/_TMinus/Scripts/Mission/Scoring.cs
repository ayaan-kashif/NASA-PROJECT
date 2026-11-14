using System;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Mission
{
    public static class MissionScoring
    {
        public static MissionScore Compute(GameState s, OpsReport ops, SpacecraftReport budget)
        {
            var m = s.Data.GetMission(s.CurrentMissionId);
            var w = m?.scoring ?? new ScoringWeights();
            if (!MissionKinds.IsCubeSat(m)) return ComputeFlightOnly(s, m, w);
            var score = new MissionScore();
            Add(score, "success", "Mission success", w.success, s.Flight.deployed && ops.survived ? 1 : s.Flight.deployed ? 0.45 : 0);
            double science = m?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked")?.threshold ?? 20;
            Add(score, "science", "Science returned", w.science, science <= 0 ? 1 : Math.Min(1, ops.imagesDownlinked / science));
            double cost = budget.budgetM > 0 ? Math.Max(0, Math.Min(1, (budget.budgetM - budget.totalCostM) / Math.Max(0.01, budget.budgetM * 0.1))) : 1;
            if (budget.totalCostM <= budget.budgetM + 1e-6) cost = 1;
            Add(score, "budget", "Budget", w.budget, budget.budgetM <= 0 ? 1 : cost);
            int healthy = new[] { budget.power, budget.battery, budget.data, budget.pointing, budget.mass }.Count(x => x != BudgetStatus.Red);
            Add(score, "margins", "Design margins", w.margins, healthy / 5.0);
            Add(score, "decision", "Decision quality", w.decisionQuality, DecisionFraction(s));
            Add(score, "crew", "Team roles", w.collaboration, CollaborationFraction(s.Crew, s.Launch.poll));
            score.total = Math.Round(score.breakdown.Sum(x => x.points), 1);
            score.stars = StarsForScore(score.total);
            BuildFixes(score, s, ops, budget);
            return score;
        }
        /// <summary>Known FAILs at launch, NO-GO rounds and hints used, as one fraction.</summary>
        static double DecisionFraction(GameState s)
        {
            double decision = s.Launch.knownFailures.Count == 0 ? 1 : Math.Max(0, 1 - s.Launch.knownFailures.Distinct().Count() * 0.2);
            decision *= 1.0 / (1 + Math.Max(0, s.Launch.noGoRounds) * 0.05);
            int hints = s.Settings?.hintsUsed?.Where(x => x > 0).Sum() ?? 0;
            return decision * Math.Max(0.75, 1 - hints * 0.02);
        }

        /// <summary>
        /// A flight-only mission (Moonshot): reaching the target orbit is the success, the propellant left for the next burn is the margin, and
        /// decision quality is the same as everywhere. There is no CubeSat, so no science, budget or crew-role lines.
        /// </summary>
        static MissionScore ComputeFlightOnly(GameState s, MissionDef m, ScoringWeights w)
        {
            var score = new MissionScore();
            var f = s.Flight;
            var goals = MissionGoals.Evaluate(m, f, new OpsReport(), null);
            double met = goals.Count == 0 ? (f.orbitAchieved ? 1 : 0) : goals.Count(g => g.met) / (double)goals.Count;
            Add(score, "success", "Mission success", w.success, f.orbitAchieved ? Math.Max(0.5, met) : met * 0.5);
            double need = m != null && m.requiredDeltaVLeftMs > 0 ? m.requiredDeltaVLeftMs : 1;
            Add(score, "margins", "Propellant margin", w.margins, f.orbitAchieved ? Math.Min(1, f.deltaVLeftMs / need) : 0);
            Add(score, "decision", "Decision quality", w.decisionQuality, DecisionFraction(s));
            score.total = Math.Round(score.breakdown.Sum(x => x.points), 1);
            score.stars = StarsForScore(score.total);
            if (!f.orbitAchieved) score.fixes.Add(string.IsNullOrEmpty(f.failureCode)
                ? "Fly again: reach the parking orbit first."
                : $"The flight ended with {f.failureCode}. Read the timeline for the cause, then try a different pitch kick.");
            else if (f.deltaVLeftMs < need) score.fixes.Add($"Only {f.deltaVLeftMs:N0} m/s were left for the Moon burn and {need:N0} are needed. Try a different pitch kick: a poor climb wastes propellant on gravity and drag.");
            return score;
        }

        static void Add(MissionScore s, string id, string label, double max, double fraction)
        { max = Math.Max(0, max); fraction = Math.Max(0, Math.Min(1, fraction)); s.breakdown.Add(new ScoreLine { id = id, label = label, maximum = max, points = Math.Round(max * fraction, 1) }); }
        static void BuildFixes(MissionScore s, GameState state, OpsReport ops, SpacecraftReport budget)
        {
            // The real cause first: a broken joint is not a deployment-wiring problem.
            bool joint = state.Flight.failureCode == "joint_failure";
            if (joint) s.fixes.Add("Torque every bolt in the joint's star pattern and verify the joint integrity before launch.");
            else if (state.Flight.failureCode == "abort_unbuilt") s.fixes.Add("Stack every part of the rocket in the VAB and bolt each joint before you go to the pad.");
            else if (state.Flight.failureCode == "ignition_failure" && state.Flight.failureCause == "av_safe_arm") s.fixes.Add("Arm the Safe/Arm plug at the pad (pre-launch checklist) before the countdown: on SAFE the igniters get no firing current.");
            else if (!state.Flight.deployed) s.fixes.Add("Restore the deployment path: check the separation pyro cable, fairing release and dispenser.");
            if (ops.powerFailureCause == "rbf_pin") s.fixes.Add("Pull the remove-before-flight pin at the pad before launch.");
            if (ops.powerFailureCause == "deployment_switch") s.fixes.Add("Wire the deployment switch inline between the battery and EPS.");
            if (ops.wiringIssues.Count > 0 && s.fixes.Count < 3) s.fixes.Add("Fix the CubeSat wiring (Integration Bay > Wire): " + ops.wiringIssues[0]);
            foreach (var issue in budget.issues)
            {
                if (s.fixes.Count >= 3) break;
                if (!s.fixes.Any(x => x.IndexOf(issue.code, StringComparison.OrdinalIgnoreCase) >= 0)) s.fixes.Add(issue.message);
            }
            double goal = state.Data.GetMission(state.CurrentMissionId)?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked")?.threshold ?? 20;
            if (!joint && state.Flight.deployed && ops.imagesDownlinked < goal && s.fixes.Count < 3) s.fixes.Add("Increase downlink capacity or image quality/power margin so more usable images reach the ground.");
            while (s.fixes.Count > 3) s.fixes.RemoveAt(s.fixes.Count - 1);
        }
        /// <summary>
        /// Team collaboration (audit A-08). It used to be "distinct roles / 4", which is always 100% because roles are always
        /// all assigned. Now: more real people sharing the four roles scores higher (1 player 40%, 2 = 60%, 3 = 80%, 4 = 100%),
        /// and every role must have taken part in the GO/NO-GO poll. A run with no poll (demos, sandbox) counts all roles.
        /// </summary>
        public static double CollaborationFraction(System.Collections.Generic.IList<CrewMember> crew, System.Collections.Generic.IList<string> poll)
        {
            int players = Math.Max(1, Math.Min(4, (crew ?? new System.Collections.Generic.List<CrewMember>()).Select(c => c.name).Distinct().Count()));
            double people = 0.4 + 0.6 * (players - 1) / 3.0;
            int votes = poll?.Count ?? 0;
            int polled = votes == 0 ? 0 : poll.Select(p => p.Split(':')[0]).Distinct().Count();
            double roles = votes == 0 ? 1.0 : Math.Min(1.0, polled / 4.0);
            return people * roles;
        }

        public static int StarsForScore(double score) => score >= 85 ? 3 : score >= 60 ? 2 : score >= 35 ? 1 : 0;
    }
}
