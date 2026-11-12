using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.UI
{
    /// <summary>
    /// Stub-scene banner: shows the scene name plus Menu / Next buttons so the whole flow
    /// can be clicked through before the real scenes exist. Each track removes it when
    /// its scene gets real UI.
    /// </summary>
    public sealed class SceneLabel : MonoBehaviour
    {
        [SerializeField] string title = "Scene";
        [SerializeField] bool showNavigation = true;

        public void Configure(string sceneTitle, bool navigation)
        {
            title = sceneTitle;
            showNavigation = navigation;
        }

        void Start()
        {
            var doc = UiDocs.Create("[SceneLabel UI]", transform, 10);
            if (doc == null) return;

            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            var bar = new VisualElement();
            var s = bar.style;
            s.position = Position.Absolute;
            s.left = 24; s.top = 24;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.paddingLeft = 16; s.paddingRight = 16; s.paddingTop = 10; s.paddingBottom = 10;
            s.backgroundColor = new Color(0.04f, 0.07f, 0.16f, 0.85f);
            s.borderTopLeftRadius = s.borderTopRightRadius = 10;
            s.borderBottomLeftRadius = s.borderBottomRightRadius = 10;
            root.Add(bar);

            var label = new Label(title.ToUpperInvariant());
            label.style.color = Color.white;
            label.style.fontSize = 28;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = 20;
            bar.Add(label);

            if (!showNavigation) return;
            bar.Add(MakeButton("Menu", () => SceneFlow.Load(SceneNames.MainMenu), false));
            bar.Add(MakeButton("Next  >", SceneFlow.Next, true));
        }

        static Button MakeButton(string text, System.Action onClick, bool accent)
        {
            var b = new Button(onClick) { text = text };
            var s = b.style;
            s.fontSize = 20;
            s.color = Color.white;
            s.marginLeft = 8;
            s.paddingLeft = 14; s.paddingRight = 14; s.paddingTop = 6; s.paddingBottom = 6;
            s.backgroundColor = accent ? new Color(1f, 0.478f, 0.102f) : new Color(1f, 1f, 1f, 0.12f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 0;
            s.borderTopLeftRadius = s.borderTopRightRadius = 8;
            s.borderBottomLeftRadius = s.borderBottomRightRadius = 8;
            return b;
        }
    }
}
