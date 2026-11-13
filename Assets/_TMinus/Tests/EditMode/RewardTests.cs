using NUnit.Framework;
using TMinus.Mission;
using UnityEngine;

namespace TMinus.Tests
{
    public class RewardTests
    {
        [Test]
        public void CoarserCamera_ResolvesFewerPixels_NeverSharperThanLandsat()
        {
            Assert.AreEqual(300, RewardGallery.ResolvedWidth(5));
            Assert.AreEqual(300, RewardGallery.ResolvedWidth(30));
            Assert.AreEqual(90, RewardGallery.ResolvedWidth(100));
            Assert.Less(RewardGallery.ResolvedWidth(150), RewardGallery.ResolvedWidth(100));
        }

        [Test]
        public void ImagesReturned_UnlockDates()
        {
            Assert.AreEqual(0, RewardGallery.UnlockedCount(0, 0, 3));
            Assert.AreEqual(1, RewardGallery.UnlockedCount(0.05, 2, 3));
            Assert.AreEqual(2, RewardGallery.UnlockedCount(0.7, 50, 3));
            Assert.AreEqual(3, RewardGallery.UnlockedCount(1, 87, 3));
        }

        [Test]
        public void LandsatImages_LoadFromMissionData()
        {
            var m = TestData.Registry.GetMission("floodwatch_1");
            Assert.IsNotNull(m, "floodwatch mission");
            Assert.AreEqual(3, m.rewardImages.Count);
            foreach (var img in m.rewardImages)
            {
                var tex = Resources.Load<Texture2D>(img.resource);
                Assert.IsNotNull(tex, img.resource);
                var small = RewardGallery.Degrade(tex, 90);
                Assert.AreEqual(90, small.width);
            }
            StringAssert.Contains("Joshua Stevens", m.rewardCredit);
        }
    }
}
