using System.Collections.Generic;
using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMinus.Spacecraft
{
    /// <summary>
    /// Integration Bay: install CubeSat parts into the 3U frame (Build), then wire them on the
    /// FlatSat bench (Wire). Saves to GameState.Spacecraft. The RBF pin starts inserted.
    /// </summary>
    public sealed class BayController : MonoBehaviour
    {
        public OrbitCamera orbit;
        public BayUI ui;
        public WiringBench bench;
        public Transform satStand;

        public static BayController Instance { get; private set; }
        public CubeSatModel Model { get; private set; }
        public string Mode { get; private set; } = "build";

        DataRegistry Data => GameState.Instance.Data;
        SpacecraftDesign Design => GameState.Instance.Spacecraft;
        Camera _cam;

        /// <summary>Raised whenever parts or cables change (budgets listen, Prompt 7).</summary>
        public event System.Action DesignChanged;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _cam = orbit.GetComponent<Camera>();
            Model = CubeSatModel.Build(satStand);
            bench.cam = _cam;
            bench.Init(Data.GetRules());
            bench.system = "IntegrationBay";
            bench.IsPointerOverUI = ui.IsPointerOverUI;
            bench.ShowHint = ui.ShowHint;
            bench.HideHint = ui.HideHint;
            bench.Toast = m => ui.Toast(m, 4000);
            orbit.IsPointerOverUI = ui.IsPointerOverUI;

            // Restore the saved design (or start with the frame and the RBF pin inserted).
            if (Design.parts.All(p => p.partId != "rbf_pin"))
                Design.parts.Add(new PlacedPart { instanceId = "rbf_pin", partId = "rbf_pin", mountId = "PIN" });
            foreach (var p in Design.parts.ToList())
            {
                var def = Data.GetPart(p.partId);
                if (def == null) continue;
                Model.Install(p.instanceId, def, p.mountId, false);
                if (def.ports.Count > 0) bench.AddDevice(p.instanceId, def);
            }
            if (Design.rbfPinPulled) Model.SetPinPulled(true);
            bench.LoadFrom(Design.cables);

            bench.FaultsChanged += f => ui.ShowFaults(f, bench.Tiles.Count);
            bench.Changed += Save;
            ui.ModeChanged += SetMode;
            ui.CutawayChanged += on => Model.SetCutaway(on);
            ui.MeterChanged += on => { bench.multimeter = on; if (on && Mode != "wire") SetMode("wire"); };
            ui.ResetCamera += () => Frame(true);
            ui.InstallPressed += Install;
            ui.RemovePressed += Remove;
            ui.FaultPressed += ShowFault;

            ui.ShowFaults(bench.Faults, bench.Tiles.Count);
            RefreshBin();
            SetMode("build");
            if (Design.parts.Count <= 1) ui.Toast("Click parts on the left to install them in the frame. Start with the EPS and a battery.", 5000);
        }

        public void SetMode(string mode)
        {
            Mode = mode;
            ui.SetMode(mode, false);
            bench.interactive = mode == "wire";
            if (mode != "wire") { bench.multimeter = false; ui.SetMeter(false); }
            Frame(true);
        }

        void Frame(bool instant)
        {
            if (Mode == "wire")
            {
                orbit.pitch = 58;
                orbit.yaw = 0;
                orbit.Frame(bench.BenchBounds(), instant);
            }
            else
            {
                orbit.pitch = 18;
                orbit.yaw = 35;
                var b = new Bounds(satStand.position + Vector3.up * 0.17f, new Vector3(0.3f, 0.45f, 0.3f));
                orbit.Frame(b, instant);
            }
        }

        // ------------------------------------------------------------------ install / remove

        IEnumerable<PartDef> CubeSatParts => Data.Parts.Where(p => p.assembly == "cubesat" && p.mount != "frame" && p.mount != "pin");

        List<string> InstancesOf(string partId) => Design.parts.Where(p => p.partId == partId).Select(p => p.instanceId).ToList();

        string BlockedReason(string partId)
        {
            var def = Data.GetPart(partId);
            if (def == null) return "unknown";
            return Model.FreeMountFor(def) == null ? "no free " + MountName(def.mount) : null;
        }

        static string MountName(string mount) =>
            mount == "slot" ? "slot" : mount == "face_long" ? "side face" : mount == "face_end" ? "end face" : mount;

        public void Install(string partId)
        {
            var def = Data.GetPart(partId);
            if (def == null) return;
            var mount = Model.FreeMountFor(def);
            if (mount == null) { ui.Toast($"No free {MountName(def.mount)} for the {def.name}. Remove something first."); AudioFx.Play("error"); return; }

            string id = partId;
            for (int n = 2; Design.parts.Any(p => p.instanceId == id); n++) id = partId + "_" + n;
            Design.parts.Add(new PlacedPart { instanceId = id, partId = partId, mountId = mount });
            Model.Install(id, def, mount, true);
            if (def.ports.Count > 0) bench.AddDevice(id, def);
            GameState.Instance.Events.Add("IntegrationBay", "PartInstalled", $"{def.name} in {mount}", id);
            ui.Toast($"{def.name} installed in {mount}.", 1800);
            if (Mode == "wire") Frame(false);
            RefreshBin();
            Save();
        }

        public void Remove(string instanceId)
        {
            var placed = Design.parts.FirstOrDefault(p => p.instanceId == instanceId);
            if (placed == null || placed.partId == "rbf_pin") return;
            Design.parts.Remove(placed);
            Model.Uninstall(instanceId);
            bench.RemoveDevice(instanceId);
            AudioFx.Play("click");
            GameState.Instance.Events.Add("IntegrationBay", "PartRemoved", $"{Data.GetPart(placed.partId)?.name} removed", instanceId);
            RefreshBin();
            Save();
        }

        void RefreshBin() => ui.RefreshBin(CubeSatParts, InstancesOf, BlockedReason);

        void ShowFault(WiringFault f)
        {
            if (Mode != "wire") SetMode("wire");
            var port = f.port != null ? bench.FindPort(f.port) : null;
            if (port == null && f.device != null && bench.Tiles.TryGetValue(f.device, out var tile)) port = tile.Ports.FirstOrDefault();
            if (port == null) { ui.Toast(f.message, 5000); return; }
            StopAllCoroutines();
            StartCoroutine(Pulse(port));
            orbit.Focus(new Bounds(port.transform.position, Vector3.one * 0.35f));
            ui.Toast(f.message, 5000);
        }

        System.Collections.IEnumerator Pulse(PortView port)
        {
            for (float t = 0; t < 2.5f && port != null; t += Time.deltaTime)
            {
                port.SetGlow(Color.Lerp(Color.black, new Color(1f, 0.3f, 0.1f), Mathf.PingPong(t * 3, 1)));
                yield return null;
            }
            if (port != null) port.SetGlow(Color.black);
        }

        // ------------------------------------------------------------------ RBF pin + save

        void Update()
        {
            ui.UpdateTileLabels(_cam, bench.Tiles.Values, Mode == "wire");
            if (Mode != "build") return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var pos = mouse.position.ReadValue();
            if (ui.IsPointerOverUI(pos)) return;
            if (Physics.Raycast(_cam.ScreenPointToRay(pos), out var hit, 20f))
            {
                var part = hit.collider.GetComponentInParent<Part>();
                if (part != null && part.partId == "rbf_pin") TogglePin();
            }
        }

        public void TogglePin()
        {
            Design.rbfPinPulled = !Design.rbfPinPulled;
            Model.SetPinPulled(Design.rbfPinPulled);
            AudioFx.Play("click");
            ui.Toast(Design.rbfPinPulled ? "RBF pin pulled. In real life this happens at the pad, as one of the last steps." : "RBF pin inserted: the satellite is safe to handle.");
            GameState.Instance.Events.Add("IntegrationBay", Design.rbfPinPulled ? "RbfPulled" : "RbfInserted", "Remove-Before-Flight pin " + (Design.rbfPinPulled ? "pulled" : "inserted"), "rbf_pin");
            Save();
        }

        void Save()
        {
            bench.SaveTo(Design.cables);
            DesignChanged?.Invoke();
        }

        public string Report() =>
            $"parts={Design.parts.Count} cables={bench.Graph.Cables.Count} faults={bench.Faults.Count} [{string.Join(" | ", bench.Faults.Select(f => f.code + ":" + f.device))}]";
    }
}
