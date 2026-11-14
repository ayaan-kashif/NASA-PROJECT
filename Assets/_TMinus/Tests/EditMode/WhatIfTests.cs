using NUnit.Framework;
using TMinus.Core;
using TMinus.Houston;

namespace TMinus.Tests
{
    public class WhatIfTests
    {
        [Test]
        public void OnlyQuestionsAboutAPartSwapAreRecognised()
        {
            Assert.IsTrue(WhatIf.LooksLikeWhatIf("What if I use the HD camera?"));
            Assert.IsTrue(WhatIf.LooksLikeWhatIf("swap to the s-band radio"));
            Assert.IsFalse(WhatIf.LooksLikeWhatIf("What does the EPS do?"));
            Assert.IsFalse(WhatIf.LooksLikeWhatIf("what if it rains"));
        }

        [Test]
        public void HdCameraCostsMoreAndShowsFinerDetail_WithoutChangingTheBuild()
        {
            var input = ChecklistTests.Perfect();
            var data = TestData.Registry;
            var mission = data.GetMission("floodwatch_1");
            var rocket = data.GetRocket("pathfinder");
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(input.Spacecraft);

            var answer = WhatIf.Evaluate("what if I use the HD camera?", data, input.Spacecraft, mission, rocket);

            Assert.IsNotNull(answer);
            StringAssert.Contains("Imaging camera", answer.text);
            StringAssert.Contains("Cost:", answer.text);
            StringAssert.Contains("Detail per pixel", answer.text);
            Assert.AreEqual("gsd", answer.codexId);
            Assert.AreEqual(before, Newtonsoft.Json.JsonConvert.SerializeObject(input.Spacecraft), "the real design must not change");
        }

        [Test]
        public void AskingAboutThePartYouAlreadyHaveSaysSo()
        {
            var input = ChecklistTests.Perfect();
            var answer = WhatIf.Evaluate("what if I use the wide camera", TestData.Registry, input.Spacecraft,
                TestData.Registry.GetMission("floodwatch_1"), TestData.Registry.GetRocket("pathfinder"));
            StringAssert.Contains("already use", answer.text);
        }
    }
}
