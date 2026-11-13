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
            Events = new EventLog(() => Time.timeAsDouble);
            Data = DataRegistry.LoadFromResources();
            SettingsStore.Load(Settings);
            gameObject.AddComponent<SceneFader>();
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

        /// <summary>Reload all JSON (e.g. after editing data during Play mode).</summary>
        public void ReloadData()
        {
            Data = DataRegistry.LoadFromResources();
        }
    }
}
