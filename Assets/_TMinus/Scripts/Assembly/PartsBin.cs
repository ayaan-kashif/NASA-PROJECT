using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TMinus.Assembly
{
    /// <summary>Left-side list of parts not yet on the rocket, filtered by category. Press an item to drag it.</summary>
    public sealed class PartsBin
    {
        public event Action<string> ItemPressed;

        readonly VisualElement _panel, _list;
        readonly Label _progress;
        readonly Dictionary<string, Button> _chips = new Dictionary<string, Button>();
        string _filter = "all";
        RocketView _view;

        static readonly (string id, string label)[] Filters =
            { ("all", "All"), ("stages", "Stages"), ("structure", "Structure"), ("payload", "Payload") };

        public PartsBin(VisualElement slot)
        {
            _panel = new VisualElement();
            _panel.AddToClassList("bin");
            slot.Add(_panel);

            var title = new Label("PARTS BIN");
            title.AddToClassList("bin-title");
            _panel.Add(title);

            _progress = new Label();
            _progress.AddToClassList("bin-progress");
            _panel.Add(_progress);

            var chips = new VisualElement();
            chips.AddToClassList("bin-chips");
            foreach (var (id, label) in Filters)
            {
                var chip = new Button(() => { _filter = id; Refresh(_view); }) { text = label };
                chip.AddToClassList("bin-chip");
                chips.Add(chip);
                _chips[id] = chip;
            }
            _panel.Add(chips);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("bin-list");
            _panel.Add(_list);

            var hint = new Label("Drag a part onto the top of the stack. Drag a part off the rocket to remove it. Ctrl+Z undoes.");
            hint.AddToClassList("bin-hint");
            _panel.Add(hint);
        }

        public void SetVisible(bool visible) => _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Refresh(RocketView view)
        {
            _view = view;
            foreach (var kv in _chips) kv.Value.EnableInClassList("on", kv.Key == _filter);
            _list.Clear();
            if (view?.Graph == null) return;

            int attached = 0, total = 0;
            foreach (var p in view.Parts)
            {
                total++;
                if (view.Graph.IsAttached(p.instanceId)) { attached++; continue; }
                if (!Matches(p)) continue;

                var item = new VisualElement();
                item.AddToClassList("bin-item");
                var name = new Label(p.instanceId == AssemblyRules.PayloadPartId ? "CubeSat payload" : p.Def?.name ?? p.instanceId);
                name.AddToClassList("bin-item-name");
                var meta = new Label($"{Group(p)}  ·  {PartStats.Mass(p.Def?.massKg ?? 0)}");
                meta.AddToClassList("bin-item-meta");
                item.Add(name);
                item.Add(meta);
                var id = p.instanceId;
                item.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) ItemPressed?.Invoke(id); });
                _list.Add(item);
            }
            _progress.text = attached == total ? $"All {total} parts mounted" : $"{attached} / {total} parts mounted";
            if (_list.childCount == 0)
            {
                var empty = new Label(attached == total ? "Everything is on the rocket." : "Nothing in this filter.");
                empty.AddToClassList("bin-empty");
                _list.Add(empty);
            }
        }

        bool Matches(Part p) => _filter == "all" || Group(p).ToLowerInvariant() == _filter;

        static string Group(Part p)
        {
            if (p.instanceId == AssemblyRules.PayloadPartId) return "Payload";
            switch (p.Def?.category)
            {
                case "rocket_stage": return "Stages";
                default: return "Structure";
            }
        }
    }
}
