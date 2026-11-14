using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Mission
{
    public sealed class GoalResult
    {
        public string text, measured;
        public bool met;
    }

    /// <summary>
    /// The mission's success criteria, checked against what actually happened (audit F5). The briefing lists the goals as
    /// empty boxes; the debrief ticks or crosses each one with the measured value.
    /// </summary>
    public static class MissionGoals
    {
        public static List<GoalResult> Evaluate(MissionDef mission, FlightRecord flight, OpsReport ops, SpacecraftReport budget)
        {
            var list = new List<GoalResult>();
            if (mission == null) return list;
            foreach (var c in mission.successCriteria)
            {
                var g = new GoalResult { text = c.text };
                switch (c.metric)
                {
                    case "deployed":
                        g.met = flight != null && flight.deployed;
                        g.measured = g.met ? "deployed" : "not deployed";
                        break;
                    case "daysAlive":
                        g.met = ops != null && ops.daysAlive >= c.threshold;
                        g.measured = $"{(ops?.daysAlive ?? 0)} days";
                        break;
                    case "imagesDownlinked":
                        g.met = ops != null && ops.imagesDownlinked >= c.threshold;
                        g.measured = $"{(ops?.imagesDownlinked ?? 0)} images";
                        break;
                    case "costMUSD":
                        g.met = budget != null && budget.totalCostM <= c.threshold + 1e-9;
                        g.measured = budget != null ? $"{budget.totalCostM:0.00} M$" : "n/a";
                        break;
                    case "periapsisKm":
                        g.met = flight != null && flight.periapsisKm >= c.threshold;
                        g.measured = flight != null ? $"{flight.periapsisKm:0} km" : "n/a";
                        break;
                    case "apogeeKm":
                        g.met = flight != null && flight.apoapsisKm >= c.threshold;
                        g.measured = flight != null ? $"{flight.apoapsisKm:0} km" : "n/a";
                        break;
                    default:
                        g.met = false;
                        g.measured = "not measured";
                        break;
                }
                list.Add(g);
            }
            return list;
        }
    }
}
