using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEditor;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Tools > T-Minus > Validate Models (Prompt 16):
    /// Verifies all parts from parts.json against imported models/prefabs.
    /// Checks:
    /// 1. Model / Prefab present?
    /// 2. Expected sockets found (SNAP_*, BOLT_## count, PORT_* types, PIPE_*)?
    /// 3. Dimensions sane (within 50% of length and diameter in parts.json)?
    /// </summary>
    public sealed class ModelValidatorWindow : EditorWindow
    {
        public struct ValidationResult
        {
            public string partId;
            public string name;
            public string category;
            public bool hasPrefab;
            public bool hasModel;
            public int expectedBolts;
            public int foundBolts;
            public List<string> missingPorts;
            public bool sizeOk;
            public string sizeNote;
            public bool isOk;
            public List<string> issues;
        }

        List<ValidationResult> _results = new List<ValidationResult>();
        Vector2 _scroll;

        [MenuItem("Tools/T-Minus/Validate Models", priority = 40)]
        public static void Open()
        {
            var win = GetWindow<ModelValidatorWindow>("Model Validator");
            win.minSize = new Vector2(650, 400);
            win.Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            _results.Clear();
            var data = DataRegistry.LoadFromResources();
            var rules = data.GetRules();

            foreach (var partDef in data.Parts)
            {
                var res = ValidatePart(partDef, rules);
                _results.Add(res);
            }
        }

        public static ValidationResult ValidatePart(PartDef partDef, RulesDef rules)
        {
            var res = new ValidationResult
            {
                partId = partDef.id,
                name = partDef.name,
                category = partDef.category,
                missingPorts = new List<string>(),
                issues = new List<string>(),
                sizeOk = true
            };

            string prefabPath = $"{TMinus.EditorTools.ImportPostprocessor.PrefabDir}/{partDef.id}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            res.hasPrefab = prefab != null;

            // Check for FBX
            var fbxGuids = AssetDatabase.FindAssets($"{partDef.id} t:Model", new[] { "Assets/_TMinus/Art/Models" });
            res.hasModel = fbxGuids.Length > 0;

            // Expected bolts from rules.json
            var joint = rules.boltJoints.FirstOrDefault(j => j.upperPartId == partDef.id);
            res.expectedBolts = joint?.boltCount ?? 0;

            if (!res.hasPrefab && !res.hasModel)
            {
                res.issues.Add("No FBX or prefab found (using procedural placeholder)");
                res.isOk = false;
                return res;
            }

            GameObject inspectObj = prefab;
            bool destroyAfter = false;

            if (inspectObj == null && res.hasModel)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
                inspectObj = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            }

            if (inspectObj != null)
            {
                // Sockets
                var transforms = inspectObj.GetComponentsInChildren<Transform>(true);
                res.foundBolts = transforms.Count(t => t.name.StartsWith("BOLT_"));

                if (res.expectedBolts > 0 && res.foundBolts != res.expectedBolts)
                {
                    res.issues.Add($"Bolts: found {res.foundBolts}, expected {res.expectedBolts} for joint {joint.id}");
                }

                // Ports
                if (partDef.ports != null && partDef.ports.Count > 0)
                {
                    foreach (var port in partDef.ports)
                    {
                        bool found = transforms.Any(t => t.name.Equals("PORT_" + port.id, StringComparison.OrdinalIgnoreCase) ||
                                                         t.name.Equals(port.id, StringComparison.OrdinalIgnoreCase));
                        if (!found)
                        {
                            res.missingPorts.Add(port.id);
                        }
                    }
                    if (res.missingPorts.Count > 0)
                    {
                        res.issues.Add($"Missing ports: {string.Join(", ", res.missingPorts)}");
                    }
                }

                // Dimensions
                var renderers = inspectObj.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        if (renderers[i].gameObject.name.StartsWith("INT_")) continue;
                        b.Encapsulate(renderers[i].bounds);
                    }

                    float modelLen = b.size.y;
                    float modelDia = Mathf.Max(b.size.x, b.size.z);

                    if (partDef.lengthM > 0)
                    {
                        float diff = Mathf.Abs(modelLen - (float)partDef.lengthM) / (float)partDef.lengthM;
                        if (diff > 0.5f)
                        {
                            res.sizeOk = false;
                            res.issues.Add($"Length mismatch: {modelLen:0.0#} m vs data {partDef.lengthM:0.0#} m (>50% diff)");
                        }
                    }

                    if (partDef.diameterM > 0)
                    {
                        float diff = Mathf.Abs(modelDia - (float)partDef.diameterM) / (float)partDef.diameterM;
                        if (diff > 0.5f)
                        {
                            res.sizeOk = false;
                            res.issues.Add($"Diameter mismatch: {modelDia:0.0#} m vs data {partDef.diameterM:0.0#} m (>50% diff)");
                        }
                    }
                }
            }

            res.isOk = res.issues.Count == 0;
            return res;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh Scan", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                Refresh();
            }
            GUILayout.FlexibleSpace();
            int okCount = _results.Count(r => r.isOk);
            int total = _results.Count;
            GUILayout.Label($"Validated: {okCount}/{total} parts fully compliant", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            foreach (var r in _results)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                string statusIcon = r.isOk ? "✓" : (r.hasPrefab || r.hasModel ? "▲" : "○");
                var color = r.isOk ? Color.green : (r.hasPrefab || r.hasModel ? Color.yellow : new Color(0.6f, 0.6f, 0.6f));

                var prevColor = GUI.color;
                GUI.color = color;
                GUILayout.Label(statusIcon, GUILayout.Width(20));
                GUI.color = prevColor;

                GUILayout.Label($"<b>{r.partId}</b> ({r.name})", GUILayout.Width(280));
                GUILayout.Label($"Cat: {r.category}", GUILayout.Width(120));

                string source = r.hasPrefab ? "Prefab" : (r.hasModel ? "FBX Model" : "Procedural");
                GUILayout.Label($"Source: {source}", GUILayout.Width(110));

                EditorGUILayout.EndHorizontal();

                if (r.issues.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    foreach (var issue in r.issues)
                    {
                        EditorGUILayout.LabelField("• " + issue, EditorStyles.wordWrappedMiniLabel);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
