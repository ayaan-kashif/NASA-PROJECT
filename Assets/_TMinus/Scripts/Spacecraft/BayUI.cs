using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Spacecraft
{
    /// <summary>Integration Bay panels: toolbar, CubeSat parts bin, wiring check, labels, hints.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BayUI : MonoBehaviour
    {
        [SerializeField] StyleSheet baseStyle;
        [SerializeField] StyleSheet style;

        public event Action<string> ModeChanged;
        public event Action<bool> CutawayChanged;
        public event Action<bool> MeterChanged;
        public event Action ResetCamera;
        public event Action<string> InstallPressed;
        public event Action<string> RemovePressed;
        public event Action<WiringFault> FaultPressed;

        VisualElement _root, _binList, _faultList, _faults, _labels;
        Label _progress, _faultCount, _toast, _hint;
        Button _build, _wire, _cutaway, _meter;
        bool _cutOn, _meterOn;
        IVisualElementScheduledItem _toastHide;
        readonly Dictionary<string, Label> _tileLabels = new Dictionary<string, Label>();

        public VisualElement BudgetsSlot { get; private set; }

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore;
            foreach (var s in new[] { baseStyle, style })
                if (s != null && !_root.styleSheets.Contains(s)) _root.styleSheets.Add(s);

            _binList = _root.Q("bin-list");
            _faultList = _root.Q("fault-list");
            _faults = _root.Q("faults");
            _labels = _root.Q("labels-layer");
            BudgetsSlot = _root.Q("budgets-slot");
            _progress = _root.Q<Label>("bin-progress");
            _faultCount = _root.Q<Label>("fault-count");
            _toast = _root.Q<Label>("toast");

            _build = Bind("btn-build", () => SetMode("build", true));
            _wire = Bind("btn-wire", () => SetMode("wire", true));
            _cutaway = Bind("btn-cutaway", () => { _cutOn = !_cutOn; _cutaway.EnableInClassList("on", _cutOn); CutawayChanged?.Invoke(_cutOn); });
            _meter = Bind("btn-meter", () => { _meterOn = !_meterOn; _meter.EnableInClassList("on", _meterOn); MeterChanged?.Invoke(_meterOn); });
            Bind("btn-reset", () => ResetCamera?.Invoke());
            Bind("btn-menu", () => SceneFlow.Load(SceneNames.MainMenu));
            Bind("btn-next", () => SceneFlow.Load(SceneNames.VAB));

            _hint = new Label { pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("drag-hint");
            _hint.style.display = DisplayStyle.None;
            _root.Add(_hint);
        }

        Button Bind(string name, Action a)
        {
            var b = _root.Q<Button>(name);
            if (b != null) b.clicked += a;
            return b;
        }

        public void SetMode(string mode, bool notify)
        {
            _build?.EnableInClassList("on", mode == "build");
            _wire?.EnableInClassList("on", mode == "wire");
            if (notify) ModeChanged?.Invoke(mode);
        }

        public void SetMeter(bool on) { _meterOn = on; _meter?.EnableInClassList("on", on); }

        // ------------------------------------------------------------------ bin

        static readonly (string title, string[] cats)[] Groups =
        {
            ("POWER", new[] { "eps", "battery", "solar", "switch" }),
            ("COMPUTER + RADIO", new[] { "obc", "radio", "antenna" }),
            ("POINTING", new[] { "adcs" }),
            ("SCIENCE PAYLOAD", new[] { "camera" }),
        };

        /// <summary>parts: every CubeSat part; installed: partId -> instance ids; canInstall: partId -> reason it can't (null = ok).</summary>
        public void RefreshBin(IEnumerable<PartDef> parts, Func<string, List<string>> installed, Func<string, string> blockedReason)
        {
            if (_binList == null) return;
            _binList.Clear();
            var list = parts.ToList();
            int count = 0;
            foreach (var (title, cats) in Groups)
            {
                var header = new Label(title);
                header.AddToClassList("bin-group");
                _binList.Add(header);
                foreach (var def in list.Where(p => cats.Contains(p.category)))
                {
                    var inst = installed(def.id);
                    count += inst.Count;
                    var item = new VisualElement();
                    item.AddToClassList("bin-item");
                    item.EnableInClassList("installed", inst.Count > 0);
                    var blocked = blockedReason(def.id);
                    item.EnableInClassList("blocked", inst.Count == 0 && blocked != null);

                    var row = new VisualElement(); row.AddToClassList("bin-row");
                    var name = new Label(def.name); name.AddToClassList("bin-item-name");
                    name.style.flexShrink = 1;
                    row.Add(name);
                    foreach (var id in inst)
                    {
                        var iid = id;
                        var rm = new Button(() => RemovePressed?.Invoke(iid)) { text = "Remove" };
                        rm.AddToClassList("remove-btn");
                        row.Add(rm);
                    }
                    item.Add(row);
                    var meta = new Label($"{def.massKg * 1000:0} g  ·  {def.costK:0} k$  ·  {Power(def)}{(inst.Count > 0 ? "  ·  installed ✓" : blocked != null ? "  ·  " + blocked : "")}");
                    meta.AddToClassList("bin-item-meta");
                    meta.style.whiteSpace = WhiteSpace.Normal;
                    item.Add(meta);
                    var pid = def.id;
                    item.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) InstallPressed?.Invoke(pid); });
                    _binList.Add(item);
                }
            }
            if (_progress != null) _progress.text = $"{count} parts installed";
        }

        static string Power(PartDef d) =>
            d.powerActiveW > 0 ? $"{d.powerActiveW:0.#} W active" : d.powerIdleW > 0 ? $"{d.powerIdleW:0.##} W" :
            d.Spec("peakW") > 0 ? $"{d.Spec("peakW"):0} W peak" : d.Spec("capacityWh") > 0 ? $"{d.Spec("capacityWh"):0} Wh" : "no power";

        // ------------------------------------------------------------------ faults

        public void ShowFaults(List<WiringFault> faults, int deviceCount)
        {
            if (_faultList == null) return;
            _faultList.Clear();
            int blockers = faults.Count(f => f.severity >= Severity.Blocker);
            _faultCount.text = faults.Count == 0 ? (deviceCount > 0 ? "0 faults ✓" : "") : $"{faults.Count} issue{(faults.Count > 1 ? "s" : "")} ({blockers} blocking)";
            _faults.EnableInClassList("clean", faults.Count == 0 && deviceCount > 0);
            if (faults.Count == 0)
            {
                var ok = new Label(deviceCount > 0 ? "Every device is wired correctly." : "Install parts to start.");
                ok.AddToClassList("fault-ok");
                _faultList.Add(ok);
                return;
            }
            foreach (var f in faults)
            {
                var item = new VisualElement();
                item.AddToClassList("fault");
                item.AddToClassList(f.severity >= Severity.Blocker ? "blocker" : f.severity == Severity.Fault ? "fault-sev" : "warning");
                var tag = new Label((f.severity >= Severity.Blocker ? "✗ BLOCKER" : f.severity == Severity.Fault ? "! FAULT" : "△ WARNING") + "  ·  click to show");
                tag.AddToClassList("fault-tag");
                var msg = new Label(f.message);
                msg.AddToClassList("fault-msg");
                item.Add(tag); item.Add(msg);
                var ff = f;
                item.RegisterCallback<ClickEvent>(_ => FaultPressed?.Invoke(ff));
                _faultList.Add(item);
            }
        }

        // ------------------------------------------------------------------ hints, toast, labels

        public void Toast(string message, int ms = 3000)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.RemoveFromClassList("hidden");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("hidden")).StartingIn(ms);
        }

        public void ShowHint(string text, bool? valid, Vector2 screen)
        {
            if (_hint == null || _root.panel == null) return;
            _hint.text = text;
            _hint.EnableInClassList("valid", valid == true);
            _hint.EnableInClassList("invalid", valid == false);
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            _hint.style.left = p.x + 22; _hint.style.top = p.y + 18;
            _hint.style.display = DisplayStyle.Flex;
        }

        public void HideHint() { if (_hint != null) _hint.style.display = DisplayStyle.None; }

        public void UpdateTileLabels(Camera cam, IEnumerable<BenchTile> tiles, bool visible)
        {
            if (_labels == null) return;
            var seen = new HashSet<string>();
            if (visible && cam != null)
                foreach (var t in tiles)
                {
                    seen.Add(t.instanceId);
                    if (!_tileLabels.TryGetValue(t.instanceId, out var l))
                    {
                        l = new Label(ShortName(t.def.name)) { pickingMode = PickingMode.Ignore };
                        l.AddToClassList("tile-label");
                        l.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
                        _labels.Add(l);
                        _tileLabels[t.instanceId] = l;
                    }
                    var sp = cam.WorldToScreenPoint(t.LabelAnchor);
                    var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(sp.x, Screen.height - sp.y));
                    l.style.left = p.x; l.style.top = p.y;
                    l.style.display = sp.z > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            foreach (var kv in _tileLabels.Where(kv => !seen.Contains(kv.Key)).ToList())
            {
                kv.Value.RemoveFromHierarchy();
                _tileLabels.Remove(kv.Key);
            }
        }

        /// <summary>"ADCS: magnetorquers + sun sensors" -> "ADCS"; "Wide camera (100 m/pixel)" -> "Wide camera".</summary>
        public static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int cut = name.Length;
            foreach (var sep in new[] { ":", " (", " board" })
            {
                int i = name.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 0 && i < cut) cut = i;
            }
            return name.Substring(0, cut);
        }

        public bool IsPointerOverUI(Vector2 screen)
        {
            if (_root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = _root.panel.Pick(p);
            return picked != null && picked != _root;
        }
    }
}
