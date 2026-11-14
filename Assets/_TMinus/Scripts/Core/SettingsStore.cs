using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Core
{
    /// <summary>
    /// Player settings saved in PlayerPrefs and applied at start-up: HOUSTON mode, language, volume, fullscreen, and (audit F10, F21)
    /// text size, low-graphics mode, reduced motion and the opt-in for anonymous gameplay stats.
    /// </summary>
    public static class SettingsStore
    {
        const string KHouston = "tminus.houstonMode", KLang = "tminus.language", KVolume = "tminus.volume", KFull = "tminus.fullscreen";
        const string KText = "tminus.textScalePct", KLow = "tminus.lowGraphics", KMotion = "tminus.reduceMotion", KStats = "tminus.shareStats";

        public static float Volume { get; private set; } = 1f;

        /// <summary>UI text size in percent: 100, 125 or 150.</summary>
        public static int TextScalePct { get; private set; } = 100;
        /// <summary>Turns off bloom, grading and anti-aliasing on the next room load: for slow computers.</summary>
        public static bool LowGraphics { get; private set; }
        /// <summary>No screen flashes or shaking (the Apollo 12 lightning flash, for example).</summary>
        public static bool ReduceMotion { get; private set; }
        /// <summary>Opt-in (off by default): send anonymous events such as which room was opened. Never any text the player typed.</summary>
        public static bool ShareStats { get; private set; }

        public static void Load(GameSettings s)
        {
            if (s == null) return;
            s.houstonMode = (HoustonMode)Mathf.Clamp(PlayerPrefs.GetInt(KHouston, (int)HoustonMode.On), 0, 2);
            s.language = PlayerPrefs.GetString(KLang, "en") == "ur" ? "ur" : "en";
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KVolume, 1f));
            AudioListener.volume = Volume;
            TextScalePct = NormaliseText(PlayerPrefs.GetInt(KText, 100));
            LowGraphics = PlayerPrefs.GetInt(KLow, 0) == 1;
            ReduceMotion = PlayerPrefs.GetInt(KMotion, 0) == 1;
            ShareStats = PlayerPrefs.GetInt(KStats, 0) == 1;
            ApplyTextScale();
            ApplyGraphics();
#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey(KFull)) Screen.fullScreen = PlayerPrefs.GetInt(KFull) == 1;
#endif
        }

        public static void Save(GameSettings s, float volume, bool fullscreen)
        {
            if (s == null) return;
            PlayerPrefs.SetInt(KHouston, (int)s.houstonMode);
            PlayerPrefs.SetString(KLang, s.language == "ur" ? "ur" : "en");
            Volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KVolume, Volume);
            PlayerPrefs.SetInt(KFull, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = Volume;
#if !UNITY_EDITOR
            Screen.fullScreen = fullscreen;
#endif
        }

        public static void SetTextScale(int pct)
        {
            TextScalePct = NormaliseText(pct);
            PlayerPrefs.SetInt(KText, TextScalePct);
            PlayerPrefs.Save();
            ApplyTextScale();
        }

        public static void SetLowGraphics(bool on)
        {
            LowGraphics = on;
            PlayerPrefs.SetInt(KLow, on ? 1 : 0);
            PlayerPrefs.Save();
            ApplyGraphics();
        }

        public static void SetReduceMotion(bool on) { ReduceMotion = on; PlayerPrefs.SetInt(KMotion, on ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetShareStats(bool on) { ShareStats = on; PlayerPrefs.SetInt(KStats, on ? 1 : 0); PlayerPrefs.Save(); }

        static int NormaliseText(int pct) => pct >= 140 ? 150 : pct >= 115 ? 125 : 100;

        // ------------------------------------------------------------------ applying

        static readonly Dictionary<PanelSettings, Vector2Int> OriginalReference = new Dictionary<PanelSettings, Vector2Int>();
        static bool _restoreHooked;

        /// <summary>
        /// Bigger text = a smaller reference resolution (the UI is scaled to the screen, so fewer reference pixels fill the same screen).
        /// The panel settings are shared assets, so the original values are put back when the game closes: in the Editor they would
        /// otherwise be saved into the project.
        /// </summary>
        public static void ApplyTextScale()
        {
            float f = TextScalePct / 100f;
            foreach (var ps in Resources.FindObjectsOfTypeAll<PanelSettings>())
            {
                if (ps == null) continue;
                if (!OriginalReference.ContainsKey(ps)) OriginalReference[ps] = ps.referenceResolution;
                var o = OriginalReference[ps];
                ps.referenceResolution = new Vector2Int(Mathf.RoundToInt(o.x / f), Mathf.RoundToInt(o.y / f));
            }
            if (!_restoreHooked)
            {
                _restoreHooked = true;
                Application.quitting += RestorePanelSettings;
            }
        }

        /// <summary>Panel settings of a room are only loaded with that room, so the size is applied again after every scene load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => { if (TextScalePct != 100 || OriginalReference.Count > 0) ApplyTextScale(); };
        }

        static void RestorePanelSettings()
        {
            foreach (var kv in OriginalReference) if (kv.Key != null) kv.Key.referenceResolution = kv.Value;
        }

        static void ApplyGraphics()
        {
            if (LowGraphics) QualitySettings.antiAliasing = 0;
        }
    }
}
