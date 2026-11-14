using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebriefUI : MonoBehaviour
    {
        [SerializeField] StyleSheet baseStyle, style;
        Label _status;
        bool _savedToBoard;
        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (baseStyle != null) root.styleSheets.Add(baseStyle);
            if (style != null) root.styleSheets.Add(style);
            root.AddToClassList("debrief-root");
            Render(root);
        }
        void Render(VisualElement root)
        {
            var s = GameState.Instance;
            if (!DebriefDebug.IsCurrent) DebriefDebug.RunMission();
            var ops = DebriefDebug.LastOps; var score = DebriefDebug.LastScore;
            var page = new ScrollView(); page.AddToClassList("debrief-page"); root.Add(page);
            page.Add(new Label("T-MINUS  /  MISSION DEBRIEF") { name = "eyebrow" });
            page.Add(new Label($"{s.Data.GetMission(s.CurrentMissionId)?.name ?? "Mission"}  ·  {new string('★', score.stars)}{new string('☆', 3 - score.stars)}") { name = "title" });
            page.Add(new Label($"{score.total:0.0} / 100 points     ·     {(s.Flight.deployed ? "DEPLOYED" : "NOT DEPLOYED")}     ·     {ops.daysAlive} / {ops.timeline.Count} days alive") { name = "summary" });
            AddGoals(page, s, ops);
            AddNarration(page, s, ops, score);
            AddHistory(page, s, score);
            var grid = new VisualElement(); grid.AddToClassList("debrief-grid"); page.Add(grid);
            var scoreCard = Card("SCORE BREAKDOWN"); grid.Add(scoreCard);
            foreach (var line in score.breakdown) scoreCard.Add(new Label($"{line.label}     {line.points:0.#} / {line.maximum:0.#}"));
            var opsCard = Card("OPERATIONS"); grid.Add(opsCard);
            opsCard.Add(new Label($"Images captured     {ops.imagesTaken}")); opsCard.Add(new Label($"Images downlinked   {ops.imagesDownlinked}"));
            opsCard.Add(new Label($"Data returned       {ops.downlinkedMB:0.#} MB")); opsCard.Add(new Label($"Storage remaining   {ops.storedMB:0.#} MB"));
            opsCard.Add(new Label($"Battery final       {ops.finalSoc:P0}"));
            AddFlightCharts(page, s.Flight);
            page.Add(new Label("STATE OF CHARGE  ·  ORBIT SAMPLES") { name = "section" });
            page.Add(BuildTimeline(ops));
            var reward = Card("SCIENCE REWARD  /  LAKE MANCHAR");
            reward.Add(new Label(ops.rewardPlaceholder));
            reward.Add(RewardGallery.Build(s.Data.GetMission(s.CurrentMissionId), ops));
            var credit = new Label(ops.rewardCredit); credit.AddToClassList("muted"); reward.Add(credit); page.Add(reward);
            AddDesignCompare(page);
            page.Add(new Label("WHAT HAPPENED") { name = "section" });
            var events = s.Events.All.Where(e => e.system == "operations" || (e.system == "flight" && (e.severity >= Severity.Warning || e.code == "orbit_achieved" || e.code == "cubesat_deployed")))
                .OrderBy(e => e.time).TakeLast(12);
            foreach (var e in events) page.Add(new Label($"{When(e, s.Flight.flightTimeS)}  {e.message}" + (string.IsNullOrEmpty(e.causeId) ? "" : $"  ← {e.causeId}")));
            foreach (var f in ops.failures) page.Add(new Label("• " + f));
            page.Add(new Label("WHAT TO CHANGE  ·  TOP FIXES") { name = "section" });
            foreach (var fix in score.fixes) page.Add(new Label("✓  " + fix));
            var buttons = new VisualElement(); buttons.AddToClassList("debrief-buttons"); page.Add(buttons);
            var why = WhyTarget.For(s.Flight, s.Events.All, s.Data.GetRules());
            if (why != null)
                AddButton(buttons, "Show me why", () => { FocusRequest.Set(why.scene, why.target, why.note); SceneFlow.Load(why.scene); });
            AddButton(buttons, "Redesign", () => { s.BeginAttempt(); SceneFlow.Load(SceneNames.MissionBriefing); });
            AddButton(buttons, "Fly again", () => { s.BeginAttempt(); DebriefDebug.ResetRun(); SceneFlow.Load(SceneNames.Flight); });
            AddButton(buttons, "Save design code", SaveCode);
            AddButton(buttons, "Leaderboard", ShowLeaderboard);
            AddButton(buttons, "Share result", () => ShareResult(s, score, ops));
            AddButton(buttons, "Export results (CSV)", ExportCsv);
            AddButton(buttons, "Main menu", () => SceneFlow.Load(SceneNames.MainMenu));
            _status = new Label(""); _status.AddToClassList("muted"); page.Add(_status);
        }
        /// <summary>Operations events carry mission-elapsed seconds (flight end + days); show them as days, flight events as T+ seconds.</summary>
        static string When(LogEvent e, double flightEndS)
        {
            if (e.system != "operations") return $"T+{e.time:0} s";
            double days = (e.time - flightEndS) / 86400.0;
            return days < 0.5 ? "On deployment" : $"Day {Math.Round(days):0}";
        }
        /// <summary>
        /// HOUSTON's summary card (audit F22): the offline three sentences at once, replaced by the AI's wording of the same facts when
        /// HOUSTON is on and the proxy answers. If the AI is unavailable the offline text simply stays.
        /// </summary>
        void AddNarration(VisualElement page, GameState s, OpsReport ops, MissionScore score)
        {
            if (s.Settings.houstonMode == HoustonMode.Off) return;
            var mission = s.Data.GetMission(s.CurrentMissionId);
            var budget = SpacecraftBudget.Compute(s.Spacecraft, s.Data, mission, s.Data.GetRocket(s.Vehicle.rocketId));
            var goals = MissionGoals.Evaluate(mission, s.Flight, ops, budget);
            var card = Card("HOUSTON'S SUMMARY"); page.Add(card);
            string offline = DebriefNarration.Offline(mission?.name ?? "The mission", s.Flight, ops, score, goals);
            var text = new Label(offline); text.style.whiteSpace = WhiteSpace.Normal; card.Add(text);
            if (s.Settings.houstonMode != HoustonMode.On || DemoHooks.NarrateDebrief == null) return;
            var events = s.Events.All.Where(e => e.system == "operations" || e.system == "flight" || e.system == "Pad").OrderBy(e => e.time).TakeLast(14);
            FetchAiSummary(text, DebriefNarration.TimelineText(events, s.Flight.flightTimeS), DebriefNarration.BreakdownText(score));
        }

        async void FetchAiSummary(Label label, string timeline, string breakdown)
        {
            try
            {
                string answer = await DemoHooks.NarrateDebrief(timeline, breakdown);
                // The client returns "Mission complete. ..." when the AI is off or unreachable: keep the offline summary then.
                if (string.IsNullOrWhiteSpace(answer) || answer.StartsWith("Mission complete.") || label.panel == null) return;
                label.text = answer.Trim() + "  (AI summary)";
            }
            catch (Exception) { /* offline text stays */ }
        }

        /// <summary>Records this flight in the mission log and compares it with the last attempt (the Judge Mode demo is not recorded).</summary>
        void AddHistory(VisualElement page, GameState s, MissionScore score)
        {
            if (JudgeMode.Active != null) return;
            var entry = new LogEntry
            {
                missionId = s.CurrentMissionId, score = score.total, stars = score.stars, outcome = s.Flight.outcome,
                cause = s.Flight.failureCause, code = s.Flight.failureCode
            };
            var previous = MissionLog.RecordOnce(s.Flight, entry);
            var best = MissionLog.Best(s.CurrentMissionId);
            int attempts = MissionLog.Count(s.CurrentMissionId);
            double bestScore = best != null ? best.score : score.total;
            string line = previous == null
                ? $"First recorded attempt on this mission. Best so far: {score.total:0.0} points."
                : $"Last attempt: {previous.score:0.0} points ({MissionLog.StarText(previous.stars)}). This attempt: {score.total:0.0} ({(score.total >= previous.score ? "+" : "")}{score.total - previous.score:0.0}). Best: {bestScore:0.0}. Attempts: {attempts}.";
            var label = new Label(line); label.AddToClassList("muted");
            page.Add(label);
        }

        /// <summary>Altitude, speed and dynamic pressure over the flight, with max-Q in orange and a failed end in red (feature F9).</summary>
        static void AddFlightCharts(VisualElement page, FlightRecord f)
        {
            if (f == null || !f.flown || f.telemetry == null || f.telemetry.Count < 6) return;
            page.Add(new Label("THE FLIGHT  ·  ALTITUDE, SPEED, DYNAMIC PRESSURE") { name = "section" });
            bool failed = !f.orbitAchieved;
            page.Add(Bars("Altitude (km)", f, p => p.altitudeKm, "km", failed));
            page.Add(Bars("Speed (m/s)", f, p => p.speedMs, "m/s", failed));
            page.Add(Bars("Dynamic pressure (kPa). Orange bar = max-Q", f, p => p.qKPa, "kPa", failed));
        }

        static VisualElement Bars(string title, FlightRecord f, Func<TelemetryPoint, float> value, string unit, bool failed)
        {
            var wrap = new VisualElement();
            wrap.Add(new Label(title));
            var graph = new VisualElement(); graph.AddToClassList("soc-timeline");
            float max = Math.Max(1e-3f, f.telemetry.Max(value));
            int step = Math.Max(1, f.telemetry.Count / 40);
            for (int i = 0; i < f.telemetry.Count; i += step)
            {
                var p = f.telemetry[i];
                var bar = new VisualElement(); bar.AddToClassList("soc-bar");
                bar.style.height = Length.Percent(Mathf.Clamp(10 + 90 * value(p) / max, 10, 100));
                bar.tooltip = $"T+{p.t:0} s: {value(p):0.#} {unit}";
                if (Math.Abs(p.t - f.maxQTimeS) <= step) bar.style.backgroundColor = new Color(1f, 0.55f, 0.1f);
                else if (failed && i + step >= f.telemetry.Count) bar.style.backgroundColor = new Color(0.9f, 0.25f, 0.2f);
                graph.Add(bar);
            }
            wrap.Add(graph);
            return wrap;
        }

        void AddGoals(VisualElement page, GameState s, OpsReport ops)
        {
            var mission = s.Data.GetMission(s.CurrentMissionId);
            var budget = SpacecraftBudget.Compute(s.Spacecraft, s.Data, mission, s.Data.GetRocket(s.Vehicle.rocketId));
            var goals = MissionGoals.Evaluate(mission, s.Flight, ops, budget);
            if (goals.Count == 0) return;
            var card = Card($"MISSION GOALS  ·  {goals.Count(g => g.met)} of {goals.Count} met"); page.Add(card);
            foreach (var g in goals) card.Add(new Label($"{(g.met ? "✓" : "✗")}  {g.text}     {g.measured}"));
        }
        static VisualElement Card(string heading) { var c = new VisualElement(); c.AddToClassList("debrief-card"); c.Add(new Label(heading)); return c; }
        void AddDesignCompare(VisualElement page)
        {
            var card = Card("COMPARE ANOTHER DESIGN");
            card.Add(new Label("Paste a TM1 design code to compare spacecraft budgets for this mission."));
            var input = new TextField { multiline = true }; input.AddToClassList("design-code-input"); card.Add(input);
            var results = new VisualElement(); card.Add(results);
            var button = new Button(() =>
            {
                results.Clear();
                if (!DesignCompare.TryCompare(GameState.Instance, input.value, out var c, out var error))
                { results.Add(new Label(error)); return; }
                results.Add(new Label(c.missionName + "  ·  current → shared design"));
                results.Add(new Label(DesignCompare.Metric("Mass", c.current.massKg, c.candidate.massKg, " kg", "lower")));
                results.Add(new Label(DesignCompare.Metric("Mission cost", c.current.totalCostM, c.candidate.totalCostM, " M USD", "lower")));
                results.Add(new Label(DesignCompare.Metric("Power margin", c.current.marginW, c.candidate.marginW, " W")));
                results.Add(new Label(DesignCompare.Metric("Battery", c.current.batteryWh, c.candidate.batteryWh, " Wh")));
                results.Add(new Label(DesignCompare.Metric("Images predicted", c.current.imagesDownlinkedMission, c.candidate.imagesDownlinkedMission, " images")));
                results.Add(new Label(DesignCompare.Metric("Pointing quality", c.current.pointingQuality, c.candidate.pointingQuality, "")));
                results.Add(new Label(DesignCompare.Metric("Ground sample distance", c.current.gsdM, c.candidate.gsdM, " m", "lower")));
                results.Add(new Label($"Power {DesignCompare.Status(c.current.power)} → {DesignCompare.Status(c.candidate.power)}   ·   Data {DesignCompare.Status(c.current.data)} → {DesignCompare.Status(c.candidate.data)}   ·   Pointing {DesignCompare.Status(c.current.pointing)} → {DesignCompare.Status(c.candidate.pointing)}"));
            }) { text = "Compare design code" };
            button.AddToClassList("debrief-button"); card.Add(button); page.Add(card);
        }
        static VisualElement BuildTimeline(OpsReport o)
        {
            var graph = new VisualElement(); graph.AddToClassList("soc-timeline");
            foreach (var p in o.timeline.Where((_, i) => i % Math.Max(1, o.timeline.Count / 30) == 0 || _.day == o.timeline.Last().day))
            {
                var bar = new VisualElement(); bar.AddToClassList("soc-bar"); bar.style.height = Length.Percent((float)(10 + 90 * p.stateOfCharge));
                bar.tooltip = $"Day {p.day}: SOC {p.stateOfCharge:P0}, {p.imagesDownlinked} images downlinked"; graph.Add(bar);
            }
            return graph;
        }
        static void AddButton(VisualElement parent, string text, Action action)
        { var b = new Button(action) { text = text }; b.AddToClassList("debrief-button"); parent.Add(b); }
        const string PlayUrl = "https://ayaan-kashif.github.io/NASA-PROJECT/";

        /// <summary>A short text you can paste into a chat (audit F15). An image card needs a screenshot pipeline; the text carries the same facts.</summary>
        void ShareResult(GameState s, MissionScore score, OpsReport ops)
        {
            var mission = s.Data.GetMission(s.CurrentMissionId);
            string orbit = s.Flight.orbitAchieved ? $"orbit {s.Flight.periapsisKm:0} x {s.Flight.apoapsisKm:0} km" : "did not reach orbit";
            string text = $"T-MINUS · {mission?.name ?? "mission"}: {MissionLog.StarText(score.stars)}  {score.total:0.0}/100 · {orbit} · {ops.imagesDownlinked} images downlinked · {ops.daysAlive} days alive. " +
                          $"Build your own: {PlayUrl}";
            ShowText(text, "Result copied. Paste it anywhere: ");
        }

        void ExportCsv()
        {
            string names = string.Join(" + ", GameState.Instance.Crew.Select(c => c.name).Distinct());
            ShowText(MissionLog.ToCsv(names), "Results copied (CSV): ");
        }

        /// <summary>Copy to the clipboard; the browser blocks that, so there the text is shown to select and copy.</summary>
        void ShowText(string text, string prefix)
        {
            GUIUtility.systemCopyBuffer = text;
            _status.text = Application.platform == RuntimePlatform.WebGLPlayer ? "Select and copy: " + text : prefix + text;
            _status.selection.isSelectable = true; _status.style.whiteSpace = WhiteSpace.Normal;
        }

        void SaveCode()
        {
            string code = DesignCode.Encode(DesignCode.FromState(GameState.Instance));
            GUIUtility.systemCopyBuffer = code;
            // Browsers block clipboard writes from the game, so show the code to select and copy there.
            _status.text = Application.platform == RuntimePlatform.WebGLPlayer ? "Your design code (select and copy): " + code : "Design code copied to clipboard.";
            _status.selection.isSelectable = true; _status.style.whiteSpace = WhiteSpace.Normal;
        }
        void ShowLeaderboard()
        {
            var rows = Leaderboard.Load();
            // One row per flight: pressing the button again only re-shows the table (audit A-09).
            if (!_savedToBoard)
            {
                _savedToBoard = true;
                string who = string.Join(" + ", GameState.Instance.Crew.Select(c => c.name).Distinct());
                rows.Add(new LeaderboardEntry { name = string.IsNullOrWhiteSpace(who) ? "Player" : who, score = DebriefDebug.LastScore.total, code = DesignCode.Encode(DesignCode.FromState(GameState.Instance)) });
                Leaderboard.Save(rows.OrderByDescending(x => x.score).Take(10).ToList());
            }
            rows = Leaderboard.Load().OrderByDescending(x => x.score).Take(10).ToList();
            _status.text = "LOCAL TOP 10 (this computer only)\n" + string.Join("\n", rows.Select((x, i) => $"{i + 1}. {x.name}  {x.score:0.0}"));
        }
    }
}
