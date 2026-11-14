using TMinus.Core;
using UnityEngine;

namespace TMinus.Flight
{
    /// <summary>
    /// Engine rumble for the flight scene (audit F8). A looping generated sound (no audio file needed) whose loudness follows
    /// thrust, and which fades as the air thins: sound needs air, so the engine goes quiet at altitude.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineSound : MonoBehaviour
    {
        const int Rate = 22050;
        const float MaxVolume = 0.55f;

        static AudioClip _clip;
        AudioSource _source;
        AudioLowPassFilter _filter;
        float _volume, _target;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = Clip();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _filter = gameObject.AddComponent<AudioLowPassFilter>();
            _filter.cutoffFrequency = 1200f;
        }

        /// <summary>thrustFraction 0..1 of the biggest stage's thrust; altitude in metres.</summary>
        public void Set(float thrustFraction, float altitudeM)
        {
            float air = Mathf.Clamp01(1f - altitudeM / 70000f);                  // 1 at the pad, 0 above the atmosphere
            _target = Mathf.Clamp01(thrustFraction) * Mathf.Lerp(0.15f, 1f, air) * MaxVolume;
            if (_filter != null) _filter.cutoffFrequency = Mathf.Lerp(500f, 2200f, air);
        }

        void Update()
        {
            _volume = Mathf.MoveTowards(_volume, _target, Time.unscaledDeltaTime * 1.2f);
            _source.volume = _volume;
            if (_volume > 0.001f && !_source.isPlaying) _source.Play();
            else if (_volume <= 0.001f && _source.isPlaying) _source.Pause();
        }

        /// <summary>Two seconds of rumble: smoothed noise plus a few low tones that fit a whole number of cycles, so the loop is seamless.</summary>
        static AudioClip Clip()
        {
            if (_clip != null) return _clip;
            const int fade = 2200;
            int total = Rate * 2 + fade;
            var raw = new float[total];
            uint state = 2463534242u;
            float low = 0f;
            for (int i = 0; i < total; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;       // xorshift noise
                float white = (state / (float)uint.MaxValue) * 2f - 1f;
                low += (white - low) * 0.07f;                                          // low-pass: a rumble, not a hiss
                float t = i / (float)Rate;
                raw[i] = low * 2.4f + 0.10f * Mathf.Sin(2 * Mathf.PI * 43f * t) + 0.08f * Mathf.Sin(2 * Mathf.PI * 57f * t) + 0.05f * Mathf.Sin(2 * Mathf.PI * 97f * t);
            }
            int n = Rate * 2;
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int k = 0; k < fade; k++)                                             // blend the extra tail into the start so the loop has no click
            {
                float w = k / (float)fade;
                data[k] = data[k] * w + raw[n + k] * (1f - w);
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            _clip = AudioClip.Create("fx_engine", n, 1, Rate, false);
            _clip.SetData(data, 0);
            return _clip;
        }
    }
}
