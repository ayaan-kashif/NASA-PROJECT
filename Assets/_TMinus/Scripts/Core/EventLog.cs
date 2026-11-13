using System;
using System.Collections.Generic;

namespace TMinus.Core
{
    public enum Severity { Info, Tip, Warning, Fault, Blocker }

    /// <summary>One gameplay event. The debrief and HOUSTON read these to explain causes.</summary>
    [Serializable]
    public sealed class LogEvent
    {
        public double time;
        public string system;
        public string code;
        public string message;
        public string causeId;
        public Severity severity;

        public override string ToString() =>
            $"[{time:0.0}s] {system}/{code} ({severity}) {message}" +
            (string.IsNullOrEmpty(causeId) ? "" : $" <- {causeId}");
    }

    /// <summary>
    /// Single log for assembly, flight and operations events. Cleared at the start of each run.
    /// </summary>
    public sealed class EventLog
    {
        readonly List<LogEvent> _events = new List<LogEvent>();

        /// <summary>Clock used when no explicit time is given. Flight sets it to sim time.</summary>
        public Func<double> Clock { get; set; }

        public event Action<LogEvent> Added;

        public IReadOnlyList<LogEvent> All => _events;

        public EventLog(Func<double> clock = null)
        {
            Clock = clock ?? (() => 0d);
        }

        public LogEvent Add(string system, string code, string message,
                            string causeId = null, Severity severity = Severity.Info)
        {
            return AddAt(Clock(), system, code, message, causeId, severity);
        }

        public LogEvent AddAt(double time, string system, string code, string message,
                              string causeId = null, Severity severity = Severity.Info)
        {
            var e = new LogEvent
            {
                time = time,
                system = system ?? "",
                code = code ?? "",
                message = message ?? "",
                causeId = causeId,
                severity = severity
            };
            _events.Add(e);
            Added?.Invoke(e);
            return e;
        }

        /// <summary>Events of one system (null = all systems) at or after fromTime.</summary>
        public List<LogEvent> Query(string system = null, double fromTime = double.NegativeInfinity)
        {
            var result = new List<LogEvent>();
            foreach (var e in _events)
            {
                if (e.time < fromTime) continue;
                if (system != null && e.system != system) continue;
                result.Add(e);
            }
            return result;
        }

        public void Clear() => _events.Clear();

        /// <summary>Remove one system's events (e.g. the previous flight's, before flying again).</summary>
        public int RemoveSystem(string system) => _events.RemoveAll(e => e.system == system);
    }
}
