using System.Collections.Generic;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Core.Data;
using TMinus.Wiring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TMinus.Houston
{
    /// <summary>
    /// Gathers the current build state from scene singletons into an AdvisorContext.
    /// Finds VabController.Instance, BayController.Instance, AvionicsBayController.Instance,
    /// BriefingUI.Instance through the existing singletons.
    /// </summary>
    public static class ContextBuilder
    {
        public static AdvisorContext Build(GameState state)
        {
            var ctx = new AdvisorContext
            {
                Data = state.Data,
                Rules = state.Data.GetRules(),
                SceneName = SceneManager.GetActiveScene().name,
                MissionId = state.CurrentMissionId,
                Vehicle = state.Vehicle,
                RbfPinInserted = !state.Spacecraft.rbfPinPulled
            };

            // CubeSat data
            ctx.CubeSatParts = state.Spacecraft.parts;
            var rules = state.Data.GetRules();
            if (rules.assemblyOrder.TryGetValue("cubesat", out var csOrder))
                ctx.CubeSatAssemblyOrder = csOrder;

            // SpacecraftReport
            var mission = state.Data.GetMission(state.CurrentMissionId);
            var rocket = state.Data.GetRocket(state.Vehicle.rocketId);
            if (mission != null)
                ctx.SpacecraftReport = SpacecraftBudget.Compute(state.Spacecraft, state.Data, mission, rocket);

            // AvionicsReport
            ctx.AvionicsReport = AvionicsReport.Compute(state.Vehicle, rules);

            // Scene-specific live singletons
            var vab = VabController.Instance;
            if (vab != null && vab.Current != null)
            {
                ctx.Graph = vab.Current.Graph;
                if (vab.boltTool != null)
                {
                    ctx.BoltJoints = new List<BoltJointState>();
                    foreach (var view in vab.boltTool.Views)
                        ctx.BoltJoints.Add(view.State);
                }
            }

            // Fallback: bolt joints loaded from vehicle data if not in VAB
            if (ctx.BoltJoints == null)
            {
                ctx.BoltJoints = new List<BoltJointState>();
                foreach (var j in rules.boltJoints)
                    ctx.BoltJoints.Add(BoltJointState.LoadFrom(j, state.Vehicle));
            }

            // Integration Bay
            var bay = TMinus.Spacecraft.BayController.Instance;
            if (bay != null && bay.bench != null)
                ctx.Wiring = bay.bench.Graph;

            // Avionics Bay
            var avBay = AvionicsBayController.Instance;
            if (avBay != null && avBay.bench != null)
                ctx.AvionicsWiring = avBay.bench.Graph;

            return ctx;
        }
    }
}
