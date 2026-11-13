using System;
using System.IO;
using TMinus.Assembly;
using TMinus.Core;
using UnityEditor;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Blender FBX import pipeline (Prompt 16):
    /// - Scale factor 1.0, no animations, preserve empties/hierarchy
    /// - Adds colliders (convex mesh collider for <= 255 tris, else box collider)
    /// - Remaps materials to MaterialLibrary by name
    /// - Automatically generates Prefabs/Parts/<partId>.prefab with Part component and socket registration.
    /// </summary>
    public sealed class ImportPostprocessor : AssetPostprocessor
    {
        const string ModelDir = "Assets/_TMinus/Art/Models";
        // Under Resources so the built game can load them (Prefabs outside Resources only exist in the Editor).
        public const string PrefabDir = "Assets/_TMinus/Resources/Prefabs/Parts";

        bool IsTargetFbx => assetPath.Replace('\\', '/').StartsWith(ModelDir, StringComparison.OrdinalIgnoreCase)
                            && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsTargetFbx) return;

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1.0f;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importVisibility = true;
            importer.preserveHierarchy = true; // Keep empties for sockets
            importer.isReadable = true;            // MeshCollider cooking in builds
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!IsTargetFbx) return;

            // Add colliders
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.gameObject.name.StartsWith("INT_") || mf.gameObject.name.StartsWith("SNAP_") ||
                    mf.gameObject.name.StartsWith("BOLT_") || mf.gameObject.name.StartsWith("PORT_") ||
                    mf.gameObject.name.StartsWith("PIPE_"))
                    continue;

                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                if (mf.GetComponent<Collider>() == null)
                {
                    // Exact (non-convex) collider: parts never use rigidbodies, and a box around a round
                    // tank would swallow clicks meant for the bolts on its flange.
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                }
            }

            // Remap materials by name
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        string matName = CleanMaterialName(mats[i].name);
                        var sharedMat = MaterialLibrary.Find(matName); // unknown names keep the FBX's own material
                        if (sharedMat != null && sharedMat != mats[i])
                        {
                            mats[i] = sharedMat;
                            changed = true;
                        }
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                string normPath = path.Replace('\\', '/');
                if (normPath.StartsWith(ModelDir, StringComparison.OrdinalIgnoreCase) && normPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    CreateOrUpdatePrefab(normPath);
                }
            }
        }

        public static GameObject CreateOrUpdatePrefab(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return null;

            string partId = Path.GetFileNameWithoutExtension(fbxPath);
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);

            string prefabPath = $"{PrefabDir}/{partId}.prefab";
            var instance = UnityEngine.Object.Instantiate(model);
            instance.name = partId;

            // Ensure Part component
            var part = instance.GetComponent<Part>();
            if (part == null) part = instance.AddComponent<Part>();
            part.partId = partId;
            part.RegisterSockets();

            // Calculate bounding dimensions
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.StartsWith("INT_")) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (hasBounds)
            {
                // Rough defaults only; PrefabPartProvider sets the real values from parts.json at runtime.
                part.bellDepth = Mathf.Max(0, -bounds.min.y);
                if (part.stackLength <= 0) part.stackLength = Mathf.Max(0, bounds.max.y);
                if (part.diameterM <= 0) part.diameterM = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            Debug.Log($"[ImportPipeline] Created/Updated prefab for {partId} at {prefabPath}");
            return prefab;
        }

        static string CleanMaterialName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            // Remove Blender's .001 suffix or " (Instance)"
            int dot = name.IndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            int inst = name.IndexOf(" (Instance)");
            if (inst > 0) name = name.Substring(0, inst);
            return name.Trim();
        }
    }
}
