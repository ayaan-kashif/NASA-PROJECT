using System.Collections.Generic;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Mission;

namespace TMinus.Tests
{
    public class WhyTargetTests
    {
        static readonly TMinus.Core.Data.RulesDef Rules = TestData.Registry.GetRules();

        [Test]
        public void ABrokenJointSendsThePlayerToTheVab()
        {
            var f = new FlightRecord { flown = true, outcome = "Failed", failureCode = "joint_failure", failureCause = "j_s1_interstage", flightTimeS = 26, maxQKPa = 40.6 };
            var t = WhyTarget.For(f, new List<LogEvent>(), Rules);
            Assert.AreEqual(SceneNames.VAB, t.scene);
            Assert.AreEqual("j_s1_interstage", t.target);
            StringAssert.Contains("40.6", t.note);
        }

        [Test]
        public void AMissingAvionicsCableSendsThePlayerToItsPort()
        {
            var f = new FlightRecord { flown = true, outcome = "Failed", failureCode = "stuck_stage", failureCause = "w_pyro2" };
            var t = WhyTarget.For(f, new List<LogEvent>(), Rules);
            Assert.AreEqual(SceneNames.AvionicsBay, t.scene);
            Assert.AreEqual("av_flight_computer/PYRO_OUT_2", t.target);
        }

        [Test]
        public void ASafePlugLaunchPointsAtTheSafeArmPlug()
        {
            var f = new FlightRecord { flown = true, outcome = "Aborted", failureCode = "ignition_failure", failureCause = "av_safe_arm" };
            Assert.AreEqual("av_safe_arm", WhyTarget.For(f, null, Rules).target);
        }

        [Test]
        public void ASatelliteWiringFaultSendsThePlayerToTheIntegrationBay()
        {
            var f = new FlightRecord { flown = true, outcome = "Orbit", orbitAchieved = true, deployed = true };
            var events = new List<LogEvent> { new LogEvent { system = "operations", code = "wiring_no_downlink", causeId = "radio_sband/RF", message = "No downlink." } };
            var t = WhyTarget.For(f, events, Rules);
            Assert.AreEqual(SceneNames.IntegrationBay, t.scene);
            Assert.AreEqual("radio_sband/RF", t.target);
        }

        [Test]
        public void ACleanSuccessHasNothingToExplain()
        {
            var f = new FlightRecord { flown = true, outcome = "Orbit", orbitAchieved = true, deployed = true };
            Assert.IsNull(WhyTarget.For(f, new List<LogEvent>(), Rules));
        }
    }
}
