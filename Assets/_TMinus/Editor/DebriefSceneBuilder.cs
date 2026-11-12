using System.IO;
using TMinus.Core;
using TMinus.Mission;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.EditorTools
{
    public static class DebriefSceneBuilder
    {
        [MenuItem("Tools/T-Minus/Build Debrief", priority = 35)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            string path = SceneBuilder.ScenePath(SceneNames.Debrief);
            var scene = File.Exists(path) ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.045f, 0.09f);
            camera.gameObject.AddComponent<AudioListener>();
            var ui = new GameObject("[Debrief UI]"); var doc = ui.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_TMinus/Resources/UI/TMinusPanelSettings.asset");
            var view = ui.AddComponent<DebriefUI>(); var so = new SerializedObject(view);
            so.FindProperty("baseStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/VAB.uss");
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_TMinus/UI/USS/Debrief.uss"); so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
            if (!string.IsNullOrEmpty(previous) && previous != path && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log("[T-Minus] Debrief scene built.");
        }
    }
}
