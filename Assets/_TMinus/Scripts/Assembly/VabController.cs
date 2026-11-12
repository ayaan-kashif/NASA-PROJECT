using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>
    /// VAB scene glue. Assemble mode: the player's Pathfinder build (parts start on the floor).
    /// Inspect mode: any rocket, fully assembled. Connects selection, info card, library, toolbar.
    /// </summary>
    public sealed class VabController : MonoBehaviour
    {
        public OrbitCamera orbit;
        public PartSelector selector;
        public VabUI ui;
        public VabEnvironment environment;
        public Transform rocketRoot;
        public BoltTool boltTool;

        public RocketView Current { get; private set; }
        public string Mode { get; private set; } = "assemble";
        public string RocketId { get; private set; }
        public static VabController Instance { get; private set; }

        DataRegistry Data => GameState.Instance.Data;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var state = GameState.Instance;
            if (orbit != null && ui != null) orbit.IsPointerOverUI = ui.IsPointerOverUI;
            if (selector != null)
            {
                if (ui != null) selector.IsPointerOverUI = ui.IsPointerOverUI;
                selector.SelectionChanged += OnSelected;
                selector.FocusRequested += p => { if (p != null) orbit?.Focus(p.WorldBounds()); };
            }

            if (ui != null)
            {
                ui.RocketChosen += ShowRocket;
                ui.ModeChanged += SetMode;
                ui.ExplodedChanged += SetExploded;
                ui.XRayChanged += on => Current?.SetXRay(on);
                ui.ResetCamera += () => orbit?.ResetView();
                ui.InfoClosed += () => selector?.Select(null);
                ui.JointsProvider = JointList;
            }

            var rocketId = state.Vehicle.rocketId;
            if (Data.GetRocket(rocketId) == null) rocketId = Data.Rockets.FirstOrDefault()?.id;
            Mode = Data.GetRocket(rocketId)?.buildable == true ? "assemble" : "inspect";
            ui?.SetMode(Mode, notify: false);
            ui?.BuildLibrary(Data.Rockets.OrderBy(r => r.real), rocketId);
            if (rocketId != null) ShowRocket(rocketId);
            else ui?.Toast("No rockets in rockets.json.");
        }

        public void SetMode(string mode)
        {
            Mode = mode;
            ui?.SetMode(mode, notify: false);
            if (RocketId != null) ShowRocket(RocketId);
        }

        public void ShowRocket(string rocketId)
        {
            var def = Data.GetRocket(rocketId);
            if (def == null) return;
            RocketId = rocketId;

            if (Current != null) Destroy(Current.gameObject);
            selector?.Select(null);
            ui?.HidePart();
            ui?.ClearLabels();
            ui?.SetToggles(false, false);

            bool assembly = Mode == "assemble" && def.buildable;
            float deck = environment != null ? environment.DeckTop : 0;
            var parent = rocketRoot != null ? rocketRoot : transform;
            parent.position = new Vector3(0, deck, 0);

            var state = GameState.Instance;
            var saved = state.Vehicle.rocketId == rocketId ? state.Vehicle : null;
            Current = RocketView.Spawn(def, Data, parent, assembly, deck, saved);
            if (assembly) boltTool?.Bind(Current);
            else boltTool?.Bind(null);

            if (Current.Graph != null)
            {
                Current.Graph.GraphChanged += () =>
                {
                    if (Current == null) return;
                    if (assembly) Current.SaveTo(state.Vehicle);
                    ui?.RefreshBin(Current);
                };
            }
            ui?.SetBinVisible(assembly);
            ui?.RefreshBin(Current);

            float diameter = (float)def.stages.Select(s => s.diameterM).DefaultIfEmpty(1).Max();
            environment?.FitTo(diameter, Current.Height);
            orbit?.Frame(Current.WorldBounds(), instant: true);
            ui?.SetSelectedRocket(rocketId);

            var mission = Data.GetMission(state.CurrentMissionId);
            if (mission != null && mission.rocketOptions.Contains(rocketId)) state.Vehicle.rocketId = rocketId;

            if (Mode == "assemble" && !def.buildable)
                ui?.Toast($"{def.name} is inspect-only. Assemble mode builds the Pathfinder.");
            else if (mission != null && !mission.rocketOptions.Contains(rocketId))
                ui?.Toast($"{def.name}: inspect only. {mission.name} flies on the Pathfinder.");
            else if (assembly && !Current.Graph.IsComplete())
                ui?.Toast("Drag the parts from the floor onto the stand, bottom first.");

            state.Events.Add("VAB", "RocketShown", $"{def.name} ({(assembly ? "assemble" : "inspect")})");
        }

        public void SetExploded(bool on)
        {
            if (Current == null) return;
            Current.SetExploded(on);
            StopAllCoroutines();
            StartCoroutine(ReframeAfterMove(on));
        }

        System.Collections.IEnumerator ReframeAfterMove(bool exploded)
        {
            yield return new WaitForSeconds(0.7f); // parts glide for about half a second
            if (Current == null || orbit == null) yield break;
            if (exploded) orbit.Focus(Current.WorldBounds(attachedOnly: true));
            else orbit.ResetView();
        }

        System.Collections.Generic.IEnumerable<(string id, string label, JointStatus status, bool mounted)> JointList()
        {
            if (boltTool == null || Current?.Graph == null) yield break;
            var g = Current.Graph;
            foreach (var v in boltTool.Views)
            {
                bool mounted = g.Edges.Any(e => e.jointId == v.Def.id);
                yield return (v.Def.id, $"{g.Rules.Name(v.Def.lowerPartId)} + {g.Rules.Name(v.Def.upperPartId)}", v.State.GetStatus(), mounted);
            }
        }

        void OnSelected(Part p)
        {
            if (p == null) ui?.HidePart();
            else ui?.ShowPart(p, Data);
        }

        void LateUpdate()
        {
            if (ui != null && orbit != null)
                ui.UpdateLabels(orbit.GetComponent<Camera>(), Current, Current != null && Current.Exploded);
        }
    }
}
