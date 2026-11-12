using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;

namespace TMinus.Assembly
{
    /// <summary>
    /// VAB scene glue: spawns the chosen rocket on the stand, frames the camera, and connects
    /// selection, the info card, the library and the toolbar.
    /// </summary>
    public sealed class VabController : MonoBehaviour
    {
        public OrbitCamera orbit;
        public PartSelector selector;
        public VabUI ui;
        public VabEnvironment environment;
        public Transform rocketRoot;

        public RocketView Current { get; private set; }
        public string Mode { get; private set; } = "inspect";
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
            if (orbit != null) orbit.IsPointerOverUI = ui != null ? ui.IsPointerOverUI : (System.Func<Vector2, bool>)null;
            if (selector != null)
            {
                selector.IsPointerOverUI = orbit?.IsPointerOverUI;
                selector.SelectionChanged += OnSelected;
                selector.FocusRequested += p => { if (p != null) orbit?.Focus(p.WorldBounds()); };
            }

            if (ui != null)
            {
                ui.RocketChosen += ShowRocket;
                ui.ModeChanged += m => Mode = m;
                ui.ExplodedChanged += SetExploded;
                ui.XRayChanged += on => Current?.SetXRay(on);
                ui.ResetCamera += () => orbit?.ResetView();
                ui.InfoClosed += () => selector?.Select(null);
            }

            var rocketId = state.Vehicle.rocketId;
            if (Data.GetRocket(rocketId) == null) rocketId = Data.Rockets.FirstOrDefault()?.id;
            ui?.BuildLibrary(Data.Rockets.OrderBy(r => r.real), rocketId);
            if (rocketId != null) ShowRocket(rocketId);
            else ui?.Toast("No rockets in rockets.json.");
        }

        public void ShowRocket(string rocketId)
        {
            var def = Data.GetRocket(rocketId);
            if (def == null) return;

            if (Current != null) Destroy(Current.gameObject);
            selector?.Select(null);
            ui?.HidePart();
            ui?.ClearLabels();
            ui?.SetToggles(false, false);

            var parent = rocketRoot != null ? rocketRoot : transform;
            parent.position = new Vector3(0, environment != null ? environment.DeckTop : 0, 0);
            Current = RocketView.Spawn(def, Data, parent);

            float diameter = (float)def.stages.Select(s => s.diameterM).DefaultIfEmpty(1).Max();
            environment?.FitTo(diameter, Current.Height);
            orbit?.Frame(Current.WorldBounds(), instant: true);
            ui?.SetSelectedRocket(rocketId);

            var mission = Data.GetMission(GameState.Instance.CurrentMissionId);
            if (mission != null && mission.rocketOptions.Contains(rocketId)) GameState.Instance.Vehicle.rocketId = rocketId;
            else if (mission != null) ui?.Toast($"{def.name}: inspect only. {mission.name} flies on {string.Join(", ", mission.rocketOptions)}.");

            GameState.Instance.Events.Add("VAB", "RocketShown", $"{def.name} on the stand");
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
            if (exploded) orbit.Focus(Current.WorldBounds());
            else orbit.ResetView();
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
