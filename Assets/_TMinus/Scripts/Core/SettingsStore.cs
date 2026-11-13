using UnityEngine;

namespace TMinus.Core
{
    /// <summary>Player settings saved in PlayerPrefs and applied at start-up (HOUSTON mode, language, volume, fullscreen).</summary>
    public static class SettingsStore
    {
        const string KHouston = "tminus.houstonMode", KLang = "tminus.language", KVolume = "tminus.volume", KFull = "tminus.fullscreen";

        public static float Volume { get; private set; } = 1f;

        public static void Load(GameSettings s)
        {
            if (s == null) return;
            s.houstonMode = (HoustonMode)Mathf.Clamp(PlayerPrefs.GetInt(KHouston, (int)HoustonMode.On), 0, 2);
            s.language = PlayerPrefs.GetString(KLang, "en") == "ur" ? "ur" : "en";
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KVolume, 1f));
            AudioListener.volume = Volume;
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
    }
}
