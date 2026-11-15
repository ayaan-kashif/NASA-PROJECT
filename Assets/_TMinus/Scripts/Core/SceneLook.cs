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
        static GameObject _volume, _padVolume;

        /// <summary>0 = no launch-site grade (high up), 1 = full warm grade (on the pad).</summary>
        public static void SetPadWeight(float w)
        {
            if (_padVolume != null) _padVolume.GetComponent<Volume>().weight = Mathf.Clamp01(w);
        }

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
            bool bay = scene == SceneNames.IntegrationBay || scene == SceneNames.AvionicsBay;
            string grade = flight || menu ? "Looks/Look_Flight" : bay ? "Looks/Look_Bay" : "Looks/Look_Interior";
            var profile = lit ? Resources.Load<VolumeProfile>(grade) : null;
            // Light fog gives the VAB depth (the far end of the hangar softens); off everywhere else.
            RenderSettings.fog = scene == SceneNames.VAB;
            if (RenderSettings.fog)
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 35f; RenderSettings.fogEndDistance = 170f;
                RenderSettings.fogColor = new Color(0.07f, 0.1f, 0.17f);
            }
            if (profile != null)
            {
                _volume = new GameObject("[Look Volume]");
                var v = _volume.AddComponent<Volume>();
                v.isGlobal = true; v.priority = 10; v.sharedProfile = profile;
            }
            // Launch site: a warm sunrise grade that fades out with altitude (FlightSky sets the weight each frame).
            if (_padVolume != null) Object.Destroy(_padVolume);
            _padVolume = null;
            var pad = flight ? Resources.Load<VolumeProfile>("Looks/Look_Pad") : null;
            if (pad != null)
            {
                _padVolume = new GameObject("[Pad Grade]");
                var pv = _padVolume.AddComponent<Volume>();
                pv.isGlobal = true; pv.priority = 11; pv.weight = 1; pv.sharedProfile = pad;
            }

            // If this device can't run URP's post-processing shader (seen in some browsers), drawing with it on
            // leaves the 3D view black. Fall back to plain rendering: no bloom or grading, but a working picture.
            var uber = Shader.Find("Hidden/Universal Render Pipeline/UberPost");
            // Low-graphics mode (Settings) takes the same plain-rendering path as a browser without post-processing.
            bool postOk = uber != null && uber.isSupported && !SettingsStore.LowGraphics;
            if (!postOk && _volume != null) { Object.Destroy(_volume); _volume = null; }

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = postOk;
                data.stopNaN = postOk; // one NaN pixel + bloom = a full-screen cyan wash
                if (!postOk) { data.antialiasing = AntialiasingMode.None; continue; }
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                if (lit) data.renderShadows = true;
            }
        }
    }
}
