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
            head.Add(Btn(Strings.Get("pause.mainMenu", "Main menu"), () => SceneFlow.Load(SceneNames.MainMenu), "tool-btn"));
            _page.Add(head);
            var grid = new VisualElement(); grid.AddToClassList("mission-grid");
            foreach (var m in Data.Missions.OrderBy(m => m.locked).ThenBy(m => m.priority))
            {
                // Repair jobs and the Sandbox are opened from the main menu, so they are not listed here (they only bounced back).
                if (m.id.StartsWith("failure_") || m.id == "sandbox") continue;
                var card = new VisualElement(); card.AddToClassList("mission-card");
                card.EnableInClassList("locked", m.locked && !m.id.StartsWith("failure_") && m.id != "sandbox");
                var top = new VisualElement(); top.AddToClassList("rocket-card-top");
                top.Add(L(m.name, "mission-name"));
                bool job = m.id.StartsWith("failure_"), sandbox = m.id == "sandbox";
                if (m.startHere) top.Add(L(Strings.Get("briefing.start_here", "START HERE"), "badge"));
                else if (job) top.Add(L(Strings.Get("briefing.repair_job", "REPAIR JOB"), "badge job"));
                else if (sandbox) top.Add(L(Strings.Get("briefing.free_build", "FREE BUILD"), "badge job"));
                else if (m.locked) top.Add(L(Strings.Get("briefing.coming_next", "COMING NEXT"), "badge training"));
                card.Add(top);
                card.Add(L(m.difficulty, "mission-diff"));
                card.Add(L(m.objective, "mission-obj"));
                var meta = new List<string>();
                if (m.budgetMUSD > 0) meta.Add($"Budget {m.budgetMUSD:0.00} M$");
                if (m.rocketOptions.Count > 0) meta.Add("Rocket: " + string.Join(", ", m.rocketOptions.Select(r => Data.GetRocket(r)?.name ?? r)));
                if (m.durationDays > 0) meta.Add($"{m.durationDays} days");
                if (!string.IsNullOrEmpty(m.sponsor)) meta.Add("Sponsored by " + m.sponsor);
                var best = MissionLog.Best(m.id);
                if (best != null) { int n = MissionLog.Count(m.id); meta.Add($"Best {MissionLog.StarText(best.stars)} {best.score:0.#} pts ({n} attempt{(n == 1 ? "" : "s")})"); }
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
                    if (Data.GetMission(id).locked) { Toast(Strings.Get("briefing.coming_next_this_mission_isn", "Coming next: this mission isn't built yet. Start with FloodWatch-1.")); return; }
                    S.CurrentMissionId = id;
                    ShowBriefing();
                });
                grid.Add(card);
            }
            _page.Add(grid);
            _page.Add(L(Strings.Get("briefing.repair_jobs_failure_lab_and", "Repair jobs (Failure Lab) and the Sandbox are in the main menu."), "mission-meta"));
        }
    }
}
