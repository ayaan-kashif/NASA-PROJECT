using UnityEngine;
using M = TMinus.Core.MaterialLibrary;

namespace TMinus.Flight
{
    /// <summary>
    /// Smoke puffs from the flame trench while the rocket is low and thrusting: about 40 small grey puffs a second,
    /// alpha-blended, for the first 1.5 km only. Cheap (a capped particle count, no per-frame allocation).
    /// </summary>
    public sealed class LiftoffSmoke
    {
        const float MaxAltitudeM = 1500f;
        readonly ParticleSystem _ps;


        public LiftoffSmoke(Transform parent)
        {
            var go = new GameObject("LiftoffSmoke");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);   // puffs spread along the ground
            _ps = go.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = new Color(0.78f, 0.78f, 0.8f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 160;
            var em0 = _ps.emission; em0.rateOverTime = 0;
            var shape = _ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 9f;
            var sz = _ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.4f, 1, 1.8f));
            var col = _ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.6f, 0.6f, 0.62f), 1) },
                      new[] { new GradientAlphaKey(0.6f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = Resources.Load<Material>("Looks/Smoke") ?? M.Get(M.Concrete);
        }

        /// <summary>Called every frame: on while the engines push the rocket off the pad, off once it is high.</summary>
        public void Sync(float altitudeM, float thrustFraction)
        {
            bool on = altitudeM < MaxAltitudeM && thrustFraction > 0.2f;
            var em = _ps.emission; em.rateOverTime = on ? 40f * Mathf.Clamp01(thrustFraction) : 0f;
            if (on && !_ps.isEmitting) _ps.Play();
            else if (!on && _ps.isEmitting) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
