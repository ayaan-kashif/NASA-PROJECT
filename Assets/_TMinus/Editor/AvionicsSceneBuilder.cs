using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Wiring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build Avionics Bay. Creates the AvionicsBay scene and adds it to Build Settings.</summary>
    public static class AvionicsSceneBuilder
    {
        [MenuItem("Tools/T-Minus/Build Avionics Bay", priority = 32)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;
            MaterialBuilder.Build();

            var path = SceneBuilder.ScenePath(SceneNames.AvionicsBay);
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.15f);
            var key = new GameObject("Key Light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.5f; key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(60, 15, 0);

            // The inside of the avionics ring: a curved black wall and a work panel.
            var env = new GameObject("[Ring Interior]").transform;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel"; panel.transform.SetParent(env, false);
            panel.transform.localScale = new Vector3(2.6f, 0.04f, 1.4f);
            panel.transform.localPosition = new Vector3(0, -0.02f, 0);
            panel.GetComponent<Renderer>().sharedMaterial = M.Get(M.MetalGrey); // brushed aluminium deck
            var deckEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deckEdge.name = "DeckFrame"; deckEdge.transform.SetParent(env, false);
            deckEdge.transform.localScale = new Vector3(2.75f, 0.03f, 1.55f);
            deckEdge.transform.localPosition = new Vector3(0, -0.05f, 0);
            deckEdge.GetComponent<Renderer>().sharedMaterial = M.Get(M.BandBlack);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Bulkhead"; floor.transform.SetParent(env, false);
            floor.transform.localScale = new Vector3(4.6f, 0.05f, 4.6f);
            floor.transform.localPosition = new Vector3(0, -0.5f, 0);
            floor.GetComponent<Renderer>().sharedMaterial = M.Get(M.Concrete);
            foreach (var (pos, col, inten) in new[] { (new Vector3(-1.4f, 1.7f, -1.0f), new Color(1f, 0.82f, 0.62f), 0.9f), (new Vector3(1.5f, 1.5f, 1.1f), new Color(0.6f, 0.75f, 1f), 0.7f) })
            {
                var pl = new GameObject("Work Light").AddComponent<Light>();
                pl.type = LightType.Point; pl.color = col; pl.intensity = inten; pl.range = 4f; pl.shadows = LightShadows.Soft;
                pl.transform.SetParent(env, false); pl.transform.localPosition = pos;
            }
            var wall = new GameObject("RingWall"); wall.transform.SetParent(env, false);
            wall.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Lathe(new[] { new Vector2(2.2f, -0.5f), new Vector2(2.2f, 1.2f) }, 64, doubleSided: true);
            wall.AddComponent<MeshRenderer>().sharedMaterial = M.Get(M.Wall);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.14f);
            cam.fieldOfView = 45; cam.nearClipPlane = 0.01f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var orbit = camGo.AddComponent<OrbitCamera>();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_TMinus/Art/VAB_PostFX.asset");

            var benchGo = new GameObject("[Avionics Bench]");
            var bench = benchGo.AddComponent<WiringBench>();

            var uiGo = new GameObject("[Avionics Bay]");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_TMinus/UI/UXML/AvionicsBay.uxml");
            var ctrl = uiGo.AddComponent<AvionicsBayController>();
            ctrl.orbit = orbit;
            ctrl.bench = bench;
            var so = new SerializedObject(ctrl);
            so.FindProperty("baseStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/VAB.uss");
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/IntegrationBay.uss");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, path);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[T-Minus] Avionics Bay scene built and added to Build Settings.");
            if (!string.IsNullOrEmpty(previous) && previous != path && System.IO.File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
        }
    }
}
