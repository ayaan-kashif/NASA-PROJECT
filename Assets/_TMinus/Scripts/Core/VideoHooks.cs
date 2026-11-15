#if UNITY_EDITOR
using System;
using System.Reflection;

namespace TMinus.Core
{
    /// <summary>Editor-only bridge for the play script: starts and stops Unity Recorder takes (DemoRecorder is editor code).</summary>
    public static class VideoHooks
    {
        static string Call(string method, string arg)
        {
            var type = Type.GetType("TMinus.EditorTools.DemoRecorder, TMinus.EditorTools");
            var m = type?.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (m == null) return "DemoRecorder not found (is the Editor assembly compiled?)";
            return m.Invoke(null, new object[] { arg ?? "" }) as string;
        }

        public static string Start(string take) => Call("Start", take);
        public static string Stop(string _) => Call("Stop", "");
    }
}
#endif
