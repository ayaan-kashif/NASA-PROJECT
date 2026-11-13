using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Assembly
{
    /// <summary>
    /// Torque wrench. Pick a joint (Parts Bin > Joints); the camera moves to its flange.
    /// Click a hole: bolt goes in finger-tight. Hold left button: torque rises (ratchet click every 10%).
    /// With the click wrench it stops at the current pass (30 / 70 / 100%); Manual can over-torque.
    /// Right-click removes a bolt. Esc finishes.
    /// </summary>
    public sealed class BoltTool : MonoBehaviour
    {
        public VabController vab;
        public OrbitCamera orbit;
        public PartSelector selector;
        public VabUI ui;

        public static bool IsActive { get; private set; }
        public BoltJointView Joint { get; private set; }
        public bool PresetWrench { get; private set; } = true;
        TorqueWrench _wrench;

        readonly Dictionary<string, BoltJointView> _views = new Dictionary<string, BoltJointView>();
        Camera _cam;
        int _holding = -1;
        float _holdTorque;
        int _lastRatchet;
        bool _presetClicked;

        void Start()
        {
            _cam = selector != null ? selector.cam : Camera.main;
            if (ui != null)
            {
                ui.JointPressed += Enter;
                ui.BoltDone += Exit;
                ui.BoltPresetChanged += p => { PresetWrench = p; RefreshUi(); };
            }
        }

        void OnDisable() => IsActive = false;

        /// <summary>Called when a rocket is (re)spawned: build bolt views for every joint and hook the detach rule.</summary>
        public void Bind(RocketView view)
        {
            Exit();
            _views.Clear();
            if (view?.Graph == null) return;
            var rules = GameState.Instance.Data.GetRules();
            foreach (var j in rules.boltJoints)
            {
                var owner = view.Parts.FirstOrDefault(p => p.partId == j.upperPartId);
                if (owner == null) continue;
                var bv = BoltJointView.Ensure(owner, j, GameState.Instance.Vehicle);
                if (bv != null) _views[j.id] = bv;
            }
            view.Graph.JointBoltsClear = id => !_views.TryGetValue(id, out var v) || !v.State.HasAnyBolt();
        }

        public IEnumerable<BoltJointView> Views => _views.Values;

        public BoltJointView View(string jointId) => _views.TryGetValue(jointId, out var v) ? v : null;

        public void Enter(string jointId)
        {
            var view = View(jointId);
            if (view == null || vab.Current?.Graph == null) return;
            var edge = vab.Current.Graph.Edges.FirstOrDefault(e => e.jointId == jointId);
            if (edge == null) { ui?.Toast("Mount both parts first, then bolt the joint."); return; }

            Joint = view;
            IsActive = true;
            if (selector != null) { selector.interactive = false; selector.Select(null); }
            ui?.HidePart();
            orbit.pitch = 32;
            orbit.Focus(view.FlangeBounds());
            RefreshUi();
            ui?.ShowBoltPanel(true);
        }

        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            if (_wrench != null) _wrench.Hide();
            _holding = -1;
            if (Joint != null) { Joint.SetHighlight(-1); Joint.SetCoach(-1); }
            Joint = null;
            if (selector != null) selector.interactive = true;
            ui?.ShowBoltPanel(false);
            ui?.HideGauge();
            ui?.HideDragHint();
            orbit?.ResetView();
            ui?.RefreshBin(vab.Current);
        }

        void Update()
        {
            if (!IsActive || Joint == null) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Exit(); return; }
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool overUI = ui != null && ui.IsPointerOverUI(pos);
            int hover = overUI ? -1 : RaycastBolt(pos);
            Joint.SetHighlight(_holding >= 0 ? _holding : hover);
            Joint.SetCoach(Joint.State.NextBolt());

            if (_holding >= 0) { HoldTorque(pos, mouse.leftButton.isPressed); return; }

            if (hover >= 0)
            {
                var s = Joint.State.StateOf(hover);
                ui?.ShowDragHint($"Bolt {hover + 1}: {Describe(s)} · {Joint.State.torque[hover]:0.#} N·m", s == BoltState.Torqued ? true : s == BoltState.OverTorqued ? false : (bool?)null, pos);
            }
            else ui?.HideDragHint();

            if (hover < 0) return;
            if (mouse.leftButton.wasPressedThisFrame) Press(hover);
            else if (mouse.rightButton.wasPressedThisFrame) Remove(hover);
        }

        int RaycastBolt(Vector2 screen)
        {
            var hits = Physics.RaycastAll(_cam.ScreenPointToRay(screen), 500f);
            float best = float.MaxValue; int idx = -1;
            foreach (var h in hits)
            {
                var bv = h.collider.GetComponent<BoltVisual>();
                if (bv == null || bv.view != Joint || h.distance >= best) continue;
                best = h.distance; idx = bv.index;
            }
            return idx;
        }

        void Press(int i)
        {
            var s = Joint.State.StateOf(i);
            if (s == BoltState.Missing) { Insert(i); return; }
            if (s == BoltState.OverTorqued)
            {
                ui?.Toast($"Bolt {i + 1} is stripped. Right-click to remove it, then fit a new one.");
                AudioFx.Play("error");
                return;
            }
            _holding = i;
            _holdTorque = (float)Joint.State.torque[i];
            _lastRatchet = Mathf.FloorToInt(_holdTorque / (float)Joint.State.Target * 10);
            _presetClicked = false;
        }

        /// <summary>Shows the wrench on bolt i at a torque fraction (screenshots / tutorials).</summary>
        public void PreviewWrench(int i, float fraction)
        {
            var hole = Joint != null ? Joint.Hole(i) : null;
            if (hole == null) return;
            if (_wrench == null) _wrench = TorqueWrench.Create(transform);
            _wrench.Show(hole, Joint.transform.position, Joint.BoltSize, fraction);
        }

        public void Insert(int i)
        {
            Joint.State.Insert(i);
            AudioFx.Play("seat");
            Log("BoltInserted", $"Bolt {i + 1} fitted finger-tight", Severity.Info);
            Changed();
        }

        public void Remove(int i)
        {
            if (Joint.State.StateOf(i) == BoltState.Missing) return;
            Joint.State.Remove(i);
            AudioFx.Play("click");
            Log("BoltRemoved", $"Bolt {i + 1} removed", Severity.Info);
            Changed();
        }

        void HoldTorque(Vector2 screen, bool stillHeld)
        {
            var st = Joint.State;
            float target = (float)st.Target;
            float preset = PresetWrench ? (float)st.PassTargetNm(Mathf.Min(st.CurrentPass(), 3)) : -1;
            if (stillHeld)
            {
                float next = _holdTorque + target / 1.1f * Time.deltaTime;
                if (preset > 0 && next >= preset)
                {
                    next = Mathf.Max(_holdTorque, preset);
                    if (!_presetClicked) { _presetClicked = true; AudioFx.Play("seat", 1f, 1.3f); }
                }
                _holdTorque = Mathf.Min(next, target * 1.5f);
                int notch = Mathf.FloorToInt(_holdTorque / target * 10);
                if (notch > _lastRatchet) { _lastRatchet = notch; AudioFx.Play("ratchet", 0.7f); }
                ui?.ShowGauge(_holdTorque, target, (float)Joint.Def.tolerancePct, preset, screen);
                var hole = Joint.Hole(_holding);
                if (hole != null)
                {
                    if (_wrench == null) _wrench = TorqueWrench.Create(transform);
                    _wrench.Show(hole, Joint.transform.position, Joint.BoltSize, _holdTorque / target);
                }
                ui?.HideDragHint();
                return;
            }
            Release(_holding, _holdTorque);
        }

        /// <summary>Apply a wrench pull to bolt i (also used by tests and the Judge Mode script).</summary>
        public void Release(int i, float nm)
        {
            _holding = -1;
            ui?.HideGauge();
            if (_wrench != null) _wrench.Hide();
            var loose = Joint.State.ApplyTorque(i, nm);
            var state = Joint.State.StateOf(i);
            if (loose.Count > 0)
            {
                var names = string.Join(" and ", loose.Select(n => (n + 1).ToString()));
                var msg = $"Joint warped! Bolt {i + 1} was fully torqued while bolt {names} {(loose.Count > 1 ? "were" : "was")} still loose. Use the star pattern in 3 passes.";
                ui?.Toast(msg, 4500);
                AudioFx.Play("error");
                Log("JointWarped", msg, Severity.Warning);
            }
            else if (state == BoltState.OverTorqued)
            {
                ui?.Toast($"Bolt {i + 1} over-torqued to {nm:0.#} N·m and stripped. Remove and replace it.", 4000);
                AudioFx.Play("error");
                Log("BoltStripped", $"Bolt {i + 1} over-torqued ({nm:0.#} N·m)", Severity.Fault);
            }
            else if (state == BoltState.Torqued) AudioFx.Play("click", 1f, 1.4f);
            Changed();
            if (Joint.State.CurrentPass() > 3 && Joint.State.GetStatus().integrity >= 0.999)
                ui?.Toast($"Joint {Joint.Def.id} fully torqued in star order. Integrity 100%.");
        }

        void Changed()
        {
            Joint.Refresh();
            Joint.Save(GameState.Instance.Vehicle);
            RefreshUi();
            ui?.RefreshBin(vab.Current);
        }

        void RefreshUi()
        {
            if (Joint == null) return;
            var rules = vab.Current?.Graph?.Rules;
            string title = rules != null ? $"{rules.Name(Joint.Def.lowerPartId)}  +  {rules.Name(Joint.Def.upperPartId)}" : Joint.Def.id;
            ui?.RefreshBoltPanel(Joint, title, PresetWrench);
        }

        void Log(string code, string message, Severity severity) =>
            GameState.Instance.Events.Add("VAB", code, $"[{Joint.Def.id}] {message}", Joint.Def.id, severity);

        static string Describe(BoltState s)
        {
            switch (s)
            {
                case BoltState.Missing: return "empty hole (click to fit)";
                case BoltState.Inserted: return "finger-tight (hold to torque)";
                case BoltState.UnderTorqued: return "under-torqued";
                case BoltState.Torqued: return "torqued ✓";
                default: return "over-torqued ✗";
            }
        }
    }
}
