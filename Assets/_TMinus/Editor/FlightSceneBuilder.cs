using TMinus.Assembly;
using TMinus.Core;
using TMinus.Flight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build Flight Scene. Rebuilds the Flight scene (Track C owns it).</summary>
    public static class FlightSceneBuilder
    {
        [MenuItem("Tools/T-Minus/Build Flight Scene", priority = 34)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;
            MaterialBuilder.Build();
            var path = SceneBuilder.ScenePath(SceneNames.Flight);
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.48f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.27f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.1f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.8f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35, 40, 0);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
            cam.fieldOfView = 50; cam.nearClipPlane = 0.3f; cam.farClipPlane = 4e7f;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var orbit = camGo.AddComponent<OrbitCamera>();

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_TMinus/Art/VAB_PostFX.asset");

            var world = new GameObject("[World]");
            var view = world.AddComponent<FlightView>();
            view.earthRoot = new GameObject("EarthRoot").transform;
            view.sun = sun;

            var uiGo = new GameObject("[Flight HUD]");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset");
            var hud = uiGo.AddComponent<FlightHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("baseStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/VAB.uss");
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/Flight.uss");
            so.ApplyModifiedPropertiesWithoutUndo();

            var ctrl = new GameObject("[Flight]").AddComponent<FlightController>();
            ctrl.view = view; ctrl.hud = hud; ctrl.orbit = orbit;

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[T-Minus] Flight scene built.");
            if (!string.IsNullOrEmpty(previous) && previous != path && System.IO.File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
        }
    }
}
