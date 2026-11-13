using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Houston;

namespace TMinus.Tests
{
    /// <summary>Regression tests for the audit fixes: HOUSTON's advice must fit the scene the player is in.</summary>
    public class HoustonScopeTests
    {
        static AdvisorContext Ctx(string scene, VehicleDesign v = null)
        {
            var data = TestData.Registry;
            var rules = data.GetRules();
            v ??= new VehicleDesign { rocketId = "pathfinder" };
            return new AdvisorContext
            {
                Data = data, Rules = rules, SceneName = scene, Vehicle = v,
                CubeSatParts = new List<PlacedPart>(),
                CubeSatAssemblyOrder = new List<string>(rules.assemblyOrder["cubesat"]),
                BoltJoints = rules.boltJoints.Select(j => BoltJointState.LoadFrom(j, v)).ToList(),
                AvionicsReport = AvionicsReport.Compute(v, rules),
                RbfPinInserted = true
            };
        }

        [Test]
        public void IntegrationBay_EmptyBuild_SuggestsEps_NotRocketBolts()
        {
            var rec = AssemblyAdvisor.NextStep(Ctx(SceneNames.IntegrationBay));
            Assert.AreEqual("eps", rec.objectRef, rec.text);
            var scan = AssemblyAdvisor.Scan(Ctx(SceneNames.IntegrationBay));
            Assert.IsFalse(scan.Any(f => f.why == "bolts_missing" || f.why == "stuck_stage"), "rocket faults don't belong in the CubeSat bay");
        }

        [Test]
        public void Vab_NeverTellsYouToArm_AndFlagsArmedAsBlocker()
        {
            var safe = AssemblyAdvisor.Scan(Ctx(SceneNames.VAB));
            Assert.IsFalse(safe.Any(f => f.why == "not_armed"), "SAFE is correct in the VAB");
            Assert.IsFalse(safe.Any(f => f.why == "rbf_pin_in"), "the RBF pin is a pad step");

            var armed = Ctx(SceneNames.VAB, new VehicleDesign { rocketId = "pathfinder", armed = true });
            Assert.IsTrue(AssemblyAdvisor.Scan(armed).Any(f => f.why == "armed_in_vab" && f.severity == FaultSeverity.Blocker));
        }

        [Test]
        public void UnmountedJoints_AreNotFaults()
        {
            var scan = AssemblyAdvisor.Scan(Ctx(SceneNames.VAB));
            Assert.IsFalse(scan.Any(f => f.why == "bolts_missing"), "no parts stacked yet, so no joint can be missing bolts");
        }

        [Test]
        public void AvionicsBay_NextStepNamesTheMissingCable()
        {
            var rec = AssemblyAdvisor.NextStep(Ctx(SceneNames.AvionicsBay));
            Assert.AreEqual("avionics_wiring", rec.reasonCode, rec.text);
            StringAssert.Contains("Without it", rec.text);
        }

        [Test]
        public void Pad_IncludesPinAndArm()
        {
            var scan = AssemblyAdvisor.Scan(Ctx(SceneNames.MissionBriefing));
            Assert.IsTrue(scan.Any(f => f.why == "rbf_pin_in"));
            Assert.IsTrue(scan.Any(f => f.why == "not_armed"));
        }

        [Test]
        public void UnderTorquedJoint_IsReported()
        {
            var data = TestData.Registry;
            var rules = data.GetRules();
            var v = new VehicleDesign { rocketId = "pathfinder" };
            foreach (var id in rules.assemblyOrder["pathfinder"]) v.parts.Add(new PlacedPart { instanceId = id, partId = id });
            var j = new BoltJointState(rules.boltJoints[0]);
            for (int i = 0; i < j.Count; i++) { j.Insert(i); j.ApplyTorque(i, j.PassTargetNm(1)); }
            j.SaveTo(v);
            var scan = AssemblyAdvisor.Scan(Ctx(SceneNames.VAB, v));
            Assert.IsTrue(scan.Any(f => f.why == "bolts_not_torqued" && f.where == rules.boltJoints[0].id));
        }
    }
}
