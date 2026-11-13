using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMinus.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TMinus.Houston
{
    /// <summary>
    /// Async LLM client for HOUSTON (Prompt 14). Sends a small JSON packet + question to the
    /// proxy, falls back to codex text on timeout or error. Config from StreamingAssets/houston.config.json.
    /// No API key anywhere in the Unity project.
    /// </summary>
    public sealed class HoustonLLMClient : MonoBehaviour
    {
        [Serializable]
        sealed class Config
        {
            public string endpointUrl = "";
            public int timeoutSec = 8;
            public string language = "en";
            public bool enabled;
        }

        Config _config;
        readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        /// <summary>True when the last Ask() answer came from the codex fallback (timeout, error or disabled).</summary>
        public bool LastAnswerOffline { get; private set; }

        public bool IsEnabled => _config != null && _config.enabled && !string.IsNullOrEmpty(_config.endpointUrl);

        void Awake()
        {
            LoadConfig();
        }

        void LoadConfig()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browsers have no file system: StreamingAssets is a URL next to the page, so fetch it.
            _config = new Config();
            StartCoroutine(LoadConfigWeb());
            return;
#endif
            try
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "houston.config.json");
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    _config = JsonConvert.DeserializeObject<Config>(json);
                    Debug.Log($"[HOUSTON LLM] Config loaded: endpoint={_config?.endpointUrl}, enabled={_config?.enabled}");
                }
                else
                {
                    _config = new Config();
                    Debug.Log("[HOUSTON LLM] No houston.config.json found, LLM disabled (offline mode).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HOUSTON LLM] Failed to load config: {ex.Message}");
                _config = new Config();
            }
        }

        System.Collections.IEnumerator LoadConfigWeb()
        {
            using (var req = UnityWebRequest.Get(Application.streamingAssetsPath + "/houston.config.json"))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { _config = JsonConvert.DeserializeObject<Config>(req.downloadHandler.text) ?? new Config(); }
                    catch (Exception ex) { Debug.LogWarning($"[HOUSTON LLM] Bad config: {ex.Message}"); }
                }
                Debug.Log($"[HOUSTON LLM] Web config: endpoint={_config?.endpointUrl}, enabled={_config?.enabled}");
            }
        }

        /// <summary>Ask HOUSTON. Returns the LLM answer or an offline fallback.</summary>
        public async Task<string> Ask(string question, Recommendation advisorRec, AdvisorContext ctx)
        {
            // Build cache key
            string cacheKey = BuildCacheKey(question, advisorRec, ctx);
            LastAnswerOffline = false;
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            LastAnswerOffline = true;
            if (!IsEnabled)
                return FallbackAnswer(question, advisorRec, ctx);

            try
            {
                var packet = BuildPacket(question, advisorRec, ctx);
                string body = JsonConvert.SerializeObject(new { packet, question });

                var result = await PostAsync(_config.endpointUrl, body, _config.timeoutSec);
                if (result != null)
                {
                    var obj = JObject.Parse(result);
                    string text = obj["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _cache[cacheKey] = text;
                        LastAnswerOffline = false;
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HOUSTON LLM] Request failed: {ex.Message}");
            }

            string fallback = FallbackAnswer(question, advisorRec, ctx);
            return fallback;
        }

        /// <summary>Ask for a debrief narration.</summary>
        public async Task<string> AskDebrief(string causalTimeline, string scoreBreakdown)
        {
            if (!IsEnabled)
                return $"Mission complete. {scoreBreakdown}";

            try
            {
                var packet = new
                {
                    type = "debrief",
                    causalTimeline,
                    scoreBreakdown,
                    language = GameState.Instance?.Settings.language ?? _config?.language ?? "en"
                };
                string body = JsonConvert.SerializeObject(new { packet, question = "Narrate the debrief" });
                var result = await PostAsync(_config.endpointUrl, body, _config.timeoutSec);
                if (result != null)
                {
                    var obj = JObject.Parse(result);
                    return obj["text"]?.ToString() ?? $"Mission complete. {scoreBreakdown}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HOUSTON LLM] Debrief request failed: {ex.Message}");
            }
            return $"Mission complete. {scoreBreakdown}";
        }

        // ------------------------------------------------------------------ packet

        object BuildPacket(string question, Recommendation rec, AdvisorContext ctx)
        {
            var state = GameState.Instance;
            var data = state?.Data;
            var mission = data?.GetMission(ctx?.MissionId);

            // Collect relevant codex entries (up to 5)
            var codexEntries = new List<object>();
            var codexIds = new HashSet<string>();
            if (!string.IsNullOrEmpty(rec?.codexId)) codexIds.Add(rec.codexId);

            // Search for matching codex entries from the question
            if (data != null)
            {
                string q = question?.ToLowerInvariant() ?? "";
                foreach (var entry in data.Codex)
                {
                    if (entry.title != null && q.Contains(entry.title.ToLowerInvariant()))
                        codexIds.Add(entry.id);
                    if (codexIds.Count >= 5) break;
                }
            }

            foreach (var cid in codexIds)
            {
                var ce = data?.GetCodex(cid);
                if (ce != null)
                    codexEntries.Add(new { id = ce.id, title = ce.title, shortText = ce.shortText, longText = ce.longText });
            }

            // Top 5 faults
            var faults = AssemblyAdvisor.Scan(ctx);
            var topFaults = new List<string>();
            for (int i = 0; i < Math.Min(5, faults.Count); i++)
                topFaults.Add(faults[i].what);

            // Recent events (last 10)
            var recentEvents = new List<string>();
            if (state?.Events != null)
            {
                var all = state.Events.All;
                int start = Math.Max(0, all.Count - 10);
                for (int i = start; i < all.Count; i++)
                    recentEvents.Add(all[i].ToString());
            }

            return new
            {
                mission = mission != null ? new { id = mission.id, objective = mission.objective } : null,
                scene = ctx?.SceneName,
                playerRole = state?.Crew?.Count > 0 ? state.Crew[0].role.ToString() : "FlightDirector",
                currentStep = rec?.text,
                advisorRecommendation = rec?.text,
                faults = topFaults,
                codex = codexEntries,
                events = recentEvents,
                language = GameState.Instance?.Settings.language ?? _config?.language ?? "en"
            };
        }

        string BuildCacheKey(string question, Recommendation rec, AdvisorContext ctx)
        {
            var sb = new StringBuilder();
            sb.Append(question ?? "");
            sb.Append('|');
            sb.Append(rec?.text ?? "");
            sb.Append('|');
            if (ctx != null)
            {
                var faults = AssemblyAdvisor.Scan(ctx);
                foreach (var f in faults)
                    sb.Append(f.why).Append(',');
            }
            return sb.ToString().GetHashCode().ToString("X8");
        }

        // ------------------------------------------------------------------ fallback

        string FallbackAnswer(string question, Recommendation rec, AdvisorContext ctx)
        {
            var data = GameState.Instance?.Data;
            if (data == null) return rec?.text ?? "I'm here to help. Ask me about any part or step.";

            // Try to match question to a codex entry
            string q = question?.ToLowerInvariant() ?? "";
            foreach (var entry in data.Codex)
            {
                if (entry.title != null && q.Contains(entry.title.ToLowerInvariant()))
                    return entry.shortText ?? entry.longText ?? rec?.text ?? "";
            }

            // Try rec's codex id
            if (!string.IsNullOrEmpty(rec?.codexId))
            {
                var ce = data.GetCodex(rec.codexId);
                if (ce != null)
                    return $"{rec.text} — {ce.shortText}";
            }

            return rec?.text ?? "I'm here to help. Ask me about any part, bolt, or cable.";
        }

        // ------------------------------------------------------------------ HTTP

        static async Task<string> PostAsync(string url, string body, int timeoutSec)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSec;

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HOUSTON LLM] HTTP {req.responseCode}: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
