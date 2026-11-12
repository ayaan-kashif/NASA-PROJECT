using System.Collections;
using System.Linq;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    public sealed partial class BriefingUI
    {
        static readonly CrewRole[] PollOrder =
            { CrewRole.PropulsionStructures, CrewRole.SpacecraftComms, CrewRole.ScienceBudget, CrewRole.FlightDirector };

        // ------------------------------------------------------------------ checklist

        public void ShowChecklist()
        {
            var items = PrelaunchChecklist.Build(S);
            _modal.Clear();
            _modal.RemoveFromClassList("hidden");
            var panel = new ScrollView(); panel.AddToClassList("pad-panel");
            _modal.Add(panel);

            int fails = items.Count(i => !i.pass);
            panel.Add(L("PRE-LAUNCH CHECKLIST", "info-category"));
            panel.Add(L(fails == 0 ? "All items PASS. Poll the crew." : $"{fails} item{(fails > 1 ? "s" : "")} FAIL. Fix them, or launch anyway and learn why.", "pad-title"));

            foreach (var role in new[] { CrewRole.FlightDirector, CrewRole.PropulsionStructures, CrewRole.SpacecraftComms, CrewRole.ScienceBudget })
            {
                var owner = S.Crew.FirstOrDefault(c => c.role == role)?.name ?? "";
                panel.Add(L($"{PrelaunchChecklist.RoleName(role).ToUpperInvariant()}  ·  {owner}", "brief-sub"));
                foreach (var it in items.Where(i => i.role == role))
                {
                    var row = new VisualElement(); row.AddToClassList("check-row");
                    row.AddToClassList(it.pass ? "pass" : "fail");
                    var mark = L(it.pass ? "✓ PASS" : "✗ FAIL", "check-mark");
                    var text = new VisualElement(); text.style.flexGrow = 1; text.style.flexShrink = 1;
                    text.Add(L(it.title, "check-title"));
                    text.Add(L(it.detail, "check-detail"));
                    row.Add(mark); row.Add(text);
                    if (!it.pass && it.action == "pull_rbf")
                        row.Add(Btn("Pull pin", () => { S.Spacecraft.rbfPinPulled = true; AudioFx.Play("click"); Log("RbfPulled", "RBF pin pulled at the pad"); ShowChecklist(); }, "card-btn accent"));
                    else if (!it.pass && it.action == "arm")
                        row.Add(Btn("Arm", () => { S.Vehicle.armed = true; AudioFx.Play("beep"); Log("Armed", "Safe/Arm switched to ARMED at the pad"); ShowChecklist(); }, "card-btn accent"));
                    else if (!it.pass && it.fixScene != null)
                        row.Add(Btn("Fix", () => { Log("FixRequested", $"Went to fix {it.id}", it.id); SceneFlow.Load(it.fixScene); }, "card-btn"));
                    panel.Add(row);
                }
            }

            var bar = new VisualElement(); bar.AddToClassList("bin-chips");
            bar.style.marginTop = 14;
            bar.Add(Btn("< Back to briefing", () => _modal.AddToClassList("hidden"), "card-btn"));
            bar.Add(Btn(fails == 0 ? "Start GO / NO-GO poll" : $"Poll anyway ({fails} known FAIL)", () => StartPoll(items.Where(i => !i.pass).Select(i => i.id).ToList()), "card-btn accent"));
            panel.Add(bar);
        }

        // ------------------------------------------------------------------ GO / NO-GO

        void StartPoll(System.Collections.Generic.List<string> knownFailures)
        {
            S.Launch.knownFailures = knownFailures;
            S.Launch.poll.Clear();
            ShowPollStep(0);
        }

        void ShowPollStep(int index)
        {
            _modal.Clear();
            if (index >= PollOrder.Length) { StartCountdown(); return; }
            var role = PollOrder[index];
            var who = S.Crew.FirstOrDefault(c => c.role == role)?.name ?? "Player 1";
            var panel = new VisualElement(); panel.AddToClassList("poll-panel");
            _modal.Add(panel);
            panel.Add(L($"GO / NO-GO POLL  ·  {index + 1} of {PollOrder.Length}", "info-category"));
            panel.Add(L(PrelaunchChecklist.RoleName(role), "poll-role"));
            panel.Add(L($"{who}, are you GO for launch?", "pad-title"));
            var mine = PrelaunchChecklist.Build(S).Where(i => i.role == role && !i.pass).ToList();
            panel.Add(L(mine.Count == 0 ? "All your items PASS." : "Your open items: " + string.Join("; ", mine.Select(i => i.title)), mine.Count == 0 ? "check-detail" : "poll-warn"));
            var row = new VisualElement(); row.AddToClassList("poll-buttons");
            row.Add(Btn("GO", () => { S.Launch.poll.Add(PrelaunchChecklist.RoleName(role) + ": GO"); AudioFx.Play("beep"); ShowPollStep(index + 1); }, "poll-go"));
            row.Add(Btn("NO-GO", () =>
            {
                S.Launch.poll.Add(PrelaunchChecklist.RoleName(role) + ": NO-GO");
                S.Launch.noGoRounds++;
                Log("NoGo", $"{PrelaunchChecklist.RoleName(role)} called NO-GO. Countdown held.", role.ToString());
                Toast("Hold the count. Fix the issue, then poll again.");
                ShowChecklist();
            }, "poll-nogo"));
            panel.Add(row);
        }

        // ------------------------------------------------------------------ countdown

        static readonly string[] Callouts =
        {
            "T-10  Guidance is internal", "T-9  Range is green", "T-8  Telemetry check", "T-7  Pyro channels armed",
            "T-6  Propellant pressurised", "T-5  Flight computer go", "T-4  Payload on internal power", "T-3  Ignition sequence start",
            "T-2", "T-1", "LIFTOFF!"
        };

        void StartCountdown()
        {
            S.Launch.launched = true;
            if (S.Launch.knownFailures.Count > 0)
                Log("LaunchWithKnownFailures", "Launched with known FAILs: " + string.Join(", ", S.Launch.knownFailures), string.Join(",", S.Launch.knownFailures), Severity.Warning);
            Log("CountdownStarted", "All roles GO. Countdown started.");
            StartCoroutine(Countdown());
        }

        IEnumerator Countdown()
        {
            _modal.Clear();
            var panel = new VisualElement(); panel.AddToClassList("poll-panel");
            var big = L("T-10", "countdown");
            var call = L("", "pad-title");
            panel.Add(L("ALL ROLES GO", "info-category"));
            panel.Add(big); panel.Add(call);
            _modal.Add(panel);
            for (int i = 0; i < Callouts.Length; i++)
            {
                big.text = i < 10 ? $"T-{10 - i}" : "T-0";
                call.text = Callouts[i];
                AudioFx.Play("beep", 0.8f, i < 10 ? 1f : 1.5f);
                yield return new WaitForSeconds(i < 10 ? 0.6f : 1.0f);
            }
            SceneFlow.Load(SceneNames.Flight);
        }

        void Log(string code, string message, string cause = null, Severity sev = Severity.Info) =>
            S.Events.Add("Pad", code, message, cause, sev);
    }
}
