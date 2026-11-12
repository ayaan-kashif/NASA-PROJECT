using System.Collections;
using System.Collections.Generic;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Spacecraft
{
    /// <summary>
    /// The 3U frame (10 x 10 x 34 cm, 4 corner rails) with named mounts:
    /// U1A..U3B (two boards per U), +X -X +Z -Z (long faces), TOP / BOTTOM (end faces),
    /// RAIL (deployment switch at a rail foot) and PIN (Remove-Before-Flight pin).
    /// Installed parts get simple placeholder shapes and slide into place.
    /// </summary>
    public sealed class CubeSatModel : MonoBehaviour
    {
        public const float W = 0.1f, H = 0.34f;
        public static readonly string[] Slots = { "U1A", "U1B", "U2A", "U2B", "U3A", "U3B" };
        public static readonly string[] LongFaces = { "-Z", "+X", "-X", "+Z" };
        public static readonly string[] EndFaces = { "TOP", "BOTTOM" };

        readonly Dictionary<string, Transform> _mounts = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> _installed = new Dictionary<string, GameObject>();
        public Transform PinFlag { get; private set; }

        public static CubeSatModel Build(Transform parent)
        {
            var go = new GameObject("CubeSat_3U");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<CubeSatModel>();
            m.BuildFrame();
            return m;
        }

        void BuildFrame()
        {
            float h = W / 2 - 0.004f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * h, z = (i < 2 ? -1 : 1) * h;
                Box("Rail_" + i, new Vector3(0.0085f, H, 0.0085f), new Vector3(x, H / 2, z), M.MetalGrey, transform);
            }
            foreach (float y in new[] { 0.004f, H - 0.004f, H / 3, 2 * H / 3 })
            {
                Box("Ring_x1", new Vector3(W, 0.006f, 0.006f), new Vector3(0, y, -h), M.MetalGrey, transform);
                Box("Ring_x2", new Vector3(W, 0.006f, 0.006f), new Vector3(0, y, h), M.MetalGrey, transform);
                Box("Ring_z1", new Vector3(0.006f, 0.006f, W), new Vector3(-h, y, 0), M.MetalGrey, transform);
                Box("Ring_z2", new Vector3(0.006f, 0.006f, W), new Vector3(h, y, 0), M.MetalGrey, transform);
            }

            for (int u = 0; u < 3; u++)
            {
                float c = H / 6 * (2 * u + 1);
                Mount(Slots[u * 2], new Vector3(0, c - 0.026f, 0));
                Mount(Slots[u * 2 + 1], new Vector3(0, c + 0.026f, 0));
            }
            Mount("+X", new Vector3(W / 2 + 0.002f, H / 2, 0), Quaternion.Euler(0, 90, 0));
            Mount("-X", new Vector3(-W / 2 - 0.002f, H / 2, 0), Quaternion.Euler(0, -90, 0));
            Mount("+Z", new Vector3(0, H / 2, W / 2 + 0.002f), Quaternion.identity);
            Mount("-Z", new Vector3(0, H / 2, -W / 2 - 0.002f), Quaternion.Euler(0, 180, 0));
            Mount("TOP", new Vector3(0, H + 0.002f, 0));
            Mount("BOTTOM", new Vector3(0, -0.002f, 0), Quaternion.Euler(180, 0, 0));
            Mount("RAIL", new Vector3(h, -0.006f, -h));
            Mount("PIN", new Vector3(-h - 0.006f, H * 0.82f, -h - 0.006f));
        }

        void Mount(string id, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject("MOUNT_" + id).transform;
            t.SetParent(transform, false);
            t.localPosition = pos;
            t.localRotation = rot ?? Quaternion.identity;
            _mounts[id] = t;
        }

        public Transform MountPoint(string id) => _mounts.TryGetValue(id, out var t) ? t : null;

        public bool IsFree(string mountId) => !_installedMounts.ContainsValue(mountId);
        readonly Dictionary<string, string> _installedMounts = new Dictionary<string, string>();

        /// <summary>First free mount for this part type, or null.</summary>
        public string FreeMountFor(PartDef def)
        {
            IEnumerable<string> options;
            switch (def.mount)
            {
                case "slot": options = Slots; break;
                case "face_long": options = LongFaces; break;
                case "face_end": options = def.category == "antenna" ? new[] { "TOP", "BOTTOM" } : new[] { "BOTTOM", "TOP" }; break;
                case "inline": options = new[] { "RAIL" }; break;
                case "pin": options = new[] { "PIN" }; break;
                default: return null;
            }
            foreach (var m in options) if (IsFree(m)) return m;
            return null;
        }

        public GameObject Install(string instanceId, PartDef def, string mountId, bool animate)
        {
            var mount = MountPoint(mountId);
            if (mount == null) return null;
            var root = new GameObject("Installed_" + instanceId);
            root.transform.SetParent(mount, false);
            Shape(root.transform, def);
            var part = root.AddComponent<Part>();
            part.partId = def.id;
            part.instanceId = instanceId;
            part.Def = def;
            _installed[instanceId] = root;
            _installedMounts[instanceId] = mountId;
            if (def.id == "rbf_pin") PinFlag = root.transform;
            if (animate) StartCoroutine(SlideIn(root.transform, def.mount));
            return root;
        }

        public void Uninstall(string instanceId)
        {
            if (_installed.TryGetValue(instanceId, out var go)) Destroy(go);
            _installed.Remove(instanceId);
            _installedMounts.Remove(instanceId);
        }

        public string MountOf(string instanceId) => _installedMounts.TryGetValue(instanceId, out var m) ? m : null;

        IEnumerator SlideIn(Transform t, string mount)
        {
            // Boards slide down the rails from above the frame; panels push in from the side.
            Vector3 startLocal = mount == "slot" ? new Vector3(0, H + 0.12f - t.parent.localPosition.y, 0) : new Vector3(0, 0, 0.12f);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.45f)
            {
                if (t == null) yield break;
                t.localPosition = Vector3.Lerp(startLocal, Vector3.zero, 1 - Mathf.Pow(1 - k, 3));
                yield return null;
            }
            if (t != null) t.localPosition = Vector3.zero;
            AudioFx.Play("seat");
        }

        /// <summary>Show the pin pulled out (true) or inserted.</summary>
        public void SetPinPulled(bool pulled)
        {
            if (PinFlag == null) return;
            StopCoroutine(nameof(PinAnim));
            StartCoroutine(PinAnim(pulled));
        }

        IEnumerator PinAnim(bool pulled)
        {
            var target = pulled ? new Vector3(-0.07f, 0.02f, -0.07f) : Vector3.zero;
            var from = PinFlag.localPosition;
            for (float k = 0; k < 1; k += Time.deltaTime / 0.35f)
            {
                PinFlag.localPosition = Vector3.Lerp(from, target, k);
                yield return null;
            }
            PinFlag.localPosition = target;
        }

        public void SetCutaway(bool on)
        {
            foreach (var kv in _installed)
            {
                var mount = MountOf(kv.Key);
                bool hide = on && (mount == "-Z" || mount == "-X");
                foreach (var r in kv.Value.GetComponentsInChildren<Renderer>()) r.enabled = !hide;
            }
        }

        // ------------------------------------------------------------------ shapes

        static void Shape(Transform root, PartDef def)
        {
            switch (def.mount)
            {
                case "slot":
                    Box("PCB", new Vector3(0.088f, 0.003f, 0.088f), Vector3.zero, def.category == "battery" ? M.Wall : M.BoardGreen, root);
                    if (def.category == "battery")
                        for (int i = 0; i < 4; i++) Box("Cell", new Vector3(0.018f, 0.018f, 0.07f), new Vector3(-0.03f + i * 0.02f, 0.011f, 0), M.InsulationOrange, root);
                    else
                    {
                        Box("Chip", new Vector3(0.022f, 0.004f, 0.022f), new Vector3(0.015f, 0.0035f, 0.01f), M.EngineDark, root);
                        Box("Chip", new Vector3(0.016f, 0.004f, 0.03f), new Vector3(-0.02f, 0.0035f, -0.015f), M.EngineDark, root);
                        Box("Header", new Vector3(0.06f, 0.006f, 0.006f), new Vector3(0, 0.004f, -0.038f), M.BandBlack, root);
                    }
                    if (def.id == "adcs_rw")
                        for (int i = 0; i < 3; i++) Box("Wheel", new Vector3(0.026f, 0.014f, 0.026f), new Vector3(-0.028f + i * 0.028f, 0.01f, 0.02f), M.MetalGrey, root);
                    break;
                case "face_long":
                    Box("Panel", new Vector3(0.09f, 0.32f, 0.002f), Vector3.zero, M.BandBlack, root);
                    for (int i = 0; i < 6; i++) Box("Cells", new Vector3(0.08f, 0.045f, 0.001f), new Vector3(0, -0.13f + i * 0.052f, 0.0015f), M.Wall, root);
                    if (def.id == "solar_wings")
                    {
                        var wing = Box("Wing", new Vector3(0.002f, 0.32f, 0.2f), new Vector3(0.045f, 0, 0.11f), M.Wall, root);
                        wing.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    }
                    break;
                case "face_end":
                    if (def.category == "antenna")
                    {
                        Box("Base", new Vector3(0.09f, 0.006f, 0.09f), new Vector3(0, 0.003f, 0), M.BandBlack, root);
                        for (int i = 0; i < 4; i++)
                        {
                            var whip = Box("Whip", new Vector3(def.id == "ant_patch" ? 0.05f : 0.17f, 0.001f, 0.004f), Vector3.zero, M.MetalGrey, root);
                            float a = 90f * i;
                            whip.transform.localRotation = Quaternion.Euler(0, a, 0);
                            whip.transform.localPosition = Quaternion.Euler(0, a, 0) * new Vector3(def.id == "ant_patch" ? 0.0f : 0.085f, 0.007f, 0);
                        }
                    }
                    else
                    {
                        Box("Body", new Vector3(0.07f, 0.04f, 0.07f), new Vector3(0, 0.02f, 0), M.EngineDark, root);
                        var lens = new GameObject("Lens");
                        lens.transform.SetParent(root, false);
                        lens.transform.localPosition = new Vector3(0, 0.04f, 0);
                        lens.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Cylinder(def.id == "cam_hd" ? 0.03f : 0.018f, 0.02f, 24);
                        lens.AddComponent<MeshRenderer>().sharedMaterial = M.Get(M.MetalGrey);
                    }
                    break;
                case "inline":
                    Box("Switch", new Vector3(0.012f, 0.012f, 0.012f), Vector3.zero, M.InsulationOrange, root);
                    break;
                case "pin":
                    Box("Pin", new Vector3(0.004f, 0.004f, 0.03f), Vector3.zero, M.MetalGrey, root);
                    Box("Flag", new Vector3(0.002f, 0.03f, 0.07f), new Vector3(-0.004f, -0.02f, -0.045f), M.GantryRed, root);
                    break;
            }
        }

        static GameObject Box(string name, Vector3 size, Vector3 pos, string mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = M.Get(mat);
            return go;
        }
    }
}
