using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Mission;

namespace TMinus.Tests
{
    public class FailureLabTests
    {
        [Test]
        public void BrokenPathfinder_SeedsThreeFaults_AllDetectedThenFixable()
        {
            var data = TestData.Registry; var rules = data.GetRules();
            var job = data.GetMission("failure_broken_pathfinder");
            Assert.IsNotNull(job);
            Assert.AreEqual(3, job.seededFaults.Count);

            var v = new VehicleDesign { rocketId = "pathfinder" };
            foreach (var l in rules.avionicsWiring) v.avionicsCables.Add(new CableLink { fromPort = l.from, toPort = l.to, cableType = l.cableType });
            foreach (var j in rules.boltJoints)
            {
                var st = new BoltJointState(j);
                for (int i = 0; i < j.boltCount; i++) st.Insert(i);
                for (int pass = 1; pass <= 3; pass++) foreach (var i in BoltMath.StarOrder(j.boltCount)) st.ApplyTorque(i, st.PassTargetNm(pass));
                st.SaveTo(v);
            }
            Assert.IsTrue(job.seededFaults.All(f => FailureLab.IsFixed(f, v, rules)), "a correct build has no faults");

            FailureLab.SeedFaults(v, rules, job.seededFaults);
            Assert.IsTrue(job.seededFaults.All(f => !FailureLab.IsFixed(f, v, rules)), "every seeded fault is detected");
            var report = AvionicsReport.Compute(v, rules);
            Assert.IsTrue(report.Has("s2_no_thrust") && report.Has("guidance_drift"));

            // Fix the wrong port: unplugging alone is not enough, the right cable must go in.
            v.avionicsCables.RemoveAll(c => c.fromPort == "av_flight_computer/DATA_3");
            Assert.IsFalse(FailureLab.IsFixed(job.seededFaults[2], v, rules));
            var imu = rules.avionicsWiring.First(l => l.id == "w_fc_imu");
            v.avionicsCables.Add(new CableLink { fromPort = imu.from, toPort = imu.to, cableType = imu.cableType });
            Assert.IsTrue(FailureLab.IsFixed(job.seededFaults[2], v, rules));
        }

        [Test]
        public void EverySeededJob_StartsFixedOnACorrectBuild_AndEveryFaultIsDetectedWhenSeeded()
        {
            var data = TestData.Registry; var rules = data.GetRules();
            var jobs = FailureLab.Jobs(data).Where(j => j.seededFaults.Count > 0).ToList();
            Assert.GreaterOrEqual(jobs.Count, 4, "Broken Pathfinder plus the three added jobs");
            foreach (var job in jobs)
            {
                var v = new VehicleDesign { rocketId = "pathfinder" };
                foreach (var l in rules.avionicsWiring) v.avionicsCables.Add(new CableLink { fromPort = l.from, toPort = l.to, cableType = l.cableType });
                foreach (var j in rules.boltJoints)
                {
                    var st = new BoltJointState(j);
                    for (int i = 0; i < j.boltCount; i++) st.Insert(i);
                    for (int pass = 1; pass <= 3; pass++) foreach (var i in BoltMath.StarOrder(j.boltCount)) st.ApplyTorque(i, st.PassTargetNm(pass));
                    st.SaveTo(v);
                }
                Assert.IsTrue(job.seededFaults.All(f => FailureLab.IsFixed(f, v, rules)), job.id + ": a correct build has no faults");
                FailureLab.SeedFaults(v, rules, job.seededFaults);
                foreach (var f in job.seededFaults)
                    Assert.IsFalse(FailureLab.IsFixed(f, v, rules), job.id + ": fault '" + f + "' must be detected once seeded");
                Assert.IsFalse(string.IsNullOrEmpty(job.lesson), job.id + " needs a lesson");
                Assert.IsFalse(string.IsNullOrEmpty(job.clientNote), job.id + " needs a client note");
            }
        }

        [Test]
        public void Apollo12_SceToAuxThenFuelCells_WrongCallsCostPoints()
        {
            var job = TestData.Registry.GetMission("failure_apollo12");
            Assert.IsTrue(job.consoleChoices.Any(c => c.label == "SCE to AUX" && c.correctAtStep == 1));
            Assert.IsTrue(job.consoleChoices.All(c => !string.IsNullOrEmpty(c.why)), "every choice explains itself");
            var run = new Apollo12Console.Run();
            Assert.IsFalse(run.Pick(job.consoleChoices.First(c => c.id == "abort")));
            Assert.IsFalse(run.Pick(job.consoleChoices.First(c => c.id == "fc_reset")), "fuel cells first is the wrong order");
            Assert.IsTrue(run.Pick(job.consoleChoices.First(c => c.id == "sce_aux")));
            Assert.IsTrue(run.Pick(job.consoleChoices.First(c => c.id == "fc_reset")));
            Assert.AreEqual(3, run.Step);
            Assert.AreEqual(job.rewardPoints - 120, run.Score(job.rewardPoints));
            StringAssert.Contains("John Aaron", job.lesson);
        }

        [Test]
        public void JobScore_PenalisesHintsAndFaultsLeftIn()
        {
            Assert.AreEqual(300, FailureJobTracker.Score(300, 3, 3, 0));
            Assert.AreEqual(270, FailureJobTracker.Score(300, 3, 3, 3));
            Assert.AreEqual(150, FailureJobTracker.Score(300, 3, 2, 0));
        }
    }
}
