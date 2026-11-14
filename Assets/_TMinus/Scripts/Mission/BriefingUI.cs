using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// MissionBriefing scene (Prompt 9): mission select -> briefing (objective, criteria, constraints,
    /// orbit, rocket, launch site, crew, design code) -> pad (checklist, GO/NO-GO poll, countdown).
    /// UI is built in code on a UIDocument; styles in UI/USS/Briefing.uss.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class BriefingUI : MonoBehaviour
    {
        [SerializeField] StyleSheet baseStyle;
        [SerializeField] StyleSheet style;

        public static BriefingUI Instance { get; private set; }

        VisualElement _root, _page, _modal;
        Label _toast;
        IVisualElementScheduledItem _toastHide;
        readonly List<string> _players = new List<string> { "Player 1" };

        GameState S => GameState.Instance;
        DataRegistry Data => S.Data;

        void Awake() => Instance = this;
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_joinActive) { _joinActive = false; SaveService.EndScript(GameState.Instance); }   // never leave autosave switched off
        }

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            foreach (var s in new[] { baseStyle, style }) if (s != null && !_root.styleSheets.Contains(s)) _root.styleSheets.Add(s);
            _root.AddToClassList("brief-root");
            _page = new VisualElement(); _page.AddToClassList("brief-page");
            _root.Add(_page);
            _modal = new VisualElement(); _modal.AddToClassList("modal"); _modal.AddToClassList("hidden");
            _root.Add(_modal);
            var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.AddToClassList("toast-row");
            _toast = new Label(); _toast.AddToClassList("toast"); _toast.AddToClassList("hidden");
            row.Add(_toast); _root.Add(row);
        }

        void Start()
        {
            foreach (var c in S.Crew.Select(c => c.name).Distinct()) if (!_players.Contains(c)) _players.Add(c);
            if (_players.Count > 1 && _players[0] == "Player 1" && !S.Crew.Any(c => c.name == "Player 1")) _players.RemoveAt(0);

            var pending = DemoHooks.PendingBriefingPanel; DemoHooks.PendingBriefingPanel = null;
            if (pending == "checklist") { ShowBriefing(); ShowChecklist(); return; }
            if (pending == "briefing") { ShowBriefing(); return; }
            ShowMissionSelect();
        }

        // ------------------------------------------------------------------ briefing

        public void ShowBriefing()
        {
            var m = Data.GetMission(S.CurrentMissionId);
            if (m == null) { ShowMissionSelect(); return; }
            if (S.Spacecraft.orbitAltitudeKm <= 0) S.Spacecraft.orbitAltitudeKm = m.defaultOrbitKm > 0 ? m.defaultOrbitKm : 550;
            if (m.rocketOptions.Count > 0 && !m.rocketOptions.Contains(S.Vehicle.rocketId)) S.Vehicle.rocketId = m.rocketOptions[0];
            if (m.launchSites.Count > 0 && !m.launchSites.Contains(S.Vehicle.launchSiteId)) S.Vehicle.launchSiteId = m.launchSites[0];

            _page.Clear();
            var head = Header(m.name.ToUpperInvariant(), m.objective);
            head.Add(Btn(Strings.Get("briefing.missions", "< Missions"), ShowMissionSelect, "tool-btn"));
            _page.Add(head);

            var cols = new VisualElement(); cols.AddToClassList("brief-cols");
            _page.Add(cols);

            // Column 1: the story, success criteria, constraints.
            var c1 = Col(cols, "THE MISSION");
            if (!string.IsNullOrEmpty(m.description)) c1.Add(L(m.description, "brief-text"));
            c1.Add(L(Strings.Get("briefing.success_criteria", "SUCCESS CRITERIA"), "brief-sub"));
            foreach (var c in m.successCriteria) c1.Add(L("☐  " + c.text, "brief-item"));
            c1.Add(L(Strings.Get("briefing.constraints", "CONSTRAINTS"), "brief-sub"));
            if (m.budgetMUSD > 0) c1.Add(L($"Budget {m.budgetMUSD:0.00} M$ (launch + spacecraft)", "brief-item"));
            if (m.massLimitKg > 0) c1.Add(L($"3U CubeSat, at most {m.massLimitKg:0.0} kg", "brief-item"));
            if (m.durationDays > 0) c1.Add(L($"{m.durationDays} days of operations, {m.groundContactsPerDay} ground passes/day × {m.contactMinutes:0} min", "brief-item"));

            // Column 2: orbit, rocket, launch site.
            var c2 = Col(cols, "CHOICES");
            if (m.orbitOptionsKm.Count > 0) BuildOrbit(c2, m);
            BuildRockets(c2, m);
            BuildSites(c2, m);
            if (m.rewardImages.Count > 0) BuildTarget(c2);

            // Column 3: crew, flow, design codes.
            var c3 = Col(cols, "CREW");
            BuildCrew(c3);
            c3.Add(L(Strings.Get("briefing.build_and_fly", "BUILD AND FLY"), "brief-sub"));
            if (MissionKinds.IsCubeSat(m)) c3.Add(Btn(Strings.Get("briefing.1_build_the_spacecraft", "1.  Build the spacecraft  >"), () => SceneFlow.Load(SceneNames.IntegrationBay), "flow-btn"));
            c3.Add(Btn(MissionKinds.IsCubeSat(m) ? Strings.Get("briefing.2_build_the_rocket", "2.  Build the rocket  >") : "1.  Inspect the rocket  >", () => SceneFlow.Load(SceneNames.VAB), "flow-btn"));
            c3.Add(Btn(Strings.Get("briefing.3_go_to_the_pad", "3.  Go to the pad  >"), ShowChecklist, "flow-btn accent"));
            BuildDesignCode(c3);
        }

        static readonly Dictionary<double, string> OrbitTradeoff = new Dictionary<double, string>
        {
            [400] = "sharper images, shorter life (more drag)", [550] = "balanced", [800] = "long life, blurrier images",
        };

        void BuildOrbit(VisualElement col, MissionDef m)
        {
            col.Add(L(Strings.Get("briefing.orbit", "ORBIT"), "brief-sub"));
            var chips = new VisualElement(); chips.AddToClassList("bin-chips");
            foreach (var km in m.orbitOptionsKm)
            {
                var k = km;
                var b = Btn($"{km:0} km", () => { S.Spacecraft.orbitAltitudeKm = k; ShowBriefing(); }, "bin-chip");
                b.EnableInClassList("on", System.Math.Abs(S.Spacecraft.orbitAltitudeKm - km) < 1);
                chips.Add(b);
            }
            col.Add(chips);
            OrbitTradeoff.TryGetValue(S.Spacecraft.orbitAltitudeKm, out var t);
            col.Add(L($"{S.Spacecraft.orbitAltitudeKm:0} km: {t}. One orbit takes {SpacecraftBudget.PeriodMinutes(S.Spacecraft.orbitAltitudeKm):0} min.", "brief-text"));
        }

        void BuildRockets(VisualElement col, MissionDef m)
        {
            col.Add(L(Strings.Get("briefing.launch_vehicle", "LAUNCH VEHICLE"), "brief-sub"));
            var report = SpacecraftBudget.Compute(S.Spacecraft, Data, m, Data.GetRocket(S.Vehicle.rocketId));
            foreach (var r in Data.Rockets)
            {
                bool allowed = m.rocketOptions.Contains(r.id);
                bool fits = report.massKg <= r.payloadLeoKg;
                var card = new VisualElement(); card.AddToClassList("choice-card");
                card.EnableInClassList("selected", S.Vehicle.rocketId == r.id);
                card.EnableInClassList("locked", !allowed);
                var top = new VisualElement(); top.AddToClassList("rocket-card-top");
                top.Add(L(r.name, "choice-name"));
                top.Add(L(!allowed ? "NOT FOR THIS MISSION" : fits ? "FITS ✓" : "TOO SMALL ✗", fits && allowed ? "badge" : "badge training"));
                card.Add(top);
                card.Add(L($"{PartStatsText.Mass(r.payloadLeoKg)} to orbit  ·  {r.launchCostMUSD:0.#} M$", "brief-text"));
                var id = r.id;
                card.RegisterCallback<ClickEvent>(_ => { if (allowed) { S.Vehicle.rocketId = id; ShowBriefing(); } else Toast($"{r.name} is for another mission."); });
                col.Add(card);
            }
        }

        /// <summary>Where the satellite looks (audit F26). Lake Manchar is bundled; the others are fetched live from NASA when you are online.</summary>
        void BuildTarget(VisualElement col)
        {
            col.Add(L(Strings.Get("briefing.target_region", "TARGET REGION"), "brief-sub"));
            var chips = new VisualElement(); chips.AddToClassList("bin-chips");
            var selected = LiveImagery.Selected;
            foreach (var r in LiveImagery.Regions)
            {
                var region = r;
                var b = Btn(r.name, () => { LiveImagery.Select(region.id); ShowBriefing(); }, "bin-chip");
                b.EnableInClassList("on", r.id == selected.id);
                chips.Add(b);
            }
            col.Add(chips);
            col.Add(L(LiveImagery.IsBundled(selected)
                ? "The debrief shows the real Landsat pictures of the 2022 Lake Manchar floods."
                : $"The debrief will show a real NASA picture of {selected.name} (MODIS, {LiveImagery.SourceResolutionM:0} m per pixel, {LiveImagery.Date}) if you are online.", "brief-text"));
        }

        void BuildSites(VisualElement col, MissionDef m)
        {
            col.Add(L(Strings.Get("briefing.launch_site", "LAUNCH SITE"), "brief-sub"));
            var rules = Data.GetRules();
            foreach (var id in m.launchSites)
            {
                var site = rules.GetLaunchSite(id);
                if (site == null) continue;
                var card = new VisualElement(); card.AddToClassList("choice-card");
                card.EnableInClassList("selected", S.Vehicle.launchSiteId == id);
                card.Add(L(site.name, "choice-name"));
                card.Add(L($"{site.latitudeDeg:0.0}°N: launching east, Earth's spin gives {PrelaunchChecklist.EarthBoostMs(site.latitudeDeg):0} m/s for free (ω × R × cos latitude).", "brief-text"));
                var sid = id;
                card.RegisterCallback<ClickEvent>(_ => { S.Vehicle.launchSiteId = sid; ShowBriefing(); });
                col.Add(card);
            }
        }

        void BuildCrew(VisualElement col)
        {
            var chips = new VisualElement(); chips.AddToClassList("bin-chips");
            chips.Add(L(Strings.Get("briefing.players", "Players"), "orbit-label"));
            for (int n = 1; n <= 4; n++)
            {
                int count = n;
                var b = Btn(n.ToString(), () =>
                {
                    while (_players.Count < count) _players.Add($"Player {_players.Count + 1}");
                    while (_players.Count > count) _players.RemoveAt(_players.Count - 1);
                    S.Crew = PrelaunchChecklist.AssignRoles(_players);
                    ShowBriefing();
                }, "bin-chip");
                b.EnableInClassList("on", _players.Count == n);
                chips.Add(b);
            }
            col.Add(chips);
            S.Crew = PrelaunchChecklist.AssignRoles(_players);
            for (int i = 0; i < _players.Count; i++)
            {
                int idx = i;
                var field = new TextField { value = _players[i] };
                field.AddToClassList("name-field");
                field.RegisterValueChangedCallback(e => { _players[idx] = e.newValue; S.Crew = PrelaunchChecklist.AssignRoles(_players); });
                var roles = string.Join(" + ", S.Crew.Where(c => c.name == _players[i]).Select(c => PrelaunchChecklist.RoleName(c.role)));
                col.Add(field);
                col.Add(L(roles, "role-line"));
            }
        }

        void BuildDesignCode(VisualElement col)
        {
            col.Add(L(Strings.Get("briefing.design_code", "DESIGN CODE"), "brief-sub"));
            var field = new TextField { value = "" };
            field.AddToClassList("name-field");
            col.Add(field);
            // A design that arrived in the page link (#d=...) waits here, ready to load (audit F16).
            if (!string.IsNullOrEmpty(DemoHooks.PendingDesignCode))
            {
                field.value = DemoHooks.PendingDesignCode;
                DemoHooks.PendingDesignCode = null;
                col.Add(L(Strings.Get("briefing.a_shared_design_came_with", "A shared design came with this link. Press Load code to open it."), "role-line"));
            }
            var row = new VisualElement(); row.AddToClassList("bin-chips");
            if (DesignLink.Available)
                row.Add(Btn(Strings.Get("briefing.copy_link", "Copy link"), () =>
                {
                    string link = DesignLink.Build(DesignCode.Encode(DesignCode.FromState(S)));
                    GUIUtility.systemCopyBuffer = link;
                    field.value = link;
                    Toast(Strings.Get("briefing.the_link_to_your_design", "The link to your design is in the box. Select it and press Ctrl+C, then send it to a friend."));
                }, "bin-chip"));
            row.Add(Btn(Strings.Get("briefing.copy_my_code", "Copy my code"), () =>
            {
                var code = DesignCode.Encode(DesignCode.FromState(S));
                GUIUtility.systemCopyBuffer = code;
                field.value = code;
                // Browsers block clipboard writes from the game: show the code and ask the player to copy it.
                Toast(Application.platform == RuntimePlatform.WebGLPlayer
                    ? "Your design code is in the box. Select it and press Ctrl+C to copy."
                    : "Design code copied. Share it with another team to compare builds.");
            }, "bin-chip"));
            row.Add(Btn(Strings.Get("briefing.load_code", "Load code"), () =>
            {
                if (DesignCode.TryDecode(field.value, out var b, out var err))
                {
                    int dropped = DesignValidator.Clean(b, Data);
                    DesignCode.Apply(b, S);
                    Toast(dropped > 0 ? $"Design loaded. {dropped} invalid item{(dropped > 1 ? "s were" : " was")} ignored." : "Design loaded.");
                    ShowBriefing();
                }
                else Toast(err);
            }, "bin-chip"));
            col.Add(row);
        }

        // ------------------------------------------------------------------ helpers

        VisualElement Header(string title, string sub)
        {
            var h = new VisualElement(); h.AddToClassList("brief-header");
            var t = new VisualElement(); t.style.flexShrink = 1;
            t.Add(L(title, "brief-title"));
            t.Add(L(sub, "brief-subtitle"));
            h.Add(t);
            return h;
        }

        static VisualElement Col(VisualElement parent, string title)
        {
            var c = new ScrollView(); c.AddToClassList("brief-col");
            c.Add(L(title, "info-category"));
            parent.Add(c);
            return c;
        }

        static Label L(string text, string cls)
        {
            var l = new Label(text);
            foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            return l;
        }

        static Button Btn(string text, System.Action a, string cls)
        {
            var b = new Button(a) { text = text };
            foreach (var c in cls.Split(' ')) b.AddToClassList(c);
            return b;
        }

        public void Toast(string message, int ms = 3500)
        {
            _toast.text = message;
            _toast.RemoveFromClassList("hidden");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("hidden")).StartingIn(ms);
        }
    }

    static class PartStatsText
    {
        public static string Mass(double kg) => kg >= 10000 ? $"{kg / 1000:N0} t" : $"{kg:N0} kg";
    }
}
