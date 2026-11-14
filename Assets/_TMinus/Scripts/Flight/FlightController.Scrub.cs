using System;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;

namespace TMinus.Flight
{
    public sealed partial class FlightController
    {
        /// <summary>True while the debrief's "Watch the failure again" replay is on screen.</summary>
        public bool IsReplay => _replay;

        /// <summary>
        /// Rewind and scrub (audit F24). The flight is deterministic and the simulation is pure C#, so any moment can be shown by flying a
        /// fresh copy of the same design up to that time. The copy writes to a throwaway log, so the real event log, the score and the
        /// attempt history are never touched. The 3D rocket keeps whichever stages it has already shed: the numbers, the position and the
        /// camera follow the scrubbed time.
        /// </summary>
        public void ScrubTo(double timeS)
        {
            if (!_initialized || !_replay || !Ended) return;
            var data = S.Data;
            var rules = data.GetRules();
            var joints = _rocket.buildable ? BoltJointState.AllStatuses(rules, S.Vehicle).Select(j => (IJointStatus)j).ToList() : null;
            var sim = FlightRuntime.Create(data, S.Vehicle, _avionics, joints, new EventLog(), S.CurrentMissionId, CarriedMassKg(data));
            sim.Options.targetAltitudeKm = Sim.Options.targetAltitudeKm;
            sim.Options.pitchKickDeg = Sim.Options.pitchKickDeg;
            sim.Options.autopilot = true;
            sim.RunForSimSeconds(Math.Max(0, timeS));
            Sim = sim;
            RefreshDebugView();
        }
    }
}
