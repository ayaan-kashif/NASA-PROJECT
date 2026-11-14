using System;
using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.Houston
{
    /// <summary>
    /// HOUSTON overlay: a collapsible bottom-right assistant panel in the gameplay scenes, spawned at runtime
    /// (no scene edits). H toggles, N next step, Shift+S scan. Proactive tips at most every 20 s.
    /// Actions and messages are in HoustonPanel.Actions.cs.
    /// </summary>
    public sealed partial class HoustonPanel : MonoBehaviour
    {
        static HoustonPanel _instance;
        UIDocument _doc;
        VisualElement _root, _panel, _msgList;
        TextField _input;
        Label _typingLabel;
        Button _toggleFab;

        bool _visible;
        float _lastTipTime = -20f;
        const float TipCooldown = 20f;
        const int MaxMessages = 50;

        readonly List<string> _messageLog = new List<string>();
        HoustonLLMClient _llm;

        static readonly HashSet<string> GameplayScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SceneNames.IntegrationBay, SceneNames.VAB, SceneNames.AvionicsBay, SceneNames.MissionBriefing, SceneNames.Debrief, SceneNames.Flight
        };

        public static HoustonPanel Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            SceneManager.sceneLoaded += (s, m) => TrySpawn();
            TrySpawn();
        }

        static void TrySpawn()
        {
            bool gameplay = GameplayScenes.Contains(SceneManager.GetActiveScene().name);
            if (_instance == null)
            {
                if (!gameplay) return;
                var go = new GameObject("[HOUSTON]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HoustonPanel>();
            }
            _instance.SetSceneActive(gameplay);
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _llm = gameObject.AddComponent<HoustonLLMClient>();
            DemoHooks.NarrateDebrief = (timeline, breakdown) => _llm.AskDebrief(timeline, breakdown);   // the debrief asks for its AI summary through this
            BuildUI();
            HoustonBus.AskRequested += OnAskRequested;
            if (GameState.Instance != null) GameState.Instance.Events.Added += OnGameEvent;
        }

        void OnDestroy()
        {
            HoustonBus.AskRequested -= OnAskRequested;
            if (GameState.Instance != null) GameState.Instance.Events.Added -= OnGameEvent;
            if (_instance == this) { _instance = null; DemoHooks.NarrateDebrief = null; }
        }

        /// <summary>Hide the whole overlay (button included) outside the gameplay scenes.</summary>
        void SetSceneActive(bool active)
        {
            if (_root != null) _root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            SetVisible(false);
        }

        void BuildUI()
        {
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = Resources.Load<PanelSettings>(UiDocs.PanelSettingsPath);
            if (_doc.panelSettings == null) { Debug.LogWarning("[HOUSTON] Missing Resources/" + UiDocs.PanelSettingsPath); return; }
            _doc.sortingOrder = 90;

            _root = _doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            var tree = Resources.Load<VisualTreeAsset>("UI/HoustonPanel");
            var style = Resources.Load<StyleSheet>("UI/Houston");
            if (tree != null) tree.CloneTree(_root);
            else Debug.LogWarning("[HOUSTON] Missing Resources/UI/HoustonPanel.uxml");
            if (style != null && !_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);
            // The overlay layer spans the whole screen; it must let clicks through to the game underneath.
            // (USS has no pointer-events: picking is set in code. Only the panel and the round button take clicks.)
            var layer = _root.Q("houston-root");
            if (layer != null) layer.pickingMode = PickingMode.Ignore;

            _panel = _root.Q("houston-panel");
            _toggleFab = _root.Q<Button>("houston-toggle-fab");
            _msgList = _root.Q("houston-messages");
            _typingLabel = _root.Q<Label>("houston-typing");
            _input = _root.Q<TextField>("houston-input");

            _toggleFab?.RegisterCallback<ClickEvent>(_ => SetVisible(true));
            _root.Q<Button>("houston-close-btn")?.RegisterCallback<ClickEvent>(_ => SetVisible(false));
            _root.Q<Button>("btn-next")?.RegisterCallback<ClickEvent>(_ => OnNextStep());
            _root.Q<Button>("btn-where")?.RegisterCallback<ClickEvent>(_ => OnWhereDoesThisGo());
            _root.Q<Button>("btn-scan")?.RegisterCallback<ClickEvent>(_ => OnScan());
            _root.Q<Button>("btn-why")?.RegisterCallback<ClickEvent>(_ => OnWhy());
            _input?.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return || string.IsNullOrWhiteSpace(_input.value)) return;
                string q = _input.value.Trim();
                _input.value = "";
                AskFreeQuestion(q);
                e.StopPropagation();
            });
            SetVisible(false);
        }

        public void SetVisible(bool vis)
        {
            _visible = vis;
            if (_panel != null) _panel.EnableInClassList("hidden", !vis);
            if (_toggleFab != null) _toggleFab.style.display = vis ? DisplayStyle.None : DisplayStyle.Flex;
        }

        bool Off => GameState.Instance?.Settings.houstonMode == HoustonMode.Off;

        void Update()
        {
            if (Off) return;
            var kb = Keyboard.current;
            if (kb == null || IsTextFieldFocused() || !GameplayScenes.Contains(SceneManager.GetActiveScene().name)) return;
            if (kb.hKey.wasPressedThisFrame) SetVisible(!_visible);
            if (kb.nKey.wasPressedThisFrame && _visible) OnNextStep();
            if (kb.sKey.wasPressedThisFrame && kb.shiftKey.isPressed && _visible) OnScan();
        }

        bool IsTextFieldFocused() => _input != null && _input.focusController?.focusedElement == _input;

        // ------------------------------------------------------------------ proactive tips

        /// <summary>Turns notable game events (a rejected part, a fault, a warped joint, a known FAIL at launch) into a tip.</summary>
        void OnGameEvent(LogEvent e)
        {
            if (Off || e.system == "HOUSTON") return;
            switch (e.code)
            {
                case "AttachRejected": ProactiveTip(e.message, "staging", e.causeId); break;
                case "FaultDetected": ProactiveTip(e.message, "eps", e.causeId); break;
                case "JointWarped": ProactiveTip("That joint warped. Loosen nothing: remove its bolts, then torque in star order over three passes.", "star_pattern", e.causeId); break;
                case "BoltStripped": ProactiveTip("A stripped bolt can't hold load. Right-click it to remove it, then fit a new one.", "torque", e.causeId); break;
                case "WorkWhileArmed": ProactiveTip("Never work on pyro wiring while ARMED. Switch Safe/Arm to SAFE first.", "safe_arm", "av_safe_arm"); break;
                case "LaunchWithKnownFailures": ProactiveTip("You're launching with known FAIL items. The debrief will show what they cost.", "staging", null); break;
            }
        }

        public void ProactiveTip(string message, string codexId = null, string objectRef = null)
        {
            if (Off || Time.unscaledTime - _lastTipTime < TipCooldown) return;
            _lastTipTime = Time.unscaledTime;
            SetVisible(true);
            AddAssistantMessage("TIP: " + message, codexId, objectRef);
        }
    }
}
