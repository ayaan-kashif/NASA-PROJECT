using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace TMinus.Core
{
    /// <summary>
    /// Opt-in, anonymous gameplay stats (audit F21). Nothing is sent unless the player turned "Share anonymous gameplay stats" on in Settings
    /// AND a stats address is set in houston.config.json (analyticsUrl). Events are a name plus a few short labels such as the room
    /// or how a flight ended: never text the player typed, names, or design codes. The session id is random per launch.
    /// </summary>
    public static class Analytics
    {
        /// <summary>Set from houston.config.json "analyticsUrl". Empty = never send.</summary>
        public static string EndpointUrl;

        static readonly string Session = Guid.NewGuid().ToString("N").Substring(0, 12);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook() => SceneManager.sceneLoaded += (scene, mode) => Track("room_entered", "room", scene.name);

        /// <summary>props are key, value, key, value... Values are cut to 60 characters.</summary>
        public static void Track(string name, params string[] props)
        {
            if (!SettingsStore.ShareStats || string.IsNullOrEmpty(EndpointUrl) || GameState.Instance == null) return;
            var body = new JObject { ["event"] = Clean(name), ["session"] = Session, ["version"] = Application.version, ["platform"] = Application.platform.ToString() };
            var p = new JObject();
            for (int i = 0; i + 1 < props.Length; i += 2) p[Clean(props[i])] = Clean(props[i + 1]);
            body["props"] = p;
            GameState.Instance.StartCoroutine(Send(EndpointUrl, body.ToString(Newtonsoft.Json.Formatting.None)));
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length > 60 ? s.Substring(0, 60) : s;
        }

        static IEnumerator Send(string url, string json)
        {
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;
                yield return req.SendWebRequest();   // the result is ignored: stats must never affect the game
            }
        }
    }
}
