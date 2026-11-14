using System.Linq;
using TMinus.Core;
using TMinus.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.Mission
{
    /// <summary>
    /// The job sheet for a seeded-fault Failure Lab job: a bar across scenes showing faults fixed (live from
    /// GameState.Vehicle), and a job debrief when the player hands the rocket back.
    /// </summary>
    public sealed class FailureJobTracker : MonoBehaviour
    {
        public static FailureJobTracker Active { get; private set; }

        MissionDef _job;
        int _hintsAtStart;
        float _startTime, _nextCheck;
        bool _leftMenu;
        bool _replaced;
        Label _status;
        VisualElement _root;

        public static void Start(MissionDef job)
        {
            if (Active != null) { Active._replaced = true; Destroy(Active.gameObject); }   // the new job takes over the saved-build hand-back
            var go = new GameObject("[FailureJob]");
            DontDestroyOnLoad(go);
            Active = go.AddComponent<FailureJobTracker>();
            Active._job = job;
            Active._hintsAtStart = HintsUsed();
            Active._startTime = Time.unscaledTime;
        }

        static int HintsUsed() => GameState.Instance?.Settings.hintsUsed?.Sum() ?? 0;

        void Awake()
        {
            var doc = UiDocs.Create("[FailureJob UI]", transform, 880);
            if (doc == null) return;
            doc.gameObject.SetActive(true);
            _root = doc.rootVisualElement; _root.pickingMode = PickingMode.Ignore;
            var bar = new VisualElement();
            bar.style.position = Position.Absolute; bar.style.top = 110; bar.style.left = Length.Percent(30); bar.style.right = Length.Percent(30);
            bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center; bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.backgroundColor = new Color(0.05f, 0.08f, 0.17f, 0.94f);
            bar.style.borderTopWidth = 3; bar.style.borderTopColor = new Color(1f, 0.48f, 0.1f);
            bar.style.paddingLeft = 16; bar.style.paddingRight = 8; bar.style.paddingTop = 8; bar.style.paddingBottom = 8;
            bar.style.borderBottomLeftRadius = 10; bar.style.borderBottomRightRadius = 10;
            _status = new Label(); _status.style.color = Color.white; _status.style.fontSize = 18; _status.style.whiteSpace = WhiteSpace.Normal; _status.style.flexShrink = 1;
            bar.Add(_status);
            var hand = new Button(HandBack) { text = "Hand back ›" };
            hand.style.fontSize = 18; hand.style.backgroundColor = new Color(1f, 0.48f, 0.1f); hand.style.color = Color.white;
            bar.Add(hand);
            _root.Add(bar);
        }

        int FixedCount(out RulesDef rules)
        {
            var s = GameState.Instance;
            rules = s.Data.GetRules();
            var r = rules;
            return _job.seededFaults.Count(f => FailureLab.IsFixed(f, s.Vehicle, r));
        }

        void Update()
        {
            if (_job == null || _status == null || Time.unscaledTime < _nextCheck) return;
            // Leaving to the main menu without handing back abandons the job.
            // (Only after we have actually left the menu: the job starts there, during the fade to the VAB.)
            bool inMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.MainMenu;
            if (!inMenu) _leftMenu = true;
            else if (_leftMenu) { Destroy(gameObject); return; }
            else return;
            _nextCheck = Time.unscaledTime + 0.5f;
            int fixedCount = FixedCount(out _);
            int hints = HintsUsed() - _hintsAtStart;
            _status.text = $"JOB · {_job.name.Replace("Failure Lab: ", "")}  ·  faults fixed {fixedCount} / {_job.seededFaults.Count}  ·  HOUSTON hints {hints}\n" +
                           "Bolts: VAB bolt tool. Cables: Avionics Bay (from the VAB). HOUSTON's Scan helps, at a cost.";
        }

        /// <summary>Points: reward for each fixed fault, minus 10 per HOUSTON hint and 50 per fault left in.</summary>
        public static int Score(int reward, int faults, int fixedCount, int hints) =>
            Mathf.Max(0, Mathf.RoundToInt(reward * (faults == 0 ? 1f : fixedCount / (float)faults)) - 10 * hints - 50 * (faults - fixedCount));

        void HandBack()
        {
            var s = GameState.Instance;
            int fixedCount = FixedCount(out var rules);
            int hints = HintsUsed() - _hintsAtStart;
            int score = Score(_job.rewardPoints, _job.seededFaults.Count, fixedCount, hints);
            float minutes = (Time.unscaledTime - _startTime) / 60f;
            s.Events.Add("failure_lab", "job_handed_back", $"{_job.id}: {fixedCount}/{_job.seededFaults.Count} faults fixed, {hints} hints, {score} pts", _job.id,
                         fixedCount == _job.seededFaults.Count ? Severity.Info : Severity.Warning);

            _root.Clear();
            var shade = new VisualElement(); shade.style.position = Position.Absolute; shade.style.left = 0; shade.style.right = 0; shade.style.top = 0; shade.style.bottom = 0;
            shade.style.backgroundColor = new Color(0.02f, 0.04f, 0.1f, 0.93f); shade.style.alignItems = Align.Center; shade.style.justifyContent = Justify.Center;
            var card = new VisualElement(); card.style.width = Length.Percent(60); card.style.backgroundColor = new Color(0.06f, 0.09f, 0.18f);
            card.style.paddingLeft = 32; card.style.paddingRight = 32; card.style.paddingTop = 24; card.style.paddingBottom = 24;
            card.style.borderTopWidth = 4; card.style.borderTopColor = new Color(1f, 0.48f, 0.1f);
            shade.Add(card);
            Line(card, "JOB DEBRIEF · " + _job.name.Replace("Failure Lab: ", ""), 34, true);
            Line(card, $"{fixedCount} of {_job.seededFaults.Count} faults fixed in {minutes:0.0} min · {hints} HOUSTON hints · {score} / {_job.rewardPoints} pts", 20, false);
            foreach (var f in _job.seededFaults)
            {
                bool ok = FailureLab.IsFixed(f, s.Vehicle, rules);
                Line(card, (ok ? "✓ FIXED   " : "✗ STILL BROKEN   ") + FailureLab.Describe(f, rules), 18, false, ok ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f));
            }
            Line(card, "How you found them: " + (hints == 0 ? "on your own, with the inspection tools." : $"with {hints} HOUSTON hint{(hints > 1 ? "s" : "")}."), 18, false);
            Line(card, "The lesson: " + _job.lesson, 18, false);
            var back = new Button(() => { Destroy(gameObject); SceneFlow.Load(SceneNames.MainMenu); }) { text = "Back to the job board" };
            back.style.fontSize = 20; back.style.marginTop = 16; back.style.backgroundColor = new Color(1f, 0.48f, 0.1f); back.style.color = Color.white;
            card.Add(back);
            _root.pickingMode = PickingMode.Position;
            _root.Add(shade);
        }

        static void Line(VisualElement parent, string text, int size, bool bold, Color? color = null)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 8;
            l.style.color = color ?? Color.white;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(l);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (!_replaced) SaveService.EndScript(GameState.Instance);   // job over or abandoned: give the player's build back
        }
    }
}
