using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TMinus.Core
{
    /// <summary>Full-screen black overlay used by SceneFlow. Lives on the GameState object.</summary>
    public sealed class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        public bool IsBusy { get; private set; }

        VisualElement _overlay;

        void Awake()
        {
            Instance = this;
            var doc = UiDocs.Create("[SceneFader UI]", transform, 1000);
            if (doc == null) return;

            _overlay = new VisualElement { name = "fade-overlay", pickingMode = PickingMode.Ignore };
            var s = _overlay.style;
            s.position = Position.Absolute;
            s.left = 0; s.right = 0; s.top = 0; s.bottom = 0;
            s.backgroundColor = Color.black;
            s.opacity = 0;
            doc.rootVisualElement.pickingMode = PickingMode.Ignore;
            doc.rootVisualElement.Add(_overlay);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void FadeTo(string sceneName, float totalSeconds)
        {
            if (IsBusy) return;
            StartCoroutine(Run(sceneName, totalSeconds * 0.5f));
        }

        IEnumerator Run(string sceneName, float half)
        {
            IsBusy = true;
            if (_overlay != null) _overlay.pickingMode = PickingMode.Position; // block clicks
            yield return Fade(0f, 1f, half);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;

            yield return Fade(1f, 0f, half);
            if (_overlay != null) _overlay.pickingMode = PickingMode.Ignore;
            IsBusy = false;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (_overlay == null) yield break;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                _overlay.style.opacity = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _overlay.style.opacity = to;
        }
    }
}
