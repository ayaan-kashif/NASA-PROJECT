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

        public static string Report(string _)
        {
            var cam = Camera.main;
            return $"rocket={C?.Current?.Rocket.id} parts={C?.Current?.Parts.Count} cam={cam?.transform.position} fps~{1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0}";
        }
    }
}
