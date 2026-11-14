#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.UI
{
    /// <summary>Play-script hook: UiDebug.PickAt "x,y" (fractions of the screen) reports which element a click there hits.</summary>
    public static class UiDebug
    {
        /// <summary>Sends a click to the first label showing this text (it bubbles up to its card or button).</summary>
        public static string ClickText(string text)
        {
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var label = d.rootVisualElement?.Query<TextElement>().Where(t => t.text == text).First();
                if (label == null) continue;
                using (var e = ClickEvent.GetPooled()) { e.target = label; label.SendEvent(e); }
                if (label is Button b) { using var n = NavigationSubmitEvent.GetPooled(); n.target = b; b.SendEvent(n); }
                return "clicked '" + text + "'";
            }
            return "no element with text '" + text + "'";
        }

        /// <summary>Lists the visible big titles/labels so a script can check which screen is showing.</summary>
        public static string Screen(string _)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                d.rootVisualElement?.Query<Label>().ForEach(l => { if (l.resolvedStyle.display == DisplayStyle.Flex && l.text.Length > 3 && l.text.ToUpperInvariant() == l.text && names.Count < 8) names.Add(l.text); });
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ": " + string.Join(" | ", names);
        }

        public static string PickAt(string frac)
        {
            var p = frac.Split(',');
            var screen = new Vector2(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture) * UnityEngine.Screen.width,
                                     float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) * UnityEngine.Screen.height);
            var doc = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).FirstOrDefault(d => d.rootVisualElement?.panel != null);
            if (doc == null) return "no UI panel";
            var panel = doc.rootVisualElement.panel;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            var hit = panel.Pick(pos);
            if (hit == null) return "nothing";
            string Path(VisualElement e) { var s = ""; for (int i = 0; e != null && i < 4; i++, e = e.parent) s += $"{e.GetType().Name}#{e.name}.{string.Join(".", e.GetClasses())} < "; return s; }
            return Path(hit);
        }
    }
}
#endif
