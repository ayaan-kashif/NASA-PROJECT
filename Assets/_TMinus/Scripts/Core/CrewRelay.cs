using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace TMinus.Core
{
    public sealed class RelayMessage
    {
        public string id, text;
    }

    /// <summary>
    /// A live room for the remote crew (audit F25), with no server of our own: a free public message service (ntfy.sh) carries the votes.
    /// A room is a random topic name; whoever knows the room code can read and send in it, which is why the code is long and why a vote is
    /// only accepted for the exact build it was made for. Only crew vote codes travel through it (a role, GO or NO-GO, a first name, a build
    /// fingerprint), never a design or anything typed freely. If the service cannot be reached, copying the vote code by hand still works.
    /// </summary>
    public static class CrewRelay
    {
        public const string BaseUrl = "https://ntfy.sh/";
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O/1/I: a code is read aloud and typed

        /// <summary>For example "K7QF-3MXA-RD2T": 12 characters from 32, about 60 bits.</summary>
        public static string NewRoomCode()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var chars = new StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                if (i > 0 && i % 4 == 0) chars.Append('-');
                chars.Append(Alphabet[bytes[i] % Alphabet.Length]);
            }
            return chars.ToString();
        }

        public static string Normalise(string room) => (room ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

        public static bool IsValid(string room)
        {
            string r = Normalise(room);
            if (r.Length != 12) return false;
            foreach (char c in r) if (Alphabet.IndexOf(c) < 0) return false;
            return true;
        }

        public static string Topic(string room) => "tminus-" + Normalise(room).ToLowerInvariant();

        /// <summary>Sends text to the room. done(true) when the service accepted it.</summary>
        public static IEnumerator Publish(string room, string text, Action<bool> done)
        {
            if (!IsValid(room)) { done?.Invoke(false); yield break; }
            using (var req = new UnityWebRequest(BaseUrl + Topic(room), "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(text ?? ""));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "text/plain");
                req.timeout = 10;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success);
            }
        }

        /// <summary>Asks for the messages since a given message id (or the last 30 minutes the first time). done(null) when it could not be reached.</summary>
        public static IEnumerator Poll(string room, string sinceId, Action<List<RelayMessage>> done)
        {
            if (!IsValid(room)) { done?.Invoke(null); yield break; }
            string since = string.IsNullOrEmpty(sinceId) ? "30m" : sinceId;
            using (var req = UnityWebRequest.Get(BaseUrl + Topic(room) + "/json?poll=1&since=" + since))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success ? ParseMessages(req.downloadHandler.text) : null);
            }
        }

        /// <summary>The service answers with one JSON object per line; only lines with event "message" are crew messages.</summary>
        public static List<RelayMessage> ParseMessages(string ndjson)
        {
            var list = new List<RelayMessage>();
            if (string.IsNullOrWhiteSpace(ndjson)) return list;
            foreach (var line in ndjson.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var o = JObject.Parse(line);
                    if ((string)o["event"] != "message") continue;
                    string text = (string)o["message"];
                    if (!string.IsNullOrEmpty(text) && text.Length <= 4096) list.Add(new RelayMessage { id = (string)o["id"], text = text });
                }
                catch (Exception) { /* a line that is not JSON is skipped */ }
            }
            return list;
        }
    }
}
