using System.Linq;
using NUnit.Framework;
using TMinus.Mission;

namespace TMinus.Tests
{
    public sealed class OpsTests
    {
        [TestCase(90, 16)]
        [TestCase(100, 14)]
        public void OrbitSamplingUsesRoundedOrbitsPerDay(double periodMinutes, int expected)
        { Assert.AreEqual(expected, OpsSimulation.OrbitsPerDay(periodMinutes)); }

        [Test] public void StateOfChargeClampsAndAccountsForCapacity()
        {
            Assert.AreEqual(0.75, OpsSimulation.StepStateOfCharge(0.5, 10, 40), 1e-9);
            Assert.AreEqual(1, OpsSimulation.StepStateOfCharge(0.9, 10, 40), 1e-9);
            Assert.AreEqual(0, OpsSimulation.StepStateOfCharge(0.1, -10, 40), 1e-9);
            Assert.AreEqual(0, OpsSimulation.StepStateOfCharge(1, 1, 0), 1e-9);
        }

        [Test] public void OrbitEnergyUsesBudgetAveragesWithoutApplyingSunlightTwice()
        { Assert.AreEqual(1.5, OpsSimulation.NetOrbitEnergyWh(3, 2, 90), 1e-9); }

        [Test] public void ImagingRequiresPowerGoodPointingAndNoReboot()
        {
            Assert.IsTrue(OpsSimulation.CanImage(true, false, 0.41, 1));
            Assert.IsFalse(OpsSimulation.CanImage(true, false, 0.4, 1));
            Assert.IsFalse(OpsSimulation.CanImage(true, true, 0.9, 1));
            Assert.IsFalse(OpsSimulation.CanImage(true, false, 0.9, 0));
        }

        [Test] public void PerfectCubeSatHasNoWiringEffects()
        {
            var fx = OpsWiring.Evaluate(ChecklistTests.Perfect());
            Assert.IsFalse(fx.Any, string.Join(" | ", fx.Messages));
        }

        [Test] public void MissingAntennaCableSilencesTheRadio()
        {
            var input = ChecklistTests.Perfect();
            input.Spacecraft.cables.RemoveAll(c => c.fromPort == "radio_sband/RF");
            var fx = OpsWiring.Evaluate(input);
            Assert.IsTrue(fx.NoDownlink, "No RF cable means nothing reaches the ground.");
            Assert.IsFalse(fx.NoImaging);
            Assert.IsFalse(fx.Dead);
            Assert.IsTrue(fx.DownlinkCause == "radio_sband/RF" || fx.DownlinkCause == "ant_patch/RF", fx.DownlinkCause);
        }

        [Test] public void MissingCameraDataCableMeansNoImages()
        {
            var input = ChecklistTests.Perfect();
            input.Spacecraft.cables.RemoveAll(c => c.fromPort == "cam_wide/DATA");
            var fx = OpsWiring.Evaluate(input);
            Assert.IsTrue(fx.NoImaging);
            Assert.IsFalse(fx.NoDownlink);
            Assert.AreEqual("cam_wide/DATA", fx.ImagingCause);
        }

        [Test] public void MissingSolarCableMeansNoGeneration()
        {
            var input = ChecklistTests.Perfect();
            input.Spacecraft.cables.RemoveAll(c => c.fromPort == "solar_body/SOLAR_OUT_1");
            var fx = OpsWiring.Evaluate(input);
            Assert.IsTrue(fx.NoSolar);
            Assert.IsFalse(fx.Dead);
        }

        [Test] public void ReversedPowerPlugKillsTheSatelliteOnDeployment()
        {
            var input = ChecklistTests.Perfect();
            input.Spacecraft.cables.First(c => c.fromPort == "eps/PWR_OUT_1").reversed = true;
            var fx = OpsWiring.Evaluate(input);
            Assert.IsTrue(fx.Dead);
            Assert.IsNotNull(fx.DeadCause);
        }

        [Test] public void AllGoalsMetForAPerfectFloodWatchRun()
        {
            var m = TestData.Registry.GetMission("floodwatch_1");
            var goals = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { deployed = true },
                new OpsReport { daysAlive = 30, imagesDownlinked = 87 }, new TMinus.Core.SpacecraftReport { totalCostM = 7.6 });
            Assert.AreEqual(m.successCriteria.Count, goals.Count);
            Assert.IsTrue(goals.All(g => g.met), string.Join(" | ", goals.Select(g => g.text + "=" + g.measured)));
        }

        [Test] public void GoalsShowWhichCriterionFailed()
        {
            var m = TestData.Registry.GetMission("floodwatch_1");
            var goals = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { deployed = true },
                new OpsReport { daysAlive = 30, imagesDownlinked = 5 }, new TMinus.Core.SpacecraftReport { totalCostM = 7.9 });
            Assert.IsFalse(goals.First(g => g.text.Contains("20 usable images")).met);
            Assert.IsFalse(goals.First(g => g.text.Contains("budget")).met);
            Assert.IsTrue(goals.First(g => g.text.Contains("deploy")).met);
        }

        [Test] public void MoonshotGoalsReadTheOrbitAndThePropellantLeft()
        {
            var m = TestData.Registry.GetMission("moonshot");
            Assert.IsFalse(m.locked, "Moonshot is playable");
            var win = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { flown = true, orbitAchieved = true, periapsisKm = 175, apoapsisKm = 185, deltaVLeftMs = 2950 }, new OpsReport(), null);
            Assert.AreEqual(2, win.Count);
            Assert.IsTrue(win.All(g => g.met), string.Join(" | ", win.Select(g => g.text + "=" + g.measured)));
            var low = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { flown = true, orbitAchieved = true, periapsisKm = 175, deltaVLeftMs = 2600 }, new OpsReport(), null);
            Assert.IsTrue(low[0].met);
            Assert.IsFalse(low[1].met, "not enough propellant left");
            Assert.IsFalse(TMinus.Core.MissionKinds.IsCubeSat(m));
            Assert.IsTrue(TMinus.Core.MissionKinds.IsCubeSat(TestData.Registry.GetMission("floodwatch_1")));
        }

        [Test] public void RehbarGoalIsTheHeightOfTheClimb()
        {
            var m = TestData.Registry.GetMission("rehbar_1962");
            var up = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { flown = true, outcome = "Apogee", apoapsisKm = 131 }, new OpsReport(), null);
            Assert.AreEqual(1, up.Count);
            Assert.IsTrue(up[0].met, up[0].measured);
            var low = MissionGoals.Evaluate(m, new TMinus.Core.FlightRecord { flown = true, outcome = "Apogee", apoapsisKm = 90 }, new OpsReport(), null);
            Assert.IsFalse(low[0].met);
        }

        [Test] public void TeamScoreGrowsWithRealPlayersAndNeedsEveryRoleInThePoll()
        {
            var solo = TMinus.Mission.PrelaunchChecklist.AssignRoles(new[] { "A" });
            var four = TMinus.Mission.PrelaunchChecklist.AssignRoles(new[] { "A", "B", "C", "D" });
            Assert.AreEqual(0.4, MissionScoring.CollaborationFraction(solo, null), 1e-9);
            Assert.AreEqual(1.0, MissionScoring.CollaborationFraction(four, null), 1e-9);
            var fullPoll = new System.Collections.Generic.List<string> { "Flight Director: GO", "Propulsion & Structures: GO", "Spacecraft & Comms: GO", "Science & Budget: GO" };
            Assert.AreEqual(1.0, MissionScoring.CollaborationFraction(four, fullPoll), 1e-9);
            var partial = new System.Collections.Generic.List<string> { "Flight Director: GO", "Science & Budget: NO-GO" };
            Assert.AreEqual(0.5, MissionScoring.CollaborationFraction(four, partial), 1e-9);
        }

        [TestCase(0, 0)] [TestCase(35, 1)] [TestCase(60, 2)] [TestCase(85, 3)] [TestCase(100, 3)]
        public void StarsFollowMissionScoreThresholds(double points, int stars)
        { Assert.AreEqual(stars, MissionScoring.StarsForScore(points)); }

        [Test] public void DesignComparisonShowsDirectionAndSignedDelta()
        {
            Assert.AreEqual("Mass: 4 → 3 kg  (-1 kg) (improved)", DesignCompare.Metric("Mass", 4, 3, " kg", "lower"));
            Assert.AreEqual("Cost: 5 → 6 M USD  (+1 M USD) (worse)", DesignCompare.Metric("Cost", 5, 6, " M USD", "lower"));
        }

        [Test] public void ComparisonBudgetStatusesAreReadable()
        {
            Assert.AreEqual("GREEN", DesignCompare.Status(TMinus.Core.BudgetStatus.Green));
            Assert.AreEqual("WATCH", DesignCompare.Status(TMinus.Core.BudgetStatus.Yellow));
            Assert.AreEqual("RED", DesignCompare.Status(TMinus.Core.BudgetStatus.Red));
        }
    }
}
