using System.Linq;
using TMinus.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.Houston
{
    /// <summary>
    /// Guided first build (audit F2): the first time a player enters the Integration Bay, the VAB or the Avionics Bay, a bar at the top
    /// shows the next step from HOUSTON's advisor, one at a time. After five steps it hands over ("press H if you need me").
    /// Guided steps are not counted as hints. Skip any time; Settings can replay it. Not shown in scripted demos.
    /// </summary>
    public sealed class FirstBuildGuide : MonoBehaviour
    {
        const int StepsToTeach = 5;

        string _room;
        Label _text;
        Button _show;
        VisualElement _bar;
        string _last, _objectRef;
        int _steps;
        float _next, _closeAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (scene, mode) => TrySpawn(scene.name);
            TrySpawn(SceneManager.GetActiveScene().name);
        }

        static void TrySpawn(string room)
        {
            if (!FirstBuildGuideFlags.Rooms.Contains(room) || FirstBuildGuideFlags.IsDone(room)) return;
            if (SaveService.Suspended) return;                                           // a demo or repair job is running
            var state = GameState.Instance;
            if (state == null || state.Settings.houstonMode == HoustonMode.Off) return;  // "fly solo" means no guidance
            if (FindAnyObjectByType<FirstBuildGuide>() != null) return;
            var go = new GameObject("[FirstBuildGuide]");                                 // lives in the room's scene and goes with it
            go.AddComponent<FirstBuildGuide>()._room = room;
        }

        void Start()
        {
            var doc = UiDocs.Create("[FirstBuildGuide UI]", transform, 85);
            if (doc == null) { Destroy(gameObject); return; }
            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            _bar = new VisualElement();
            var b = _bar.style;
            b.position = Position.Absolute; b.top = 78; b.left = Length.Percent(22); b.right = Length.Percent(22);
            b.flexDirection = FlexDirection.Row; b.alignItems = Align.Center; b.justifyContent = Justify.SpaceBetween;
            b.backgroundColor = new Color(0.05f, 0.08f, 0.17f, 0.95f);
            b.borderTopWidth = 3; b.borderTopColor = new Color(0.25f, 0.8f, 0.45f);
            b.paddingLeft = 16; b.paddingRight = 10; b.paddingTop = 8; b.paddingBottom = 8;
            b.borderBottomLeftRadius = b.borderBottomRightRadius = 10;
            _text = new Label("Starting the guided build...");
            _text.style.color = Color.white; _text.style.fontSize = 18; _text.style.whiteSpace = WhiteSpace.Normal; _text.style.flexShrink = 1; _text.style.flexGrow = 1;
            _bar.Add(_text);
            _show = Make("Show me", () => HoustonPanel.Instance?.FocusOn(_objectRef), true);
            _bar.Add(_show);
            _bar.Add(Make("Skip tutorial", Finish, false));
            root.Add(_bar);
        }

        static Button Make(string label, System.Action click, bool accent)
        {
            var b = new Button(click) { text = label };
            var s = b.style;
            s.fontSize = 15; s.color = Color.white; s.marginLeft = 8;
            s.backgroundColor = accent ? new Color(0.2f, 0.65f, 0.35f) : new Color(1f, 1f, 1f, 0.12f);
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = 0;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 6;
            return b;
        }

        void Update()
        {
            if (_closeAt > 0f) { if (Time.unscaledTime >= _closeAt) Destroy(gameObject); return; }
            if (_text == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.7f;
            if (SaveService.Suspended) { Destroy(gameObject); return; }

            var rec = AssemblyAdvisor.NextStep(ContextBuilder.Build(GameState.Instance));
            if (rec == null) return;
            _objectRef = rec.objectRef;
            _show.style.display = string.IsNullOrEmpty(_objectRef) ? DisplayStyle.None : DisplayStyle.Flex;
            if (rec.text != _last)
            {
                if (_last != null) _steps++;       // the advisor moved on: the player did the last step
                _last = rec.text;
            }
            bool allDone = rec.reasonCode != null && rec.reasonCode.EndsWith("_ready");
            if (_steps >= StepsToTeach || allDone)
            {
                _text.text = "You've got it. HOUSTON is on H whenever you want help, and N shows the next step.";
                _show.style.display = DisplayStyle.None;
                FirstBuildGuideFlags.MarkDone(_room);
                _closeAt = Time.unscaledTime + 5f;
                return;
            }
            _text.text = $"Guided build, step {_steps + 1} of {StepsToTeach}:  {rec.text}";
        }

        void Finish()
        {
            FirstBuildGuideFlags.MarkDone(_room);
            Destroy(gameObject);
        }
    }
}
