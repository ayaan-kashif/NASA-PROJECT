using System;
using System.Collections.Generic;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Assembly
{
    /// <summary>VAB panels (UI/UXML/VAB.uxml): toolbar, info card, rocket library, codex modal, labels.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class VabUI : MonoBehaviour
    {
        [SerializeField] StyleSheet style;

        public event Action<string> RocketChosen;
        public event Action<string> ModeChanged;      // "assemble" | "inspect"
        public event Action<bool> ExplodedChanged;
        public event Action<bool> XRayChanged;
        public event Action ResetCamera;
        public event Action InfoClosed;

        VisualElement _root, _card, _stats, _labels, _modal, _library;
        Label _title, _category, _badge, _codex, _toast;
        Button _assemble, _inspect, _exploded, _xray;
        readonly Dictionary<string, VisualElement> _rocketCards = new Dictionary<string, VisualElement>();
        readonly Dictionary<Part, Label> _partLabels = new Dictionary<Part, Label>();
        IVisualElementScheduledItem _toastHide;
        string _shownPartId;
        bool _explodedOn, _xrayOn;

        public VisualElement PartsBinSlot { get; private set; }
        public PartsBin Bin { get; private set; }
        public event Action<string> BinItemPressed;
        Label _dragHint;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore;
            if (style != null && !_root.styleSheets.Contains(style)) _root.styleSheets.Add(style);

            _card = _root.Q("info-card");
            _stats = _root.Q("info-stats");
            _labels = _root.Q("labels-layer");
            _modal = _root.Q("codex-modal");
            _library = _root.Q<ScrollView>("library-scroll");
            PartsBinSlot = _root.Q("parts-bin-slot");
            _title = _root.Q<Label>("info-title");
            _category = _root.Q<Label>("info-category");
            _badge = _root.Q<Label>("info-badge");
            _codex = _root.Q<Label>("info-codex");
            _toast = _root.Q<Label>("toast");

            _assemble = Bind("btn-mode-assemble", () => SetMode("assemble"));
            _inspect = Bind("btn-mode-inspect", () => SetMode("inspect"));
            _exploded = Bind("btn-exploded", () => { _explodedOn = !_explodedOn; _exploded.EnableInClassList("on", _explodedOn); ExplodedChanged?.Invoke(_explodedOn); });
            _xray = Bind("btn-xray", () => { _xrayOn = !_xrayOn; _xray.EnableInClassList("on", _xrayOn); XRayChanged?.Invoke(_xrayOn); });
            Bind("btn-reset", () => ResetCamera?.Invoke());
            Bind("btn-menu", () => SceneFlow.Load(SceneNames.MainMenu));
            // The pad checklist and the GO/NO-GO poll live in the briefing scene: go there, never straight to Flight.
            Bind("btn-next", () => { DemoHooks.PendingBriefingPanel = "checklist"; SceneFlow.Load(SceneNames.MissionBriefing); });
            Bind("btn-avionics", () => SceneFlow.Load(SceneNames.AvionicsBay));
            Bind("btn-info-close", () => { HidePart(); InfoClosed?.Invoke(); });
            Bind("btn-learn", () => OpenCodex(GameState.Instance?.Data.GetPart(_shownPartId)?.codexId));
            Bind("btn-ask", () => HoustonBus.Ask(_shownPartId, "VAB"));
            Bind("btn-codex-close", () => _modal.AddToClassList("hidden"));
            SetMode("inspect", notify: false);

            if (PartsBinSlot != null)
            {
                Bin = new PartsBin(PartsBinSlot);
                Bin.ItemPressed += id => BinItemPressed?.Invoke(id);
                Bin.SetVisible(false);
            }
            _dragHint = new Label { pickingMode = PickingMode.Ignore };
            _dragHint.AddToClassList("drag-hint");
            _dragHint.style.display = DisplayStyle.None;
            _root.Add(_dragHint);
            InitBoltUi();
        }

        public void RefreshBin(RocketView view) => Bin?.Refresh(view, Joints());

        public void SetBinVisible(bool visible) => Bin?.SetVisible(visible);

        /// <summary>Tooltip next to the cursor while dragging. valid: true = green, false = red, null = neutral.</summary>
        public void ShowDragHint(string text, bool? valid, Vector2 screenPos)
        {
            if (_dragHint == null || _root.panel == null) return;
            _dragHint.text = text;
            _dragHint.EnableInClassList("valid", valid == true);
            _dragHint.EnableInClassList("invalid", valid == false);
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            _dragHint.style.left = p.x + 22;
            _dragHint.style.top = p.y + 18;
            _dragHint.style.display = DisplayStyle.Flex;
        }

        public void HideDragHint()
        {
            if (_dragHint != null) _dragHint.style.display = DisplayStyle.None;
        }

        Button Bind(string name, Action onClick)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += onClick;
            else Debug.LogWarning($"[VabUI] Missing button {name}");
            return b;
        }

        public void SetMode(string mode, bool notify = true)
        {
            _assemble?.EnableInClassList("on", mode == "assemble");
            _inspect?.EnableInClassList("on", mode == "inspect");
            if (notify) ModeChanged?.Invoke(mode);
        }

        public void SetToggles(bool exploded, bool xray)
        {
            _explodedOn = exploded; _xrayOn = xray;
            _exploded?.EnableInClassList("on", exploded);
            _xray?.EnableInClassList("on", xray);
        }

        // ------------------------------------------------------------------ library

        public void BuildLibrary(IEnumerable<RocketDef> rockets, string selectedId)
        {
            if (_library == null) return;
            _library.Clear();
            _rocketCards.Clear();
            foreach (var r in rockets)
            {
                var card = new VisualElement();
                card.AddToClassList("rocket-card");
                var top = new VisualElement(); top.AddToClassList("rocket-card-top");
                var name = new Label(r.name); name.AddToClassList("rocket-name");
                var badge = new Label(r.real ? "REAL" : "TRAINING"); badge.AddToClassList("badge");
                if (!r.real) badge.AddToClassList("training");
                top.Add(name); top.Add(badge);
                card.Add(top);
                card.Add(Stat($"Height  {r.heightM:0.#} m"));
                card.Add(Stat($"Liftoff mass  {PartStats.Mass(r.LiftoffMassKg(r.referencePayloadKg))}"));
                card.Add(Stat($"Payload to LEO  {PartStats.Mass(r.payloadLeoKg)}"));
                var id = r.id;
                card.RegisterCallback<ClickEvent>(_ => RocketChosen?.Invoke(id));
                _library.Add(card);
                _rocketCards[id] = card;
            }
            SetSelectedRocket(selectedId);
        }

        static Label Stat(string text)
        {
            var l = new Label(text);
            l.AddToClassList("rocket-stat");
            return l;
        }

        public void SetSelectedRocket(string id)
        {
            foreach (var kv in _rocketCards) kv.Value.EnableInClassList("selected", kv.Key == id);
        }

        // ------------------------------------------------------------------ info card

        public void ShowPart(Part part, DataRegistry data)
        {
            if (part == null || _card == null) { HidePart(); return; }
            var def = part.Def;
            _shownPartId = part.partId;
            _title.text = def?.name ?? part.partId;
            _category.text = PartStats.CategoryLabel(def);

            bool real = def != null && (def.assembly == "saturn_v") && !def.illustrative;
            bool training = def != null && def.illustrative;
            _badge.text = real ? "REAL DATA" : training ? "GAME VALUE" : "";
            _badge.EnableInClassList("training", training);
            _badge.EnableInClassList("empty", !real && !training);

            _stats.Clear();
            foreach (var (k, v) in PartStats.Build(def, data))
            {
                var row = new VisualElement(); row.AddToClassList("stat-row");
                var key = new Label(k); key.AddToClassList("stat-key");
                var val = new Label(v); val.AddToClassList("stat-value");
                row.Add(key); row.Add(val);
                _stats.Add(row);
            }

            var codex = def != null ? data?.GetCodex(def.codexId) : null;
            _codex.text = codex != null ? $"{codex.title}: {codex.shortText}" : def?.description ?? "";
            _card.RemoveFromClassList("hidden");
        }

        public void HidePart()
        {
            _shownPartId = null;
            _card?.AddToClassList("hidden");
        }

        public void OpenCodex(string codexId)
        {
            var entry = GameState.Instance?.Data.GetCodex(codexId);
            if (entry == null) { Toast("No Space Guide entry for this part yet."); return; }
            _root.Q<Label>("codex-title").text = entry.title;
            _root.Q<Label>("codex-body").text = entry.longText;
            _root.Q<Label>("codex-source").text = "Source: " + entry.sourceUrl + (entry.needsReview ? "   (being fact-checked)" : "");
            _modal.RemoveFromClassList("hidden");
        }

        public void Toast(string message, int ms = 2500)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.RemoveFromClassList("hidden");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("hidden")).StartingIn(ms);
        }

        // ------------------------------------------------------------------ labels

        /// <summary>Exploded-view name tags that follow each part on screen.</summary>
        public void UpdateLabels(Camera cam, RocketView rocket, bool visible)
        {
            if (_labels == null) return;
            if (!visible || rocket == null || cam == null)
            {
                foreach (var l in _partLabels.Values) l.style.display = DisplayStyle.None;
                return;
            }
            foreach (var p in rocket.Parts)
            {
                if (p == null) continue;
                if (!_partLabels.TryGetValue(p, out var label))
                {
                    label = new Label(p.Def?.name ?? p.partId) { pickingMode = PickingMode.Ignore };
                    label.AddToClassList("part-label");
                    _labels.Add(label);
                    _partLabels[p] = label;
                }
                var sp = cam.WorldToScreenPoint(rocket.LabelAnchor(p));
                if (sp.z <= 0) { label.style.display = DisplayStyle.None; continue; }
                var panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
                label.style.display = DisplayStyle.Flex;
                label.style.left = panelPos.x + 24;
                label.style.top = panelPos.y - 12;
            }
        }

        public void ClearLabels()
        {
            foreach (var l in _partLabels.Values) l.RemoveFromHierarchy();
            _partLabels.Clear();
        }

        /// <summary>True when the mouse is over any UI panel (so the 3D view should ignore it).</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            if (_root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = _root.panel.Pick(p);
            return picked != null && picked != _root;
        }
    }
}
