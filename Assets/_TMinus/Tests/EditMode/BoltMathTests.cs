using NUnit.Framework;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Tests
{
    public class BoltMathTests
    {
        static BoltJointDef Joint12() => new BoltJointDef
        {
            id = "j_s1_interstage", boltCount = 12, size = "M10", torqueNm = 45, tolerancePct = 10, maxQkPa = 45
        };

        [Test]
        public void StarOrder8_Matches()
        {
            Assert.AreEqual("1-5-3-7-2-6-4-8", BoltMath.StarOrderText(8));
        }

        [Test]
        public void StarOrder12_Matches()
        {
            Assert.AreEqual("1-7-4-10-2-8-5-11-3-9-6-12", BoltMath.StarOrderText(12));
        }

        [Test]
        public void StarOrder_OddCount_VisitsEveryHoleOnce()
        {
            var o = BoltMath.StarOrder(7);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6 }, o);
        }

        [Test]
        public void StarPattern_ThreePasses_GivesIntegrityOne()
        {
            var j = new BoltJointState(Joint12());
            for (int i = 0; i < 12; i++) j.Insert(i);
            foreach (var pass in new[] { 1, 2, 3 })
                foreach (var i in BoltMath.StarOrder(12))
                    Assert.IsEmpty(j.ApplyTorque(i, j.PassTargetNm(pass)), $"warp on pass {pass} bolt {i + 1}");

            var s = j.GetStatus();
            Assert.AreEqual(1.0, s.integrity, 1e-9);
            Assert.AreEqual(12, s.torqued);
            Assert.AreEqual(0, s.sequenceErrors);
            Assert.AreEqual(4, j.CurrentPass());
            Assert.AreEqual(-1, j.NextBolt());
        }

        [Test]
        public void ClockwiseFullTorqueOnPassOne_WarpsAndLowersIntegrity()
        {
            var j = new BoltJointState(Joint12());
            for (int i = 0; i < 12; i++) j.Insert(i);
            var loose = j.ApplyTorque(0, 45);
            CollectionAssert.AreEquivalent(new[] { 1, 11 }, loose);
            for (int i = 1; i < 12; i++) j.ApplyTorque(i, 45);

            var s = j.GetStatus();
            Assert.Greater(s.sequenceErrors, 0);
            Assert.Less(s.integrity, 1.0);
            Assert.AreEqual(1.0 - BoltMath.MaxWarpPenalty, s.integrity, 1e-9); // fails a max-Q check tuned at 0.75
            Assert.AreEqual(12, s.torqued);
        }

        [Test]
        public void IntegrityCountsMissingUnderAndOver()
        {
            var j = new BoltJointState(Joint12());
            for (int i = 0; i < 8; i++) j.Insert(i);      // 4 missing
            for (int p = 1; p <= 3; p++)
                foreach (var i in BoltMath.StarOrder(12))
                    if (i < 6) j.ApplyTorque(i, j.PassTargetNm(p));
            j.ApplyTorque(6, 20);                         // under
            j.ApplyTorque(7, 60);                         // over (stripped)

            var s = j.GetStatus();
            Assert.AreEqual(4, s.missing);
            Assert.AreEqual(1, s.under);
            Assert.AreEqual(1, s.over);
            Assert.AreEqual(BoltState.OverTorqued, j.StateOf(7));
            // 6 good of 12, minus over-torque penalty, minus any sequence errors.
            Assert.AreEqual(6.0 / 12 - BoltMath.OverPenalty - BoltMath.WarpPenalty(s.sequenceErrors), s.integrity, 1e-9);
        }

        [Test]
        public void NextBolt_FollowsStarOrder_AndSaveLoadRoundTrips()
        {
            var j = new BoltJointState(Joint12());
            Assert.AreEqual(0, j.NextBolt());
            for (int i = 0; i < 12; i++) j.Insert(i);
            j.ApplyTorque(0, j.PassTargetNm(1));
            Assert.AreEqual(6, j.NextBolt()); // bolt 7 after bolt 1

            var design = new VehicleDesign();
            j.SaveTo(design);
            var k = BoltJointState.LoadFrom(Joint12(), design);
            Assert.AreEqual(j.GetStatus().ToString(), k.GetStatus().ToString());
            Assert.AreEqual(6, k.NextBolt());
        }
    }
}
