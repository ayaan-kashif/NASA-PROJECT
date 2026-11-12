using System.Collections.Generic;
using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class DataRegistryTests
    {
        [Test]
        public void EmptyDataFolder_LoadsZeroWithoutErrors()
        {
            var r = DataRegistry.LoadFromText(new Dictionary<string, string>());
            Assert.AreEqual(0, r.Parts.Count);
            Assert.AreEqual(0, r.Rockets.Count);
            Assert.AreEqual(0, r.Missions.Count);
            Assert.IsTrue(r.GetRules().IsEmpty);
            Assert.IsEmpty(r.Errors);
            Assert.IsNull(r.GetPart("anything"));
        }

        [Test]
        public void NullInput_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => DataRegistry.LoadFromText(null));
        }

        [Test]
        public void BareArrayAndWrappedArray_BothLoad()
        {
            var r = DataRegistry.LoadFromText(new Dictionary<string, string>
            {
                ["parts"] = "[{\"id\":\"eps\",\"name\":\"EPS\",\"massKg\":0.1}]",
                ["rockets"] = "{\"rockets\":[{\"id\":\"pathfinder\",\"name\":\"Pathfinder\",\"futureField\":42}]}"
            });
            Assert.IsEmpty(r.Errors);
            Assert.AreEqual(0.1, r.GetPart("eps").massKg, 1e-9);
            Assert.AreEqual(42, (int)r.GetRocket("pathfinder").extra["futureField"]);
        }

        [Test]
        public void InvalidJsonAndDuplicates_AreReportedNotThrown()
        {
            var r = DataRegistry.LoadFromText(new Dictionary<string, string>
            {
                ["parts"] = "[{\"id\":\"a\"},{\"id\":\"a\"},{\"name\":\"no id\"}]",
                ["missions"] = "{ not json",
                ["rules"] = "[]"
            });
            Assert.AreEqual(1, r.Parts.Count);
            Assert.AreEqual(4, r.Errors.Count); // duplicate, missing id, bad missions, rules not object
        }
    }

    public class EventLogTests
    {
        [Test]
        public void AddQueryClear()
        {
            double now = 0;
            var log = new EventLog(() => now);
            log.Add("VAB", "PartAttached", "Stage 1 attached");
            now = 10;
            log.Add("Flight", "SepFail", "Stage separation failed", "PYRO_OUT_2", Severity.Fault);

            Assert.AreEqual(2, log.All.Count);
            Assert.AreEqual(1, log.Query("Flight").Count);
            Assert.AreEqual(1, log.Query(null, 5).Count);
            Assert.AreEqual("PYRO_OUT_2", log.Query("Flight")[0].causeId);

            log.Clear();
            Assert.AreEqual(0, log.All.Count);
        }
    }

    public class SceneFlowTests
    {
        [Test]
        public void NextFollowsMissionLoop()
        {
            Assert.AreEqual(SceneNames.IntegrationBay, SceneFlow.NextAfter(SceneNames.MissionBriefing));
            Assert.AreEqual(SceneNames.VAB, SceneFlow.NextAfter(SceneNames.IntegrationBay));
            Assert.AreEqual(SceneNames.Flight, SceneFlow.NextAfter(SceneNames.VAB));
            Assert.AreEqual(SceneNames.Debrief, SceneFlow.NextAfter(SceneNames.Flight));
            Assert.AreEqual(SceneNames.MissionBriefing, SceneFlow.NextAfter(SceneNames.Debrief));
            Assert.AreEqual(SceneNames.MissionBriefing, SceneFlow.NextAfter(SceneNames.MainMenu));
        }

        [Test]
        public void BootIsFirstScene()
        {
            Assert.AreEqual(SceneNames.Boot, SceneNames.All[0]);
            Assert.AreEqual(7, SceneNames.All.Length);
        }
    }
}
