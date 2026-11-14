using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>One quick-check question for a Space Guide topic (Resources/Data/quiz.json).</summary>
    public sealed class QuizItem
    {
        public string id, q, correct;
        public List<string> wrong = new List<string>();
    }

    /// <summary>
    /// Space Guide progress (audit F14): which topics the player has opened and which quick checks they got right. Kept on this
    /// computer only. The offline quiz does not need the AI.
    /// </summary>
    public static class CodexProgress
    {
        const string SeenKey = "tminus.codex.seen", PassedKey = "tminus.codex.passed";
        static Dictionary<string, QuizItem> _quiz;

        // ------------------------------------------------------------------ quiz data

        public static List<QuizItem> Parse(string json)
        {
            var list = new List<QuizItem>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                var root = JObject.Parse(json);
                if (root["quiz"] is JArray arr)
                    foreach (var token in arr)
                    {
                        var item = token.ToObject<QuizItem>();
                        if (item != null && !string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.correct) && item.wrong != null && item.wrong.Count > 0) list.Add(item);
                    }
            }
            catch (Exception ex) { Debug.LogWarning("[CodexProgress] quiz.json is not valid: " + ex.Message); }
            return list;
        }

        public static QuizItem QuizFor(string codexId)
        {
            if (_quiz == null)
            {
                var asset = Resources.Load<TextAsset>("Data/quiz");
                _quiz = Parse(asset != null ? asset.text : null).GroupBy(i => i.id).ToDictionary(g => g.Key, g => g.First());
            }
            return codexId != null && _quiz.TryGetValue(codexId, out var item) ? item : null;
        }

        /// <summary>The answers in a fixed but mixed order, so the right one is not always first (the same every time for one question).</summary>
        public static List<string> Options(QuizItem item) =>
            new[] { item.correct }.Concat(item.wrong).OrderBy(o => Hash(item.id + "|" + o)).ToList();

        static uint Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }

        // ------------------------------------------------------------------ progress

        public static bool IsSeen(string id) => Read(SeenKey).Contains(id);
        public static bool IsPassed(string id) => Read(PassedKey).Contains(id);
        public static void MarkSeen(string id) => Add(SeenKey, id);
        public static void MarkPassed(string id) => Add(PassedKey, id);
        public static int SeenCount(IEnumerable<string> ids) { var seen = Read(SeenKey); return ids.Count(seen.Contains); }
        public static int PassedCount(IEnumerable<string> ids) { var passed = Read(PassedKey); return ids.Count(passed.Contains); }

        static HashSet<string> Read(string key)
        {
            string raw = PlayerPrefs.GetString(key, "");
            return new HashSet<string>(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        static void Add(string key, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var set = Read(key);
            if (!set.Add(id)) return;
            PlayerPrefs.SetString(key, string.Join(",", set));
            PlayerPrefs.Save();
        }
    }
}
