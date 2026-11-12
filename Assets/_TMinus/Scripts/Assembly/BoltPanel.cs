using System;
using TMinus.Core;
using UnityEngine.UIElements;

namespace TMinus.Assembly
{
    /// <summary>Right-side panel while bolting a joint: spec, pass, wrench mode, counts, integrity.</summary>
    public sealed class BoltPanel
    {
        public event Action DoneClicked;
        public event Action<bool> PresetChanged;

        readonly VisualElement _panel, _bar, _stats;
        readonly Label _title, _spec, _pass, _order, _integrity, _hint;
        readonly Button _preset, _manual;

        public BoltPanel(VisualElement parent)
        {
            _panel = new VisualElement();
            _panel.AddToClassList("bolt-panel");
            parent.Add(_panel);

            var cat = new Label("BOLTED JOINT");
            cat.AddToClassList("info-category");
            _panel.Add(cat);
            _title = Add("info-title");
            _spec = Add("bolt-spec");
            _pass = Add("bolt-pass");
            _order = Add("bolt-order");

            var modes = new VisualElement();
            modes.AddToClassList("bolt-modes");
            _preset = new Button(() => SetPreset(true, true)) { text = "Click wrench (pass preset)" };
            _manual = new Button(() => SetPreset(false, true)) { text = "Manual" };
            _preset.AddToClassList("bin-chip"); _manual.AddToClassList("bin-chip");
            modes.Add(_preset); modes.Add(_manual);
            _panel.Add(modes);

            _stats = new VisualElement();
            _stats.AddToClassList("info-stats");
            _panel.Add(_stats);

            _integrity = Add("bolt-integrity-label");
            var track = new VisualElement();
            track.AddToClassList("bolt-bar-track");
            _bar = new VisualElement();
            _bar.AddToClassList("bolt-bar-fill");
            track.Add(_bar);
            _panel.Add(track);

            var legend = new Label("Hole = missing · Yellow, raised = finger-tight · Orange = under · Green ✓ = torqued · Red ✗ = over (stripped)");
            legend.AddToClassList("bolt-legend");
            _panel.Add(legend);
            _hint = Add("bolt-hint");
            _hint.text = "Click a hole to fit a bolt. Hold the left button on a bolt to torque it. Right-click removes a bolt. Esc to finish.";

            var done = new Button(() => DoneClicked?.Invoke()) { text = "Done" };
            done.AddToClassList("card-btn");
            done.AddToClassList("accent");
            _panel.Add(done);
            SetPreset(true, false);
            Show(false);
        }

        Label Add(string cls)
        {
            var l = new Label();
            l.AddToClassList(cls);
            _panel.Add(l);
            return l;
        }

        public void SetPreset(bool preset, bool notify)
        {
            _preset.EnableInClassList("on", preset);
            _manual.EnableInClassList("on", !preset);
            if (notify) PresetChanged?.Invoke(preset);
        }

        public void Show(bool on) => _panel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        public void Refresh(BoltJointView view, string title, bool preset)
        {
            var st = view.State;
            var def = view.Def;
            var s = st.GetStatus();
            int pass = st.CurrentPass();
            _title.text = title;
            _spec.text = $"{def.boltCount} x {def.size} at {def.torqueNm:0} N·m ±{def.tolerancePct:0}%";
            _pass.text = pass > 3 ? "All 3 passes done" :
                preset ? $"Pass {pass} of 3: wrench clicks at {st.PassTargetNm(pass):0.#} N·m ({BoltMath.PassFractions[pass - 1] * 100:0}%)"
                       : $"Pass {pass} of 3: aim for {st.PassTargetNm(pass):0.#} N·m";
            int next = st.NextBolt();
            _order.text = $"Star order: {BoltMath.StarOrderText(def.boltCount)}" + (next >= 0 ? $"   ·   next: bolt {next + 1}" : "");

            _stats.Clear();
            Row("Torqued", $"{s.torqued} / {s.boltCount}");
            if (s.inserted > 0) Row("Finger-tight", s.inserted.ToString());
            if (s.under > 0) Row("Under-torqued", s.under.ToString());
            if (s.over > 0) Row("Over-torqued (stripped)", s.over.ToString());
            if (s.missing > 0) Row("Missing", s.missing.ToString());
            if (s.sequenceErrors > 0) Row("Warped (out of sequence)", s.sequenceErrors.ToString());

            _integrity.text = $"Joint integrity {s.integrity * 100:0}%  ·  holds up to {s.FailureQkPa:0} kPa";
            _bar.style.width = Length.Percent((float)(s.integrity * 100));
            _bar.EnableInClassList("bad", s.integrity < 0.75);
        }

        void Row(string k, string v)
        {
            var row = new VisualElement(); row.AddToClassList("stat-row");
            var key = new Label(k); key.AddToClassList("stat-key");
            var val = new Label(v); val.AddToClassList("stat-value");
            row.Add(key); row.Add(val);
            _stats.Add(row);
        }
    }
}
