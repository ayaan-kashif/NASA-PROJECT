using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TMinus.Assembly
{
    /// <summary>
    /// Connects the Blender import pipeline (Prompt 16) to ProceduralPartFactory.
    /// Hooks ProceduralPartFactory.PrefabProvider at startup to return real prefabs
    /// from Prefabs/Parts/<partId>.prefab, falling back to procedural generation.
    /// </summary>
    public static class PrefabPartProvider
    {
        static TMinus.Core.DataRegistry _data;

        public const string PrefabsFolder = "Assets/_TMinus/Resources/Prefabs/Parts";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            ProceduralPartFactory.PrefabProvider = GetPrefab;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void EditorInit()
        {
            ProceduralPartFactory.PrefabProvider = GetPrefab;
        }
#endif

        /// <summary>Stacking needs the data's length/diameter (a fairing adds no height); engine depth comes from the model.</summary>
        static void ApplyPhysicalData(Part part)
        {
            var data = TMinus.Core.GameState.Instance != null ? TMinus.Core.GameState.Instance.Data : (_data ??= TMinus.Core.DataRegistry.LoadFromResources());
            var def = data?.GetPart(part.partId);
            if (def == null) return;
            part.Def = def;
            if (def.diameterM > 0) part.diameterM = (float)def.diameterM;
            if (def.lengthM > 0) part.stackLength = def.category == "fairing" ? 0 : (float)def.lengthM;
            part.isShell = def.category == "rocket_stage" || def.category == "saturn_stage" || def.category == "interstage" || def.category == "fairing";
            float minY = 0;
            foreach (var r in part.GetComponentsInChildren<Renderer>(true))
                minY = Mathf.Min(minY, part.transform.InverseTransformPoint(r.bounds.min).y);
            part.bellDepth = Mathf.Max(0, -minY);
        }

        public static GameObject GetPrefab(string partId, Transform parent)
        {
            if (string.IsNullOrEmpty(partId)) return null;

            GameObject prefab = null;

#if UNITY_EDITOR
            string path = $"{PrefabsFolder}/{partId}.prefab";
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif

            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>($"Prefabs/Parts/{partId}");
            }

            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, parent);
            go.name = partId;

            var part = go.GetComponent<Part>();
            if (part == null) part = go.AddComponent<Part>();
            part.partId = partId;
            part.RegisterSockets();
            ApplyPhysicalData(part);

            return go;
        }
    }
}
