using TMinus.Assembly;
using TMinus.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build VAB Scene. Rebuilds the whole VAB scene (Track A owns it).</summary>
    public static class VabSceneBuilder
    {
        const string VolumePath = "Assets/_TMinus/Art/VAB_PostFX.asset";
        const string Uxml = "Assets/_TMinus/UI/UXML/VAB.uxml";
        const string Uss = "Assets/_TMinus/UI/USS/VAB.uss";
        const string PanelPath = "Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset";

        [MenuItem("Tools/T-Minus/Build VAB Scene", priority = 30)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;

            MaterialBuilder.Build();
            var scenePath = SceneBuilder.ScenePath(SceneNames.VAB);
            var scene = System.IO.File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            // Lighting: soft blue trilight ambient + 3-point rig.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.5f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.28f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.12f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            // The camera starts at yaw 30 looking +x/+z, so the key light shines the same way.
            Light("Key Light", new Vector3(38, 15, 0), 2.0f, new Color(1f, 0.96f, 0.9f), LightShadows.Soft);
            Light("Fill Light", new Vector3(15, 120, 0), 0.5f, new Color(0.6f, 0.72f, 1f), LightShadows.None);
            Light("Rim Light", new Vector3(10, 210, 0), 0.9f, new Color(0.85f, 0.9f, 1f), LightShadows.None);

            var env = VabEnvironment.Build(null);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.055f, 0.13f);
            cam.fieldOfView = 50;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var orbit = camGo.AddComponent<OrbitCamera>();
            var selector = camGo.AddComponent<PartSelector>();
            selector.cam = cam;

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = EnsureProfile();

            var uiGo = new GameObject("[VAB UI]");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml);
            var ui = uiGo.AddComponent<VabUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(Uss);
            so.ApplyModifiedPropertiesWithoutUndo();

            var ctrlGo = new GameObject("[VAB]");
            var ctrl = ctrlGo.AddComponent<VabController>();
            ctrl.orbit = orbit;
            ctrl.selector = selector;
            ctrl.ui = ui;
            ctrl.environment = env;
            ctrl.rocketRoot = new GameObject("RocketRoot").transform;
            var assembly = ctrlGo.AddComponent<AssemblyController>();
            assembly.vab = ctrl;
            assembly.selector = selector;
            assembly.ui = ui;
            var bolts = ctrlGo.AddComponent<BoltTool>();
            bolts.vab = ctrl;
            bolts.orbit = orbit;
            bolts.selector = selector;
            bolts.ui = ui;
            ctrl.boltTool = bolts;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[T-Minus] VAB scene built.");

            if (!string.IsNullOrEmpty(previous) && previous != scenePath && System.IO.File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
        }

        static void Light(string name, Vector3 euler, float intensity, Color color, LightShadows shadows)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
