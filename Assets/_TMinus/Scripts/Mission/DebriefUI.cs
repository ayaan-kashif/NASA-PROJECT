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
        LogEntry _thisAttempt;
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
            bool flightOnly = !MissionKinds.IsCubeSat(s.Data.GetMission(s.CurrentMissionId));
            page.Add(new Label(flightOnly
                ? $"{score.total:0.0} / 100 points     ·     {(s.Flight.outcome == "Apogee" ? $"APOGEE {s.Flight.apoapsisKm:0} km" : s.Flight.orbitAchieved ? $"ORBIT {s.Flight.periapsisKm:0} x {s.Flight.apoapsisKm:0} km" : "NO ORBIT")}     ·     {s.Flight.deltaVLeftMs:N0} m/s left"
                : $"{score.total:0.0} / 100 points     ·     {(s.Flight.deployed ? "DEPLOYED" : "NOT DEPLOYED")}     ·     {ops.daysAlive} / {ops.timeline.Count} days alive") { name = "summary" });
            AddGoals(page, s, ops);
            AddNarration(page, s, ops, score);
            AddHistory(page, s, score);
            var grid = new VisualElement(); grid.AddToClassList("debrief-grid"); page.Add(grid);
            var scoreCard = Card("SCORE BREAKDOWN"); grid.Add(scoreCard);
            foreach (var line in score.breakdown) scoreCard.Add(new Label($"{line.label}     {line.points:0.#} / {line.maximum:0.#}"));
            if (flightOnly)
            {
                var flightCard = Card("FLIGHT"); grid.Add(flightCard);
                flightCard.Add(new Label(s.Flight.outcome == "Apogee" ? $"Highest point      {s.Flight.apoapsisKm:0} km" : $"Orbit              {s.Flight.periapsisKm:0} x {s.Flight.apoapsisKm:0} km"));
                flightCard.Add(new Label($"Max-Q              {s.Flight.maxQKPa:0.0} kPa at T+{s.Flight.maxQTimeS:0} s"));
                if (s.Flight.outcome != "Apogee") flightCard.Add(new Label($"Propellant left    {s.Flight.deltaVLeftMs:N0} m/s of delta-v"));
                flightCard.Add(new Label($"Flight time        {s.Flight.flightTimeS:0} s"));
                AddFlightCharts(page, s.Flight);
            }
            else
            {
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
                AddLiveView(page, ops);
            }
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
                AddButton(buttons, Strings.Get("debrief.show_me_why", "Show me why"), () => { FocusRequest.Set(why.scene, why.target, why.note); SceneFlow.Load(why.scene); });
            if (s.Flight.flown && s.Flight.outcome == "Failed" && s.Flight.flightTimeS > 10)
                AddButton(buttons, Strings.Get("debrief.watch_the_failure_again", "Watch the failure again"), () => { DemoHooks.ReplayFailure = true; SceneFlow.Load(SceneNames.Flight); });
            AddButton(buttons, Strings.Get("debrief.redesign", "Redesign"), () => { s.BeginAttempt(); SceneFlow.Load(SceneNames.MissionBriefing); });
            AddButton(buttons, "Fly again", () => { s.BeginAttempt(); DebriefDebug.ResetRun(); SceneFlow.Load(SceneNames.Flight); });
            AddButton(buttons, Strings.Get("debrief.save_design_code", "Save design code"), SaveCode);
            AddButton(buttons, Strings.Get("debrief.leaderboard", "Leaderboard"), ShowLeaderboard);
            AddButton(buttons, Strings.Get("debrief.share_result", "Share result"), () => ShareResult(s, score, ops));
            AddButton(buttons, Strings.Get("debrief.save_picture", "Save picture"), () => StartCoroutine(ShareCard.Save(s.CurrentMissionId, m => { _status.text = m; _status.style.whiteSpace = WhiteSpace.Normal; })));
            AddButton(buttons, Strings.Get("debrief.export_results_csv", "Export results (CSV)"), ExportCsv);
            AddClassSender(page, s);
            AddButton(buttons, Strings.Get("pause.mainMenu", "Main menu"), () => SceneFlow.Load(SceneNames.MainMenu));
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
            string offline = DebriefNarration.Offline(mission?.name ?? "The mission", s.Flight, ops, score, goals, !MissionKinds.IsCubeSat(mission));
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

        /// <summary>The player's chosen region as NASA's satellites see it (audit F26), shown at the detail of the camera that was built.</summary>
        void AddLiveView(VisualElement page, OpsReport ops)
        {
            var region = LiveImagery.Selected;
            if (LiveImagery.IsBundled(region) || JudgeMode.Active != null) return;
            var card = Card("LIVE VIEW FROM NASA  /  " + region.name.ToUpperInvariant()); page.Add(card);
            if (ops.imagesDownlinked <= 0) { card.Add(new Label(Strings.Get("debrief.no_images_came_home_so", "No images came home, so there is no picture of this region to show."))); return; }
            var note = new Label(Strings.Get("debrief.fetching_the_picture_from_nasa", "Fetching the picture from NASA Worldview...")); note.AddToClassList("muted"); note.style.whiteSpace = WhiteSpace.Normal; card.Add(note);
            var pic = new Image { scaleMode = ScaleMode.ScaleToFit }; pic.style.width = RewardGallery.TileWidthPx * 1.4f; pic.style.height = RewardGallery.TileWidthPx * 1.4f;
            card.Add(pic);
            StartCoroutine(LiveImagery.Fetch(region, 512, tex =>
            {
                if (note.panel == null) return;   // the player already left the debrief
                if (tex == null) { note.text = "Could not reach NASA Worldview (you may be offline). The bundled Lake Manchar images above always work."; return; }
                int width = RewardGallery.ResolvedWidth(ops.rewardGsdM, 512);
                pic.image = RewardGallery.Degrade(tex, System.Math.Max(8, System.Math.Min(512, width)));
                note.text = $"Seen at your camera's detail ({ops.rewardGsdM:0.#} m per pixel). The source is MODIS Terra, {LiveImagery.SourceResolutionM:0} m per pixel at best, {LiveImagery.Date}. Credit: NASA EOSDIS Worldview / GIBS.";
            }));
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
            _thisAttempt = entry;
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
            bool failed = !f.orbitAchieved && f.outcome != "Apogee";
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
            card.Add(new Label(Strings.Get("debrief.paste_a_tm1_design_code", "Paste a TM1 design code to compare spacecraft budgets for this mission.")));
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
            }) { text = Strings.Get("debrief.compare_design_code", "Compare design code") };
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

        /// <summary>A short text you can paste into a chat (audit F15). The picture version is the Save picture button (ShareCard).</summary>
        void ShareResult(GameState s, MissionScore score, OpsReport ops)
        {
            var mission = s.Data.GetMission(s.CurrentMissionId);
            string orbit = s.Flight.orbitAchieved ? $"orbit {s.Flight.periapsisKm:0} x {s.Flight.apoapsisKm:0} km" : "did not reach orbit";
            string text = $"T-MINUS · {mission?.name ?? "mission"}: {MissionLog.StarText(score.stars)}  {score.total:0.0}/100 · {orbit} · {ops.imagesDownlinked} images downlinked · {ops.daysAlive} days alive. " +
                          $"Build your own: {PlayUrl}";
            ShowText(text, "Result copied. Paste it anywhere: ");
        }

        /// <summary>
        /// Live class dashboard (audit F20): the student types the class code the teacher's page shows, and this attempt is sent to that room
        /// (a free public relay, see CrewRelay). The teacher page updates by itself. It sends the team name, mission, score and outcome, so it
        /// says so; nothing is sent unless the button is pressed.
        /// </summary>
        void AddClassSender(VisualElement page, GameState s)
        {
            if (_thisAttempt == null) return;   // a Judge Mode demo is not an attempt
            var card = Card("SEND THIS RESULT TO MY CLASS"); page.Add(card);
            var note = new Label(Strings.Get("debrief.your_teacher_s_results_page", "Your teacher's results page shows a class code. Type it here and press the button. This sends your team name, the mission, your score and how the flight ended to anyone who has that code."));
            note.AddToClassList("muted"); note.style.whiteSpace = WhiteSpace.Normal; card.Add(note);
            var field = new TextField { value = PlayerPrefs.GetString("tminus.classCode", "") }; card.Add(field);
            var result = new Label(""); result.AddToClassList("muted"); result.style.whiteSpace = WhiteSpace.Normal;
            var send = new Button(() =>
            {
                if (!CrewRelay.IsValid(field.value)) { result.text = "That is not a class code. It has 12 letters and digits, like K7QF-3MXA-RD2T."; return; }
                PlayerPrefs.SetString("tminus.classCode", CrewRelay.Normalise(field.value)); PlayerPrefs.Save();
                string names = string.Join(" + ", s.Crew.Select(c => c.name).Distinct());
                result.text = "Sending...";
                StartCoroutine(CrewRelay.PublishToTopic(CrewRelay.ClassTopic(field.value), MissionLog.CsvRow(names, _thisAttempt),
                    ok => result.text = ok ? "Sent. Your teacher sees it now." : "Could not reach the relay (are you online?). Use Export results (CSV) instead."));
            }) { text = Strings.Get("debrief.send_result_to_my_class", "Send result to my class") };
            send.AddToClassList("debrief-button");
            card.Add(send); card.Add(result);
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
