using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace TMinus.Core
{
    [Serializable]
    sealed class SaveFile
    {
        public int v = 1;
        public string savedAtUtc;
        public string design;                       // a "TM1-" design code: mission + CubeSat + rocket
        public List<CrewMember> crew;
    }

    /// <summary>
    /// Autosave and Continue (audit A-04 / feature F1). The build used to live in memory only, so quitting, refreshing the
    /// browser tab, pressing F9 or taking a Failure Lab job lost up to an hour of work. The design is stored as a design code in
    /// PlayerPrefs (IndexedDB in the browser). Scripted modes (Judge Mode, repair jobs) back the build up and restore it.
    /// </summary>
    public static class SaveService
    {
        const string Key = "tminus.save.v1";

        /// <summary>True while a scripted mode owns the build: autosave stops so the player's real save is not overwritten.</summary>
        public static bool Suspended { get; private set; }

        static string _lastBundleJson;
        static int _owners;
        static string _backupCode;
        static List<CrewMember> _backupCrew;
        static int[] _backupHints;

        public static bool HasSave => PlayerPrefs.HasKey(Key);

        /// <summary>True once the player has actually built something (the empty starting state is never saved).</summary>
        public static bool HasBuild(GameState s) =>
            s != null && (s.Spacecraft.parts.Count > 1 || s.Spacecraft.cables.Count > 0 || s.Vehicle.parts.Count > 0 ||
                          s.Vehicle.bolts.Count > 0 || s.Vehicle.avionicsCables.Count > 0);

        /// <summary>Writes the build if it changed since the last write. Cheap enough to call every couple of seconds.</summary>
        public static bool SaveIfChanged(GameState s)
        {
            if (Suspended || !HasBuild(s)) return false;
            try
            {
                var bundle = DesignCode.FromState(s);
                string json = JsonConvert.SerializeObject(bundle, Formatting.None);
                string crewJson = JsonConvert.SerializeObject(s.Crew, Formatting.None);
                string fingerprint = json + "|" + crewJson;
                if (fingerprint == _lastBundleJson) return false;
                var file = new SaveFile { savedAtUtc = DateTime.UtcNow.ToString("o"), design = DesignCode.Encode(bundle), crew = s.Crew };
                PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(file, Formatting.None));
                PlayerPrefs.Save();
                _lastBundleJson = fingerprint;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveService] Could not save: " + ex.Message);
                return false;
            }
        }

        /// <summary>Loads the saved build into GameState. Invalid items in the file are dropped (DesignValidator).</summary>
        public static bool TryLoad(GameState s, out string message)
        {
            message = null;
            if (s == null || !HasSave) return false;
            try
            {
                var file = JsonConvert.DeserializeObject<SaveFile>(PlayerPrefs.GetString(Key, ""));
                string error = null;
                if (file == null || !DesignCode.TryDecode(file.design, out var bundle, out error)) { message = error ?? "The save file is damaged."; return false; }
                int fixedCount = DesignValidator.Clean(bundle, s.Data);
                DesignCode.Apply(bundle, s);
                if (file.crew != null && file.crew.Count > 0) s.Crew = file.crew;
                s.Launch = new LaunchRecord();
                s.Flight = new FlightRecord();
                _lastBundleJson = null;
                message = fixedCount > 0 ? $"Loaded your build ({fixedCount} invalid item{(fixedCount > 1 ? "s" : "")} ignored)." : "Loaded your build.";
                return true;
            }
            catch (Exception ex)
            {
                message = "The save file is damaged (" + ex.GetType().Name + ").";
                return false;
            }
        }

        /// <summary>"saved 14 Nov, 16:20" for the Continue button, or null.</summary>
        public static string Describe()
        {
            try
            {
                var file = JsonConvert.DeserializeObject<SaveFile>(PlayerPrefs.GetString(Key, ""));
                if (file == null || !DateTime.TryParse(file.savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return null;
                return "saved " + t.ToLocalTime().ToString("d MMM, HH:mm");
            }
            catch { return null; }
        }

        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _lastBundleJson = null;
        }

        // ------------------------------------------------------------------ scripted modes

        /// <summary>
        /// A scripted mode (Judge Mode, a repair job) is about to replace the build. Save the real one first, remember it and
        /// stop autosaving. Calls nest: the build comes back when the last owner calls EndScript.
        /// </summary>
        public static void BeginScript(GameState s)
        {
            if (s == null) return;
            if (_owners++ > 0) return;
            SaveIfChanged(s);
            _backupCode = DesignCode.Encode(DesignCode.FromState(s));
            _backupCrew = s.Crew == null ? null : new List<CrewMember>(s.Crew.ConvertAll(c => new CrewMember { name = c.name, role = c.role }));
            _backupHints = s.Settings.hintsUsed == null ? null : (int[])s.Settings.hintsUsed.Clone();
            Suspended = true;
        }

        public static void EndScript(GameState s)
        {
            if (_owners == 0) return;
            if (--_owners > 0) return;
            Suspended = false;
            if (s == null || _backupCode == null) return;
            if (DesignCode.TryDecode(_backupCode, out var bundle, out _))
            {
                DesignCode.Apply(bundle, s);
                if (_backupCrew != null && _backupCrew.Count > 0) s.Crew = _backupCrew;
                if (_backupHints != null) s.Settings.hintsUsed = _backupHints;
                s.Launch = new LaunchRecord();
                s.Flight = new FlightRecord();
            }
            _backupCode = null; _backupCrew = null; _backupHints = null;
        }
    }

    /// <summary>Calls SaveService every couple of seconds, and when the game is paused or closed (the browser tab loses focus).</summary>
    public sealed class AutoSave : MonoBehaviour
    {
        float _next;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            SaveService.SaveIfChanged(GameState.Instance);
        }

        void OnApplicationPause(bool paused) { if (paused) SaveService.SaveIfChanged(GameState.Instance); }
        void OnApplicationQuit() => SaveService.SaveIfChanged(GameState.Instance);
    }
}
