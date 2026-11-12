using System.Linq;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>Static hooks for automated play-mode checks (AgentPlayScript "call" lines).</summary>
    public static class VabDebug
    {
        static VabController C => VabController.Instance;

        public static string ShowRocket(string id)
        {
            if (C == null) return "no VAB";
            C.ShowRocket(id);
            return $"{C.Current?.Rocket.name}: {C.Current?.Parts.Count} parts, height {C.Current?.Height:0.0} m";
        }

        public static string Exploded(string on)
        {
            bool v = on != "off";
            C?.SetExploded(v);
            return v.ToString();
        }

        public static string XRay(string on)
        {
            bool v = on != "off";
            C?.Current?.SetXRay(v);
            return v.ToString();
        }

        public static string Select(string partId)
        {
            var part = C?.Current?.Parts.FirstOrDefault(p => p.partId == partId);
            if (part == null) return "not found";
            C.selector.Select(part);
            return part.Def?.name;
        }

        public static string Mode(string mode)
        {
            C?.SetMode(mode);
            return C?.Mode;
        }

        /// <summary>Simulates the player dragging a part onto (lower/socket), e.g. "pf_stage2 pf_stage1/top".</summary>
        public static string Attach(string args)
        {
            var a = args.Split(' ');
            var g = C?.Current?.Graph;
            if (g == null) return "no graph";
            var mount = a[1].Split('/');
            bool ok = g.TryAttach(a[0], mount[0], mount[1], out var reason);
            C.ui?.Toast(ok ? $"{a[0]} mounted" : reason);
            return ok ? "ok" : reason;
        }

        /// <summary>Shows the drag ghost of a loose part hovering at a mount, as the player would see it.</summary>
        public static string Hover(string args)
        {
            var a = args.Split(' ');
            var v = C?.Current;
            var part = v?.Find(a[0]);
            if (part == null) return "no part";
            var mount = a[1].Split('/');
            bool ok = v.Graph.CanAttach(a[0], mount[0], mount[1], out var reason);
            v.Dragging = part;
            part.transform.position = v.SocketWorld(mount[0], mount[1], part) + (ok ? Vector3.zero : Vector3.right * 0.6f);
            part.transform.rotation = v.transform.rotation;
            part.SetOverrideMaterial(Core.MaterialLibrary.Get(ok ? Core.MaterialLibrary.GhostValid : Core.MaterialLibrary.GhostInvalid));
            var sp = Camera.main.WorldToScreenPoint(part.transform.position);
            C.ui?.ShowDragHint(ok ? "Release to mount" : reason, ok, sp);
            return ok ? "valid" : reason;
        }

        /// <summary>Real drag path: "pf_stage2 pf_interstage/top release" drags the part to that mount's screen point.</summary>
        public static string Drag(string args)
        {
            var a = args.Split(' ');
            var v = C?.Current;
            var part = v?.Find(a[0]);
            var ctrl = Object.FindAnyObjectByType<AssemblyController>();
            if (part == null || ctrl == null) return "missing part or controller";
            var mount = a[1].Split('/');
            var world = v.SocketWorld(mount[0], mount[1], part);
            var screen = (Vector2)Camera.main.WorldToScreenPoint(world);
            return ctrl.DebugDrag(part, screen, a.Length > 2 && a[2] == "release");
        }

        public static string Undo(string _) => C?.Current?.Graph?.Undo().ToString();

        public static string Report(string _)
        {
            var cam = Camera.main;
            return $"rocket={C?.Current?.Rocket.id} parts={C?.Current?.Parts.Count} cam={cam?.transform.position} fps~{1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0}";
        }
    }
}
