using System.Collections.Generic;
using UnityEngine;

namespace TMinus.Core
{
    /// <summary>
    /// Small generated sound effects (no audio files needed): click, snap, error, ratchet, plug, seat.
    /// Replace with CC0 recordings in the polish pass (Prompt 17) by keeping the same names.
    /// </summary>
    public static class AudioFx
    {
        static AudioSource _source;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        public static float Volume = 0.6f;

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (_source == null)
            {
                var host = GameState.Instance != null ? GameState.Instance.gameObject : new GameObject("[AudioFx]");
                _source = host.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0;
            }
            var clip = Get(name);
            if (clip == null) return;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, Volume * volume);
        }

        static AudioClip Get(string name)
        {
            if (Clips.TryGetValue(name, out var c)) return c;
            switch (name)
            {
                case "click": c = Make(name, 0.05f, (t, i) => Noise(i) * Decay(t, 90)); break;
                case "snap": c = Make(name, 0.18f, (t, i) => (Sine(t, 140) * 0.7f + Noise(i) * 0.3f) * Decay(t, 28)); break;
                case "error": c = Make(name, 0.25f, (t, i) => Square(t, 110) * 0.35f * Decay(t, 9)); break;
                case "ratchet": c = Make(name, 0.03f, (t, i) => Noise(i) * Decay(t, 160) * 0.8f); break;
                case "seat": c = Make(name, 0.12f, (t, i) => (Sine(t, 900) * 0.5f + Noise(i) * 0.3f) * Decay(t, 40)); break;
                case "plug": c = Make(name, 0.08f, (t, i) => (Sine(t, 520) * 0.4f + Noise(i) * 0.4f) * Decay(t, 60)); break;
                case "spark": c = Make(name, 0.3f, (t, i) => Noise(i) * Decay(t, 12) * (Mathf.Sin(t * 300) > 0 ? 1 : 0.2f)); break;
                case "beep": c = Make(name, 0.12f, (t, i) => Sine(t, 1000) * 0.4f * (t < 0.1f ? 1 : 0)); break;
                default: return null;
            }
            Clips[name] = c;
            return c;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> f)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(seconds * rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate, i), -1f, 1f);
            var clip = AudioClip.Create("fx_" + name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Decay(float t, float k) => Mathf.Exp(-t * k);
        static float Sine(float t, float hz) => Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Square(float t, float hz) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * hz * t));
        static float Noise(int i)
        {
            uint x = (uint)i * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / (float)uint.MaxValue * 2f - 1f;
        }
    }
}
