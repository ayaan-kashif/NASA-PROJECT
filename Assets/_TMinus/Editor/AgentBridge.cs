using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Lets a coding agent drive the open Editor without MCP: drop a flag file in Temp/ and the
    /// Editor reacts on its next tick. Results go to Logs/agent-*.txt.
    ///   Temp/agent_refresh.flag  -> AssetDatabase.Refresh (import + compile)
    ///   Temp/agent_tests.flag    -> run all EditMode tests, write Logs/agent-tests.txt
    ///   Temp/agent_menu.flag     -> run the menu item named inside the file
    /// </summary>
    [InitializeOnLoad]
    static class AgentBridge
    {
        const string RefreshFlag = "Temp/agent_refresh.flag";
        const string TestsFlag = "Temp/agent_tests.flag";
        const string MenuFlag = "Temp/agent_menu.flag";
        const string TestsOut = "Logs/agent-tests.txt";
        const string StatusOut = "Logs/agent-status.txt";

        static double _next;
        static bool _running;

        static AgentBridge()
        {
            EditorApplication.update += Tick;
            WriteStatus("loaded");
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            if (_running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            try
            {
                if (Take(RefreshFlag))
                {
                    WriteStatus("refresh");
                    AssetDatabase.Refresh();
                }
                else if (Take(MenuFlag, out var menu))
                {
                    bool ok = EditorApplication.ExecuteMenuItem(menu.Trim());
                    WriteStatus($"menu '{menu.Trim()}' -> {(ok ? "ok" : "not found")}");
                }
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && Take(TestsFlag))
                {
                    RunTests();
                }
            }
            catch (Exception ex)
            {
                WriteStatus("error: " + ex.Message);
            }
        }

        static bool Take(string path) => Take(path, out _);

        static bool Take(string path, out string content)
        {
            content = null;
            if (!File.Exists(path)) return false;
            content = File.ReadAllText(path);
            File.Delete(path);
            return true;
        }

        static void RunTests()
        {
            _running = true;
            WriteStatus("tests running");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(api));
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        static void WriteStatus(string s)
        {
            try { File.WriteAllText(StatusOut, $"{DateTime.Now:O} {s}\n"); } catch { /* Logs may be locked */ }
        }

        sealed class Callbacks : ICallbacks
        {
            readonly TestRunnerApi _api;
            readonly StringBuilder _fails = new StringBuilder();
            int _pass, _fail, _skip;

            public Callbacks(TestRunnerApi api) { _api = api; }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: _pass++; break;
                    case TestStatus.Failed:
                        _fail++;
                        _fails.AppendLine($"FAIL {result.Test.FullName}\n  {result.Message?.Trim()}");
                        break;
                    default: _skip++; break;
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var text = $"{DateTime.Now:O}\nPASS {_pass}  FAIL {_fail}  SKIP {_skip}\n{_fails}";
                File.WriteAllText(TestsOut, text);
                WriteStatus($"tests done: {_pass} pass, {_fail} fail");
                _api.UnregisterCallbacks(this);
                _running = false;
            }
        }
    }
}
