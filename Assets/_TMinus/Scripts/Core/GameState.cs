using System.Collections.Generic;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// The only singleton. Holds cross-scene state and the shared services.
    /// It creates itself before the first scene loads, so any scene can be played directly.
    /// </summary>
    public sealed class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        public DataRegistry Data { get; private set; }
        public EventLog Events { get; private set; }

        public string CurrentMissionId = "floodwatch_1";
        public List<CrewMember> Crew = new List<CrewMember>
        {
            new CrewMember { name = "Player 1", role = CrewRole.FlightDirector }
        };
        public SpacecraftDesign Spacecraft = new SpacecraftDesign();
        public VehicleDesign Vehicle = new VehicleDesign();
        public GameSettings Settings = new GameSettings();
        public LaunchRecord Launch = new LaunchRecord();
        public FlightRecord Flight = new FlightRecord();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameState]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameState>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
#if !UNITY_WEBGL
            // Performance: with vertical sync off the desktop build draws as many frames as it can, which heats a laptop until it slows down.
            // One frame per screen refresh is as smooth as the eye can tell. (A browser already paces itself to the screen.)
            if (QualitySettings.vSyncCount == 0) QualitySettings.vSyncCount = 1;
#endif
            Events = new EventLog(() => Time.timeAsDouble);
            Data = DataRegistry.LoadFromResources();
            SettingsStore.Load(Settings);
            gameObject.AddComponent<SceneFader>();
            gameObject.AddComponent<AutoSave>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Clear per-run state (event log) before a new flight attempt.</summary>
        public void BeginRun()
        {
            Events.Clear();
        }

        /// <summary>
        /// A new attempt (Redesign / Fly again): hints and NO-GO calls from the last attempt must not lower this attempt's
        /// decision-quality score (audit A-08).
        /// </summary>
        public void BeginAttempt()
        {
            if (Settings.hintsUsed != null) System.Array.Clear(Settings.hintsUsed, 0, Settings.hintsUsed.Length);
            Launch.noGoRounds = 0;
        }

        /// <summary>Reload all JSON (e.g. after editing data during Play mode).</summary>
        public void ReloadData()
        {
            Data = DataRegistry.LoadFromResources();
        }
    }
}
