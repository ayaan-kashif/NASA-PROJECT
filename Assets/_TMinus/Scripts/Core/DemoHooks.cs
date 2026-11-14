using System;

namespace TMinus.Core
{
    /// <summary>Loose links to demo features in other assemblies (set at runtime, may be null).</summary>
    public static class DemoHooks
    {
        /// <summary>Starts the scripted Judge Mode demo (set by TMinus.Mission.JudgeMode).</summary>
        public static Action StartJudgeMode;

        /// <summary>Opens the Failure Lab job board inside a menu root (set by TMinus.Mission.FailureLab).</summary>
        public static Action<UnityEngine.UIElements.VisualElement> OpenFailureLab;

        /// <summary>Main-menu panel to open on the next load ("failure" or "sandbox"), set by the mission list.</summary>
        public static string PendingMenuPanel;

        /// <summary>What the mission-briefing scene shows when it loads: "checklist" (pre-launch checklist, opened from the VAB)
        /// or "briefing" (the current mission's briefing, opened by Continue). Null = the mission list.</summary>
        public static string PendingBriefingPanel;

        /// <summary>A design code that arrived in the page link (#d=...). The briefing puts it in the code box, ready to Load.</summary>
        public static string PendingDesignCode;
    }
}
