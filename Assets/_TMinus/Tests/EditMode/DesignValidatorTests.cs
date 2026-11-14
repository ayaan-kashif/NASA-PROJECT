using System.Linq;
using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class DesignValidatorTests
    {
        static DesignBundle Good()
        {
            var input = ChecklistTests.Perfect();
            return new DesignBundle { missionId = "floodwatch_1", spacecraft = input.Spacecraft, vehicle = input.Vehicle };
        }

        [Test]
        public void ACleanDesignIsLeftAlone()
        {
            var b = Good();
            int parts = b.spacecraft.parts.Count, cables = b.spacecraft.cables.Count, bolts = b.vehicle.bolts.Count;
            Assert.AreEqual(0, DesignValidator.Clean(b, TestData.Registry));
            Assert.AreEqual(parts, b.spacecraft.parts.Count);
            Assert.AreEqual(cables, b.spacecraft.cables.Count);
            Assert.AreEqual(bolts, b.vehicle.bolts.Count);
        }

        [Test]
        public void UnknownPartsAndCablesToNowhereAreDropped()
        {
            var b = Good();
            b.spacecraft.parts.Add(new PlacedPart { instanceId = "x", partId = "no_such_part" });
            b.spacecraft.cables.Add(new CableLink { fromPort = "ghost/PWR_OUT", toPort = "eps/PWR_IN" });
            b.spacecraft.cables.Add(new CableLink { fromPort = null, toPort = "eps/PWR_IN" });
            int dropped = DesignValidator.Clean(b, TestData.Registry);
            Assert.AreEqual(3, dropped);
            Assert.IsFalse(b.spacecraft.parts.Any(p => p.partId == "no_such_part"));
            Assert.IsFalse(b.spacecraft.cables.Any(c => c.fromPort == null || c.fromPort.StartsWith("ghost")));
        }

        [Test]
        public void NullListsAreReplacedAndBadBoltsRemoved()
        {
            var b = Good();
            b.vehicle.avionicsCables = null;
            b.vehicle.bolts.Add(new BoltRecord { jointId = "nope", boltIndex = 0, torqueNm = 10 });
            b.vehicle.bolts.Add(new BoltRecord { jointId = "j_s1_interstage", boltIndex = 99, torqueNm = 10 });
            Assert.DoesNotThrow(() => DesignValidator.Clean(b, TestData.Registry));
            Assert.IsNotNull(b.vehicle.avionicsCables);
            Assert.IsFalse(b.vehicle.bolts.Any(x => x.jointId == "nope" || x.boltIndex == 99));
        }

        [Test]
        public void LockedOrUnknownMissionFallsBackToFloodWatch()
        {
            var b = Good();
            b.missionId = "rehbar_1962";   // still a locked "coming next" mission
            Assert.Greater(DesignValidator.Clean(b, TestData.Registry), 0);
            Assert.AreEqual("floodwatch_1", b.missionId);
            b.missionId = "does_not_exist";
            DesignValidator.Clean(b, TestData.Registry);
            Assert.AreEqual("floodwatch_1", b.missionId);
        }

        [Test]
        public void OutOfRangeNumbersAreClamped()
        {
            var b = Good();
            b.spacecraft.orbitAltitudeKm = 99999;
            b.vehicle.pitchKickDeg = -5;
            b.vehicle.rocketId = "weird";
            DesignValidator.Clean(b, TestData.Registry);
            Assert.AreEqual(2000, b.spacecraft.orbitAltitudeKm);
            Assert.AreEqual(0, b.vehicle.pitchKickDeg);
            Assert.AreEqual("pathfinder", b.vehicle.rocketId);
        }
    }
}
