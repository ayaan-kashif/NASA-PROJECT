using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Play-mode smoke tests for a coding agent. Drop Temp/agent_play.flag with one command per line:
    ///   scene Assets/_TMinus/Scenes/VAB.unity   (opens it and enters Play mode)
    ///   wait 3                                  (seconds)
    ///   shot vab_pathfinder                     (Game view -> Logs/agent-shots/vab_pathfinder.png)
    ///   call TMinus.Assembly.VabDebug.ShowRocket saturn_v   (static method, optional string arg)
    ///   stop                                    (exit Play mode)
    /// Progress goes to Logs/agent-play.txt. State survives the domain reload via SessionState.
    /// </summary>
    [InitializeOnLoad]
    static class AgentPlayScript
    {
        const string Flag = "Temp/agent_play.flag";
        const string Out = "Logs/agent-play.txt";
        const string KeyLines = "TMinus.Agent.Lines", KeyIndex = "TMinus.Agent.Index", KeyWait = "TMinus.Agent.WaitUntil";

        static double _nextPoll;

        static AgentPlayScript()
        {
            EditorApplication.update += Tick;
        }

        static string[] Lines
        {
            get { var s = SessionState.GetString(KeyLines, ""); return string.IsNullOrEmpty(s) ? null : s.Split('\n'); }
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 0.2;

            if (Lines == null)
            {
                if (!File.Exists(Flag) || EditorApplication.isCompiling || EditorApplication.isPlaying) return;
                var text = File.ReadAllText(Flag).Replace("\r", "");
                File.Delete(Flag);
                SessionState.SetString(KeyLines, text.Trim());
                SessionState.SetInt(KeyIndex, 0);
                SessionState.SetFloat(KeyWait, 0);
                File.WriteAllText(Out, $"{DateTime.Now:O} script started\n");
            }

            if (now < SessionState.GetFloat(KeyWait, 0)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return; // mid transition

            var lines = Lines;
            int i = SessionState.GetInt(KeyIndex, 0);
            if (i >= lines.Length) { Finish("done"); return; }

            var line = lines[i].Trim();
            SessionState.SetInt(KeyIndex, i + 1);
            if (line.Length == 0 || line.StartsWith("#")) return;

            var parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            try { Run(parts, now); }
            catch (Exception ex) { Log($"ERROR '{line}': {ex.GetBaseException().Message}"); }
        }

        static void Run(string[] p, double now)
        {
            switch (p[0])
            {
                case "scene":
                    if (EditorApplication.isPlaying) { Log("scene: already playing"); return; }
                    var active = EditorSceneManager.GetActiveScene();
                    if (active.isDirty) EditorSceneManager.SaveScene(active);
                    EditorSceneManager.OpenScene(p[1]);
                    EditorApplication.isPlaying = true;
                    Log("play " + p[1]);
                    SessionState.SetFloat(KeyWait, (float)(now + 1.0));
                    break;
                case "wait":
                    SessionState.SetFloat(KeyWait, (float)(now + double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case "shot":
                    Directory.CreateDirectory("Logs/agent-shots");
                    var path = $"Logs/agent-shots/{p[1]}.png";
                    ScreenCapture.CaptureScreenshot(path);
                    Log("shot " + path);
                    SessionState.SetFloat(KeyWait, (float)(now + 0.8));
                    break;
                case "shot3d":
                {
                    // Renders Camera.main into a texture: works even when the Game view isn't visible (no UI overlay).
                    Directory.CreateDirectory("Logs/agent-shots");
                    var cam = Camera.main;
                    if (cam == null) { Log("shot3d: no main camera"); break; }
                    var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
                    var was = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                    RenderTexture.active = was;
                    var file = $"Logs/agent-shots/{p[1]}.png";
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    Log("shot3d " + file);
                    break;
                }
                case "call":
                    Call(p[1], p.Length > 2 ? p[2] : null);
                    break;
                case "menu":
                    Log($"menu {p[1]} -> {EditorApplication.ExecuteMenuItem(string.Join(" ", p, 1, p.Length - 1))}");
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    Log("stop");
                    SessionState.SetFloat(KeyWait, (float)(now + 1.0));
                    break;
                default:
                    Log("unknown command " + p[0]);
                    break;
            }
        }

        static void Call(string target, string arg)
        {
            int dot = target.LastIndexOf('.');
            string typeName = target.Substring(0, dot), method = target.Substring(dot + 1);
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if ((type = asm.GetType(typeName)) != null) break;
            if (type == null) { Log("call: no type " + typeName); return; }

            var m = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null) { Log("call: no method " + target); return; }
            var result = m.Invoke(null, m.GetParameters().Length == 0 ? null : new object[] { arg });
            Log($"call {target}({arg}) -> {result}");
        }

        static void Finish(string why)
        {
            SessionState.EraseString(KeyLines);
            SessionState.EraseInt(KeyIndex);
            Log("finished: " + why);
        }

        static void Log(string s)
        {
            try { File.AppendAllText(Out, $"{DateTime.Now:HH:mm:ss.fff} {s}\n"); } catch { }
        }
    }
}
