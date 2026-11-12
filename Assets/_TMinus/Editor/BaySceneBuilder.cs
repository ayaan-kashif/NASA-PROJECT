using TMinus.Assembly;
using TMinus.Core;
using TMinus.Spacecraft;
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
    /// <summary>Tools > T-Minus > Build Integration Bay. Rebuilds the IntegrationBay scene (Track B owns it).</summary>
    public static class BaySceneBuilder
    {
        const float TableTop = 0.9f;

        [MenuItem("Tools/T-Minus/Build Integration Bay", priority = 31)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;
            MaterialBuilder.Build();

            var path = SceneBuilder.ScenePath(SceneNames.IntegrationBay);
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            // Bright clean room.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.8f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.26f, 0.3f);
            RenderSettings.skybox = null;
            Light("Key Light", new Vector3(55, 20, 0), 1.4f, LightShadows.Soft);
            Light("Fill Light", new Vector3(30, 160, 0), 0.5f, LightShadows.None);

            var room = new GameObject("[Clean Room]").transform;
            Box(room, "Floor", new Vector3(12, 0.1f, 12), new Vector3(0, -0.05f, 0), M.Concrete);
            Box(room, "Wall_Back", new Vector3(12, 4, 0.1f), new Vector3(0, 2, 4), M.BodyWhite);
            Box(room, "Wall_Left", new Vector3(0.1f, 4, 12), new Vector3(-5, 2, 0), M.BodyWhite);
            Box(room, "Wall_Right", new Vector3(0.1f, 4, 12), new Vector3(5, 2, 0), M.BodyWhite);
            Box(room, "Wall_Front", new Vector3(12, 4, 0.1f), new Vector3(0, 2, -6), M.BodyWhite);
            Box(room, "Table", new Vector3(2.6f, 0.05f, 1.3f), new Vector3(0, TableTop - 0.025f, 0), M.MetalGrey);
            for (int i = 0; i < 4; i++)
                Box(room, "Leg", new Vector3(0.05f, TableTop, 0.05f), new Vector3((i % 2 == 0 ? -1.2f : 1.2f), TableTop / 2, (i < 2 ? -0.55f : 0.55f)), M.MetalGrey);
            Box(room, "AntiStaticMat", new Vector3(2.4f, 0.004f, 1.15f), new Vector3(0, TableTop + 0.002f, 0), M.Wall);
            var stand = Box(room, "SatStand", new Vector3(0.16f, 0.05f, 0.16f), new Vector3(-0.85f, TableTop + 0.029f, 0), M.BandBlack);

            var satStand = new GameObject("SatMount").transform;
            satStand.position = new Vector3(-0.85f, TableTop + 0.054f, 0);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.88f, 0.92f);
            cam.fieldOfView = 45;
            cam.nearClipPlane = 0.01f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var orbit = camGo.AddComponent<OrbitCamera>();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_TMinus/Art/VAB_PostFX.asset");

            var uiGo = new GameObject("[Bay UI]");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_TMinus/UI/UXML/IntegrationBay.uxml");
            var ui = uiGo.AddComponent<BayUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("baseStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/VAB.uss");
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/IntegrationBay.uss");
            so.ApplyModifiedPropertiesWithoutUndo();

            var benchGo = new GameObject("[FlatSat Bench]");
            benchGo.transform.position = new Vector3(0.25f, TableTop + 0.005f, 0);
            var bench = benchGo.AddComponent<WiringBench>();

            var ctrl = new GameObject("[Integration Bay]").AddComponent<BayController>();
            ctrl.orbit = orbit;
            ctrl.ui = ui;
            ctrl.bench = bench;
            ctrl.satStand = satStand;

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[T-Minus] Integration Bay scene built.");
            if (!string.IsNullOrEmpty(previous) && previous != path && System.IO.File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
        }

        static void Light(string name, Vector3 euler, float intensity, LightShadows shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.shadows = shadows;
            l.transform.rotation = Quaternion.Euler(euler);
        }

        static GameObject Box(Transform parent, string name, Vector3 size, Vector3 pos, string mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = M.Get(mat);
            go.isStatic = true;
            return go;
        }
    }
}
