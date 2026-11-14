using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Mission
{
    /// <summary>Where "Show me why" takes the player: the room, the joint or port to frame, and a one-line reason (audit F24).</summary>
    public sealed class WhyTarget
    {
        public string scene, target, note;

        public static WhyTarget For(FlightRecord f, IEnumerable<LogEvent> events, RulesDef rules)
        {
            if (f != null && f.flown && !f.orbitAchieved && !string.IsNullOrEmpty(f.failureCause) && rules != null)
            {
                string c = f.failureCause;
                if (rules.GetJoint(c) != null)
                    return new WhyTarget { scene = SceneNames.VAB, target = c, note = $"{c} was the weak link: the flight ended at T+{f.flightTimeS:0} s with max-Q {f.maxQKPa:0.0} kPa. Missing, loose or out-of-order bolts lower a joint's limit." };
                var link = rules.avionicsWiring.Find(l => l.id == c);
                if (link != null)
                    return new WhyTarget { scene = SceneNames.AvionicsBay, target = link.from, note = link.message };
                if (c == "av_safe_arm")
                    return new WhyTarget { scene = SceneNames.AvionicsBay, target = "av_safe_arm", note = "Safe/Arm was still on SAFE. Arm it at the pad (pre-launch checklist) before the countdown." };
                if (c == "rk_stack")
                    return new WhyTarget { scene = SceneNames.VAB, target = null, note = "The rocket was not fully stacked: mount every part, then bolt each joint." };
            }
            var ev = events?.FirstOrDefault(e => e.system == "operations" && !string.IsNullOrEmpty(e.causeId) && e.causeId.Contains("/") &&
                                                 (e.code.StartsWith("wiring_") || e.code == "satellite_wiring_dead"));
            if (ev != null) return new WhyTarget { scene = SceneNames.IntegrationBay, target = ev.causeId, note = ev.message };
            return null;
        }
    }
}
