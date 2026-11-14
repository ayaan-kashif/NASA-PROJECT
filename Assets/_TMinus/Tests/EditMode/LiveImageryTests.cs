using System.Linq;
using NUnit.Framework;
using TMinus.Mission;

namespace TMinus.Tests
{
    public class LiveImageryTests
    {
        [Test]
        public void EveryRegionHasASensibleBoxAndAUniqueId()
        {
            var live = LiveImagery.Regions.Where(r => !LiveImagery.IsBundled(r)).ToList();
            Assert.GreaterOrEqual(live.Count, 5);
            foreach (var r in live)
            {
                Assert.Less(r.minLat, r.maxLat, r.id);
                Assert.Less(r.minLon, r.maxLon, r.id);
                Assert.That(r.minLat, Is.InRange(-90, 90), r.id);
                Assert.That(r.maxLat, Is.InRange(-90, 90), r.id);
                Assert.That(r.minLon, Is.InRange(-180, 180), r.id);
                Assert.That(r.maxLon, Is.InRange(-180, 180), r.id);
            }
            Assert.AreEqual(LiveImagery.Regions.Length, LiveImagery.Regions.Select(r => r.id).Distinct().Count());
        }

        [Test]
        public void TheAddressUsesSouthWestNorthEastAndNeverTheLocalDecimalComma()
        {
            var r = LiveImagery.ById("karachi");
            string url = LiveImagery.Url(r, 512);
            StringAssert.StartsWith("https://wvs.earthdata.nasa.gov/api/v1/snapshot?", url);
            StringAssert.Contains("BBOX=23.5,66,25.8,68.4", url);
            StringAssert.Contains("WIDTH=512&HEIGHT=512", url);
            StringAssert.Contains("LAYERS=MODIS_Terra_CorrectedReflectance_TrueColor", url);
            StringAssert.DoesNotContain("WRAP", url, "NASA Worldview answers 'Invalid value for WRAP' to WRAP=DAY,DAY");
        }

        [Test]
        public void AnUnknownRegionFallsBackToTheBundledOne()
        {
            Assert.IsTrue(LiveImagery.IsBundled(LiveImagery.ById("nowhere")));
        }
    }
}
