using System;
using System.Collections.Generic;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Assembly
{
    /// <summary>Bolting UI: joint list in the parts bin, bolt panel, torque gauge.</summary>
    public sealed partial class VabUI
    {
        public event Action<string> JointPressed;
        public event Action BoltDone;
        public event Action<bool> BoltPresetChanged;

        /// <summary>Set by the VAB: (jointId, label, status, both parts mounted) for every joint.</summary>
        public Func<IEnumerable<(string id, string label, JointStatus status, bool mounted)>> JointsProvider;

        BoltPanel _boltPanel;
        TorqueGauge _gauge;

        void InitBoltUi()
        {
            var host = new VisualElement { pickingMode = PickingMode.Ignore };
            host.AddToClassList("bolt-panel-host");
            _root.Add(host);
            _boltPanel = new BoltPanel(host);
            _boltPanel.DoneClicked += () => BoltDone?.Invoke();
            _boltPanel.PresetChanged += p => BoltPresetChanged?.Invoke(p);

            _gauge = new TorqueGauge();
            _gauge.style.display = DisplayStyle.None;
            _root.Add(_gauge);

            if (Bin != null) Bin.JointPressed += id => JointPressed?.Invoke(id);
        }

        public void ShowBoltPanel(bool on)
        {
            _boltPanel?.Show(on);
            if (on) _card?.AddToClassList("hidden");
        }

        public void RefreshBoltPanel(BoltJointView view, string title, bool preset) => _boltPanel?.Refresh(view, title, preset);

        public void ShowGauge(float value, float target, float tolPct, float preset, Vector2 screen)
        {
            if (_gauge == null || _root.panel == null) return;
            _gauge.Set(value, target, tolPct, preset);
            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            _gauge.style.left = p.x + 30;
            _gauge.style.top = p.y - 75;
            _gauge.style.display = DisplayStyle.Flex;
        }

        public void HideGauge()
        {
            if (_gauge != null) _gauge.style.display = DisplayStyle.None;
        }

        IEnumerable<(string id, string label, JointStatus status, bool mounted)> Joints() =>
            JointsProvider != null ? JointsProvider() : Array.Empty<(string, string, JointStatus, bool)>();
    }
}
