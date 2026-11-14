using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Wiring;

namespace TMinus.Tests
{
    public class AvionicsTests
    {
        static VehicleDesign FullyWired()
        {
            var v = new VehicleDesign { armed = true };   // armed at the pad: Safe/Arm blocks ignition while SAFE
            foreach (var l in TestData.Registry.GetRules().avionicsWiring)
                v.avionicsCables.Add(new CableLink { fromPort = l.from, toPort = l.to, cableType = l.cableType });
            return v;
        }

        [Test]
        public void FullWiring_NoConsequences_AndValidatorClean()
        {
            var rules = TestData.Registry.GetRules();
            var v = FullyWired();
            var r = AvionicsReport.Compute(v, rules);
            Assert.IsTrue(r.AllOk, r.ToString());
            Assert.AreEqual(rules.avionicsWiring.Count, r.wiredLinks);

            // The same cables are legal in the generic wiring graph (types, one cable per port).
            var g = new WiringGraph(rules);
            foreach (var p in TestData.Registry.Parts.Where(p => p.assembly == "avionics")) g.AddDevice(p.id, p);
            foreach (var c in v.avionicsCables) Assert.IsNotNull(g.Connect(c.fromPort, c.toPort, false, out var reason), reason);
            Assert.IsEmpty(WiringValidator.Validate(g));
        }

        [Test]
        public void EachMissingCable_GivesItsConsequence()
        {
            var rules = TestData.Registry.GetRules();
            foreach (var link in rules.avionicsWiring)
            {
                var v = FullyWired();
                v.avionicsCables.RemoveAll(c => c.fromPort == link.from && c.toPort == link.to);
                var r = AvionicsReport.Compute(v, rules);
                Assert.AreEqual(1, r.consequences.Count, link.id);
                Assert.AreEqual(link.consequence, r.consequences[0].consequence, link.id);
                Assert.AreEqual(link.id, r.consequences[0].linkId);
            }
        }

        [Test]
        public void PowerFailure_Cascades()
        {
            var rules = TestData.Registry.GetRules();
            var v = FullyWired();
            v.avionicsCables.RemoveAll(c => c.fromPort == "av_battery/PWR_OUT_1");
            var r = AvionicsReport.Compute(v, rules);
            Assert.IsFalse(r.S1IgnitionOk);
            Assert.IsFalse(r.TelemetryOk);
            Assert.IsFalse(r.StageSeparationOk);
        }

        [Test]
        public void SafePlug_BlocksIgnition_AndNamesTheSafeArmPlug()
        {
            var rules = TestData.Registry.GetRules();
            var v = FullyWired();
            v.armed = false;
            var r = AvionicsReport.Compute(v, rules);
            Assert.IsFalse(r.S1IgnitionOk);
            Assert.AreEqual("av_safe_arm", r.CauseId("no_ignition"));
            Assert.IsTrue(r.StageSeparationOk, "Only ignition is gated by Safe/Arm in this model.");
        }

        [Test]
        public void ArmedAndWired_Ignites()
        {
            var r = AvionicsReport.Compute(FullyWired(), TestData.Registry.GetRules());
            Assert.IsTrue(r.S1IgnitionOk);
            Assert.IsNull(r.CauseId("no_ignition"));
            Assert.IsTrue(AvionicsReport.Perfect().S1IgnitionOk);
        }

        [Test]
        public void PowerCascade_BlamesThePowerCable()
        {
            var rules = TestData.Registry.GetRules();
            var v = FullyWired();
            v.avionicsCables.RemoveAll(c => c.fromPort == "av_battery/PWR_OUT_1");
            var r = AvionicsReport.Compute(v, rules);
            Assert.AreEqual("w_batt_pdu", r.CauseId("stuck_stage"));
            Assert.AreEqual("w_batt_pdu", r.CauseId("no_ignition"));
        }

        [Test]
        public void SeparationMissing_OnlySeparationFails()
        {
            var rules = TestData.Registry.GetRules();
            var v = FullyWired();
            v.avionicsCables.RemoveAll(c => c.toPort == "av_stage_sep/PYRO_IN");
            var r = AvionicsReport.Compute(v, rules);
            Assert.IsTrue(r.S1IgnitionOk);
            Assert.IsFalse(r.StageSeparationOk);
            Assert.IsTrue(r.Has("stuck_stage"));
        }
    }
}
