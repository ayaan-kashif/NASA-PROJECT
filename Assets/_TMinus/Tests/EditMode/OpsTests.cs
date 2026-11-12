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

        [TestCase(0, 0)] [TestCase(35, 1)] [TestCase(60, 2)] [TestCase(85, 3)] [TestCase(100, 3)]
        public void StarsFollowMissionScoreThresholds(double points, int stars)
        { Assert.AreEqual(stars, MissionScoring.StarsForScore(points)); }
    }
}
