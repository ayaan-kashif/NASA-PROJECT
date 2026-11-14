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
            var score = new MissionScore();
            Add(score, "success", "Mission success", w.success, s.Flight.deployed && ops.survived ? 1 : s.Flight.deployed ? 0.45 : 0);
            double science = m?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked")?.threshold ?? 20;
            Add(score, "science", "Science returned", w.science, science <= 0 ? 1 : Math.Min(1, ops.imagesDownlinked / science));
            double cost = budget.budgetM > 0 ? Math.Max(0, Math.Min(1, (budget.budgetM - budget.totalCostM) / Math.Max(0.01, budget.budgetM * 0.1))) : 1;
            if (budget.totalCostM <= budget.budgetM + 1e-6) cost = 1;
            Add(score, "budget", "Budget", w.budget, budget.budgetM <= 0 ? 1 : cost);
            int healthy = new[] { budget.power, budget.battery, budget.data, budget.pointing, budget.mass }.Count(x => x != BudgetStatus.Red);
            Add(score, "margins", "Design margins", w.margins, healthy / 5.0);
            double decision = s.Launch.knownFailures.Count == 0 ? 1 : Math.Max(0, 1 - s.Launch.knownFailures.Distinct().Count() * 0.2);
            decision *= 1.0 / (1 + Math.Max(0, s.Launch.noGoRounds) * 0.05);
            int hints = s.Settings?.hintsUsed?.Where(x => x > 0).Sum() ?? 0;
            decision *= Math.Max(0.75, 1 - hints * 0.02);
            Add(score, "decision", "Decision quality", w.decisionQuality, decision);
            Add(score, "crew", "Team roles", w.collaboration, Math.Min(1, s.Crew.Select(c => c.role).Distinct().Count() / 4.0));
            score.total = Math.Round(score.breakdown.Sum(x => x.points), 1);
            score.stars = StarsForScore(score.total);
            BuildFixes(score, s, ops, budget);
            return score;
        }
        static void Add(MissionScore s, string id, string label, double max, double fraction)
        { max = Math.Max(0, max); fraction = Math.Max(0, Math.Min(1, fraction)); s.breakdown.Add(new ScoreLine { id = id, label = label, maximum = max, points = Math.Round(max * fraction, 1) }); }
        static void BuildFixes(MissionScore s, GameState state, OpsReport ops, SpacecraftReport budget)
        {
            // The real cause first: a broken joint is not a deployment-wiring problem.
            bool joint = state.Flight.failureCode == "joint_failure";
            if (joint) s.fixes.Add("Torque every bolt in the joint's star pattern and verify the joint integrity before launch.");
            else if (state.Flight.failureCode == "ignition_failure" && state.Flight.failureCause == "av_safe_arm") s.fixes.Add("Arm the Safe/Arm plug at the pad (pre-launch checklist) before the countdown: on SAFE the igniters get no firing current.");
            else if (!state.Flight.deployed) s.fixes.Add("Restore the deployment path: check the separation pyro cable, fairing release and dispenser.");
            if (ops.powerFailureCause == "rbf_pin") s.fixes.Add("Pull the remove-before-flight pin at the pad before launch.");
            if (ops.powerFailureCause == "deployment_switch") s.fixes.Add("Wire the deployment switch inline between the battery and EPS.");
            foreach (var issue in budget.issues)
            {
                if (s.fixes.Count >= 3) break;
                if (!s.fixes.Any(x => x.IndexOf(issue.code, StringComparison.OrdinalIgnoreCase) >= 0)) s.fixes.Add(issue.message);
            }
            double goal = state.Data.GetMission(state.CurrentMissionId)?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked")?.threshold ?? 20;
            if (!joint && state.Flight.deployed && ops.imagesDownlinked < goal && s.fixes.Count < 3) s.fixes.Add("Increase downlink capacity or image quality/power margin so more usable images reach the ground.");
            while (s.fixes.Count > 3) s.fixes.RemoveAt(s.fixes.Count - 1);
        }
        public static int StarsForScore(double score) => score >= 85 ? 3 : score >= 60 ? 2 : score >= 35 ? 1 : 0;
    }
}
