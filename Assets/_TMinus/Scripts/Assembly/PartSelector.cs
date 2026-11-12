using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Assembly
{
    /// <summary>Hover tints a part, click selects, double-click or F focuses, Esc deselects.</summary>
    public sealed class PartSelector : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color HoverGlow = new Color(0.18f, 0.2f, 0.24f);
        static readonly Color SelectGlow = new Color(0.55f, 0.24f, 0.04f);

        public Camera cam;
        public Func<Vector2, bool> IsPointerOverUI;
        /// <summary>When false (e.g. while dragging a part in Prompt 4), hover and click are ignored.</summary>
        public bool interactive = true;

        public event Action<Part> SelectionChanged;
        public event Action<Part> FocusRequested;

        public Part Hovered { get; private set; }
        public Part Selected { get; private set; }

        MaterialPropertyBlock _mpb;
        Vector2 _downPos;
        float _lastClickTime;
        Part _lastClickPart;

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || cam == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool overUI = IsPointerOverUI != null && IsPointerOverUI(pos);
            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            Part hit = null;
            if (interactive && !overUI && Physics.Raycast(cam.ScreenPointToRay(pos), out var info, 5000f))
                hit = info.collider.GetComponentInParent<Part>();
            SetHovered(hit);

            if (mouse.leftButton.wasPressedThisFrame) _downPos = pos;
            if (interactive && !overUI && !alt && mouse.leftButton.wasReleasedThisFrame && (pos - _downPos).sqrMagnitude < 25f)
            {
                bool dbl = hit != null && hit == _lastClickPart && Time.unscaledTime - _lastClickTime < 0.35f;
                _lastClickTime = Time.unscaledTime;
                _lastClickPart = hit;
                if (hit != null) Select(hit);
                if (dbl) FocusRequested?.Invoke(hit);
            }

            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) Select(null);
                if (kb.fKey.wasPressedThisFrame && Selected != null) FocusRequested?.Invoke(Selected);
            }
        }

        public void Select(Part p)
        {
            if (p == Selected) return;
            var old = Selected;
            Selected = p;
            Tint(old, old == Hovered ? HoverGlow : Color.black);
            Tint(Selected, SelectGlow);
            SelectionChanged?.Invoke(Selected);
        }

        void SetHovered(Part p)
        {
            if (p == Hovered) return;
            var old = Hovered;
            Hovered = p;
            if (old != Selected) Tint(old, Color.black);
            if (Hovered != null && Hovered != Selected) Tint(Hovered, HoverGlow);
        }

        void Tint(Part p, Color glow)
        {
            if (p == null) return;
            _mpb.Clear();
            _mpb.SetColor(EmissionId, glow);
            foreach (var r in p.Renderers)
                if (r != null) r.SetPropertyBlock(_mpb);
        }
    }
}
