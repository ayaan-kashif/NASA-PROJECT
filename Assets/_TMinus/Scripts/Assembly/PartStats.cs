using System.Collections.Generic;
using System.Globalization;
using TMinus.Core;
using TMinus.Core.Data;

namespace TMinus.Assembly
{
    /// <summary>Turns a part's JSON data into readable "key: value" rows for the info card.</summary>
    public static class PartStats
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public static string CategoryLabel(PartDef def)
        {
            if (def == null) return "PART";
            switch (def.category)
            {
                case "rocket_stage": case "saturn_stage": return "STAGE";
                case "payload_adapter": return "PAYLOAD ADAPTER";
                case "avionics_ring": return "AVIONICS";
                case "avionics_device": return "AVIONICS DEVICE";
                default: return def.category.Replace('_', ' ').ToUpperInvariant();
            }
        }

        public static List<(string key, string value)> Build(PartDef def, DataRegistry data)
        {
            var rows = new List<(string, string)>();
            if (def == null) return rows;

            var (stage, component) = Find(def.id, data);
            if (stage != null)
            {
                rows.Add(("Dry mass", Mass(stage.dryMassKg)));
                rows.Add(("Propellant", Mass(stage.propMassKg)));
                rows.Add(("Engines", $"{stage.engineCount} x {stage.engineName}"));
                rows.Add(("Thrust (sea level)", Force(stage.thrustSlKN)));
                rows.Add(("Thrust (vacuum)", Force(stage.thrustVacKN)));
                rows.Add(("Isp sea level / vacuum", $"{stage.ispSl:0} s / {stage.ispVac:0} s"));
                if (stage.burnTimeS > 0) rows.Add(("Burn time", $"{stage.burnTimeS:0} s"));
                rows.Add(("Size", $"{stage.diameterM.ToString("0.##", C)} m x {stage.lengthM.ToString("0.##", C)} m"));
                rows.Add(("Propellants", stage.propellants));
                return rows;
            }

            rows.Add(("Mass", Mass(component?.massKg ?? def.massKg)));
            if (def.costK > 0) rows.Add(("Cost", $"{def.costK:0} k USD"));
            if (def.lengthM > 0) rows.Add(("Size", $"{def.diameterM.ToString("0.##", C)} m x {def.lengthM.ToString("0.##", C)} m"));
            if (component != null && component.jettisonAltitudeKm > 0) rows.Add(("Jettisoned at", $"{component.jettisonAltitudeKm:0} km"));
            if (def.powerIdleW > 0) rows.Add(("Power (idle)", $"{def.powerIdleW.ToString("0.##", C)} W"));
            if (def.powerActiveW > 0) rows.Add(("Power (active)", $"{def.powerActiveW.ToString("0.##", C)} W"));
            foreach (var kv in def.specs)
                rows.Add((Nice(kv.Key), kv.Value.ToString("0.###", C)));
            if (def.ports.Count > 0) rows.Add(("Ports", def.ports.Count.ToString()));
            return rows;
        }

        static (StageDef, RocketComponentDef) Find(string partId, DataRegistry data)
        {
            if (data == null) return (null, null);
            foreach (var r in data.Rockets)
            {
                foreach (var s in r.stages) if (s.partId == partId) return (s, null);
                foreach (var c in r.components) if (c.partId == partId) return (null, c);
            }
            return (null, null);
        }

        public static string Mass(double kg) =>
            kg >= 10000 ? $"{(kg / 1000).ToString("N0", C)} t" :
            kg >= 1 ? $"{kg.ToString("N0", C)} kg" : $"{(kg * 1000).ToString("0", C)} g";

        public static string Force(double kN) =>
            kN >= 1000 ? $"{(kN / 1000).ToString("0.0", C)} MN" : $"{kN.ToString("0", C)} kN";

        static string Nice(string key)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char ch = key[i];
                if (i > 0 && char.IsUpper(ch)) sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
