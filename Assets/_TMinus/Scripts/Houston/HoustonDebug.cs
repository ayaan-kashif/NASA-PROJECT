#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMinus.Core;
using UnityEngine;

namespace TMinus.Houston
{
    /// <summary>Play-script hooks: HoustonDebug.Ask &lt;question&gt; (starts an online ask), then HoustonDebug.Last.</summary>
    public static class HoustonDebug
    {
        static string _last = "(none yet)";

        public static string Ask(string question)
        {
            var panel = HoustonPanel.Instance;
            var llm = panel != null ? panel.GetComponent<HoustonLLMClient>() : Object.FindFirstObjectByType<HoustonLLMClient>();
            if (llm == null) return "no HOUSTON client in this scene";
            _last = "(waiting)";
            Run(llm, question);
            return $"asked (enabled={llm.IsEnabled})";
        }

        static async void Run(HoustonLLMClient llm, string q)
        {
            var ctx = ContextBuilder.Build(GameState.Instance);
            var rec = AssemblyAdvisor.NextStep(ctx);
            string a = await llm.Ask(q, rec, ctx);
            _last = (llm.LastAnswerOffline ? "[offline] " : "[online] ") + a;
        }

        public static string Last(string _) => _last;
    }
}
#endif
