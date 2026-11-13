using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Houston;
using TMinus.Mission;
using TMinus.Wiring;
using UnityEngine;

namespace TMinus.Tests
{
    [TestFixture]
    public sealed class HoustonTests
    {
        DataRegistry _data;
        RulesDef _rules;

        [SetUp]
        public void Setup()
        {
            _data = TestData.Registry;
            _rules = _data.GetRules();
        }

        [Test]
        public void HoldingBatteryHighlightsEpsBattPort()
        {
            var ctx = new AdvisorContext
            {
                Data = _data,
                Rules = _rules,
                CubeSatParts = new List<PlacedPart>()
            };

            var targets = AssemblyAdvisor.WhereDoesThisGo("batt_20", ctx);
            Assert.That(targets, Is.Not.Empty, "Expected valid targets for battery.");

            var epsBattTarget = targets.FirstOrDefault(t => t.objectRef == "eps/BATT" || t.text.Contains("BATT"));
            Assert.That(epsBattTarget, Is.Not.Null, "Expected EPS BATT port as recommendation.");
            Assert.That(epsBattTarget.reasonCode, Is.EqualTo("required_connection"));
            Assert.That(epsBattTarget.codexId, Is.EqualTo("eps"));
        }

        [Test]
        public void EmptyCubeSatNextStepYieldsCompleteBuildOrder()
        {
            var ctx = new AdvisorContext
            {
                Data = _data,
                Rules = _rules,
                CubeSatAssemblyOrder = new List<string>(_rules.assemblyOrder["cubesat"]),
                CubeSatParts = new List<PlacedPart>()
            };

            // Step 1: empty CubeSat -> EPS
            var step1 = AssemblyAdvisor.NextStep(ctx);
            Assert.That(step1.objectRef, Is.EqualTo("eps"));
            Assert.That(step1.text, Does.Contain("EPS"));

            // Add EPS
            ctx.CubeSatParts.Add(new PlacedPart { partId = "eps", instanceId = "eps", mountId = "U1A" });
            var step2 = AssemblyAdvisor.NextStep(ctx);
            Assert.That(step2.text, Does.Contain("battery").IgnoreCase);

            // Add Battery
            ctx.CubeSatParts.Add(new PlacedPart { partId = "batt_20", instanceId = "batt_20", mountId = "U1B" });
            var step3 = AssemblyAdvisor.NextStep(ctx);
            Assert.That(step3.text, Does.Contain("deployment switch").IgnoreCase);

            // Add switch
            ctx.CubeSatParts.Add(new PlacedPart { partId = "dep_switch", instanceId = "dep_switch", mountId = "RAIL" });
            var step4 = AssemblyAdvisor.NextStep(ctx);
            Assert.That(step4.text, Does.Contain("computer").IgnoreCase.Or.Contain("OBC").IgnoreCase);

            // Add OBC
            ctx.CubeSatParts.Add(new PlacedPart { partId = "obc", instanceId = "obc", mountId = "U2A" });
            var step5 = AssemblyAdvisor.NextStep(ctx);
            Assert.That(step5.text, Does.Contain("radio").IgnoreCase);
        }

        [Test]
        public void BoltCoachFollowsStarPattern12()
        {
            var jointDef = _rules.boltJoints.First(j => j.boltCount == 12);
            var state = new BoltJointState(jointDef);

            var coach = AssemblyAdvisor.BoltCoach(state);
            Assert.That(coach.starOrderText, Is.EqualTo("1-7-4-10-2-8-5-11-3-9-6-12"));
            Assert.That(coach.nextBoltNumber, Is.EqualTo(1));
            Assert.That(coach.currentPass, Is.EqualTo(1));

            // Tighten first bolt in star order
            state.Insert(0);
            state.ApplyTorque(0, state.PassTargetNm(1));

            coach = AssemblyAdvisor.BoltCoach(state);
            // Next bolt in star order: 0 + 12/2 = 6 (0-based) -> 7 (1-based)
            Assert.That(coach.nextBoltNumber, Is.EqualTo(7));
        }

        [Test]
        public void ScanFindsAllSeededFaults()
        {
            // Seed a build with 5 specific faults:
            // 1. Missing RF cable (wiring fault)
            // 2. Missing bolts on joint 1 (bolt fault)
            // 3. Warped joint on joint 1 (bolt fault)
            // 4. Stuck stage from missing separation pyro (avionics consequence)
            // 5. RBF pin still inserted (prelaunch fault)
            // 6. Safe/Arm plug not ARMED (prelaunch fault)

            var g = new WiringGraph(_rules);
            var radio = _data.GetPart("radio_sband");
            var obc = _data.GetPart("obc");
            var eps = _data.GetPart("eps");
            var ant = _data.GetPart("ant_patch");

            g.AddDevice("radio_sband", radio);
            g.AddDevice("obc", obc);
            g.AddDevice("eps", eps);
            g.AddDevice("ant_patch", ant);

            // Wire power and data, but intentionally leave out RF antenna cable
            g.Connect("eps/PWR_OUT_1", "radio_sband/PWR_IN", false, out _);
            g.Connect("obc/DATA_1", "radio_sband/DATA", false, out _);

            // Joint with 3 missing bolts and out-of-order sequence (warped)
            var jointDef = _rules.boltJoints.First(j => j.id == "j_s1_interstage");
            var badJoint = new BoltJointState(jointDef);
            for (int i = 0; i < jointDef.boltCount - 3; i++) badJoint.Insert(i);
            for (int i = 0; i < jointDef.boltCount - 3; i++) badJoint.ApplyTorque(i, badJoint.Target);

            // Avionics with missing separation cable
            var v = new VehicleDesign { rocketId = "pathfinder", armed = false };
            var avReport = new AvionicsReport { armed = false };
            avReport.consequences.Add(new AvionicsConsequence
            {
                linkId = "w_pyro2",
                consequence = "stuck_stage",
                message = "Stage separation pyro unwired."
            });

            var ctx = new AdvisorContext
            {
                Data = _data,
                Rules = _rules,
                Wiring = g,
                BoltJoints = new List<BoltJointState> { badJoint },
                AvionicsReport = avReport,
                RbfPinInserted = true,
                Vehicle = v
            };

            var faults = AssemblyAdvisor.Scan(ctx);
            Assert.That(faults.Count, Is.GreaterThanOrEqualTo(5), $"Expected at least 5 faults, found {faults.Count}");

            bool hasMissingRf = faults.Any(f => f.what.Contains("RF") || f.what.Contains("antenna") || f.why == "missing_connection");
            bool hasMissingBolts = faults.Any(f => f.why == "bolts_missing" || f.what.Contains("missing"));
            bool hasWarpedJoint = faults.Any(f => f.why == "joint_warped" || f.what.Contains("warped"));
            bool hasPyroConsequence = faults.Any(f => f.why == "stuck_stage" || f.what.Contains("separation"));
            bool hasRbfPin = faults.Any(f => f.why == "rbf_pin_in" || f.what.Contains("Remove-Before-Flight"));
            bool hasSafeArm = faults.Any(f => f.why == "not_armed" || f.what.Contains("Safe/Arm"));

            Assert.That(hasMissingRf, Is.True, "Should detect missing RF cable.");
            Assert.That(hasMissingBolts, Is.True, "Should detect missing bolts.");
            Assert.That(hasWarpedJoint, Is.True, "Should detect warped joint.");
            Assert.That(hasPyroConsequence, Is.True, "Should detect stage separation consequence.");
            Assert.That(hasRbfPin, Is.True, "Should detect RBF pin still inserted.");
            Assert.That(hasSafeArm, Is.True, "Should detect Safe/Arm not ARMED.");
        }
    }
}
