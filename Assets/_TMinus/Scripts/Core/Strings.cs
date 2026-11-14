using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Player-facing text kept in data instead of C# (audit A-24). Resources/Data/strings.json holds {"en": {"key": "text"}}; a room asks
    /// <c>Strings.Get("pause.resume", "Resume")</c>, and the second argument is the fallback, so a missing key never shows an empty button.
    /// Another language is one more block in the same file (for example "ur"), picked from Settings. The menus that use it today are the
    /// pause menu and the main-menu messages; the rest of the game's text still lives in the scripts and moves over the same way.
    /// </summary>
    public static class Strings
    {
        static Dictionary<string, string> _en, _other;
        static string _otherLanguage;

        public static string Get(string key, string fallback)
        {
            Load();
            string lang = GameState.Instance != null ? GameState.Instance.Settings.language : "en";
            if (lang != "en" && lang != _otherLanguage) { _otherLanguage = lang; _other = Read(lang); }
            if (lang != "en" && _other != null && _other.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated)) return translated;
            return _en.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text) ? text : fallback;
        }

        /// <summary>Like Get, then string.Format with the arguments.</summary>
        public static string Format(string key, string fallback, params object[] args)
        {
            try { return string.Format(Get(key, fallback), args); }
            catch (System.FormatException) { return string.Format(fallback, args); }
        }

        /// <summary>Parses a table text. Public so a test can check the real file.</summary>
        public static Dictionary<string, string> Parse(string json, string language)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return map;
            try
            {
                if (JObject.Parse(json)[language] is JObject block)
                    foreach (var p in block.Properties()) map[p.Name] = (string)p.Value;
            }
            catch (System.Exception ex) { Debug.LogWarning("[Strings] strings.json is not valid: " + ex.Message); }
            return map;
        }

        static void Load()
        {
            if (_en == null) _en = Read("en");
        }

        static Dictionary<string, string> Read(string language)
        {
            var asset = Resources.Load<TextAsset>("Data/strings");
            return Parse(asset != null ? asset.text : null, language);
        }
    }
}
