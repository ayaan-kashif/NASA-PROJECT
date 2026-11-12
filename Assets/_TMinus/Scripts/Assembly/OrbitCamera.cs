using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Assembly
{
    /// <summary>
    /// Right-drag orbits, middle-drag pans, scroll zooms, Alt + left-drag orbits (touchpads).
    /// Smoothly damped. Frame() fits a bounds in view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Vector3 target;
        public float yaw = 30, pitch = 12, distance = 30;
        public float minDistance = 1, maxDistance = 400;
        public float damping = 10;

        /// <summary>Set by the scene so the camera ignores input over UI panels.</summary>
        public Func<Vector2, bool> IsPointerOverUI;

        Vector3 _target;
        float _yaw, _pitch, _distance;
        Vector3 _homeTarget;
        float _homeDistance, _homeYaw = 30, _homePitch = 6;
        Camera _cam;
        bool _dragging;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            Snap();
        }

        void Snap()
        {
            _target = target; _yaw = yaw; _pitch = pitch; _distance = distance;
            Apply();
        }

        /// <summary>Fit bounds into view and remember it as the Reset position.</summary>
        public void Frame(Bounds b, bool instant = false, bool setHome = true)
        {
            float radius = Mathf.Max(0.3f, b.extents.magnitude);
            float fov = (_cam != null ? _cam.fieldOfView : 60f) * Mathf.Deg2Rad * 0.5f;
            // Aim a little low so the object sits above the bottom panels.
            target = b.center - Vector3.up * b.size.y * 0.07f;
            distance = radius / Mathf.Sin(fov) * 1.3f;
            minDistance = radius * 0.15f;
            maxDistance = radius * 8f;
            if (setHome)
            {
                _homeTarget = target; _homeDistance = distance;
                yaw = _homeYaw; pitch = _homePitch;
            }
            if (instant) Snap();
        }

        public void Focus(Bounds b) => Frame(b, false, false);

        public void ResetView()
        {
            target = _homeTarget; distance = _homeDistance; yaw = _homeYaw; pitch = _homePitch;
        }

        void Update()
        {
            ReadInput();
            float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * damping);
            _target = Vector3.Lerp(_target, target, k);
            _yaw = Mathf.LerpAngle(_yaw, yaw, k);
            _pitch = Mathf.Lerp(_pitch, pitch, k);
            _distance = Mathf.Lerp(_distance, distance, k);
            Apply();
        }

        void ReadInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var kb = Keyboard.current;
            Vector2 pos = mouse.position.ReadValue();
            bool overUI = IsPointerOverUI != null && IsPointerOverUI(pos);

            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
            bool orbitPressed = mouse.rightButton.wasPressedThisFrame || (alt && mouse.leftButton.wasPressedThisFrame);
            bool panPressed = mouse.middleButton.wasPressedThisFrame;
            if ((orbitPressed || panPressed) && !overUI) _dragging = true;
            if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed && !mouse.leftButton.isPressed) _dragging = false;

            Vector2 delta = mouse.delta.ReadValue();
            if (_dragging)
            {
                if (mouse.rightButton.isPressed || (alt && mouse.leftButton.isPressed))
                {
                    yaw += delta.x * 0.25f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -10f, 85f);
                }
                else if (mouse.middleButton.isPressed)
                {
                    float scale = distance * 0.0016f;
                    target -= (transform.right * delta.x + transform.up * delta.y) * scale;
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (!overUI && Mathf.Abs(scroll) > 0.01f)
            {
                float notches = scroll / 120f;
                if (Mathf.Abs(notches) < 0.05f) notches = Mathf.Sign(scroll); // some devices report +-1
                distance = Mathf.Clamp(distance * Mathf.Pow(0.88f, notches), minDistance, maxDistance);
            }
        }

        void Apply()
        {
            var rot = Quaternion.Euler(_pitch, _yaw, 0);
            transform.SetPositionAndRotation(_target - rot * Vector3.forward * _distance, rot);
            if (_cam != null)
            {
                _cam.nearClipPlane = Mathf.Clamp(_distance * 0.01f, 0.01f, 1f);
                _cam.farClipPlane = Mathf.Max(1000f, _distance * 20f);
            }
        }
    }
}
