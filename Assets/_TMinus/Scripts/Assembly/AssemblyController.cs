using TMinus.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Assembly
{
    /// <summary>
    /// Assemble mode: drag parts from the floor or the parts bin onto the rocket. A translucent ghost
    /// turns green over a valid mount and red (with the reason) over a wrong one. Dragging a part off
    /// the rocket detaches it if nothing depends on it. Ctrl+Z / Ctrl+Y undo and redo.
    /// </summary>
    public sealed class AssemblyController : MonoBehaviour
    {
        public VabController vab;
        public PartSelector selector;
        public VabUI ui;

        const float DragStartPixels = 6f;

        Camera _cam;
        Part _pressed, _drag;
        Vector2 _pressPos;
        string _snapLower, _snapSocket, _snapReason;
        bool _snapValid;

        RocketView View => vab != null ? vab.Current : null;
        public bool Active => vab != null && vab.Mode == "assemble" && View != null && View.Graph != null && !BoltTool.IsActive;
        public bool IsDragging => _drag != null;

        void Start()
        {
            _cam = selector != null ? selector.cam : Camera.main;
            if (ui != null) ui.BinItemPressed += id => BeginDrag(View?.Find(id));
        }

        void Update()
        {
            if (!Active) { if (_drag != null) CancelDrag(); return; }
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;

            if (kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && _drag == null)
            {
                if (kb.zKey.wasPressedThisFrame) { if (View.Graph.Undo()) AudioFx.Play("click"); }
                if (kb.yKey.wasPressedThisFrame) { if (View.Graph.Redo()) AudioFx.Play("click"); }
            }

            Vector2 pos = mouse.position.ReadValue();
            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            if (_drag == null)
            {
                if (mouse.leftButton.wasPressedThisFrame && !alt && !(ui != null && ui.IsPointerOverUI(pos)))
                {
                    _pressed = RaycastPart(pos);
                    _pressPos = pos;
                }
                if (_pressed != null && mouse.leftButton.isPressed && (pos - _pressPos).magnitude > DragStartPixels)
                {
                    BeginDrag(_pressed);
                    _pressed = null;
                }
                if (mouse.leftButton.wasReleasedThisFrame) _pressed = null;
                return;
            }

            UpdateDrag(pos);
            if (!mouse.leftButton.isPressed) EndDrag();
        }

        Part RaycastPart(Vector2 screen)
        {
            if (_cam == null || !Physics.Raycast(_cam.ScreenPointToRay(screen), out var hit, 5000f)) return null;
            var p = hit.collider.GetComponentInParent<Part>();
            return p != null && View.Find(p.instanceId) == p ? p : null;
        }

        public void BeginDrag(Part p)
        {
            if (p == null || !Active || _drag != null) return;
            var g = View.Graph;
            if (g.IsAttached(p.instanceId))
            {
                if (!g.TryDetach(p.instanceId, out var reason))
                {
                    ui?.Toast(reason);
                    AudioFx.Play("error");
                    return;
                }
                GameState.Instance.Events.Add("VAB", "PartDetached", $"{p.Def?.name} removed", p.instanceId);
            }
            _drag = p;
            View.Dragging = p;
            p.transform.localRotation = Quaternion.identity;
            p.SetOverrideMaterial(MaterialLibrary.Get(MaterialLibrary.XRay));
            if (selector != null) { selector.interactive = false; selector.Select(null); }
            AudioFx.Play("click", 0.6f);
        }

        void UpdateDrag(Vector2 screen)
        {
            // Drag on a vertical plane through the rocket's axis, facing the camera.
            var axis = View.transform.position;
            var normal = _cam.transform.forward; normal.y = 0;
            if (normal.sqrMagnitude < 1e-4f) normal = Vector3.forward;
            var plane = new Plane(-normal.normalized, axis);
            var ray = _cam.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float enter)) return;
            var hit = ray.GetPoint(enter);
            hit.y = Mathf.Max(hit.y, 0f);

            // Nearest free mount on the rocket within reach.
            float best = Mathf.Max(0.8f, View.Height * 0.12f);
            _snapLower = null; _snapSocket = null; _snapReason = null; _snapValid = false;
            foreach (var (lower, socket) in View.Graph.FreeSockets())
            {
                var p = View.SocketWorld(lower, socket, _drag);
                float d = Vector3.Distance(p, hit);
                if (d >= best) continue;
                best = d;
                _snapLower = lower; _snapSocket = socket;
            }

            Material ghost;
            if (_snapLower != null)
            {
                _snapValid = View.Graph.CanAttach(_drag.instanceId, _snapLower, _snapSocket, out _snapReason);
                if (_snapValid)
                {
                    _drag.transform.position = View.SocketWorld(_snapLower, _snapSocket, _drag);
                    ghost = MaterialLibrary.Get(MaterialLibrary.GhostValid);
                    ui?.ShowDragHint($"Release to mount on {View.Graph.Rules.Name(View.Graph.PartIdOf(_snapLower))}", true, screen);
                }
                else
                {
                    _drag.transform.position = hit;
                    ghost = MaterialLibrary.Get(MaterialLibrary.GhostInvalid);
                    ui?.ShowDragHint(_snapReason, false, screen);
                }
            }
            else
            {
                _drag.transform.position = hit;
                ghost = MaterialLibrary.Get(MaterialLibrary.XRay);
                ui?.ShowDragHint("Bring it to the top of the stack", null, screen);
            }
            _drag.transform.rotation = View.transform.rotation;
            _drag.SetOverrideMaterial(ghost);
        }

        void EndDrag()
        {
            var p = _drag;
            bool attached = false;
            if (_snapLower != null && _snapValid)
            {
                attached = View.Graph.TryAttach(p.instanceId, _snapLower, _snapSocket, out _snapReason);
            }
            Finish(p);

            if (attached)
            {
                AudioFx.Play("snap");
                p.Flash(new Color(0.2f, 1f, 0.4f));
                var edge = View.Graph.EdgeBelow(p.instanceId);
                GameState.Instance.Events.Add("VAB", "PartAttached", $"{p.Def?.name} mounted", edge?.jointId ?? p.instanceId);
                if (View.Graph.IsComplete()) ui?.Toast("Stack complete! Next: bolt every joint.");
            }
            else if (_snapLower != null && !_snapValid)
            {
                AudioFx.Play("error");
                ui?.Toast(_snapReason);
                GameState.Instance.Events.Add("VAB", "AttachRejected", _snapReason, p.instanceId, Severity.Tip);
            }
        }

        /// <summary>Test hook: runs the real drag path (begin, move to a screen point, optionally release).</summary>
        public string DebugDrag(Part p, Vector2 screen, bool release)
        {
            if (_drag == null) BeginDrag(p);
            if (_drag == null) return "could not start drag";
            UpdateDrag(screen);
            string result = _snapLower == null ? "no mount in reach" : _snapValid ? "valid " + _snapLower + "/" + _snapSocket : _snapReason;
            if (release) EndDrag();
            return result;
        }

        void CancelDrag()
        {
            if (_drag != null) Finish(_drag);
        }

        void Finish(Part p)
        {
            p.ClearOverrideMaterial();
            if (View != null)
            {
                View.Dragging = null;
                View.Relayout();
                View.SaveTo(GameState.Instance.Vehicle);
            }
            _drag = null;
            if (selector != null) selector.interactive = true;
            ui?.HideDragHint();
            ui?.RefreshBin(View);
        }
    }
}
