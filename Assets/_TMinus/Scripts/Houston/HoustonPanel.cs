using System;
using System.Collections.Generic;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.Houston
{
    /// <summary>
    /// HOUSTON overlay: A collapsible bottom-right assistant panel present across gameplay scenes.
    /// Spawns via [RuntimeInitializeOnLoadMethod] into IntegrationBay, VAB, AvionicsBay,
    /// MissionBriefing, and Debrief without modifying any scene files.
    /// Handles shortcut keys (H, N, Shift+S), proactive tips, thumbs up/down feedback,
    /// and offline/LLM responses.
    /// </summary>
    public sealed class HoustonPanel : MonoBehaviour
    {
        static HoustonPanel _instance;
        UIDocument _doc;
        VisualElement _root;
        VisualElement _panel;
        VisualElement _msgList;
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
            "IntegrationBay", "VAB", "AvionicsBay", "MissionBriefing", "Debrief", "Flight"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrySpawn();
        }

        static void TrySpawn()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (!GameplayScenes.Contains(scene))
            {
                if (_instance != null) _instance.SetVisible(false);
                return;
            }
            if (_instance != null)
            {
                _instance.SetVisible(false);
                return;
            }
            var go = new GameObject("[HOUSTON]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HoustonPanel>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _llm = gameObject.AddComponent<HoustonLLMClient>();
            BuildUI();
            HoustonBus.AskRequested += OnAskRequested;
        }

        void OnDestroy()
        {
            HoustonBus.AskRequested -= OnAskRequested;
            if (_instance == this) _instance = null;
        }

        void BuildUI()
        {
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = Resources.Load<PanelSettings>("UI/TMinusPanelSettings");
            if (_doc.panelSettings == null)
            {
                Debug.LogWarning("[HOUSTON] No PanelSettings found at Resources/UI/TMinusPanelSettings");
                return;
            }
            _doc.sortingOrder = 90;

            var style = Resources.Load<StyleSheet>("UI/Houston");
            var tree = Resources.Load<VisualTreeAsset>("UI/HoustonPanel");

            _root = _doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            if (tree != null)
            {
                tree.CloneTree(_root);
            }
            else
            {
                BuildFallbackTree(_root);
            }

            if (style != null && !_root.styleSheets.Contains(style))
                _root.styleSheets.Add(style);

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

            if (_input != null)
            {
                _input.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return && !string.IsNullOrWhiteSpace(_input.value))
                    {
                        string q = _input.value.Trim();
                        _input.value = "";
                        AskFreeQuestion(q);
                        e.StopPropagation();
                    }
                });
            }

            SetVisible(false);
        }

        void BuildFallbackTree(VisualElement root)
        {
            var r = new VisualElement { name = "houston-root" };
            r.AddToClassList("houston-root");

            var fab = new Button { name = "houston-toggle-fab", text = "🎧" };
            fab.AddToClassList("houston-toggle-fab");
            r.Add(fab);

            var p = new VisualElement { name = "houston-panel" };
            p.AddToClassList("houston-panel");

            var header = new VisualElement { name = "houston-header" };
            header.AddToClassList("houston-header");
            var title = new Label("🎧 HOUSTON");
            title.AddToClassList("houston-title");
            header.Add(title);
            var close = new Button { name = "houston-close-btn", text = "✕" };
            close.AddToClassList("houston-close-btn");
            header.Add(close);
            p.Add(header);

            var scroll = new ScrollView { name = "houston-messages-scroll" };
            scroll.AddToClassList("houston-messages-scroll");
            var msgs = new VisualElement { name = "houston-messages" };
            scroll.Add(msgs);
            p.Add(scroll);

            var typing = new Label { name = "houston-typing" };
            typing.AddToClassList("houston-typing");
            typing.AddToClassList("hidden");
            p.Add(typing);

            var btnRow = new VisualElement { name = "houston-buttons-row" };
            btnRow.AddToClassList("houston-buttons-row");
            btnRow.Add(new Button { name = "btn-next", text = "What's next? [N]" });
            btnRow.Add(new Button { name = "btn-where", text = "Where does this go?" });
            btnRow.Add(new Button { name = "btn-scan", text = "Scan build [Shift+S]" });
            btnRow.Add(new Button { name = "btn-why", text = "Why?" });
            p.Add(btnRow);

            var inRow = new VisualElement { name = "houston-input-row" };
            inRow.AddToClassList("houston-input-row");
            var inp = new TextField { name = "houston-input" };
            inp.AddToClassList("houston-input");
            inRow.Add(inp);
            p.Add(inRow);

            r.Add(p);
            root.Add(r);
        }

        public void SetVisible(bool vis)
        {
            _visible = vis;
            if (_panel != null)
            {
                if (vis) _panel.RemoveFromClassList("hidden");
                else _panel.AddToClassList("hidden");
            }
            if (_toggleFab != null)
            {
                _toggleFab.style.display = vis ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        // ------------------------------------------------------------------ keyboard shortcuts

        void Update()
        {
            if (GameState.Instance?.Settings.houstonMode == HoustonMode.Off) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.hKey.wasPressedThisFrame && !IsTextFieldFocused())
                SetVisible(!_visible);
            if (kb.nKey.wasPressedThisFrame && !IsTextFieldFocused() && _visible)
                OnNextStep();
            if (kb.sKey.wasPressedThisFrame && kb.shiftKey.isPressed && !IsTextFieldFocused() && _visible)
                OnScan();
        }

        bool IsTextFieldFocused()
        {
            return _input != null && _input.focusController?.focusedElement == _input;
        }

        // ------------------------------------------------------------------ Actions

        public void OnNextStep()
        {
            var ctx = ContextBuilder.Build(GameState.Instance);
            var rec = AssemblyAdvisor.NextStep(ctx);
            AddAssistantMessage($"▶ {rec.text}", rec.codexId, rec.objectRef);
            CountHint(HintLevel.TextNudge);
        }

        public void OnWhereDoesThisGo()
        {
            var ctx = ContextBuilder.Build(GameState.Instance);
            string held = GetHeldItem();
            if (string.IsNullOrEmpty(held))
            {
                AddAssistantMessage("Select a part or click a connector port first, then ask where it goes.");
                return;
            }
            var targets = AssemblyAdvisor.WhereDoesThisGo(held, ctx);
            if (targets.Count == 0)
            {
                AddAssistantMessage($"No valid targets currently available for {held}.");
                return;
            }
            var top = targets[0];
            AddAssistantMessage($"➜ {top.text}", top.codexId, top.objectRef);
            CountHint(HintLevel.Highlight);
        }

        public void OnScan()
        {
            var ctx = ContextBuilder.Build(GameState.Instance);
            var faults = AssemblyAdvisor.Scan(ctx);
            if (faults.Count == 0)
            {
                AddAssistantMessage("✓ Scan complete: 0 problems found. Vehicle and spacecraft are ready!");
                return;
            }

            AddAssistantMessage($"🔍 Scan found {faults.Count} problem{(faults.Count > 1 ? "s" : "")}:");
            int count = 0;
            foreach (var f in faults)
            {
                string icon = f.severity == FaultSeverity.Blocker ? "🔴" :
                              f.severity == FaultSeverity.Warning ? "🟡" : "💡";
                AddAssistantMessage($"  {icon} {f.what}", f.codexId, f.where);
                if (++count >= 8) break;
            }
            if (faults.Count > 8)
                AddAssistantMessage($"  ... and {faults.Count - 8} more minor issues.");

            CountHint(HintLevel.TextNudge);
        }

        public void OnWhy()
        {
            if (_messageLog.Count == 0)
            {
                AddAssistantMessage("Ask me for advice or scan your build first, and I will explain the engineering principles.");
                return;
            }
            AskFreeQuestion("Why is this required?");
        }

        void OnAskRequested(string partId, string context)
        {
            if (GameState.Instance?.Settings.houstonMode == HoustonMode.Off) return;
            SetVisible(true);
            var ctx = ContextBuilder.Build(GameState.Instance);

            if (!string.IsNullOrEmpty(partId))
            {
                var targets = AssemblyAdvisor.WhereDoesThisGo(partId, ctx);
                if (targets.Count > 0)
                    AddAssistantMessage($"➜ {targets[0].text}", targets[0].codexId, targets[0].objectRef);
                else
                    AskFreeQuestion($"Tell me about {partId}");
            }
            else
            {
                OnNextStep();
            }
        }

        async void AskFreeQuestion(string question)
        {
            AddUserMessage(question);
            var ctx = ContextBuilder.Build(GameState.Instance);
            var rec = AssemblyAdvisor.NextStep(ctx);

            if (_llm != null && _llm.IsEnabled)
            {
                SetTyping(true);
                string answer = await _llm.Ask(question, rec, ctx);
                SetTyping(false);
                AddAssistantMessage(answer, rec?.codexId, rec?.objectRef);
            }
            else
            {
                string answer = OfflineCodexAnswer(question, rec);
                AddAssistantMessage(answer + "  [offline]", rec?.codexId, rec?.objectRef);
            }
        }

        string OfflineCodexAnswer(string question, Recommendation rec)
        {
            var data = GameState.Instance?.Data;
            if (data != null)
            {
                string q = question.ToLowerInvariant();
                foreach (var entry in data.Codex)
                {
                    if (!string.IsNullOrEmpty(entry.title) && q.Contains(entry.title.ToLowerInvariant()))
                        return entry.shortText ?? entry.longText ?? rec?.text;
                }
                if (!string.IsNullOrEmpty(rec?.codexId))
                {
                    var ce = data.GetCodex(rec.codexId);
                    if (ce != null) return ce.shortText ?? ce.longText ?? rec.text;
                }
            }
            return rec?.text ?? "Refer to the Codex in the main menu for engineering specifications.";
        }

        // ------------------------------------------------------------------ Proactive Tips

        public void ProactiveTip(string message, string codexId = null, string objectRef = null)
        {
            if (GameState.Instance?.Settings.houstonMode == HoustonMode.Off) return;
            if (Time.time - _lastTipTime < TipCooldown) return;
            _lastTipTime = Time.time;
            SetVisible(true);
            AddAssistantMessage($"💡 Tip: {message}", codexId, objectRef);
        }

        // ------------------------------------------------------------------ UI Helpers

        void AddUserMessage(string text)
        {
            _messageLog.Add("User: " + text);
            if (_msgList == null) return;

            var item = new VisualElement();
            item.AddToClassList("houston-msg-item");
            item.style.backgroundColor = new Color(0.15f, 0.2f, 0.35f, 0.4f);

            var lbl = new Label($"You: {text}");
            lbl.AddToClassList("houston-msg-text");
            lbl.style.color = new Color(0.7f, 0.85f, 1f);
            item.Add(lbl);

            _msgList.Add(item);
            ScrollToBottom();
        }

        void AddAssistantMessage(string text, string codexId = null, string objectRef = null)
        {
            _messageLog.Add("HOUSTON: " + text);
            if (_messageLog.Count > MaxMessages) _messageLog.RemoveAt(0);

            if (_msgList == null) return;

            var item = new VisualElement();
            item.AddToClassList("houston-msg-item");

            var lbl = new Label(text);
            lbl.AddToClassList("houston-msg-text");
            item.Add(lbl);

            // Action row: Feedback & Show me
            var actRow = new VisualElement();
            actRow.AddToClassList("houston-msg-actions");

            if (!string.IsNullOrEmpty(objectRef))
            {
                var showBtn = new Button(() => OnShowMeClicked(objectRef)) { text = "Show me" };
                showBtn.AddToClassList("houston-rating-btn");
                showBtn.style.color = new Color(1, 0.478f, 0.1f);
                actRow.Add(showBtn);
            }

            var upBtn = new Button(() => LogFeedback(text, true)) { text = "👍" };
            upBtn.AddToClassList("houston-rating-btn");
            var downBtn = new Button(() => LogFeedback(text, false)) { text = "👎" };
            downBtn.AddToClassList("houston-rating-btn");

            actRow.Add(upBtn);
            actRow.Add(downBtn);
            item.Add(actRow);

            _msgList.Add(item);

            while (_msgList.childCount > MaxMessages)
                _msgList.RemoveAt(0);

            ScrollToBottom();
            GameState.Instance?.Events.Add("HOUSTON", "Message", text, codexId);
        }

        void OnShowMeClicked(string objectRef)
        {
            CountHint(HintLevel.ShowMe);
            // Camera framing if in VAB or Bay
            var vab = VabController.Instance;
            if (vab != null && vab.orbit != null && vab.Current != null)
            {
                var part = vab.Current.transform.Find(objectRef)?.GetComponent<Part>();
                if (part != null) vab.orbit.Focus(part.WorldBounds());
            }
        }

        void LogFeedback(string message, bool helpful)
        {
            GameState.Instance?.Events.Add("HOUSTON", "Feedback", helpful ? "Helpful (thumbs up)" : "Unhelpful (thumbs down)", message);
        }

        void SetTyping(bool typing)
        {
            if (_typingLabel == null) return;
            _typingLabel.text = typing ? "HOUSTON is thinking..." : "";
            if (typing) _typingLabel.RemoveFromClassList("hidden");
            else _typingLabel.AddToClassList("hidden");
        }

        void ScrollToBottom()
        {
            var scroll = _msgList?.parent as ScrollView;
            scroll?.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, float.MaxValue));
        }

        string GetHeldItem()
        {
            var vab = VabController.Instance;
            if (vab != null && vab.selector != null)
            {
                var sel = vab.selector.Selected;
                if (sel != null) return sel.partId;
            }
            return null;
        }

        void CountHint(HintLevel level)
        {
            var settings = GameState.Instance?.Settings;
            if (settings == null) return;
            int idx = (int)level;
            if (idx >= 0 && idx < settings.hintsUsed.Length)
                settings.hintsUsed[idx]++;
        }
    }
}
