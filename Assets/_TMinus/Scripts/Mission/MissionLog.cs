using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using UnityEngine;

namespace TMinus.Mission
{
    [Serializable]
    public sealed class LogEntry
    {
        public string whenUtc, missionId, outcome, cause, code;
        public double score;
        public int stars;
    }

    [Serializable]
    sealed class LogList { public List<LogEntry> entries = new List<LogEntry>(); }

    /// <summary>
    /// Every debriefed flight, kept on this computer (audit F11): the debrief shows how you did against your last attempt, and the
    /// mission cards show your best result. Demo runs (Judge Mode) are not recorded.
    /// </summary>
    public static class MissionLog
    {
        const string Key = "tminus.missionlog.v1";
        public const int MaxEntries = 50;
        static FlightRecord _recorded;
        static LogEntry _previousOfRecorded;

        public static List<LogEntry> Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                return string.IsNullOrEmpty(json) ? new List<LogEntry>() : JsonUtility.FromJson<LogList>(json)?.entries ?? new List<LogEntry>();
            }
            catch { return new List<LogEntry>(); }
        }

        /// <summary>Newest first.</summary>
        public static List<LogEntry> ForMission(string missionId) =>
            Load().Where(e => e.missionId == missionId).OrderByDescending(e => e.whenUtc).ToList();

        public static LogEntry Best(string missionId) =>
            Load().Where(e => e.missionId == missionId).OrderByDescending(e => e.score).FirstOrDefault();

        public static int Count(string missionId) => Load().Count(e => e.missionId == missionId);

        /// <summary>
        /// Adds the entry once per flight (the debrief scene can be entered again for the same flight).
        /// Returns the attempt before this one, or null for the first.
        /// </summary>
        public static LogEntry RecordOnce(FlightRecord flight, LogEntry entry)
        {
            if (ReferenceEquals(_recorded, flight)) return _previousOfRecorded;
            _previousOfRecorded = ForMission(entry.missionId).FirstOrDefault();
            _recorded = flight;
            var all = Load();
            entry.whenUtc = DateTime.UtcNow.ToString("o");
            all.Add(entry);
            if (all.Count > MaxEntries) all = all.OrderByDescending(e => e.whenUtc).Take(MaxEntries).ToList();
            try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(new LogList { entries = all })); PlayerPrefs.Save(); }
            catch { /* storage blocked: the log just is not kept */ }
            return _previousOfRecorded;
        }

        /// <summary>
        /// This computer's attempts as CSV text (audit F20, offline form): a teacher can collect one from each student's machine and
        /// open them in a spreadsheet. A live class dashboard would need a server and is not part of this build.
        /// </summary>
        public static string ToCsv(string studentNames)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("students,date_utc,mission,score,stars,outcome,failure_code,failure_cause\n");
            foreach (var e in Load().OrderBy(x => x.whenUtc))
                sb.Append(Csv(studentNames)).Append(',').Append(Csv(e.whenUtc)).Append(',').Append(Csv(e.missionId)).Append(',')
                  .Append(e.score.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(e.stars).Append(',')
                  .Append(Csv(e.outcome)).Append(',').Append(Csv(e.code)).Append(',').Append(Csv(e.cause)).Append('\n');
            return sb.ToString();
        }

        static string Csv(string v)
        {
            v = v ?? "";
            // A cell that starts with = + - @ can run as a formula in a spreadsheet: neutralise it.
            if (v.Length > 0 && "=+-@".IndexOf(v[0]) >= 0) v = "'" + v;
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        public static string StarText(int stars)
        {
            int n = Mathf.Clamp(stars, 0, 3);
            return new string('★', n) + new string('☆', 3 - n);
        }
    }
}
