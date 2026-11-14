using System;
using System.Collections.Generic;
using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.UI
{
    /// <summary>Main-menu overlays built in code: Codex (every "why" in the game), Settings and the Sandbox scene picker.</summary>
    public static class MenuPanels
    {
        static VisualElement Overlay(VisualElement root, string title, out VisualElement body)
        {
            root.Q("menu-overlay")?.RemoveFromHierarchy();
            var overlay = new VisualElement { name = "menu-overlay" };
            overlay.AddToClassList("overlay");
            var card = new VisualElement(); card.AddToClassList("overlay-card"); overlay.Add(card);
            var head = new VisualElement(); head.AddToClassList("overlay-head"); card.Add(head);
            var t = new Label(title); t.AddToClassList("overlay-title"); head.Add(t);
            var close = new Button(() => overlay.RemoveFromHierarchy()) { text = "Close  ✕" };
            close.AddToClassList("overlay-close"); head.Add(close);
            body = new VisualElement(); body.AddToClassList("overlay-body"); card.Add(body);
            overlay.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) overlay.RemoveFromHierarchy(); });
            root.Add(overlay);
            return overlay;
        }

        // ---------------------------------------------------------------- Codex

        public static void ShowCodex(VisualElement root)
        {
            var data = GameState.Instance?.Data;
            Overlay(root, "SPACE GUIDE", out var body);
            var entries = data?.Codex.OrderBy(c => c.title).ToList() ?? new List<CodexEntry>();
            var search = new TextField { label = "Search" }; search.AddToClassList("codex-search"); body.Add(search);
            var count = new Label(); count.AddToClassList("muted"); body.Add(count);
            var list = new ScrollView(); list.AddToClassList("codex-list"); body.Add(list);

            void Fill(string q)
            {
                list.Clear();
                var shown = entries.Where(c => string.IsNullOrWhiteSpace(q) ||
                    (c.title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.shortText ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                count.text = $"{shown.Count} of {entries.Count} topics. Every number in the game links to one of these.";
                foreach (var c in shown)
                {
                    var f = new Foldout { text = c.title ?? c.id, value = false }; f.AddToClassList("codex-item");
                    var s = new Label(c.shortText); s.AddToClassList("codex-short"); f.Add(s);
                    if (!string.IsNullOrEmpty(c.longText)) { var l = new Label(c.longText); l.AddToClassList("codex-long"); f.Add(l); }
                    if (!string.IsNullOrEmpty(c.sourceUrl))
                    {
                        var src = new Button(() => Application.OpenURL(c.sourceUrl)) { text = "Source: " + c.sourceUrl };
                        src.AddToClassList("codex-source"); f.Add(src);
                    }
                    list.Add(f);
                }
            }
            search.RegisterValueChangedCallback(e => Fill(e.newValue));
            Fill("");
        }

        // ---------------------------------------------------------------- Settings

        public static void ShowSettings(VisualElement root)
        {
            var s = GameState.Instance?.Settings;
            if (s == null) return;
            Overlay(root, "SETTINGS", out var body);

            var houston = new RadioButtonGroup("HOUSTON assistant", new List<string>
                { "On: advice, scans and AI chat", "Hints only: buttons, no AI chat", "Off: fly solo (best decision score)" })
                { value = (int)s.houstonMode };
            var lang = new RadioButtonGroup("HOUSTON answer language", new List<string> { "English", "Urdu (experimental: needs the AI and an Urdu-capable font)" })
                { value = s.language == "ur" ? 1 : 0 };
            var volume = new Slider("Volume", 0, 1) { value = SettingsStore.Volume };
            var full = new Toggle("Fullscreen") { value = Screen.fullScreen };
            // Browsers only allow fullscreen from their own control, so the toggle is hidden in the web build.
            if (Application.platform == RuntimePlatform.WebGLPlayer) full.style.display = DisplayStyle.None;
            foreach (var e in new VisualElement[] { houston, lang, volume, full }) { e.AddToClassList("settings-row"); body.Add(e); }
            var note = new Label("Saved automatically. HOUSTON's hints count toward decision quality: fewer hints, higher score.");
            note.AddToClassList("muted"); body.Add(note);
            // The Helpful / Not helpful buttons on HOUSTON's messages used to count in the dark; show the totals here.
            int up = PlayerPrefs.GetInt("houston.helpful", 0), down = PlayerPrefs.GetInt("houston.unhelpful", 0);
            if (up + down > 0)
            {
                var rated = new Label($"You rated HOUSTON's messages: {up} helpful, {down} not helpful ({100 * up / (up + down)}% helpful).");
                rated.AddToClassList("muted"); body.Add(rated);
            }

            void Save()
            {
                s.houstonMode = (HoustonMode)Mathf.Clamp(houston.value, 0, 2);
                s.language = lang.value == 1 ? "ur" : "en";
                SettingsStore.Save(s, volume.value, full.value);
            }
            houston.RegisterValueChangedCallback(_ => Save());
            lang.RegisterValueChangedCallback(_ => Save());
            volume.RegisterValueChangedCallback(_ => Save());
            full.RegisterValueChangedCallback(_ => Save());
        }

        // ---------------------------------------------------------------- Sandbox

        public static void ShowSandbox(VisualElement root)
        {
            Overlay(root, "SANDBOX", out var body);
            var intro = new Label("Free build: open any workshop directly, no briefing, no budget and no mass limit. Your designs carry over between rooms.");
            intro.AddToClassList("muted"); body.Add(intro);
            Room(body, "Integration Bay", "Build and wire the 3U CubeSat.", SceneNames.IntegrationBay);
            Room(body, "Vehicle Assembly Building", "Stack, bolt and wire Pathfinder. Inspect Saturn V.", SceneNames.VAB);
            Room(body, "Avionics Bay", "Wire the flight computer, batteries and pyro circuits.", SceneNames.AvionicsBay);
            Room(body, "Launch pad", "Fly your current design (fails honestly if it isn't ready).", SceneNames.Flight);
        }

        static void Room(VisualElement body, string name, string what, string scene)
        {
            var b = new Button(() =>
            {
                // The sandbox mission has no budget and no mass limit, so any build can be tried (audit I14).
                var gs = GameState.Instance;
                if (gs != null) gs.CurrentMissionId = "sandbox";
                SceneFlow.Load(scene);
            }) { text = $"{name}\n<size=18>{what}</size>" };
            b.AddToClassList("menu-button"); b.AddToClassList("sandbox-room");
            body.Add(b);
        }
    }
}

namespace TMinus.UI
{
    /// <summary>Agent/play-script hook: MenuDebug.Open codex|settings|sandbox.</summary>
    public static class MenuDebug
    {
        public static string Open(string which)
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            var root = menu != null ? menu.GetComponent<UIDocument>().rootVisualElement : null;
            if (root == null) return "no main menu";
            switch (which)
            {
                case "codex": MenuPanels.ShowCodex(root); break;
                case "settings": MenuPanels.ShowSettings(root); break;
                default: MenuPanels.ShowSandbox(root); break;
            }
            return "opened " + which;
        }
    }
}
