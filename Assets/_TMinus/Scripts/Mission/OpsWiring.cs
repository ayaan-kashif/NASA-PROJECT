using System.Collections.Generic;
using System.Linq;
using TMinus.Core;

namespace TMinus.Mission
{
    /// <summary>What the CubeSat's wiring faults do once the satellite is in orbit (audit A-01).</summary>
    public sealed class WiringEffects
    {
        /// <summary>A short circuit, reversed plug or missing power feed: the satellite is dead on deployment.</summary>
        public bool Dead;
        /// <summary>The radio or its antenna is not fully wired: nothing reaches the ground.</summary>
        public bool NoDownlink;
        /// <summary>The camera or the pointing system is not fully wired: no usable images.</summary>
        public bool NoImaging;
        /// <summary>The EPS has no solar panel connected: no power generation.</summary>
        public bool NoSolar;

        /// <summary>The port or device to blame, for the event log and the debrief ("radio_sband/RF").</summary>
        public string DeadCause, DownlinkCause, ImagingCause, SolarCause;
        public string DeadMessage, DownlinkMessage, ImagingMessage, SolarMessage;

        public bool Any => Dead || NoDownlink || NoImaging || NoSolar;

        /// <summary>One line per effect, in plain language, most serious first.</summary>
        public IEnumerable<string> Messages
        {
            get
            {
                if (Dead) yield return DeadMessage;
                if (NoSolar) yield return SolarMessage;
                if (NoDownlink) yield return DownlinkMessage;
                if (NoImaging) yield return ImagingMessage;
            }
        }
    }

    /// <summary>
    /// Turns the wiring check the player sees in the Integration Bay into consequences in operations. Before this, an unplugged
    /// antenna cable cost a few score points but the satellite still sent every image home.
    /// </summary>
    public static class OpsWiring
    {
        public static WiringEffects Evaluate(PrelaunchChecklist.Input input)
        {
            var fx = new WiringEffects();
            foreach (var f in PrelaunchChecklist.SpacecraftWiringFaults(input))
            {
                if (f.severity < Severity.Fault) continue;
                string blame = f.port ?? f.device;
                string category = CategoryOf(input, f.device);
                switch (f.code)
                {
                    case "short_circuit":
                    case "reversed_polarity":
                        if (!fx.Dead) { fx.Dead = true; fx.DeadCause = blame; fx.DeadMessage = "The satellite died on deployment: " + f.message; }
                        break;
                    case "missing_connection":
                        switch (category)
                        {
                            case "radio":
                            case "antenna":
                                if (!fx.NoDownlink) { fx.NoDownlink = true; fx.DownlinkCause = blame; fx.DownlinkMessage = "Nothing reached the ground: " + f.message; }
                                break;
                            case "camera":
                            case "adcs":
                                if (!fx.NoImaging) { fx.NoImaging = true; fx.ImagingCause = blame; fx.ImagingMessage = "No usable images were taken: " + f.message; }
                                break;
                            case "eps":
                                if (f.port != null && f.port.Contains("SOLAR_IN"))
                                {
                                    if (!fx.NoSolar) { fx.NoSolar = true; fx.SolarCause = blame; fx.SolarMessage = "The solar panels never charged the battery: " + f.message; }
                                }
                                else if (!fx.Dead) { fx.Dead = true; fx.DeadCause = blame; fx.DeadMessage = "The satellite had no power source: " + f.message; }
                                break;
                            case "obc":
                                if (!fx.Dead) { fx.Dead = true; fx.DeadCause = blame; fx.DeadMessage = "The flight computer never powered on: " + f.message; }
                                break;
                        }
                        break;
                }
            }
            return fx;
        }

        static string CategoryOf(PrelaunchChecklist.Input input, string instanceId)
        {
            if (instanceId == null) return null;
            var placed = input.Spacecraft.parts.FirstOrDefault(p => p.instanceId == instanceId);
            return placed == null ? null : input.Data.GetPart(placed.partId)?.category;
        }
    }
}
