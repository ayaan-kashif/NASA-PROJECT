using TMinus.Core;
using TMinus.Mission;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.EditorTools
{
    /// <summary>Tools > T-Minus > Build Mission Briefing. Rebuilds the MissionBriefing scene (Track B owns it).</summary>
    public static class BriefingSceneBuilder
    {
        [MenuItem("Tools/T-Minus/Build Mission Briefing", priority = 33)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetActiveScene().path;
            var path = SceneBuilder.ScenePath(SceneNames.MissionBriefing);
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.055f, 0.13f);
            camGo.AddComponent<AudioListener>();

            var uiGo = new GameObject("[Mission Briefing]");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset");
            var ui = uiGo.AddComponent<BriefingUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("baseStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/VAB.uss");
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/Briefing.uss");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[T-Minus] Mission Briefing scene built.");
            if (!string.IsNullOrEmpty(previous) && previous != path && System.IO.File.Exists(previous))
                EditorSceneManager.OpenScene(previous);
        }
    }
}
