using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core.Data;

namespace TMinus.Core
{
    public enum BudgetStatus { Green, Yellow, Red }

    public sealed class BudgetIssue
    {
        public string code;
        public Severity severity;
        public string message;
        public string codexId;
        public override string ToString() => $"[{severity}] {code}: {message}";
    }

    /// <summary>Every number the bay, HOUSTON, the checklist and the operations sim need about a CubeSat design.</summary>
    public sealed class SpacecraftReport
    {
        // Orbit
        public double altitudeKm, periodMin, eclipseMin, sunlitFraction;
        // Mass and cost
        public double massKg, massLimitKg, spacecraftCostK, launchCostM, totalCostM, budgetM;
        // Power (orbit average)
        public double generationW, consumptionW, marginW, alwaysOnW, imagingW, transmitW;
        // Battery
        public double batteryWh, eclipseEnergyWh, requiredBatteryWh;
        // Data
        public double imagesPerDay, imageMB, dataPerDayMB, downlinkPerDayMB, storageMB, daysUntilFull, imagesDownlinkedMission;
        public int imageGoal, durationDays;
        // Pointing and resolution
        public double pointingQuality, gsdM;
        public string cameraId, radioId, adcsId;

        public BudgetStatus mass, cost, power, battery, data, pointing;
        public readonly List<BudgetIssue> issues = new List<BudgetIssue>();

        public bool HasBlockers => issues.Any(i => i.severity >= Severity.Blocker);
    }

    /// <summary>
    /// Budgets for a 3U CubeSat at a target altitude (Prompt 7 formulas):
    /// T = 2π√(r³/μ); eclipse fraction = asin(R/r)/π (worst case); generation = Σ peak × sunlit × pointing;
    /// battery needs eclipse energy / 0.3 (max depth of discharge); downlink = rate × contacts × minutes.
    /// </summary>
    public static class SpacecraftBudget
    {
        public const double Mu = 3.986004418e14, EarthRadiusKm = 6371.0, MaxDepthOfDischarge = 0.3;
        /// <summary>Camera is on for this long per imaging pass.</summary>
        public const double ImagingMinutesPerPass = 10;
        public const double MagnetorquerImageQuality = 0.3;

        public static double PeriodMinutes(double altitudeKm)
        {
            double r = (EarthRadiusKm + altitudeKm) * 1000;
            return 2 * Math.PI * Math.Sqrt(r * r * r / Mu) / 60.0;
        }

        public static double EclipseFraction(double altitudeKm) =>
            Math.Asin(EarthRadiusKm / (EarthRadiusKm + altitudeKm)) / Math.PI;

        public static SpacecraftReport Compute(SpacecraftDesign design, DataRegistry data, MissionDef mission, RocketDef rocket)
        {
            var r = new SpacecraftReport();
            var parts = design.parts.Select(p => data.GetPart(p.partId)).Where(p => p != null).ToList();
            var frame = data.GetPart("frame_3u");
            if (frame != null && parts.All(p => p.id != "frame_3u")) parts.Add(frame);

            // ---- orbit
            r.altitudeKm = design.orbitAltitudeKm > 0 ? design.orbitAltitudeKm : mission?.defaultOrbitKm ?? 550;
            r.periodMin = PeriodMinutes(r.altitudeKm);
            double fe = EclipseFraction(r.altitudeKm);
            r.sunlitFraction = 1 - fe;
            r.eclipseMin = fe * r.periodMin;

            // ---- mass and cost
            r.massKg = parts.Sum(p => p.massKg);
            // A mission with massLimitKg 0 (the Sandbox) has no mass limit; show a generous one so the bar still reads.
            r.massLimitKg = mission == null ? 4.0 : mission.massLimitKg > 0 ? mission.massLimitKg : 100.0;
            r.spacecraftCostK = parts.Sum(p => p.costK);
            r.launchCostM = rocket?.launchCostMUSD ?? 0;
            r.totalCostM = r.launchCostM + r.spacecraftCostK / 1000.0;
            r.budgetM = mission?.budgetMUSD ?? 0;
            r.mass = r.massKg > r.massLimitKg ? BudgetStatus.Red : r.massKg > r.massLimitKg * 0.9 ? BudgetStatus.Yellow : BudgetStatus.Green;
            r.cost = r.budgetM <= 0 ? BudgetStatus.Green : r.totalCostM > r.budgetM + 1e-9 ? BudgetStatus.Red : r.totalCostM > r.budgetM * 0.99 ? BudgetStatus.Yellow : BudgetStatus.Green;
            if (r.mass == BudgetStatus.Red) Add(r, "over_mass", Severity.Blocker, $"Too heavy: {r.massKg:0.00} kg, the 3U limit is {r.massLimitKg:0.0} kg.", "staging");
            if (r.cost == BudgetStatus.Red) Add(r, "over_budget", Severity.Blocker, $"Over budget: {r.totalCostM:0.000} M USD of {r.budgetM:0.00} M (launch {r.launchCostM:0.0} M + spacecraft {r.spacecraftCostK:0} k).", null);

            // ---- power
            int contacts = mission?.groundContactsPerDay ?? 4;
            double contactMin = mission?.contactMinutes ?? 8;
            int passes = mission?.imagingPassesPerDay ?? 1;
            double contactDuty = contacts * contactMin / 1440.0;
            double imagingDuty = passes * ImagingMinutesPerPass / 1440.0;

            var camera = parts.FirstOrDefault(p => p.category == "camera");
            var radio = parts.FirstOrDefault(p => p.category == "radio");
            var adcs = parts.FirstOrDefault(p => p.category == "adcs");
            r.cameraId = camera?.id; r.radioId = radio?.id; r.adcsId = adcs?.id;

            r.alwaysOnW = parts.Sum(p => p.powerIdleW);
            r.imagingW = camera != null ? (camera.powerActiveW - camera.powerIdleW) * imagingDuty : 0;
            r.transmitW = radio != null ? (radio.powerActiveW - radio.powerIdleW) * contactDuty : 0;
            r.consumptionW = r.alwaysOnW + r.imagingW + r.transmitW;
            r.generationW = parts.Where(p => p.category == "solar").Sum(p => p.Spec("peakW") * r.sunlitFraction * p.Spec("pointingFactor", 0.6));
            r.marginW = r.generationW - r.consumptionW;
            r.power = r.marginW < 0 ? BudgetStatus.Red : r.marginW < 0.1 * r.consumptionW ? BudgetStatus.Yellow : BudgetStatus.Green;
            if (r.generationW <= 0) Add(r, "no_solar", Severity.Blocker, "No solar panels: the battery will run flat.", "eclipse");
            else if (r.power == BudgetStatus.Red) Add(r, "power_negative", Severity.Blocker, $"Power deficit: makes {r.generationW:0.00} W but uses {r.consumptionW:0.00} W on average. Add panels or remove loads.", "eclipse");
            else if (r.power == BudgetStatus.Yellow) Add(r, "power_tight", Severity.Warning, $"Power margin under 10% ({r.marginW:0.00} W).", "eclipse");

            // ---- battery
            r.batteryWh = parts.Where(p => p.category == "battery").Sum(p => p.Spec("capacityWh"));
            r.eclipseEnergyWh = r.consumptionW * r.eclipseMin / 60.0;
            r.requiredBatteryWh = r.eclipseEnergyWh / MaxDepthOfDischarge;
            r.battery = r.batteryWh < r.requiredBatteryWh ? BudgetStatus.Red : r.batteryWh < r.requiredBatteryWh * 1.2 ? BudgetStatus.Yellow : BudgetStatus.Green;
            if (r.batteryWh <= 0) Add(r, "no_battery", Severity.Blocker, "No battery: nothing powers the satellite in eclipse.", "battery_dod");
            else if (r.battery == BudgetStatus.Red) Add(r, "battery_small", Severity.Fault, $"Battery too small: needs {r.requiredBatteryWh:0.0} Wh to stay under 30% depth of discharge, has {r.batteryWh:0} Wh.", "battery_dod");

            // ---- data
            var obc = parts.FirstOrDefault(p => p.category == "obc");
            r.storageMB = obc?.Spec("storageMB") ?? 0;
            r.imageMB = camera?.Spec("imageMB") ?? 0;
            r.imagesPerDay = camera != null ? passes * (mission?.imagesPerPass ?? 3) : 0;
            r.dataPerDayMB = r.imagesPerDay * r.imageMB;
            double rateKbps = radio?.Spec("dataRateKbps") ?? 0;
            r.downlinkPerDayMB = rateKbps * contacts * contactMin * 60 / 8.0 / 1000.0;
            r.durationDays = mission?.durationDays > 0 ? mission.durationDays : 30;
            r.daysUntilFull = r.downlinkPerDayMB >= r.dataPerDayMB || r.storageMB <= 0 ? double.PositiveInfinity
                            : r.storageMB / (r.dataPerDayMB - r.downlinkPerDayMB);
            double imagesSendable = r.imageMB > 0 ? r.downlinkPerDayMB * r.durationDays / r.imageMB : 0;
            r.imagesDownlinkedMission = Math.Min(imagesSendable, r.imagesPerDay * r.durationDays);
            var goal = mission?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked");
            r.imageGoal = goal != null ? (int)goal.threshold : 0;
            if (camera == null) { r.data = BudgetStatus.Red; Add(r, "no_camera", Severity.Blocker, "No camera: this mission needs images.", "gsd"); }
            else if (radio == null) { r.data = BudgetStatus.Red; Add(r, "no_radio", Severity.Blocker, "No radio: images can't reach the ground.", "downlink"); }
            else if (r.imageGoal > 0 && r.imagesDownlinkedMission < r.imageGoal)
            {
                r.data = BudgetStatus.Yellow;
                Add(r, "downlink_short", Severity.Warning,
                    $"Only about {r.imagesDownlinkedMission:0} images reach the ground in {r.durationDays} days ({r.downlinkPerDayMB:0.0} MB/day downlink); the goal is {r.imageGoal}. A faster radio (S-band) fixes this.", "downlink");
            }
            else r.data = BudgetStatus.Green;

            // ---- pointing and resolution
            bool needsWheels = camera != null && camera.requires.Contains("adcs_rw");
            bool hasWheels = adcs?.id == "adcs_rw";
            r.pointingQuality = adcs == null ? 0 : needsWheels && !hasWheels ? MagnetorquerImageQuality : 1.0;
            r.gsdM = camera != null ? camera.Spec("gsdMAt550") * r.altitudeKm / 550.0 : 0;
            r.pointing = adcs == null ? BudgetStatus.Red : r.pointingQuality < 1 ? BudgetStatus.Yellow : BudgetStatus.Green;
            if (adcs == null) Add(r, "no_adcs", Severity.Blocker, "No ADCS: the satellite will tumble and the camera can't aim.", "adcs");
            else if (r.pointingQuality < 1)
                Add(r, "pointing_coarse", Severity.Warning, $"{camera.name} needs reaction wheels. With magnetorquers it blurs: image quality {r.pointingQuality:0.0}.", "adcs");

            r.issues.Sort((a, b) => b.severity.CompareTo(a.severity));
            return r;
        }

        static void Add(SpacecraftReport r, string code, Severity s, string msg, string codex) =>
            r.issues.Add(new BudgetIssue { code = code, severity = s, message = msg, codexId = codex });
    }
}
