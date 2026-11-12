using System.Collections.Generic;
using System.Linq;
using TMinus.Core.Data;

namespace TMinus.Core
{
    /// <summary>One missing avionics cable and what it does to the flight.</summary>
    public sealed class AvionicsConsequence
    {
        public string linkId;       // rules.json avionicsWiring id = causeId in the event log
        public string consequence;  // no_ignition, stuck_stage, s2_no_thrust, guidance_drift, no_telemetry, fairing_stuck, no_avionics_power, no_flight_computer
        public string message;
        public override string ToString() => $"{consequence} <- {linkId}: {message}";
    }

    /// <summary>
    /// What the rocket's avionics wiring means for the flight (Prompt 8), read by FlightSim (Prompt 10)
    /// and the pre-launch checklist (Prompt 9). Power faults cascade: no battery -> PDU means nothing works.
    /// </summary>
    public sealed class AvionicsReport : IAvionicsReport
    {
        public readonly List<AvionicsConsequence> consequences = new List<AvionicsConsequence>();
        public bool armed;
        public int wiredLinks, requiredLinks;

        public bool Has(string consequence) => consequences.Any(c => c.consequence == consequence);
        public AvionicsConsequence First(string consequence) => consequences.FirstOrDefault(c => c.consequence == consequence);

        bool PowerOk => !Has("no_avionics_power") && !Has("no_flight_computer");
        public bool S1IgnitionOk => PowerOk && !Has("no_ignition");
        public bool StageSeparationOk => PowerOk && !Has("stuck_stage");
        public bool S2IgnitionOk => PowerOk && !Has("s2_no_thrust");
        public bool GuidanceOk => PowerOk && !Has("guidance_drift");
        public bool TelemetryOk => PowerOk && !Has("no_telemetry");
        public bool FairingSeparationOk => PowerOk && !Has("fairing_stuck");
        public bool AllOk => consequences.Count == 0;

        public string CauseId(string consequence) => First(consequence)?.linkId;

        /// <summary>All-OK report (e.g. Saturn V, which has no buildable avionics).</summary>
        public static AvionicsReport Perfect() => new AvionicsReport();

        public static AvionicsReport Compute(VehicleDesign vehicle, RulesDef rules)
        {
            var r = new AvionicsReport { armed = vehicle?.armed ?? false };
            var cables = vehicle?.avionicsCables ?? new List<CableLink>();
            foreach (var link in rules.avionicsWiring)
            {
                r.requiredLinks++;
                bool wired = cables.Any(c => Same(c, link.from, link.to) && !c.reversed);
                if (wired) { r.wiredLinks++; continue; }
                r.consequences.Add(new AvionicsConsequence { linkId = link.id, consequence = link.consequence, message = link.message });
            }
            return r;
        }

        static bool Same(CableLink c, string a, string b) =>
            (c.fromPort == a && c.toPort == b) || (c.fromPort == b && c.toPort == a);

        public override string ToString() =>
            $"avionics {wiredLinks}/{requiredLinks} wired, {(armed ? "ARMED" : "SAFE")}" +
            (consequences.Count > 0 ? ": " + string.Join("; ", consequences.Select(c => c.consequence + "<-" + c.linkId)) : "");
    }
}
