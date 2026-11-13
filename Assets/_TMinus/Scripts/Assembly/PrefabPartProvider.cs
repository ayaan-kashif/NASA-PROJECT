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
        public const string PrefabsFolder = "Assets/_TMinus/Prefabs/Parts";

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
            if (part != null)
            {
                part.partId = partId;
                part.RegisterSockets();
            }

            return go;
        }
    }
}
