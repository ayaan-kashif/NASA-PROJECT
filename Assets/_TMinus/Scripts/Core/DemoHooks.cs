using System;

namespace TMinus.Core
{
    /// <summary>Loose links to demo features in other assemblies (set at runtime, may be null).</summary>
    public static class DemoHooks
    {
        /// <summary>Starts the scripted Judge Mode demo (set by TMinus.Mission.JudgeMode).</summary>
        public static Action StartJudgeMode;
    }
}
