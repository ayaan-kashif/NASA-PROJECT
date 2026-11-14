using System;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Mission
{
    /// <summary>Deterministic one-step-per-orbit mission operations model.</summary>
    public static class OpsSimulation
    {
        public const double StorageLimitMB = 2000;
        public static OpsReport Run(GameState state, SpacecraftReport budget)
        {
            var mission = state.Data.GetMission(state.CurrentMissionId);
            var report = new OpsReport { capacityWh = budget.batteryWh, rewardCredit = mission?.rewardCredit ?? "" };
            report.rewardGsdM = budget.gsdM;
            var powerIssue = budget.issues.FirstOrDefault(i => i.code == "power_negative");
            if (powerIssue != null)
            {
                report.failures.Add(powerIssue.message);
                state.Events.AddAt(state.Flight.flightTimeS, "operations", "power_budget_deficit", powerIssue.message, powerIssue.codexId ?? "eclipse", Severity.Fault);
            }
            var firstImage = mission?.rewardImages?.FirstOrDefault();
            report.rewardTitle = firstImage?.title ?? "Lake Manchar — Landsat reward";
            var sw = state.Spacecraft.parts.FirstOrDefault(p => state.Data.GetPart(p.partId)?.id == "dep_switch");
            var battery = state.Spacecraft.parts.FirstOrDefault(p => state.Data.GetPart(p.partId)?.category == "battery");
            var eps = state.Spacecraft.parts.FirstOrDefault(p => state.Data.GetPart(p.partId)?.category == "eps");
            bool hasSwitch = sw != null;
            bool powerCable = sw != null && battery != null && eps != null &&
                state.Spacecraft.cables.Any(c => Joins(c, battery.instanceId + "/BATT", sw.instanceId + "/BATT_IN")) &&
                state.Spacecraft.cables.Any(c => Joins(c, sw.instanceId + "/BATT_OUT", eps.instanceId + "/BATT"));
            report.powered = state.Flight.deployed && state.Spacecraft.rbfPinPulled && hasSwitch && powerCable;
            if (!report.powered)
            {
                report.powerFailureCause = !state.Flight.deployed ? "deployment" : !state.Spacecraft.rbfPinPulled ? "rbf_pin" : "deployment_switch";
                report.failures.Add(!state.Flight.deployed ? "The launch did not deploy a satellite; operations could not begin." : !state.Spacecraft.rbfPinPulled ? "RBF pin was still installed; satellite never powered on." : "Deployment switch missing or not wired inline; satellite never powered on.");
                state.Events.AddAt(state.Flight.flightTimeS, "operations", "satellite_no_power", report.failures[0], report.powerFailureCause, Severity.Fault);
            }

            var events = (mission?.randomEvents ?? new System.Collections.Generic.List<RandomEventDef>()).OrderBy(e => e.day).ToList();
            int days = Math.Max(1, mission?.durationDays ?? 30);
            double soc = 1, stored = 0, down = 0, capacity = Math.Max(0, budget.batteryWh);
            int taken = 0, sent = 0, eventIndex = 0, aliveDays = 0;
            double period = Math.Max(1, budget.periodMin), eclipse = Math.Max(0, budget.eclipseMin);
            // SpacecraftBudget.generationW is already averaged across sunlit and eclipse portions.
            double orbitPowerWh = NetOrbitEnergyWh(budget.generationW, budget.consumptionW, period);
            int orbitsPerDay = OrbitsPerDay(period);
            for (int day = 1; day <= days; day++)
            {
                bool reboot = false, outage = false;
                while (eventIndex < events.Count && events[eventIndex].day == day)
                {
                    var ev = events[eventIndex++];
                    if (ev.type == "solar_storm") reboot = true;
                    if (ev.type == "ground_outage") outage = true;
                    if (ev.type == "battery_ageing") capacity *= Math.Max(0, 1 - (ev.value <= 1 ? ev.value : ev.value / 100.0));
                    string msg = ev.type == "solar_storm" ? "Solar storm rebooted the flight computer; one science day was lost." : ev.type == "ground_outage" ? "Ground station outage prevented today's downlink." : $"Battery ageing reduced usable capacity by {(ev.value <= 1 ? ev.value * 100 : ev.value):0.#}%.";
                    report.failures.Add(msg);
                    state.Events.AddAt(state.Flight.flightTimeS + day * 86400, "operations", ev.type, msg, ev.codexId ?? ev.id, Severity.Warning);
                }
                if (report.powered)
                {
                    for (int n = 0; n < orbitsPerDay; n++)
                    {
                        soc = StepStateOfCharge(soc, orbitPowerWh, capacity);
                    }
                    if (CanImage(true, reboot, soc, budget.pointingQuality))
                    {
                        int count = Math.Max(0, mission?.imagesPerPass ?? 3);
                        double each = Math.Max(0, budget.imageMB);
                        for (int i = 0; i < count; i++) if (stored + each <= Math.Min(StorageLimitMB, budget.storageMB)) { stored += each; taken++; }
                    }
                    double rate = state.Data.GetPart(budget.radioId)?.Spec("dataRateKbps") ?? 0;
                    double contactMB = rate * (mission?.groundContactsPerDay ?? 4) * (mission?.contactMinutes ?? 8) * 60 / 8 / 1000;
                    if (!outage && soc > 0.05)
                    {
                        double sentToday = Math.Min(stored, contactMB);
                        stored -= sentToday; down += sentToday;
                        // Whole images from the running total: a slow link still finishes an image over several days.
                        sent = budget.imageMB > 0 ? (int)Math.Floor(down / budget.imageMB + 1e-9) : 0;
                    }
                    if (report.powered) aliveDays++;
                    if (soc <= 0.001) { report.powered = false; report.failures.Add("Battery depleted: the spacecraft stopped collecting science and radioing home."); state.Events.AddAt(state.Flight.flightTimeS + day * 86400, "operations", "battery_depleted", "Battery depleted; spacecraft went silent.", "battery_dod", Severity.Fault); }
                }
                report.timeline.Add(new OpsPoint { day = day, stateOfCharge = soc, storedMB = stored, downlinkedMB = down, imagesTaken = taken, imagesDownlinked = sent });
            }
            report.daysAlive = aliveDays;
            report.survived = report.powered && report.daysAlive == days;
            report.finalSoc = soc; report.storedMB = stored; report.downlinkedMB = down; report.imagesTaken = taken; report.imagesDownlinked = sent;
            report.rewardImageQuality = budget.pointingQuality;
            var imageCriterion = mission?.successCriteria.FirstOrDefault(c => c.metric == "imagesDownlinked");
            report.rewardImageShare = Math.Min(1, imageCriterion != null && imageCriterion.threshold > 0 ? (double)sent / imageCriterion.threshold : 0);
            report.rewardPlaceholder = firstImage == null ? "No reward image metadata is available." : string.IsNullOrEmpty(firstImage.resource)
                ? "Reward images are not available for this mission yet."
                : $"Real Landsat images of the 2022 floods, shown at your camera's detail. Your design: {budget.gsdM:0.#} m/pixel GSD, {report.rewardImageQuality:P0} pointing quality and {report.rewardImageShare:P0} of the image goal returned (each third unlocks one more date).";
            state.Events.AddAt(state.Flight.flightTimeS + days * 86400, "operations", "operations_complete", $"Operations ended after {report.daysAlive} days; {sent} images downlinked.", null, Severity.Info);
            return report;
        }
        static bool Joins(CableLink c, string a, string b) => (c.fromPort == a && c.toPort == b) || (c.fromPort == b && c.toPort == a);
        static double Clamp01(double x) => Math.Max(0, Math.Min(1, x));
        public static int OrbitsPerDay(double periodMinutes) => Math.Max(1, (int)Math.Round(1440 / Math.Max(1, periodMinutes)));
        public static double NetOrbitEnergyWh(double averageGenerationW, double averageLoadW, double periodMinutes) => (averageGenerationW - averageLoadW) * Math.Max(0, periodMinutes) / 60.0;
        public static double StepStateOfCharge(double soc, double netEnergyWh, double capacityWh) => capacityWh <= 0 ? 0 : Clamp01(soc + netEnergyWh / capacityWh);
        public static bool CanImage(bool satelliteReady, bool rebooted, double soc, double pointingQuality) => satelliteReady && !rebooted && soc > 0.4 && pointingQuality > 0;
    }
}
