using System;
using System.Linq;
using Newtonsoft.Json;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Mission;

namespace TMinus.Houston
{
    /// <summary>
    /// "What if I use the HD camera?" (audit F23). The brief promises that HOUSTON shows how a different choice changes the outcome.
    /// The numbers come from the game's own budget model (the same one the Integration Bay shows), not from the AI, so they are right
    /// and work offline.
    /// </summary>
    public static class WhatIf
    {
        static readonly (string[] keys, string partId)[] Swaps =
        {
            (new[] { "hd camera", "imaging camera", "hd cam", "5 m/pixel" }, "cam_hd"),
            (new[] { "wide camera", "wide cam", "100 m/pixel" }, "cam_wide"),
            (new[] { "s-band", "sband", "s band" }, "radio_sband"),
            (new[] { "uhf" }, "radio_uhf"),
            (new[] { "reaction wheel", "star tracker", "wheels" }, "adcs_rw"),
            (new[] { "magnetorquer" }, "adcs_mag"),
            (new[] { "solar wings", "deployable wings", "wings" }, "solar_wings"),
            (new[] { "body panel", "body-mounted", "body solar" }, "solar_body"),
            (new[] { "40 wh", "40wh", "bigger battery", "larger battery" }, "batt_40"),
            (new[] { "20 wh", "20wh", "smaller battery" }, "batt_20"),
        };

        static readonly string[] Triggers = { "what if", "what would happen", "swap", "replace", "instead of", "switch to", "change to" };

        public static bool LooksLikeWhatIf(string question)
        {
            string q = (question ?? "").ToLowerInvariant();
            return Triggers.Any(q.Contains) && Swaps.Any(s => s.keys.Any(q.Contains));
        }

        /// <summary>The live game's design. Returns null when the question is not a "what if" about a part.</summary>
        public static Recommendation Try(string question, GameState state)
        {
            if (state == null || !LooksLikeWhatIf(question)) return null;
            var mission = state.Data.GetMission(state.CurrentMissionId);
            return Evaluate(question, state.Data, state.Spacecraft, mission, state.Data.GetRocket(state.Vehicle.rocketId));
        }

        /// <summary>Pure version for tests: the answer for swapping one part in a copy of the design.</summary>
        public static Recommendation Evaluate(string question, DataRegistry data, SpacecraftDesign design, MissionDef mission, RocketDef rocket)
        {
            if (!LooksLikeWhatIf(question)) return null;
            string q = question.ToLowerInvariant();
            string newId = Swaps.First(s => s.keys.Any(q.Contains)).partId;
            var newDef = data.GetPart(newId);
            if (newDef == null) return null;

            var copy = JsonConvert.DeserializeObject<SpacecraftDesign>(JsonConvert.SerializeObject(design));
            var existing = copy.parts.FirstOrDefault(p => data.GetPart(p.partId)?.category == newDef.category);
            string oldName = "nothing";
            if (existing != null)
            {
                if (existing.partId == newId)
                    return new Recommendation { action = "WhatIf", text = $"You already use the {newDef.name}. Ask about a different part to compare.", reasonCode = "what_if_same", codexId = CodexFor(newDef.category) };
                oldName = data.GetPart(existing.partId)?.name ?? existing.partId;
                existing.partId = newId;
            }
            else copy.parts.Add(new PlacedPart { instanceId = newId, partId = newId, mountId = null });

            var before = SpacecraftBudget.Compute(design, data, mission, rocket);
            var after = SpacecraftBudget.Compute(copy, data, mission, rocket);
            var lines = new[]
            {
                DesignCompare.Metric("Mass", before.massKg, after.massKg, " kg", "lower"),
                DesignCompare.Metric("Cost", before.totalCostM, after.totalCostM, " M$", "lower"),
                DesignCompare.Metric("Power margin", before.marginW, after.marginW, " W"),
                DesignCompare.Metric("Battery needed", before.requiredBatteryWh, after.requiredBatteryWh, " Wh", "lower"),
                DesignCompare.Metric("Images reaching the ground", before.imagesDownlinkedMission, after.imagesDownlinkedMission, ""),
                DesignCompare.Metric("Detail per pixel", before.gsdM, after.gsdM, " m", "lower"),
                DesignCompare.Metric("Pointing quality", before.pointingQuality, after.pointingQuality, ""),
            };
            string status = $"Power {DesignCompare.Status(before.power)} → {DesignCompare.Status(after.power)}  ·  Data {DesignCompare.Status(before.data)} → {DesignCompare.Status(after.data)}  ·  Pointing {DesignCompare.Status(before.pointing)} → {DesignCompare.Status(after.pointing)}";
            string text = $"WHAT IF: {newDef.name} instead of {oldName}?\n" + string.Join("\n", lines) + "\n" + status +
                          "\n(Numbers from the same budget model as the Integration Bay. Nothing was changed in your build.)";
            return new Recommendation { action = "WhatIf", text = text, reasonCode = "what_if", codexId = CodexFor(newDef.category) };
        }

        static string CodexFor(string category)
        {
            switch (category)
            {
                case "camera": return "gsd";
                case "radio": return "downlink";
                case "adcs": return "adcs";
                case "solar": return "eclipse";
                case "battery": return "battery_dod";
                default: return "eps";
            }
        }
    }
}
