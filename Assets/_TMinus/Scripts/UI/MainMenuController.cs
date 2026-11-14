using TMinus.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMinus.UI
{
    /// <summary>Main menu. UXML: UI/UXML/MainMenu.uxml, style: UI/USS/MainMenu.uss.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] StyleSheet style;

        static bool _linkChecked;
        Label _toast;
        IVisualElementScheduledItem _toastHide;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);

            var cont = root.Q<Button>("btn-continue");
            if (cont != null && SaveService.HasSave)
            {
                string when = SaveService.Describe();
                cont.text = when != null ? $"Continue  ·  {when}" : "Continue";
                cont.style.display = DisplayStyle.Flex;
                cont.clicked += () =>
                {
                    if (SaveService.TryLoad(GameState.Instance, out string msg)) { DemoHooks.PendingBriefingPanel = "briefing"; SceneFlow.Load(SceneNames.MissionBriefing); }
                    else Toast(msg ?? "Could not load the save.");
                };
            }
            Bind(root, "btn-missions", () => SceneFlow.Load(SceneNames.MissionBriefing));
            Bind(root, "btn-judge", () => { if (DemoHooks.StartJudgeMode != null) DemoHooks.StartJudgeMode(); else Toast("Judge Mode is not available in this build."); });
            Bind(root, "btn-failure", () => { if (DemoHooks.OpenFailureLab != null) DemoHooks.OpenFailureLab(root); else Toast("The Failure Lab is not available in this build."); });
            Bind(root, "btn-sandbox", () => MenuPanels.ShowSandbox(root));
            Bind(root, "btn-codex", () => MenuPanels.ShowCodex(root));
            Bind(root, "btn-settings", () => MenuPanels.ShowSettings(root));
            Bind(root, "btn-quit", Quit);
            // A browser tab cannot be closed by the page, so Quit does nothing there: hide it.
            if (Application.platform == RuntimePlatform.WebGLPlayer) { var quit = root.Q<Button>("btn-quit"); if (quit != null) quit.style.display = DisplayStyle.None; }

            _toast = root.Q<Label>("toast");
            if (_toast != null) _toast.AddToClassList("hidden");

            var pending = DemoHooks.PendingMenuPanel; DemoHooks.PendingMenuPanel = null;
            if (pending == "failure" && DemoHooks.OpenFailureLab != null) DemoHooks.OpenFailureLab(root);
            else if (pending == "sandbox") MenuPanels.ShowSandbox(root);

            // The page link may carry a design (#d=...): keep it for the briefing and tell the player once.
            if (!_linkChecked && DesignLink.TryRead(out string linked))
            {
                _linkChecked = true;   // the address keeps its #d=... part; only react once per session
                DemoHooks.PendingDesignCode = linked;
                Toast("A shared design came with this link. Open Missions and press Load code.");
            }

            var version = root.Q<Label>("version");
            if (version != null) version.text = "v" + Application.version + "  ·  NASA Space Apps 2026";
        }

        static void Bind(VisualElement root, string name, System.Action onClick)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                Debug.LogWarning($"[MainMenu] Button \"{name}\" not found in MainMenu.uxml.");
                return;
            }
            button.clicked += onClick;
        }

        void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.RemoveFromClassList("hidden");
            _toastHide?.Pause();
            _toastHide = _toast.schedule.Execute(() => _toast.AddToClassList("hidden")).StartingIn(2500);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
