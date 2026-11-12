using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Core
{
    /// <summary>
    /// Creates UI Toolkit documents at runtime with the shared panel settings
    /// (Resources/UI/TMinusPanelSettings, made by Tools > T-Minus > Rebuild Scenes).
    /// </summary>
    public static class UiDocs
    {
        public const string PanelSettingsPath = "UI/TMinusPanelSettings";

        static PanelSettings _panel;
        static bool _warned;

        public static PanelSettings Panel
        {
            get
            {
                if (_panel == null) _panel = Resources.Load<PanelSettings>(PanelSettingsPath);
                if (_panel == null && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[UiDocs] Missing Resources/" + PanelSettingsPath +
                                     ". Run Tools > T-Minus > Rebuild Scenes.");
                }
                return _panel;
            }
        }

        /// <summary>Make a child object with a UIDocument. Returns null if panel settings are missing.</summary>
        public static UIDocument Create(string name, Transform parent, int sortingOrder,
                                        VisualTreeAsset tree = null)
        {
            var panel = Panel;
            if (panel == null) return null;

            var go = new GameObject(name);
            go.SetActive(false); // configure before OnEnable builds the visual tree
            go.transform.SetParent(parent, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = sortingOrder;
            if (tree != null) doc.visualTreeAsset = tree;
            go.SetActive(true);
            return doc;
        }
    }
}
