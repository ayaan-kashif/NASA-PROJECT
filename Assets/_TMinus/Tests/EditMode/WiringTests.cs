using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMinus.Core;
using TMinus.Wiring;
using UnityEngine;

namespace TMinus.Tests
{
    public static class TestData
    {
        static DataRegistry _r;

        /// <summary>The real JSON files in Assets/_TMinus/Resources/Data.</summary>
        public static DataRegistry Registry
        {
            get
            {
                if (_r != null) return _r;
                var dir = Path.Combine(Application.dataPath, "_TMinus/Resources/Data");
                var files = Directory.GetFiles(dir, "*.json")
                    .ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllText, StringComparer.OrdinalIgnoreCase);
                _r = DataRegistry.LoadFromText(files);
                return _r;
            }
        }
    }

    public class WiringTests
    {
        static WiringGraph DefaultCubeSat(bool wire = true)
        {
            var d = TestData.Registry;
            var g = new WiringGraph(d.GetRules());
            foreach (var id in new[] { "eps", "batt_20", "dep_switch", "obc", "radio_uhf", "ant_uhf", "adcs_mag", "cam_wide", "solar_body" })
                g.AddDevice(id, d.GetPart(id));
            if (!wire) return g;
            Connect(g, "solar_body/SOLAR_OUT_1", "eps/SOLAR_IN_1");
            Connect(g, "batt_20/BATT", "dep_switch/BATT_IN");
            Connect(g, "dep_switch/BATT_OUT", "eps/BATT");
            Connect(g, "eps/PWR_OUT_1", "obc/PWR_IN");
            Connect(g, "eps/PWR_OUT_2", "radio_uhf/PWR_IN");
            Connect(g, "eps/PWR_OUT_3", "adcs_mag/PWR_IN");
            Connect(g, "eps/PWR_OUT_4", "cam_wide/PWR_IN");
            Connect(g, "eps/DATA", "obc/DATA_1");
            Connect(g, "radio_uhf/DATA", "obc/DATA_2");
            Connect(g, "adcs_mag/DATA", "obc/DATA_3");
            Connect(g, "cam_wide/DATA", "obc/DATA_4");
            Connect(g, "radio_uhf/RF", "ant_uhf/RF");
            return g;
        }

        static WiringGraph.Cable Connect(WiringGraph g, string a, string b, bool reversed = false)
        {
            var c = g.Connect(a, b, reversed, out var reason);
            Assert.IsNotNull(c, reason);
            return c;
        }

        static string Dump(System.Collections.Generic.List<WiringFault> f) => string.Join("\n", f);

        [Test]
        public void DefaultCubeSat_FullyWired_HasZeroFaults()
        {
            var faults = WiringValidator.Validate(DefaultCubeSat());
            Assert.IsEmpty(faults, Dump(faults));
        }

        [Test]
        public void WrongCableType_IsRejectedWithReason()
        {
            var g = DefaultCubeSat(false);
            Assert.IsNull(g.Connect("radio_uhf/RF", "obc/DATA_1", false, out var reason));
            StringAssert.Contains("RF coax can't plug into a DATA port", reason);
        }

        [Test]
        public void ReversedPolarity_IsAFault()
        {
            var g = DefaultCubeSat(false);
            Connect(g, "eps/PWR_OUT_1", "obc/PWR_IN", reversed: true);
            var faults = WiringValidator.Validate(g);
            Assert.IsTrue(faults.Any(f => f.code == "reversed_polarity"), Dump(faults));
        }

        [Test]
        public void ShortCircuit_IsAFault()
        {
            var d = TestData.Registry;
            var g = new WiringGraph(d.GetRules());
            g.AddDevice("eps", d.GetPart("eps"));
            g.AddDevice("pdu", d.GetPart("av_pdu"));
            var c = Connect(g, "eps/PWR_OUT_1", "pdu/PWR_OUT_1");
            Assert.IsTrue(c.shortCircuit);
            Assert.IsTrue(WiringValidator.Validate(g).Any(f => f.code == "short_circuit" && f.severity == Severity.Blocker));
        }

        [Test]
        public void SecondCableOnAnOutput_IsRefused()
        {
            var g = DefaultCubeSat(false);
            Connect(g, "eps/PWR_OUT_1", "radio_uhf/PWR_IN");
            Assert.IsNull(g.Connect("eps/PWR_OUT_1", "obc/PWR_IN", false, out var reason));
            StringAssert.Contains("already has a cable", reason);
        }

        [Test]
        public void RailOverload_IsAFault()
        {
            var d = TestData.Registry;
            var g = DefaultCubeSat(false);
            // A small shared supply rail (bus) rated 5 W feeding 4 W + 2 W of loads.
            var supply = new TMinus.Core.Data.PartDef { id = "test_supply", name = "Test supply", category = "test" };
            supply.ports.Add(new TMinus.Core.Data.PortDef { id = "PWR_OUT_1", type = "PWR_OUT", maxPowerW = 5, bus = true, label = "5 W rail" });
            g.AddDevice("supply", supply);
            Connect(g, "supply/PWR_OUT_1", "radio_uhf/PWR_IN");
            Connect(g, "supply/PWR_OUT_1", "cam_wide/PWR_IN");
            var faults = WiringValidator.Validate(g);
            Assert.IsTrue(faults.Any(f => f.code == "rail_overload" && f.device == "supply"), Dump(faults));
        }

        [Test]
        public void MissingRequiredConnection_IsReported()
        {
            var g = DefaultCubeSat();
            var rf = g.Cables.First(c => c.type == "rf");
            g.Disconnect(rf.id);
            var faults = WiringValidator.Validate(g);
            Assert.IsTrue(faults.Any(f => f.code == "missing_connection" && f.device == "radio_uhf"), Dump(faults));
            Assert.IsTrue(faults.Any(f => f.code == "missing_connection" && f.device == "ant_uhf"), Dump(faults));
        }

        [Test]
        public void BatteryStraightToEps_SwitchNotInline()
        {
            var g = DefaultCubeSat(false);
            Connect(g, "batt_20/BATT", "eps/BATT");
            var faults = WiringValidator.Validate(g);
            Assert.AreEqual(1, faults.Count(f => f.code == "switch_not_inline"), Dump(faults));
        }

        [Test]
        public void BatteryToLoad_WarnsBypass()
        {
            var g = DefaultCubeSat(false);
            Connect(g, "batt_20/BATT", "cam_wide/PWR_IN");
            Assert.IsTrue(WiringValidator.Validate(g).Any(f => f.code == "battery_bypasses_eps" && f.severity == Severity.Warning));
        }

        [Test]
        public void Multimeter_FindsPathOrOpenCircuit()
        {
            var g = DefaultCubeSat();
            Assert.AreEqual(1, g.PathLength("radio_uhf/RF", "ant_uhf/RF"));
            Assert.AreEqual(-1, g.PathLength("radio_uhf/RF", "cam_wide/DATA"));
            Assert.AreEqual(2, g.PathLength("radio_uhf/DATA", "cam_wide/DATA")); // via the OBC's shared data bus
        }
    }
}
