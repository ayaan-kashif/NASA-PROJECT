using System.Collections.Generic;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Mission;

namespace TMinus.Tests
{
    public class DebriefNarrationTests
    {
        static MissionScore Score(params string[] fixes)
        {
            var s = new MissionScore { total = 70, stars = 2 };
            s.fixes.AddRange(fixes);
            return s;
        }

        [Test]
        public void ALostRocketNamesTheCauseAndTheFix()
        {
            var flight = new FlightRecord { flown = true, outcome = "Failed", failureCode = "joint_failure", failureCause = "j_s1_interstage", flightTimeS = 26 };
            string text = DebriefNarration.Offline("FloodWatch-1", flight, new OpsReport(), Score("Torque every bolt in star order."), new List<GoalResult>());
            StringAssert.Contains("j_s1_interstage", text);
            StringAssert.Contains("26 seconds", text);
            StringAssert.Contains("Torque every bolt", text);
        }

        [Test]
        public void ARocketThatNeverFlewIsNotCalledASuccess()
        {
            string text = DebriefNarration.Offline("FloodWatch-1", new FlightRecord(), new OpsReport(), Score(), null);
            StringAssert.Contains("No flight", text);
        }

        [Test]
        public void AWiringLimitedSuccessBlamesTheWiring()
        {
            var flight = new FlightRecord { flown = true, outcome = "Orbit", orbitAchieved = true, deployed = true };
            var ops = new OpsReport { daysAlive = 30 };
            ops.wiringIssues.Add("Nothing reached the ground: the radio has no antenna cable.");
            string text = DebriefNarration.Offline("FloodWatch-1", flight, ops, Score("Fix the CubeSat wiring."), new List<GoalResult>());
            StringAssert.Contains("wiring fault", text);
            StringAssert.Contains("antenna", text);
        }

        [Test]
        public void ASuccessReportsGoalsAndStars()
        {
            var flight = new FlightRecord { flown = true, outcome = "Orbit", orbitAchieved = true, deployed = true };
            var ops = new OpsReport { daysAlive = 30, imagesDownlinked = 87, survived = true };
            var goals = new List<GoalResult> { new GoalResult { met = true }, new GoalResult { met = true }, new GoalResult { met = false } };
            string text = DebriefNarration.Offline("FloodWatch-1", flight, ops, Score(), goals);
            StringAssert.Contains("succeeded", text);
            StringAssert.Contains("2 of 3 goals", text);
            StringAssert.Contains("87 images", text);
        }
    }
}
