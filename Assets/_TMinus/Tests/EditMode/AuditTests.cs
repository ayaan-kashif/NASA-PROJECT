using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Houston;
using TMinus.Mission;

namespace TMinus.Tests
{
    /// <summary>Regression tests for the final audit of Codex's and Antigravity's work.</summary>
    public class AuditTests
    {
        [Test]
        public void SpaceGuide_MatchesQuestionsByKeyword()
        {
            var data = TestData.Registry;
            Assert.AreEqual("eps", CodexMatcher.Match("What does the EPS do?", data, 3).FirstOrDefault());
            CollectionAssert.Contains(CodexMatcher.Match("why torque bolts in a star pattern", data, 3), "star_pattern");
            CollectionAssert.Contains(CodexMatcher.Match("tell me about max-q", data, 3), "max_q");
            Assert.AreEqual(0, CodexMatcher.Match("hello there", data, 3).Count);
        }

        [Test]
        public void DesignCode_RoundTrips_AndRejectsGarbage()
        {
            var b = new DesignBundle { missionId = "floodwatch_1", spacecraft = new SpacecraftDesign { orbitAltitudeKm = 550 }, vehicle = new VehicleDesign { rocketId = "pathfinder" } };
            Assert.IsTrue(DesignCode.TryDecode(DesignCode.Encode(b), out var back, out _));
            Assert.AreEqual("pathfinder", back.vehicle.rocketId);
            Assert.IsFalse(DesignCode.TryDecode("TM1-not a code", out _, out var err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Compare_LabelsHigherIsBetterMetrics()
        {
            StringAssert.Contains("improved", DesignCompare.Metric("Power margin", 1, 2, " W"));
            StringAssert.Contains("worse", DesignCompare.Metric("Mass", 1, 2, " kg", "lower"));
            StringAssert.Contains("same", DesignCompare.Metric("Mass", 2, 2, " kg", "lower"));
        }

        [Test]
        public void FloodWatch_BatteryAgeing_IsInMissionData()
        {
            var m = TestData.Registry.GetMission("floodwatch_1");
            Assert.IsTrue(m.randomEvents.Any(e => e.type == "battery_ageing" && e.value == 10));
        }
    }
}
