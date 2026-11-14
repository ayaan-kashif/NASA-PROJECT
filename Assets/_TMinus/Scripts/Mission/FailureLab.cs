using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// Failure Lab (Prompt 15): repair jobs from real NASA history, like broken-PC jobs. Jobs are missions whose id
    /// starts with "failure_". A job with seededFaults gets a pre-built Pathfinder with those faults and opens the VAB;
    /// a job with consoleChoices runs a scripted console (Apollo 12).
    /// </summary>
    public static class FailureLab
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook() => DemoHooks.OpenFailureLab = ShowBoard;

        public static List<MissionDef> Jobs(DataRegistry data) =>
            data?.Missions.Where(m => m.id.StartsWith("failure_")).OrderBy(m => m.difficulty == "Beginner" ? 0 : 1).ToList() ?? new List<MissionDef>();

        public static void ShowBoard(VisualElement root)
        {
            var s = GameState.Instance;
            root.Q("menu-overlay")?.RemoveFromHierarchy();
            var overlay = new VisualElement { name = "menu-overlay" }; overlay.AddToClassList("overlay");
            var card = new VisualElement(); card.AddToClassList("overlay-card"); overlay.Add(card);
            var head = new VisualElement(); head.AddToClassList("overlay-head"); card.Add(head);
            var title = new Label("FAILURE LAB"); title.AddToClassList("overlay-title"); head.Add(title);
            var close = new Button(() => overlay.RemoveFromHierarchy()) { text = "Close  ✕" }; close.AddToClassList("overlay-close"); head.Add(close);
            var body = new VisualElement(); body.AddToClassList("overlay-body"); card.Add(body);
            var intro = new Label("Repair jobs from real spaceflight history. Find what's broken, fix it, learn why it matters. HOUSTON can help, but every hint costs points.");
            intro.AddToClassList("muted"); body.Add(intro);

            foreach (var job in Jobs(s?.Data))
            {
                var row = new VisualElement(); row.AddToClassList("job-card"); body.Add(row);
                row.Add(new Label($"{job.name.Replace("Failure Lab: ", "")}") { name = "job-title" });
                var meta = new Label($"{job.difficulty}  ·  reward {job.rewardPoints} pts"); meta.AddToClassList("muted"); row.Add(meta);
                var note = new Label("Client note: " + (job.clientNote ?? job.objective)); note.AddToClassList("job-note"); row.Add(note);
                var take = new Button(() => Begin(root, job)) { text = "Take the job ›" };
                take.AddToClassList("menu-button"); take.AddToClassList("primary"); row.Add(take);
            }
            root.Add(overlay);
        }

        public static void Begin(VisualElement root, MissionDef job)
        {
            root?.Q("menu-overlay")?.RemoveFromHierarchy();
            if (job.consoleChoices.Count > 0) { Apollo12Console.Show(root, job); return; }
            var s = GameState.Instance;
            if (FailureJobTracker.Active == null) SaveService.BeginScript(s);   // keep the player's own build; it returns after the job
            DemoBuilds.Apply(s);          // a complete, correct Pathfinder + CubeSat...
            SeedFaults(s.Vehicle, s.Data.GetRules(), job.seededFaults); // ...then break it
            s.CurrentMissionId = "floodwatch_1";
            FailureJobTracker.Start(job);
            SceneFlow.Load(SceneNames.VAB);
        }

        // ---------------------------------------------------------------- faults (pure, tested)

        public static void SeedFaults(VehicleDesign v, RulesDef rules, IEnumerable<string> faults)
        {
            foreach (var f in faults)
            {
                var p = f.Split(':');
                switch (p[0])
                {
                    case "bolts_missing":
                    {
                        var def = rules.boltJoints.First(j => j.id == p[1]);
                        var st = BoltJointState.LoadFrom(def, v);
                        int n = p.Length > 2 ? int.Parse(p[2]) : 1;
                        for (int i = def.boltCount - n; i < def.boltCount; i++) st.Remove(i);
                        st.SaveTo(v);
                        break;
                    }
                    case "unplugged":
                        v.avionicsCables.RemoveAll(c => Matches(c, rules, p[1]));
                        break;
                    case "wrong_port":
                    {
                        var link = rules.avionicsWiring.First(l => l.id == p[1]);
                        v.avionicsCables.RemoveAll(c => Matches(c, rules, p[1]));
                        v.avionicsCables.Add(new CableLink { fromPort = p[2], toPort = link.to, cableType = link.cableType });
                        break;
                    }
                }
            }
        }

        static bool Matches(CableLink c, RulesDef rules, string linkId)
        {
            var l = rules.avionicsWiring.First(x => x.id == linkId);
            return (c.fromPort == l.from && c.toPort == l.to) || (c.fromPort == l.to && c.toPort == l.from);
        }

        /// <summary>True once the player has fixed this seeded fault.</summary>
        public static bool IsFixed(string fault, VehicleDesign v, RulesDef rules)
        {
            var p = fault.Split(':');
            switch (p[0])
            {
                case "bolts_missing":
                    var def = rules.boltJoints.First(j => j.id == p[1]);
                    var st = BoltJointState.LoadFrom(def, v);
                    return st.NextBolt() < 0;
                case "unplugged":
                case "wrong_port":
                    bool ok = AvionicsReport.Compute(v, rules).consequences.All(c => c.linkId != p[1]);
                    // A wrong-port cable must also be taken out, not just a second cable added.
                    return ok && (p[0] != "wrong_port" || !v.avionicsCables.Any(c => c.fromPort == p[2] || c.toPort == p[2]));
            }
            return false;
        }

        public static string Describe(string fault, RulesDef rules)
        {
            var p = fault.Split(':');
            switch (p[0])
            {
                case "bolts_missing": return $"{p[2]} bolts missing on joint {p[1]}. Under load the joint flexes and fails near max-Q.";
                case "unplugged": return $"Cable {p[1]} unplugged: {rules.avionicsWiring.FirstOrDefault(l => l.id == p[1])?.message}";
                case "wrong_port": return $"Cable {p[1]} plugged into {p[2]} instead of {rules.avionicsWiring.FirstOrDefault(l => l.id == p[1])?.from}: {rules.avionicsWiring.FirstOrDefault(l => l.id == p[1])?.message}";
            }
            return fault;
        }
    }
}

namespace TMinus.Mission
{
    /// <summary>Agent/play-script hooks: FailureDebug.Board | Job &lt;id&gt; | Pick &lt;label&gt; | HandBack.</summary>
    public static class FailureDebug
    {
        static UnityEngine.UIElements.VisualElement MenuRoot()
        {
            foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (d.rootVisualElement?.Q("menu-root") != null) return d.rootVisualElement;
            return null;
        }
        public static string Board(string _) { var r = MenuRoot(); if (r == null) return "no menu"; FailureLab.ShowBoard(r); return "board"; }
        public static string Job(string id) { var j = GameState.Instance.Data.GetMission(id); FailureLab.Begin(MenuRoot(), j); return "job " + id; }
        public static string Pick(string label)
        {
            var btn = MenuRoot()?.Query<Button>(className: "a12-switch").ToList().Find(b => b.text == label);
            if (btn == null) return "no switch " + label;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = btn; btn.SendEvent(e); }
            return "picked " + label + " · " + MenuRoot().Q<Label>(className: "a12-feedback")?.text;
        }
        public static string Status(string _)
        {
            var t = FailureJobTracker.Active; var s = GameState.Instance;
            var job = s.Data.GetMission("failure_broken_pathfinder");
            return string.Join(" | ", job.seededFaults.Select(f => f.Split(':')[0] + "=" + FailureLab.IsFixed(f, s.Vehicle, s.Data.GetRules()))) + (t == null ? " (no tracker)" : " (tracker on)");
        }
    }
}
