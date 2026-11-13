using System.Linq;
using TMinus.Assembly;
using TMinus.Core;
using TMinus.Wiring;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Houston
{
    public sealed partial class HoustonPanel
    {
        // ------------------------------------------------------------------ actions

        public void OnNextStep()
        {
            var rec = AssemblyAdvisor.NextStep(ContextBuilder.Build(GameState.Instance));
            AddAssistantMessage("NEXT: " + rec.text, rec.codexId, rec.objectRef);
            CountHint(HintLevel.TextNudge);
        }

        public void OnWhereDoesThisGo()
        {
            var ctx = ContextBuilder.Build(GameState.Instance);
            string held = GetHeldItem();
            if (string.IsNullOrEmpty(held))
            {
                AddAssistantMessage("Select a part (or start a cable from a port) first, then ask where it goes.");
                return;
            }
            var targets = AssemblyAdvisor.WhereDoesThisGo(held, ctx);
            if (targets.Count == 0) { AddAssistantMessage($"Nothing can take {held} right now."); return; }
            AddAssistantMessage("GOES TO: " + targets[0].text, targets[0].codexId, targets[0].objectRef);
            CountHint(HintLevel.Highlight);
        }

        public void OnScan()
        {
            var faults = AssemblyAdvisor.Scan(ContextBuilder.Build(GameState.Instance));
            CountHint(HintLevel.TextNudge);
            if (faults.Count == 0) { AddAssistantMessage("SCAN: 0 problems found here."); return; }
            AddAssistantMessage($"SCAN: {faults.Count} problem{(faults.Count > 1 ? "s" : "")}, most serious first:");
            foreach (var f in faults.Take(8))
            {
                // Text tag + symbol, never colour alone.
                string tag = f.severity == FaultSeverity.Blocker ? "✗ BLOCKER" : f.severity == FaultSeverity.Warning ? "△ WARNING" : "· TIP";
                AddAssistantMessage($"{tag}: {f.what}", f.codexId, f.where);
            }
            if (faults.Count > 8) AddAssistantMessage($"... and {faults.Count - 8} more.");
        }

        public void OnWhy()
        {
            if (_messageLog.Count == 0)
            {
                AddAssistantMessage("Ask for the next step or scan your build first, then I'll explain why.");
                return;
            }
            AskFreeQuestion("Why is this required?");
        }

        void OnAskRequested(string partId, string context)
        {
            if (Off) return;
            SetVisible(true);
            var ctx = ContextBuilder.Build(GameState.Instance);
            if (string.IsNullOrEmpty(partId)) { OnNextStep(); return; }

            // A part that is already installed / mounted: report its own problems instead of "install it".
            bool placed = (ctx.CubeSatParts != null && ctx.CubeSatParts.Exists(p => p.partId == partId || p.instanceId == partId)) ||
                          (ctx.Graph != null && ctx.Graph.PartIdOf(partId) != null && ctx.Graph.IsAttached(partId));
            var def = ctx.Data?.GetPart(partId);
            if (placed)
            {
                var mine = AssemblyAdvisor.Scan(ctx).FindAll(f => f.where != null &&
                    (f.where == partId || f.where.StartsWith(partId + "/") || (ctx.Graph != null && ctx.Rules.boltJoints.Any(j => j.id == f.where && (j.lowerPartId == partId || j.upperPartId == partId)))));
                if (mine.Count > 0) AddAssistantMessage("CHECK: " + mine[0].what, mine[0].codexId, mine[0].where);
                else AddAssistantMessage($"OK: the {def?.name ?? partId} is in place and its checks pass.", def?.codexId, partId);
                return;
            }
            var targets = AssemblyAdvisor.WhereDoesThisGo(partId, ctx);
            if (targets.Count > 0) AddAssistantMessage("GOES TO: " + targets[0].text, targets[0].codexId, targets[0].objectRef);
            else AskFreeQuestion($"Tell me about the {def?.name ?? partId}");
        }

        async void AskFreeQuestion(string question)
        {
            AddUserMessage(question);
            var ctx = ContextBuilder.Build(GameState.Instance);
            var rec = AssemblyAdvisor.NextStep(ctx);
            SetTyping(_llm != null && _llm.IsEnabled);
            string answer = _llm != null ? await _llm.Ask(question, rec, ctx) : rec?.text;
            SetTyping(false);
            bool offline = _llm == null || _llm.LastAnswerOffline;
            AddAssistantMessage(answer + (offline ? "  [offline]" : ""), rec?.codexId, rec?.objectRef);
        }

        // ------------------------------------------------------------------ messages

        void AddUserMessage(string text)
        {
            _messageLog.Add("User: " + text);
            if (_msgList == null) return;
            var item = new VisualElement();
            item.AddToClassList("houston-msg-item");
            item.style.backgroundColor = new Color(0.15f, 0.2f, 0.35f, 0.4f);
            var lbl = new Label("You: " + text);
            lbl.AddToClassList("houston-msg-text");
            lbl.style.color = new Color(0.7f, 0.85f, 1f);
            item.Add(lbl);
            _msgList.Add(item);
            ScrollToBottom();
        }

        void AddAssistantMessage(string text, string codexId = null, string objectRef = null)
        {
            _messageLog.Add("HOUSTON: " + text);
            if (_messageLog.Count > MaxMessages) _messageLog.RemoveAt(0);
            if (_msgList == null) return;

            var item = new VisualElement();
            item.AddToClassList("houston-msg-item");
            var lbl = new Label(text);
            lbl.AddToClassList("houston-msg-text");
            item.Add(lbl);

            var actions = new VisualElement();
            actions.AddToClassList("houston-msg-actions");
            if (!string.IsNullOrEmpty(objectRef))
            {
                var show = new Button(() => ShowMe(objectRef)) { text = "Show me" };
                show.AddToClassList("houston-rating-btn");
                show.style.color = new Color(1, 0.478f, 0.1f);
                actions.Add(show);
            }
            var up = new Button(() => LogFeedback(text, true)) { text = "Helpful" };
            var down = new Button(() => LogFeedback(text, false)) { text = "Not helpful" };
            up.AddToClassList("houston-rating-btn");
            down.AddToClassList("houston-rating-btn");
            actions.Add(up); actions.Add(down);
            item.Add(actions);

            _msgList.Add(item);
            while (_msgList.childCount > MaxMessages) _msgList.RemoveAt(0);
            ScrollToBottom();
        }

        /// <summary>"Show me": frame the part, joint or port the advice is about (highest hint level).</summary>
        void ShowMe(string objectRef)
        {
            CountHint(HintLevel.ShowMe);
            var vab = VabController.Instance;
            if (vab != null && vab.Current != null && vab.orbit != null)
            {
                var part = vab.Current.Find(objectRef);
                if (part != null) { vab.orbit.Focus(part.WorldBounds()); return; }
                var joint = vab.boltTool?.View(objectRef);
                if (joint != null) { vab.orbit.Focus(joint.FlangeBounds()); return; }
            }
            var bay = TMinus.Spacecraft.BayController.Instance;
            if (bay != null && FocusPort(bay.bench, bay.orbit, objectRef)) return;
            var av = AvionicsBayController.Instance;
            if (av != null) FocusPort(av.bench, av.orbit, objectRef);
        }

        static bool FocusPort(WiringBench bench, OrbitCamera orbit, string address)
        {
            if (bench == null || orbit == null || string.IsNullOrEmpty(address)) return false;
            PortView port = address.Contains("/") ? bench.FindPort(address) : null;
            if (port == null && bench.Tiles.TryGetValue(address, out var tile)) port = tile.Ports.FirstOrDefault();
            if (port == null) return false;
            orbit.Focus(new Bounds(port.transform.position, Vector3.one * 0.35f));
            port.SetGlow(new Color(1f, 0.45f, 0.1f));
            return true;
        }

        /// <summary>Thumbs up/down are counted locally (PlayerPrefs) for the pitch: "X% rated helpful".</summary>
        static void LogFeedback(string message, bool helpful)
        {
            string key = helpful ? "houston.helpful" : "houston.unhelpful";
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.Save();
        }

        void SetTyping(bool typing)
        {
            if (_typingLabel == null) return;
            _typingLabel.text = typing ? "HOUSTON is thinking..." : "";
            _typingLabel.EnableInClassList("hidden", !typing);
        }

        void ScrollToBottom()
        {
            var scroll = _msgList?.GetFirstAncestorOfType<ScrollView>();
            scroll?.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, float.MaxValue));
        }

        static string GetHeldItem()
        {
            var vab = VabController.Instance;
            return vab?.selector?.Selected != null ? vab.selector.Selected.instanceId ?? vab.selector.Selected.partId : null;
        }

        static void CountHint(HintLevel level)
        {
            var hints = GameState.Instance?.Settings.hintsUsed;
            if (hints != null && (int)level < hints.Length) hints[(int)level]++;
        }
    }
}
