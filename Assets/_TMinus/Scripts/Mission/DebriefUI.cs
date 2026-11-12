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
            if (DebriefDebug.LastOps == null) DebriefDebug.RunMission();
            var ops = DebriefDebug.LastOps; var score = DebriefDebug.LastScore;
            var page = new ScrollView(); page.AddToClassList("debrief-page"); root.Add(page);
            page.Add(new Label("T-MINUS  /  MISSION DEBRIEF") { name = "eyebrow" });
            page.Add(new Label($"{s.Data.GetMission(s.CurrentMissionId)?.name ?? "Mission"}  ·  {new string('★', score.stars)}{new string('☆', 3 - score.stars)}") { name = "title" });
            page.Add(new Label($"{score.total:0.0} / 100 points     ·     {(s.Flight.deployed ? "DEPLOYED" : "NOT DEPLOYED")}     ·     {ops.daysAlive} / {ops.timeline.Count} days alive") { name = "summary" });
            var grid = new VisualElement(); grid.AddToClassList("debrief-grid"); page.Add(grid);
            var scoreCard = Card("SCORE BREAKDOWN"); grid.Add(scoreCard);
            foreach (var line in score.breakdown) scoreCard.Add(new Label($"{line.label}     {line.points:0.#} / {line.maximum:0.#}"));
            var opsCard = Card("OPERATIONS"); grid.Add(opsCard);
            opsCard.Add(new Label($"Images captured     {ops.imagesTaken}")); opsCard.Add(new Label($"Images downlinked   {ops.imagesDownlinked}"));
            opsCard.Add(new Label($"Data returned       {ops.downlinkedMB:0.#} MB")); opsCard.Add(new Label($"Storage remaining   {ops.storedMB:0.#} MB"));
            opsCard.Add(new Label($"Battery final       {ops.finalSoc:P0}"));
            page.Add(new Label("STATE OF CHARGE  ·  ORBIT SAMPLES") { name = "section" });
            page.Add(BuildTimeline(ops));
            var reward = Card("SCIENCE REWARD  /  LAKE MANCHAR");
            reward.Add(new Label(ops.rewardPlaceholder));
            var credit = new Label(ops.rewardCredit); credit.AddToClassList("muted"); reward.Add(credit); page.Add(reward);
            page.Add(new Label("WHAT HAPPENED") { name = "section" });
            var events = s.Events.All.Where(e => e.system == "operations" || (e.system == "flight" && (e.severity >= Severity.Warning || e.code == "orbit_achieved" || e.code == "cubesat_deployed")))
                .OrderBy(e => e.time).TakeLast(12);
            foreach (var e in events) page.Add(new Label($"T+{e.time:0}s  {e.system}/{e.code}: {e.message}" + (string.IsNullOrEmpty(e.causeId) ? "" : $"  ← {e.causeId}")));
            foreach (var f in ops.failures) page.Add(new Label("• " + f));
            page.Add(new Label("WHAT TO CHANGE  ·  TOP FIXES") { name = "section" });
            foreach (var fix in score.fixes) page.Add(new Label("✓  " + fix));
            var buttons = new VisualElement(); buttons.AddToClassList("debrief-buttons"); page.Add(buttons);
            AddButton(buttons, "Redesign", () => SceneFlow.Load(SceneNames.MissionBriefing));
            AddButton(buttons, "Fly again", () => { DebriefDebug.ResetRun(); SceneFlow.Load(SceneNames.Flight); });
            AddButton(buttons, "Save design code", SaveCode);
            AddButton(buttons, "Leaderboard", ShowLeaderboard);
            _status = new Label(""); _status.AddToClassList("muted"); page.Add(_status);
        }
        static VisualElement Card(string heading) { var c = new VisualElement(); c.AddToClassList("debrief-card"); c.Add(new Label(heading)); return c; }
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
        void SaveCode()
        {
            GUIUtility.systemCopyBuffer = DesignCode.Encode(DesignCode.FromState(GameState.Instance));
            _status.text = "Design code copied to clipboard.";
        }
        void ShowLeaderboard()
        {
            var rows = Leaderboard.Load(); rows.Add(new LeaderboardEntry { name = "Player", score = DebriefDebug.LastScore.total, code = DesignCode.Encode(DesignCode.FromState(GameState.Instance)) });
            rows = rows.OrderByDescending(x => x.score).Take(10).ToList(); Leaderboard.Save(rows);
            _status.text = "LOCAL TOP 10\n" + string.Join("\n", rows.Select((x, i) => $"{i + 1}. {x.name}  {x.score:0.0}"));
        }
    }
    [Serializable] sealed class LeaderboardEntry { public string name, code; public double score; }
    [Serializable] sealed class LeaderboardList { public List<LeaderboardEntry> entries = new List<LeaderboardEntry>(); }
    static class Leaderboard
    {
        const string Key = "TMinus.LocalLeaderboard.v1";
        public static List<LeaderboardEntry> Load() { var json = PlayerPrefs.GetString(Key, ""); return string.IsNullOrEmpty(json) ? new List<LeaderboardEntry>() : JsonUtility.FromJson<LeaderboardList>(json)?.entries ?? new List<LeaderboardEntry>(); }
        public static void Save(List<LeaderboardEntry> rows) { PlayerPrefs.SetString(Key, JsonUtility.ToJson(new LeaderboardList { entries = rows })); PlayerPrefs.Save(); }
    }
}
