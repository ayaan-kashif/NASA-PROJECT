using UnityEngine;

namespace TMinus.Flight
{
    /// <summary>
    /// Flight sky: a physically based atmosphere (Skybox/Procedural) that thins with altitude, a star field once
    /// above ~60 km, and a blue atmosphere glow around the planet seen from orbit. Assets come from Build Look.
    /// </summary>
    public sealed class FlightSky
    {
        const double SpaceAltM = 60000;
        Material _sky, _stars, _atmo;
        Light _sun;
        bool _inSpace;

        public static FlightSky Build(Camera cam, Light sun, Transform earthRoot, Mesh shellMesh)
        {
            var s = new FlightSky { _sun = sun };
            var sky = Resources.Load<Material>("Looks/Sky_Earth");
            var stars = Resources.Load<Material>("Looks/Sky_Stars");
            if (sky == null || stars == null) return s;      // look not built: keep the solid-colour sky
            s._sky = new Material(sky); s._stars = new Material(stars); // instances: never edit the assets at runtime
            RenderSettings.skybox = s._sky;
            if (sun != null) RenderSettings.sun = sun;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;

            var atmo = Resources.Load<Material>("Looks/Atmosphere");
            if (atmo != null)
            {
                s._atmo = new Material(atmo);
                // Smooth shell just above the surface (the built-in sphere is too faceted at planet size).
                var shell = new GameObject("AtmosphereGlow");
                shell.transform.SetParent(earthRoot, false);
                shell.AddComponent<MeshFilter>().sharedMesh = shellMesh;
                shell.AddComponent<MeshRenderer>();
                var r = shell.GetComponent<MeshRenderer>();
                r.sharedMaterial = s._atmo; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return s;
        }

        public void Sync(double altitudeM)
        {
            if (_sky == null) return;
            bool space = altitudeM >= SpaceAltM;
            if (space != _inSpace) { _inSpace = space; RenderSettings.skybox = space ? _stars : _sky; }
            if (!space)
            {
                // Air thins roughly exponentially; the sky darkens from blue to black as we climb.
                float k = Mathf.Exp(-(float)altitudeM / 9000f);
                _sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.05f, 1.05f, k));
                _sky.SetFloat("_Exposure", Mathf.Lerp(0.7f, 1.25f, k));
            }
            if (_atmo != null && _sun != null) _atmo.SetVector("_SunDir", -_sun.transform.forward);
        }
    }
}
