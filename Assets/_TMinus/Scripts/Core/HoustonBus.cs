using System;

namespace TMinus.Core
{
    /// <summary>
    /// Loose link between scenes and HOUSTON (Track D). Scenes raise requests; the HOUSTON
    /// overlay (Prompt 13) subscribes. Nothing breaks if no one is listening.
    /// </summary>
    public static class HoustonBus
    {
        /// <summary>partId (may be null), scene/context name.</summary>
        public static event Action<string, string> AskRequested;

        public static void Ask(string partId, string context)
        {
            GameState.Instance?.Events.Add("HOUSTON", "AskRequested", $"Asked about {partId ?? "the build"}", partId);
            AskRequested?.Invoke(partId, context);
        }
    }
}
