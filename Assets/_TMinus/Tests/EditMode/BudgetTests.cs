using System.Linq;
using NUnit.Framework;
using TMinus.Core;

namespace TMinus.Tests
{
    public class BudgetTests
    {
        static SpacecraftDesign Design(params string[] partIds)
        {
            var d = new SpacecraftDesign { orbitAltitudeKm = 550 };
            foreach (var id in partIds) d.parts.Add(new PlacedPart { instanceId = id, partId = id });
            return d;
        }

        static readonly string[] Default =
            { "eps", "batt_20", "dep_switch", "obc", "radio_uhf", "ant_uhf", "adcs_mag", "cam_wide", "solar_body", "rbf_pin" };

        static SpacecraftReport Report(SpacecraftDesign d)
        {
            var data = TestData.Registry;
            return SpacecraftBudget.Compute(d, data, data.GetMission("floodwatch_1"), data.GetRocket("pathfinder"));
        }

        [Test]
        public void Orbit550_PeriodAndEclipse()
        {
            Assert.AreEqual(95.5, SpacecraftBudget.PeriodMinutes(550), 1.0);
            Assert.AreEqual(35.0, SpacecraftBudget.EclipseFraction(550) * SpacecraftBudget.PeriodMinutes(550), 1.0);
        }

        [Test]
        public void DefaultWideBuild_MatchesBrief()
        {
            var r = Report(Design(Default));
            Assert.AreEqual(3.0, r.generationW, 0.1, "generation");
            Assert.AreEqual(2.1, r.consumptionW, 0.15, "consumption");
            Assert.AreEqual(BudgetStatus.Green, r.power);
            Assert.AreEqual(BudgetStatus.Green, r.battery);
            Assert.AreEqual(2.3, r.downlinkPerDayMB, 0.05);
            Assert.AreEqual(17, r.imagesDownlinkedMission, 1.0);
            Assert.AreEqual(BudgetStatus.Yellow, r.data);
            Assert.IsTrue(r.issues.Any(i => i.code == "downlink_short" && i.message.Contains("S-band")));
            Assert.AreEqual(BudgetStatus.Green, r.mass);
            Assert.AreEqual(BudgetStatus.Green, r.cost);
            Assert.IsFalse(r.HasBlockers, string.Join("\n", r.issues));
        }

        [Test]
        public void HdCameraWithoutWheels_PowerRedAndPointingWarning()
        {
            var parts = Default.Select(p => p == "cam_wide" ? "cam_hd" : p).ToArray();
            var r = Report(Design(parts));
            Assert.AreEqual(BudgetStatus.Red, r.power, $"gen {r.generationW:0.00} cons {r.consumptionW:0.00}");
            Assert.IsTrue(r.issues.Any(i => i.code == "power_negative"));
            Assert.IsTrue(r.issues.Any(i => i.code == "pointing_coarse"));
            Assert.AreEqual(SpacecraftBudget.MagnetorquerImageQuality, r.pointingQuality, 1e-9);
        }

        [Test]
        public void PremiumHdBuild_JustFitsBudget()
        {
            var r = Report(Design("eps", "batt_40", "dep_switch", "obc", "radio_sband", "ant_patch", "adcs_rw", "cam_hd", "solar_wings", "rbf_pin"));
            Assert.LessOrEqual(r.totalCostM, r.budgetM, "the brief says the premium HD build only just fits");
            Assert.AreEqual(BudgetStatus.Green, r.power, $"gen {r.generationW:0.00} cons {r.consumptionW:0.00}");
            Assert.AreEqual(1.0, r.pointingQuality);
            Assert.AreEqual(5.0, r.gsdM, 1e-9);
        }

        [Test]
        public void GsdScalesWithAltitude()
        {
            var d = Design(Default);
            d.orbitAltitudeKm = 800;
            Assert.AreEqual(100 * 800 / 550.0, Report(d).gsdM, 1e-6);
        }
    }
}
