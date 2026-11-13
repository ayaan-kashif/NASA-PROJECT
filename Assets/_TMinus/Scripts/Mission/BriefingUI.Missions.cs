using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    public sealed partial class BriefingUI
    {
        // ------------------------------------------------------------------ mission select

        public void ShowMissionSelect()
        {
            _page.Clear();
            var head = Header("CHOOSE A MISSION", "Every mission is a full build: satellite, rocket, launch and operations.");
            head.Add(Btn("Main menu", () => SceneFlow.Load(SceneNames.MainMenu), "tool-btn"));
            _page.Add(head);
            var grid = new VisualElement(); grid.AddToClassList("mission-grid");
            foreach (var m in Data.Missions.OrderBy(m => m.locked).ThenBy(m => m.priority))
            {
                var card = new VisualElement(); card.AddToClassList("mission-card");
                card.EnableInClassList("locked", m.locked && !m.id.StartsWith("failure_") && m.id != "sandbox");
                var top = new VisualElement(); top.AddToClassList("rocket-card-top");
                top.Add(L(m.name, "mission-name"));
                bool job = m.id.StartsWith("failure_"), sandbox = m.id == "sandbox";
                if (m.startHere) top.Add(L("START HERE", "badge"));
                else if (job) top.Add(L("REPAIR JOB", "badge job"));
                else if (sandbox) top.Add(L("FREE BUILD", "badge job"));
                else if (m.locked) top.Add(L("COMING NEXT", "badge training"));
                card.Add(top);
                card.Add(L(m.difficulty, "mission-diff"));
                card.Add(L(m.objective, "mission-obj"));
                var meta = new List<string>();
                if (m.budgetMUSD > 0) meta.Add($"Budget {m.budgetMUSD:0.00} M$");
                if (m.rocketOptions.Count > 0) meta.Add("Rocket: " + string.Join(", ", m.rocketOptions.Select(r => Data.GetRocket(r)?.name ?? r)));
                if (m.durationDays > 0) meta.Add($"{m.durationDays} days");
                card.Add(L(string.Join("  ·  ", meta), "mission-meta"));
                var id = m.id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    // Repair jobs and the sandbox live in the main menu: open them there.
                    if (id.StartsWith("failure_") || id == "sandbox")
                    {
                        DemoHooks.PendingMenuPanel = id == "sandbox" ? "sandbox" : "failure";
                        SceneFlow.Load(SceneNames.MainMenu);
                        return;
                    }
                    if (Data.GetMission(id).locked) { Toast("Coming next: this mission isn't built yet. Start with FloodWatch-1."); return; }
                    S.CurrentMissionId = id;
                    ShowBriefing();
                });
                grid.Add(card);
            }
            _page.Add(grid);
        }
    }
}
