using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Mission;

namespace TMinus.Tests
{
    public class ChecklistTests
    {
        static readonly string[] Parts =
            { "eps", "batt_20", "dep_switch", "obc", "radio_sband", "ant_patch", "adcs_mag", "cam_wide", "solar_body", "rbf_pin" };

        static readonly string[][] Cables =
        {
            new[] { "solar_body/SOLAR_OUT_1", "eps/SOLAR_IN_1" }, new[] { "batt_20/BATT", "dep_switch/BATT_IN" },
            new[] { "dep_switch/BATT_OUT", "eps/BATT" }, new[] { "eps/PWR_OUT_1", "obc/PWR_IN" },
            new[] { "eps/PWR_OUT_2", "radio_sband/PWR_IN" }, new[] { "eps/PWR_OUT_3", "adcs_mag/PWR_IN" },
            new[] { "eps/PWR_OUT_4", "cam_wide/PWR_IN" }, new[] { "eps/DATA", "obc/DATA_1" },
            new[] { "radio_sband/DATA", "obc/DATA_2" }, new[] { "adcs_mag/DATA", "obc/DATA_3" },
            new[] { "cam_wide/DATA", "obc/DATA_4" }, new[] { "radio_sband/RF", "ant_patch/RF" },
        };

        /// <summary>A flight-ready FloodWatch-1 build: S-band CubeSat, Pathfinder stacked, bolted in star order, fully wired.</summary>
        public static PrelaunchChecklist.Input Perfect()
        {
            var data = TestData.Registry;
            var rules = data.GetRules();
            var sc = new SpacecraftDesign { orbitAltitudeKm = 550 };
            foreach (var p in Parts) sc.parts.Add(new PlacedPart { instanceId = p, partId = p });
            foreach (var c in Cables) sc.cables.Add(new CableLink { fromPort = c[0], toPort = c[1] });

            var v = new VehicleDesign { rocketId = "pathfinder", launchSiteId = "sonmiani" };
            foreach (var id in rules.assemblyOrder["pathfinder"]) v.parts.Add(new PlacedPart { instanceId = id, partId = id });
            foreach (var j in rules.boltJoints)
            {
                var st = new BoltJointState(j);
                for (int i = 0; i < j.boltCount; i++) st.Insert(i);
                for (int pass = 1; pass <= 3; pass++) foreach (var i in BoltMath.StarOrder(j.boltCount)) st.ApplyTorque(i, st.PassTargetNm(pass));
                st.SaveTo(v);
            }
            foreach (var l in rules.avionicsWiring) v.avionicsCables.Add(new CableLink { fromPort = l.from, toPort = l.to, cableType = l.cableType });
            return new PrelaunchChecklist.Input { Data = data, CurrentMissionId = "floodwatch_1", Spacecraft = sc, Vehicle = v };
        }

        static string Dump(System.Collections.Generic.List<ChecklistItem> items) => string.Join("\n", items);

        [Test]
        public void PerfectBuild_OnlyPadActionsRemain()
        {
            var items = PrelaunchChecklist.Build(Perfect());
            var fails = items.Where(i => !i.pass).Select(i => i.id).ToList();
            CollectionAssert.AreEquivalent(new[] { "pad_rbf", "pad_arm" }, fails, Dump(items));
            Assert.AreEqual("pull_rbf", items.First(i => i.id == "pad_rbf").action);
        }

        [Test]
        public void PadActionsDone_EverythingPasses()
        {
            var input = Perfect();
            input.Spacecraft.rbfPinPulled = true;
            input.Vehicle.armed = true;
            var items = PrelaunchChecklist.Build(input);
            Assert.IsTrue(items.All(i => i.pass), Dump(items));
        }

        [Test]
        public void ChecklistReflectsRealFaults()
        {
            var input = Perfect();
            input.Spacecraft.cables.RemoveAll(c => c.fromPort == "radio_sband/RF");
            input.Vehicle.avionicsCables.RemoveAll(c => c.toPort == "av_stage_sep/PYRO_IN");
            input.Vehicle.bolts.RemoveAll(b => b.jointId == "j_s1_interstage" && b.boltIndex < 3);
            var items = PrelaunchChecklist.Build(input);
            Assert.IsFalse(items.First(i => i.id == "sc_wiring").pass);
            var av = items.First(i => i.id == "rk_avionics");
            Assert.IsFalse(av.pass);
            Assert.AreEqual(SceneNames.AvionicsBay, av.fixScene);
            var joints = items.First(i => i.id == "rk_joints");
            Assert.IsFalse(joints.pass);
            StringAssert.Contains("3 missing", joints.detail);
        }

        [Test]
        public void SonmianiBoost_IsAbout420()
        {
            Assert.AreEqual(420, PrelaunchChecklist.EarthBoostMs(25.2), 6);
        }

        [Test]
        public void RolesMerge_ForFewerPlayers()
        {
            var two = PrelaunchChecklist.AssignRoles(new[] { "Ayesha", "Bilal" });
            Assert.AreEqual(4, two.Count);
            Assert.AreEqual("Ayesha", two.First(c => c.role == CrewRole.FlightDirector).name);
            Assert.AreEqual("Bilal", two.First(c => c.role == CrewRole.PropulsionStructures).name);
            Assert.IsTrue(PrelaunchChecklist.AssignRoles(new[] { "Solo" }).All(c => c.name == "Solo"));
        }

        [Test]
        public void DesignCode_RoundTripsExactly()
        {
            var input = Perfect();
            var bundle = new DesignBundle { missionId = "floodwatch_1", spacecraft = input.Spacecraft, vehicle = input.Vehicle };
            var code = DesignCode.Encode(bundle);
            StringAssert.StartsWith(DesignCode.Prefix, code);
            Assert.IsTrue(DesignCode.TryDecode(code, out var back, out var err), err);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(bundle), Newtonsoft.Json.JsonConvert.SerializeObject(back));
            Assert.IsFalse(DesignCode.TryDecode("not a code", out _, out _));
        }
    }
}
