using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Loads every JSON file in Resources/Data. Each list file may be a bare array or an
    /// object wrapping it ({"parts": [...]}). Problems are collected in Errors and logged,
    /// never thrown, so one bad file can't stop the game.
    /// </summary>
    public sealed class DataRegistry
    {
        public const string ResourcesFolder = "Data";

        readonly Dictionary<string, PartDef> _parts = new Dictionary<string, PartDef>();
        readonly Dictionary<string, RocketDef> _rockets = new Dictionary<string, RocketDef>();
        readonly Dictionary<string, MissionDef> _missions = new Dictionary<string, MissionDef>();
        readonly Dictionary<string, CodexEntry> _codex = new Dictionary<string, CodexEntry>();
        readonly List<string> _errors = new List<string>();
        RulesDef _rules = new RulesDef();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyCollection<PartDef> Parts => _parts.Values;
        public IReadOnlyCollection<RocketDef> Rockets => _rockets.Values;
        public IReadOnlyCollection<MissionDef> Missions => _missions.Values;
        public IReadOnlyCollection<CodexEntry> Codex => _codex.Values;

        public PartDef GetPart(string id) => Find(_parts, id);
        public RocketDef GetRocket(string id) => Find(_rockets, id);
        public MissionDef GetMission(string id) => Find(_missions, id);
        public CodexEntry GetCodex(string id) => Find(_codex, id);
        public RulesDef GetRules() => _rules;

        public string Summary =>
            $"Loaded {_parts.Count} parts, {_rockets.Count} rockets, {_missions.Count} missions, " +
            $"{_codex.Count} codex entries, rules {(_rules.IsEmpty ? "empty" : "ok")}";

        /// <summary>Load from Assets/_TMinus/Resources/Data/*.json.</summary>
        public static DataRegistry LoadFromResources()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in Resources.LoadAll<TextAsset>(ResourcesFolder))
                files[asset.name] = asset.text;

            var registry = LoadFromText(files);
            foreach (var err in registry.Errors) Debug.LogError("[DataRegistry] " + err);
            Debug.Log("[DataRegistry] " + registry.Summary);
            return registry;
        }

        /// <summary>Load from file name (without .json) -> JSON text. Used by tests.</summary>
        public static DataRegistry LoadFromText(IDictionary<string, string> files)
        {
            var r = new DataRegistry();
            files ??= new Dictionary<string, string>();
            r.LoadList(files, "parts", r._parts);
            r.LoadList(files, "rockets", r._rockets);
            r.LoadList(files, "missions", r._missions);
            r.LoadList(files, "codex", r._codex);
            r.LoadRules(files);
            return r;
        }

        void LoadList<T>(IDictionary<string, string> files, string key, Dictionary<string, T> target)
            where T : DefBase
        {
            if (!files.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return;

            JToken token;
            try { token = JToken.Parse(text); }
            catch (Exception ex)
            {
                _errors.Add($"{key}.json is not valid JSON: {ex.Message}");
                return;
            }

            var array = token as JArray ?? (token as JObject)?[key] as JArray;
            if (array == null)
            {
                _errors.Add($"{key}.json must be an array or an object with a \"{key}\" array.");
                return;
            }

            for (int i = 0; i < array.Count; i++)
            {
                T item;
                try { item = array[i].ToObject<T>(); }
                catch (Exception ex)
                {
                    _errors.Add($"{key}.json entry {i}: {ex.Message}");
                    continue;
                }

                if (item == null || string.IsNullOrEmpty(item.id))
                    _errors.Add($"{key}.json entry {i} has no \"id\".");
                else if (target.ContainsKey(item.id))
                    _errors.Add($"{key}.json has a duplicate id \"{item.id}\".");
                else
                    target[item.id] = item;
            }
        }

        void LoadRules(IDictionary<string, string> files)
        {
            if (!files.TryGetValue("rules", out var text) || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                if (JToken.Parse(text) is JObject obj)
                {
                    _rules = obj.ToObject<RulesDef>() ?? new RulesDef();
                    _rules.raw = obj;
                }
                else _errors.Add("rules.json must be a JSON object.");
            }
            catch (Exception ex)
            {
                _errors.Add($"rules.json is not valid JSON: {ex.Message}");
            }
        }

        static T Find<T>(Dictionary<string, T> map, string id) where T : class =>
            id != null && map.TryGetValue(id, out var v) ? v : null;
    }
}
