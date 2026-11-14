using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// Failure Lab job "Apollo 12, 1969: lightning". The EECOM console during ascent: two strikes (T+36.5 s, T+52 s),
    /// caution lights flood, telemetry turns to garbage, 20 seconds per call. Choices come from missions.json
    /// (consoleChoices); every pick, right or wrong, explains itself.
    /// </summary>
    public static class Apollo12Console
    {
        const float StrikeT = 36.5f, SecondStrikeT = 52f, StartT = 30f, Window = 20f;
        static readonly string[] Lights = { "FC 1", "FC 2", "FC 3", "MAIN BUS A", "MAIN BUS B", "AC BUS 1", "AC BUS 2", "SCE", "CMC", "IMU", "BAT CHG", "MASTER ALARM" };
        static readonly (string name, string ok)[] Tlm = { ("MAIN BUS A", "28.4 V"), ("MAIN BUS B", "28.3 V"), ("FC 1 AMPS", "31.0 A"), ("FC 2 AMPS", "30.6 A"), ("SCE", "5.01 V"), ("CABIN P", "5.0 psi") };

        /// <summary>The state of one run; kept separate from UI so it can be tested.</summary>
        public sealed class Run
        {
            public int Step = 1;          // 1 = restore telemetry, 2 = restore power, 3 = done
            public int Wrong;
            public bool TimedOut;
            public string Feedback;

            public bool Pick(JobChoiceDef c)
            {
                Feedback = c.why;
                if (c.correctAtStep == Step) { Step++; return true; }
                Wrong++; return false;
            }

            public int Score(int reward) => Mathf.Max(0, reward - 60 * Wrong - (TimedOut ? reward / 2 : 0));
        }

        public static void Show(VisualElement root, MissionDef job)
        {
            if (root == null) return;
            root.Q("menu-overlay")?.RemoveFromHierarchy();
            var run = new Run();
            var overlay = new VisualElement { name = "menu-overlay" }; overlay.AddToClassList("overlay");
            var card = new VisualElement(); card.AddToClassList("overlay-card"); overlay.Add(card);
            var head = new VisualElement(); head.AddToClassList("overlay-head"); card.Add(head);
            var title = new Label("APOLLO 12 · EECOM"); title.AddToClassList("overlay-title"); head.Add(title);
            var close = new Button(() => overlay.RemoveFromHierarchy()) { text = "Leave  ✕" }; close.AddToClassList("overlay-close"); head.Add(close);
            var body = new VisualElement(); body.AddToClassList("overlay-body"); card.Add(body);

            var clock = new Label(); clock.AddToClassList("a12-clock"); body.Add(clock);
            var prompt = new Label(job.clientNote); prompt.AddToClassList("a12-prompt"); body.Add(prompt);
            var panels = new VisualElement(); panels.style.flexDirection = FlexDirection.Row; body.Add(panels);
            var lightGrid = new VisualElement(); lightGrid.AddToClassList("a12-lights"); panels.Add(lightGrid);
            var lights = Lights.Select(n => { var l = new Label(n); l.AddToClassList("a12-light"); lightGrid.Add(l); return l; }).ToArray();
            var tlmBox = new VisualElement(); tlmBox.AddToClassList("a12-tlm"); panels.Add(tlmBox);
            var tlm = Tlm.Select(t => { var l = new Label(); l.AddToClassList("a12-tlm-row"); tlmBox.Add(l); return l; }).ToArray();
            var feedback = new Label(); feedback.AddToClassList("a12-feedback"); body.Add(feedback);
            var switches = new VisualElement(); switches.AddToClassList("a12-switches"); body.Add(switches);

            float t0 = Time.unscaledTime, stepStart = -1;
            bool flashing = false;
            foreach (var c in job.consoleChoices)
            {
                var b = new Button(() =>
                {
                    if (run.Step > 2 || Time.unscaledTime - t0 + StartT < StrikeT) return;
                    bool right = run.Pick(c);
                    feedback.text = (right ? "✓ " : "✗ ") + run.Feedback;
                    feedback.EnableInClassList("a12-good", right); feedback.EnableInClassList("a12-bad", !right);
                    if (right) stepStart = Time.unscaledTime;
                    if (run.Step > 2) Finish(body, job, run);
                }) { text = c.label };
                b.AddToClassList("a12-switch");
                switches.Add(b);
            }

            overlay.schedule.Execute(() =>
            {
                if (overlay.panel == null) return;
                float t = Time.unscaledTime - t0 + StartT;
                bool struck = t >= StrikeT;
                if (struck && stepStart < 0) stepStart = Time.unscaledTime;
                float left = struck ? Window - (Time.unscaledTime - stepStart) : 0;
                clock.text = $"T+{t:00.0} s   ·   ALT {(t * t * 0.0055f):0.0} km" + (run.Step > 2 ? "   ·   all systems restored" : struck ? $"   ·   DECIDE: {Mathf.Max(0, left):0} s" : "   ·   nominal ascent");
                bool strikeFlash = (t >= StrikeT && t < StrikeT + 0.4f) || (t >= SecondStrikeT && t < SecondStrikeT + 0.4f);
                if (strikeFlash != flashing) { flashing = strikeFlash; card.EnableInClassList("a12-flash", flashing); }
                if (run.Step > 2) prompt.text = "Telemetry and power are back. The Saturn V never stopped flying.";
                else if (struck && prompt.text == job.clientNote) prompt.text = "LIGHTNING. Fuel cells offline, caution lights everywhere, telemetry is garbage. What's your call, EECOM?";

                for (int i = 0; i < lights.Length; i++)
                    lights[i].EnableInClassList("a12-on", struck && (run.Step == 1 || (run.Step == 2 && i < 3) || i == lights.Length - 1 && run.Step < 3) && ((int)(t * 3) + i) % 4 != 0);
                for (int i = 0; i < tlm.Length; i++)
                    tlm[i].text = $"{Tlm[i].name,-12} " + (!struck ? Tlm[i].ok : run.Step == 1 ? Garbage() : i < 4 && run.Step == 2 ? "OFFLINE" : Tlm[i].ok);

                if (struck && left <= 0 && run.Step <= 2)
                {
                    run.TimedOut = true;
                    var right = job.consoleChoices.FirstOrDefault(c => c.correctAtStep == run.Step);
                    feedback.text = $"Out of time. The call was \"{right?.label}\". {right?.why}";
                    feedback.EnableInClassList("a12-bad", true);
                    run.Step++; stepStart = Time.unscaledTime;
                    if (run.Step > 2) Finish(body, job, run);
                }
            }).Every(100);
            root.Add(overlay);
        }

        static string Garbage()
        {
            const string g = "#%&*0123456789XZ?";
            var c = new char[6];
            for (int i = 0; i < c.Length; i++) c[i] = g[Random.Range(0, g.Length)];
            return new string(c);
        }

        static void Finish(VisualElement body, MissionDef job, Run run)
        {
            GameState.Instance?.Events.Add("failure_lab", "job_complete", $"{job.id}: {run.Wrong} wrong calls, timed out={run.TimedOut}, {run.Score(job.rewardPoints)} pts", job.id, Severity.Info);
            body.Q(className: "a12-switches")?.RemoveFromHierarchy();
            var done = new VisualElement(); done.AddToClassList("job-card"); body.Add(done);
            done.Add(new Label($"JOB DEBRIEF · {run.Score(job.rewardPoints)} / {job.rewardPoints} pts · {run.Wrong} wrong call{(run.Wrong == 1 ? "" : "s")}{(run.TimedOut ? " · ran out of time" : "")}") { name = "job-title" });
            var lesson = new Label(job.lesson); lesson.AddToClassList("job-note"); done.Add(lesson);
            var src = new Label("Read more in the Space Guide: Apollo 12 lightning strike."); src.AddToClassList("muted"); done.Add(src);
        }
    }
}
