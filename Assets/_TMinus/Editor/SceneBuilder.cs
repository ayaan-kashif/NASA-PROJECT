using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMinus.Core;
using TMinus.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Tools > T-Minus > Rebuild Scenes. Creates missing scenes, the shared PanelSettings and
    /// Build Settings (Boot first). Safe to run any time: existing scenes are never overwritten
    /// unless you pick "Force Rebuild Stub Scenes".
    /// </summary>
    public static class SceneBuilder
    {
        public const string Root = "Assets/_TMinus";
        public const string ScenesDir = Root + "/Scenes";
        const string PanelPath = Root + "/Resources/UI/TMinusPanelSettings.asset";
        const string ThemePath = Root + "/UI/USS/TMinusTheme.tss";
        const string MenuUxml = Root + "/UI/UXML/MainMenu.uxml";
        const string MenuUss = Root + "/UI/USS/MainMenu.uss";

        static readonly Color Navy = new Color(0.03f, 0.055f, 0.13f);

        public static string ScenePath(string name) => $"{ScenesDir}/{name}.unity";

        [MenuItem("Tools/T-Minus/Rebuild Scenes", priority = 0)]
        public static void RebuildScenes() => Build(false);

        [MenuItem("Tools/T-Minus/Force Rebuild Stub Scenes", priority = 1)]
        static void ForceRebuild()
        {
            if (EditorUtility.DisplayDialog("Force rebuild scenes?",
                    "This overwrites ALL 7 T-Minus scenes with empty stubs. Work saved in those scenes is lost.",
                    "Overwrite", "Cancel"))
                Build(true);
        }

        [MenuItem("Tools/T-Minus/Open Boot Scene", priority = 20)]
        public static void OpenBoot()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
        }

        public static void Build(bool overwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();
            var panel = EnsurePanelSettings();

            int created = 0;
            foreach (var name in SceneNames.All)
            {
                if (!overwrite && File.Exists(ScenePath(name))) continue;
                CreateScene(name, panel);
                created++;
            }

            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[T-Minus] Scenes ready ({created} created). Build Settings: Boot first, " +
                      $"{EditorBuildSettings.scenes.Length} scenes. Open Boot and press Play.");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { ScenesDir, Root + "/Resources/UI" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            if (panel.themeStyleSheet == null)
                Debug.LogWarning("[T-Minus] Theme not found at " + ThemePath);
            return panel;
        }

        static void CreateScene(string name, PanelSettings panel)
        {
            // Unity can't add an untitled scene next to another untitled one; fall back to Single.
            bool untitledOpen = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) untitledOpen = true;
            var mode = untitledOpen ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(0, 2, -10), Quaternion.Euler(8, 0, 0));
            SceneManager.MoveGameObjectToScene(cam, scene);

            var light = new GameObject("Directional Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            SceneManager.MoveGameObjectToScene(light, scene);

            if (name == SceneNames.Boot)
            {
                var boot = new GameObject("[Boot]");
                boot.AddComponent<BootLoader>();
                SceneManager.MoveGameObjectToScene(boot, scene);
            }
            else if (name == SceneNames.MainMenu)
            {
                SceneManager.MoveGameObjectToScene(CreateMainMenu(panel), scene);
            }
            else
            {
                var stub = new GameObject("[SceneLabel]");
                stub.AddComponent<SceneLabel>().Configure(name, true);
                SceneManager.MoveGameObjectToScene(stub, scene);
            }

            EditorSceneManager.SaveScene(scene, ScenePath(name));
            if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }

        static GameObject CreateMainMenu(PanelSettings panel)
        {
            var go = new GameObject("[MainMenu]");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuUxml);
            var controller = go.AddComponent<MainMenuController>();

            var so = new SerializedObject(controller);
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(MenuUss);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        static void UpdateBuildSettings()
        {
            var ours = SceneNames.All.Select(n => new EditorBuildSettingsScene(ScenePath(n), true)).ToList();
            var ourPaths = new HashSet<string>(ours.Select(s => s.path));

            // Keep other scenes teammates added under _TMinus (e.g. AvionicsBay); drop the template sample.
            var extras = EditorBuildSettings.scenes
                .Where(s => !ourPaths.Contains(s.path) && s.path.StartsWith(ScenesDir) && File.Exists(s.path));

            EditorBuildSettings.scenes = ours.Concat(extras).ToArray();
        }
    }

    /// <summary>First time the project opens with this code, build the scenes automatically.</summary>
    [InitializeOnLoad]
    static class SceneBuilderAutoRun
    {
        static SceneBuilderAutoRun()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(SceneBuilder.ScenePath(SceneNames.Boot))) return;
                SceneBuilder.Build(false);

                var active = SceneManager.GetActiveScene();
                if (!active.isDirty && (string.IsNullOrEmpty(active.path) || active.path.EndsWith("SampleScene.unity")))
                    EditorSceneManager.OpenScene(SceneBuilder.ScenePath(SceneNames.Boot));
            };
        }
    }
}
