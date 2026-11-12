using System;
using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Spacecraft
{
    /// <summary>
    /// Live budget bars (mass, cost, power, battery, data, pointing) with a status icon, the numbers,
    /// and a "why?" line each. Orbit picker 400 / 550 / 800 km with its trade-off.
    /// </summary>
    public sealed class BudgetsPanel
    {
        public event Action<double> OrbitChosen;

        readonly VisualElement _panel, _bars;
        readonly Label _orbitInfo;
        readonly Dictionary<double, Button> _orbitChips = new Dictionary<double, Button>();
        readonly HashSet<string> _openWhy = new HashSet<string>();
        SpacecraftReport _last;

        static readonly Dictionary<double, string> Tradeoffs = new Dictionary<double, string>
        {
            [400] = "400 km: sharper images, more drag, shorter life",
            [550] = "550 km: balanced",
            [800] = "800 km: long life, blurrier images, slightly shorter eclipses",
        };

        public BudgetsPanel(VisualElement slot, IEnumerable<double> orbitOptions)
        {
            _panel = new VisualElement();
            _panel.AddToClassList("budgets-panel");
            slot.Add(_panel);

            var head = new VisualElement(); head.AddToClassList("card-header");
            var title = new Label("BUDGETS"); title.AddToClassList("info-category");
            head.Add(title);
            _panel.Add(head);

            var orbitRow = new VisualElement(); orbitRow.AddToClassList("bin-chips");
            var orbitLabel = new Label("Orbit"); orbitLabel.AddToClassList("orbit-label");
            orbitRow.Add(orbitLabel);
            foreach (var km in orbitOptions)
            {
                var k = km;
                var chip = new Button(() => OrbitChosen?.Invoke(k)) { text = $"{km:0} km" };
                chip.AddToClassList("bin-chip");
                orbitRow.Add(chip);
                _orbitChips[km] = chip;
            }
            _panel.Add(orbitRow);
            _orbitInfo = new Label(); _orbitInfo.AddToClassList("orbit-info");
            _panel.Add(_orbitInfo);

            _bars = new VisualElement();
            _panel.Add(_bars);
        }

        public void Refresh(SpacecraftReport r)
        {
            _last = r;
            foreach (var kv in _orbitChips) kv.Value.EnableInClassList("on", Math.Abs(kv.Key - r.altitudeKm) < 1);
            Tradeoffs.TryGetValue(r.altitudeKm, out var trade);
            _orbitInfo.text = $"{trade}\nPeriod {r.periodMin:0.0} min · eclipse {r.eclipseMin:0} min · camera ≈ {r.gsdM:0.#} m/pixel";

            _bars.Clear();
            Bar("mass", "Mass", r.mass, r.massKg / Math.Max(0.01, r.massLimitKg),
                $"{r.massKg:0.00} / {r.massLimitKg:0.0} kg",
                "Sum of every part. A 3U CubeSat must stay under the mission's mass limit to fit the dispenser.");
            Bar("cost", "Cost", r.cost, r.budgetM > 0 ? r.totalCostM / r.budgetM : 0,
                r.budgetM > 0 ? $"{r.totalCostM:0.000} / {r.budgetM:0.00} M$" : $"{r.totalCostM:0.000} M$",
                $"Launch {r.launchCostM:0.0} M$ + spacecraft {r.spacecraftCostK:0} k$. The budget leaves {Math.Max(0, (r.budgetM - r.launchCostM) * 1000):0} k$ for parts.");
            Bar("power", "Power", r.power, r.generationW > 0 ? r.consumptionW / r.generationW : 1.5,
                $"uses {r.consumptionW:0.00} of {r.generationW:0.00} W",
                $"Orbit average. Panels: peak × {r.sunlitFraction * 100:0}% sunlit × pointing factor. Always-on {r.alwaysOnW:0.00} W + camera {r.imagingW:0.00} W + radio transmit {r.transmitW:0.00} W.");
            Bar("battery", "Battery", r.battery, r.batteryWh > 0 ? r.requiredBatteryWh / r.batteryWh : 1.5,
                $"needs {r.requiredBatteryWh:0.0} of {r.batteryWh:0} Wh",
                $"Each eclipse uses {r.eclipseEnergyWh:0.00} Wh. Keeping that under 30% of the battery (depth of discharge) lets it survive thousands of cycles.");
            Bar("data", "Data", r.data, r.imageGoal > 0 ? r.imageGoal / Math.Max(0.1, r.imagesDownlinkedMission) : 0.5,
                r.imageGoal > 0 ? $"≈{r.imagesDownlinkedMission:0} of {r.imageGoal} images down" : $"{r.downlinkPerDayMB:0.0} MB/day",
                $"{r.imagesPerDay:0} images/day × {r.imageMB:0} MB = {r.dataPerDayMB:0} MB/day made; radio sends {r.downlinkPerDayMB:0.0} MB/day in 4 × 8 min passes.{(double.IsInfinity(r.daysUntilFull) ? "" : $" Storage fills in {r.daysUntilFull:0} days.")}");
            Bar("pointing", "Pointing", r.pointing, r.pointingQuality > 0 ? 1 / r.pointingQuality * 0.5 : 1.5,
                r.adcsId == null ? "no ADCS" : $"image quality {r.pointingQuality:0.0}",
                "The HD camera needs reaction wheels (±0.1°). Magnetorquers (±10°) are fine for the wide camera.");
        }

        void Bar(string id, string name, BudgetStatus s, double used, string value, string why)
        {
            var row = new VisualElement(); row.AddToClassList("budget-row");
            var top = new VisualElement(); top.AddToClassList("budget-top");
            var icon = new Label(s == BudgetStatus.Green ? "✓" : s == BudgetStatus.Yellow ? "△" : "✗");
            icon.AddToClassList("budget-icon"); icon.AddToClassList(Cls(s));
            var label = new Label(name); label.AddToClassList("budget-name");
            var val = new Label(value); val.AddToClassList("budget-value");
            var q = new Button(() => { if (!_openWhy.Remove(id)) _openWhy.Add(id); if (_last != null) Refresh(_last); }) { text = "why?" };
            q.AddToClassList("why-btn");
            top.Add(icon); top.Add(label); top.Add(val); top.Add(q);
            row.Add(top);

            var track = new VisualElement(); track.AddToClassList("budget-track");
            var fill = new VisualElement(); fill.AddToClassList("budget-fill"); fill.AddToClassList(Cls(s));
            fill.style.width = Length.Percent(Mathf.Clamp((float)used * 100f, 2f, 100f));
            track.Add(fill);
            row.Add(track);

            if (_openWhy.Contains(id))
            {
                var w = new Label(why); w.AddToClassList("budget-why");
                row.Add(w);
            }
            _bars.Add(row);
        }

        static string Cls(BudgetStatus s) => s == BudgetStatus.Green ? "green" : s == BudgetStatus.Yellow ? "yellow" : "red";
    }
}
