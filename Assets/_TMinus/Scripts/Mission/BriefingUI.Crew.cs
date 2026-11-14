using System.Linq;
using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// Remote crew by copy and paste (audit F25, no-server form). The host shows a pad code; a teammate on another computer joins with it,
    /// sees only their own role's checklist items, votes, and sends back a vote code that fills the host's GO / NO-GO poll.
    /// </summary>
    public sealed partial class BriefingUI
    {
        bool _joinActive;
        bool _remoteOpen;
        string _liveRoom, _lastRelayId;

        /// <summary>Opens a live room: votes sent to it arrive by themselves (see CrewRelay). Polls every few seconds while this scene is open.</summary>
        void StartLiveRoom()
        {
            _liveRoom = CrewRelay.NewRoomCode();
            _lastRelayId = null;
            StartCoroutine(PollLiveRoom(_liveRoom));
            SendPadToRoom(null);
        }

        /// <summary>Puts the current pad code in the room so a teammate only needs the room code. It is the same text that is copied by hand.</summary>
        void SendPadToRoom(UnityEngine.UIElements.Label status)
        {
            string message = CrewRelay.PadMessage(DesignCode.Encode(DesignCode.FromState(S)));
            if (message == null) { if (status != null) status.text = "This design is too long to send through the room: copy the pad code by hand."; return; }
            StartCoroutine(CrewRelay.Publish(_liveRoom, message, ok => { if (status != null) status.text = ok ? "Pad code sent to the room." : "Could not reach the room service: copy the pad code by hand."; }));
        }

        System.Collections.IEnumerator PollLiveRoom(string room)
        {
            while (_liveRoom == room)
            {
                System.Collections.Generic.List<RelayMessage> messages = null;
                bool answered = false;
                yield return CrewRelay.Poll(room, _lastRelayId, m => { messages = m; answered = true; });
                bool changed = false;
                if (answered && messages != null)
                {
                    var current = DesignCode.FromState(S);
                    foreach (var message in messages)
                    {
                        _lastRelayId = message.id;
                        if (!CrewCodes.TryDecodeVote(message.text, out var vote, out _) || !CrewCodes.Matches(vote, current)) continue;
                        S.Launch.remoteVotes.RemoveAll(v => v.role == vote.role);
                        S.Launch.remoteVotes.Add(vote);
                        Log("RemoteVote", $"{vote.role}: {(vote.go ? "GO" : "NO-GO")} from {vote.name} (live room)", vote.role);
                        changed = true;
                    }
                }
                if (changed && _remoteOpen) ShowRemoteCrew();
                yield return new WaitForSeconds(3f);
            }
        }

        // ------------------------------------------------------------------ host

        void ShowRemoteCrew()
        {
            _modal.Clear();
            _modal.RemoveFromClassList("hidden");
            _remoteOpen = true;
            var panel = new ScrollView(); panel.AddToClassList("pad-panel");
            _modal.Add(panel);
            panel.Add(L("REMOTE CREW", "info-category"));
            panel.Add(L("Send your teammates this pad code in any chat. Each one opens T-MINUS, chooses Join a crew on the mission list, pastes it, checks their own role and sends you back a vote code. Do the pad actions first (pull the pin, arm): a vote only counts for the exact build it was made for.", "check-detail"));

            var current = DesignCode.FromState(S);
            string code = DesignCode.Encode(current);
            var codeBox = new TextField { value = code, isReadOnly = true }; codeBox.AddToClassList("name-field");
            panel.Add(codeBox);
            panel.Add(Btn("Copy pad code", () =>
            {
                GUIUtility.systemCopyBuffer = code;
                Toast(Application.platform == RuntimePlatform.WebGLPlayer ? "Select the code in the box and press Ctrl+C." : "Pad code copied.");
            }, "card-btn"));

            panel.Add(L("LIVE ROOM (optional)", "brief-sub"));
            if (_liveRoom == null)
            {
                panel.Add(L("A live room lets votes arrive by themselves, with no copying back. It uses the free public service ntfy.sh; only crew vote codes travel through it.", "check-detail"));
                panel.Add(Btn("Open a live room", () => { StartLiveRoom(); ShowRemoteCrew(); }, "card-btn"));
            }
            else
            {
                panel.Add(L($"Room code: {_liveRoom}", "check-title"));
                panel.Add(L("Tell your teammates this room code. They type it in Join a crew (the pad code is fetched from the room), and their vote shows up below within a few seconds. If you change your design, send the pad code again.", "check-detail"));
                var padStatus = L("", "check-detail");
                panel.Add(Btn("Send current pad code to the room", () => SendPadToRoom(padStatus), "card-btn"));
                panel.Add(padStatus);
            }

            panel.Add(L("VOTES", "brief-sub"));
            foreach (var role in PollOrder)
            {
                var vote = S.Launch.remoteVotes.LastOrDefault(v => v.role == role.ToString() && CrewCodes.Matches(v, current));
                string state = vote == null ? "waiting, or answered at this computer"
                             : $"{(vote.go ? "GO" : "NO-GO")} from {(string.IsNullOrEmpty(vote.name) ? "a teammate" : vote.name)}";
                panel.Add(L($"{PrelaunchChecklist.RoleName(role)}: {state}", vote == null ? "check-detail" : vote.go ? "check-title" : "poll-warn"));
            }

            panel.Add(L("PASTE A VOTE CODE", "brief-sub"));
            var input = new TextField(); input.AddToClassList("name-field");
            panel.Add(input);
            var result = L("", "check-detail");
            panel.Add(result);
            panel.Add(Btn("Add vote", () =>
            {
                if (!CrewCodes.TryDecodeVote(input.value, out var vote, out var error)) { result.text = error; return; }
                if (!CrewCodes.Matches(vote, DesignCode.FromState(S)))
                {
                    result.text = "That vote is about a different build. Your design changed after you sent the pad code: send the new pad code and ask for the vote again.";
                    return;
                }
                S.Launch.remoteVotes.RemoveAll(v => v.role == vote.role);
                S.Launch.remoteVotes.Add(vote);
                Log("RemoteVote", $"{vote.role}: {(vote.go ? "GO" : "NO-GO")} from {vote.name}", vote.role);
                ShowRemoteCrew();
            }, "card-btn accent"));
            panel.Add(Btn("< Back to checklist", () => { _remoteOpen = false; ShowChecklist(); }, "card-btn"));
        }

        /// <summary>A teammate's vote answers this role's poll step; a NO-GO holds the count like a local one.</summary>
        void ApplyRemoteVote(int index, CrewRole role, CrewVote vote)
        {
            string who = string.IsNullOrEmpty(vote.name) ? "a teammate" : vote.name;
            S.Launch.poll.Add($"{PrelaunchChecklist.RoleName(role)}: {(vote.go ? "GO" : "NO-GO")} (remote, {who})");
            if (!vote.go)
            {
                S.Launch.remoteVotes.Remove(vote);   // used up: the host asks again after fixing the problem
                S.Launch.noGoRounds++;
                Log("NoGo", $"{PrelaunchChecklist.RoleName(role)} called NO-GO from another computer ({who}). Countdown held.", role.ToString());
                Toast($"Hold the count: {who} called NO-GO for {PrelaunchChecklist.RoleName(role)}.");
                ShowChecklist();
                return;
            }
            Log("RemoteGo", $"{PrelaunchChecklist.RoleName(role)} is GO (remote, {who}).", role.ToString());
            AudioFx.Play("beep");
            ShowPollStep(index + 1);
        }

        // ------------------------------------------------------------------ teammate

        void ShowJoinCrew()
        {
            _modal.Clear();
            _modal.RemoveFromClassList("hidden");
            var panel = new ScrollView(); panel.AddToClassList("pad-panel");
            _modal.Add(panel);
            panel.Add(L("JOIN A CREW", "info-category"));
            panel.Add(L("Paste the pad code your host sent, choose your role and type your name. You will see only your own checklist items. Your own build is kept safe and comes back when you finish.", "check-detail"));

            var codeField = new TextField(); codeField.AddToClassList("name-field");
            panel.Add(L("ROOM CODE (from your host: then you do not need the pad code)", "brief-sub"));
            var roomField = new TextField(); roomField.AddToClassList("name-field");
            panel.Add(roomField);
            panel.Add(L("PAD CODE (only if you have no room code)", "brief-sub"));
            panel.Add(codeField);
            panel.Add(L("YOUR NAME", "brief-sub"));
            var nameField = new TextField { value = "" }; nameField.AddToClassList("name-field");
            panel.Add(nameField);

            panel.Add(L("YOUR ROLE", "brief-sub"));
            var chosen = CrewRole.PropulsionStructures;
            var chips = new VisualElement(); chips.AddToClassList("bin-chips");
            var buttons = new System.Collections.Generic.List<(Button b, CrewRole r)>();
            foreach (var role in PollOrder)
            {
                var r = role;
                var b = Btn(PrelaunchChecklist.RoleName(r), null, "bin-chip");
                b.clicked += () => { chosen = r; foreach (var x in buttons) x.b.EnableInClassList("on", x.r == chosen); };
                b.EnableInClassList("on", r == chosen);
                buttons.Add((b, r));
                chips.Add(b);
            }
            panel.Add(chips);

            void Open(string padCode)
            {
                if (!DesignCode.TryDecode(padCode, out var bundle, out var error)) { Toast(error); return; }
                DesignValidator.Clean(bundle, Data);
                if (!_joinActive) { SaveService.BeginScript(S); _joinActive = true; }   // the host's build replaces yours until you press Done
                DesignCode.Apply(bundle, S);
                _joinRoom = CrewRelay.IsValid(roomField.value) ? CrewRelay.Normalise(roomField.value) : null;
                ShowCrewVote(chosen, nameField.value.Trim());
            }
            panel.Add(Btn("Open my checklist", () =>
            {
                if (!string.IsNullOrWhiteSpace(codeField.value)) { Open(codeField.value); return; }
                if (!CrewRelay.IsValid(roomField.value)) { Toast("Type the room code or paste the pad code from your host."); return; }
                Toast("Fetching the pad code from the room...");
                StartCoroutine(CrewRelay.Poll(roomField.value, null, messages =>
                {
                    string pad = CrewRelay.LatestPad(messages);
                    if (pad == null) Toast(messages == null ? "Could not reach the room service. Ask your host for the pad code and paste it." : "No pad code in that room yet. Ask your host to press Send current pad code to the room.");
                    else Open(pad);
                }));
            }, "card-btn accent"));
            panel.Add(Btn("Close", () => _modal.AddToClassList("hidden"), "card-btn"));
        }

        void ShowCrewVote(CrewRole role, string name)
        {
            _modal.Clear();
            var panel = new ScrollView(); panel.AddToClassList("pad-panel");
            _modal.Add(panel);
            panel.Add(L($"YOUR ROLE: {PrelaunchChecklist.RoleName(role).ToUpperInvariant()}", "info-category"));
            var items = PrelaunchChecklist.Build(S).Where(i => i.role == role).ToList();
            int open = items.Count(i => !i.pass);
            panel.Add(L(open == 0 ? "All your items PASS. Vote GO if you agree." : $"{open} of your items FAIL. Tell your host, or vote NO-GO.", "pad-title"));
            foreach (var it in items)
            {
                var row = new VisualElement(); row.AddToClassList("check-row"); row.AddToClassList(it.pass ? "pass" : "fail");
                var text = new VisualElement(); text.style.flexGrow = 1; text.style.flexShrink = 1;
                text.Add(L(it.title, "check-title"));
                text.Add(L(it.detail, "check-detail"));
                row.Add(L(it.pass ? "✓ PASS" : "✗ FAIL", "check-mark"));
                row.Add(text);
                panel.Add(row);
            }
            var bar = new VisualElement(); bar.AddToClassList("poll-buttons");
            bar.Add(Btn("GO", () => ShowVoteCode(role, name, true, open), "poll-go"));
            bar.Add(Btn("NO-GO", () => ShowVoteCode(role, name, false, open), "poll-nogo"));
            panel.Add(bar);
            panel.Add(Btn("Cancel", EndJoin, "card-btn"));
        }

        string _joinRoom;

        void ShowVoteCode(CrewRole role, string name, bool go, int open)
        {
            var vote = new CrewVote { role = role.ToString(), go = go, name = name, designHash = CrewCodes.DesignHash(DesignCode.FromState(S)), openItems = open };
            string code = CrewCodes.EncodeVote(vote);
            _modal.Clear();
            var panel = new ScrollView(); panel.AddToClassList("pad-panel");
            _modal.Add(panel);
            panel.Add(L("YOUR VOTE: " + (go ? "GO" : "NO-GO"), "info-category"));
            panel.Add(L("Copy this vote code and send it to your host.", "pad-title"));
            var box = new TextField { value = code, isReadOnly = true }; box.AddToClassList("name-field");
            panel.Add(box);
            if (_joinRoom != null)
            {
                var status = L("Sending your vote to the live room...", "check-detail");
                panel.Add(status);
                StartCoroutine(CrewRelay.Publish(_joinRoom, code, ok => status.text = ok
                    ? "Sent to the live room: your host sees it now. You can still copy the code below."
                    : "Could not reach the live room. Copy the vote code below and send it by hand."));
            }
            panel.Add(Btn("Copy vote code", () =>
            {
                GUIUtility.systemCopyBuffer = code;
                Toast(Application.platform == RuntimePlatform.WebGLPlayer ? "Select the code in the box and press Ctrl+C." : "Vote code copied.");
            }, "card-btn accent"));
            panel.Add(Btn("Done (restore my own build)", EndJoin, "card-btn"));
        }

        void EndJoin()
        {
            if (_joinActive) { _joinActive = false; SaveService.EndScript(S); }
            _modal.AddToClassList("hidden");
            ShowMissionSelect();
        }
    }
}
