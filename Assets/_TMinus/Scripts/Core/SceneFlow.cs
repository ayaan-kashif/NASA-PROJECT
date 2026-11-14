using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TMinus.Core
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";
        public const string MissionBriefing = "MissionBriefing";
        public const string IntegrationBay = "IntegrationBay";
        public const string VAB = "VAB";
        public const string Flight = "Flight";
        public const string Debrief = "Debrief";
        /// <summary>Close-up of the Pathfinder avionics ring (Prompt 8), opened from the VAB.</summary>
        public const string AvionicsBay = "AvionicsBay";

        /// <summary>Build Settings order. Boot must stay first. AvionicsBay is a side room opened from the VAB: it is added to Build Settings
        /// by its own builder (Tools > T-Minus > Build Avionics Bay), so it is deliberately not in this list.</summary>
        public static readonly string[] All =
            { Boot, MainMenu, MissionBriefing, IntegrationBay, VAB, Flight, Debrief };
    }

    /// <summary>Scene changes with a short fade, and the mission order for Next().</summary>
    public static class SceneFlow
    {
        public const float FadeSeconds = 0.3f;

        public static readonly string[] MissionOrder =
        {
            SceneNames.MissionBriefing, SceneNames.IntegrationBay, SceneNames.VAB,
            SceneNames.Flight, SceneNames.Debrief
        };

        public static bool IsLoading => SceneFader.Instance != null && SceneFader.Instance.IsBusy;

        /// <summary>Next scene in the mission loop. After Debrief the loop returns to the briefing.</summary>
        public static string NextAfter(string sceneName)
        {
            int i = Array.IndexOf(MissionOrder, sceneName);
            if (i < 0) return SceneNames.MissionBriefing;
            return MissionOrder[(i + 1) % MissionOrder.Length];
        }

        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneFlow] Scene \"{sceneName}\" is not in Build Settings. " +
                               "Run Tools > T-Minus > Rebuild Scenes.");
                return;
            }

            if (SceneFader.Instance != null) SceneFader.Instance.FadeTo(sceneName, FadeSeconds);
            else SceneManager.LoadScene(sceneName);
        }

        public static void Next() => Load(NextAfter(SceneManager.GetActiveScene().name));
    }
}
