using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TMinus.Core
{
    /// <summary>
    /// Applies the look on every scene load (assets made by Tools > T-Minus > Build Look): studio reflections so
    /// paint and metal catch light, a graded post-processing profile, and SMAA on every camera. Flight's sky is
    /// driven by FlightView (atmosphere fades to stars with altitude).
    /// </summary>
    public static class SceneLook
    {
        static GameObject _volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => Apply(s.name);
            Apply(SceneManager.GetActiveScene().name);
        }

        public static void Apply(string scene)
        {
            bool flight = scene == SceneNames.Flight;
            bool menu = scene == SceneNames.MainMenu;
            bool lit = flight || menu || scene == SceneNames.IntegrationBay || scene == SceneNames.VAB || scene == SceneNames.AvionicsBay;

            var reflection = Resources.Load<Cubemap>("Looks/StudioReflection");
            if (reflection != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = reflection;
                RenderSettings.reflectionIntensity = flight ? 0.7f : 1f;
            }

            if (_volume != null) Object.Destroy(_volume);
            var profile = lit ? Resources.Load<VolumeProfile>(flight || menu ? "Looks/Look_Flight" : "Looks/Look_Interior") : null;
            if (profile != null)
            {
                _volume = new GameObject("[Look Volume]");
                var v = _volume.AddComponent<Volume>();
                v.isGlobal = true; v.priority = 10; v.sharedProfile = profile;
            }

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = true;
                data.stopNaN = true; // one NaN pixel + bloom = a full-screen cyan wash
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                if (lit) data.renderShadows = true;
            }
        }
    }
}
