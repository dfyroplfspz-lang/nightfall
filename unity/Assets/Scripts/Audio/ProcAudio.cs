// C# / Assets/Scripts/Audio/ProcAudio.cs / Unity 2022.3+ / every sound synthesized at startup
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// No audio files ship with the game: gunshots, growls and UI blips are all built
    /// from noise and sine stacks the first time the game loads, the same way the
    /// original synthesized them with numpy.
    /// </summary>
    public class ProcAudio
    {
        const int Rate = 44100;

        readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Rng _rng = new Rng(20260101);
        int _next;
        bool _muted;
        public float masterVolume = 0.85f;

        public bool Muted { get { return _muted; } }

        public ProcAudio(GameObject host, int voices = 16)
        {
            for (int i = 0; i < voices; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources.Add(src);
            }
            Build();
        }

        /// <summary>Silent instance for the self-test: no voices, no synthesis, no output.</summary>
        public ProcAudio()
        {
            _muted = true;
        }

        // ------------------------------------------------------ dsp helpers
        static int N(float seconds) { return Mathf.Max(1, (int)(seconds * Rate)); }

        float[] Noise(int n)
        {
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = _rng.Range(-1f, 1f);
            return x;
        }

        static float[] Sine(float freq, int n, float phase = 0f)
        {
            var x = new float[n];
            float step = MathUtil.TAU * freq / Rate;
            for (int i = 0; i < n; i++) x[i] = Mathf.Sin(phase + step * i);
            return x;
        }

        static float[] Saw(float freq, int n)
        {
            var x = new float[n];
            float period = Rate / Mathf.Max(1f, freq);
            for (int i = 0; i < n; i++)
            {
                float t = (i % period) / period;
                x[i] = t * 2f - 1f;
            }
            return x;
        }

        static float[] Decay(int n, float k = 12f)
        {
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = Mathf.Exp(-k * i / (float)n);
            return x;
        }

        static void Attack(float[] x, float ms = 4f)
        {
            int a = Mathf.Min(x.Length, (int)(ms * Rate / 1000f));
            for (int i = 0; i < a; i++) x[i] *= i / (float)a;
        }

        static float[] Lowpass(float[] x, float k)
        {
            var y = new float[x.Length];
            float prev = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                prev += (x[i] - prev) * k;
                y[i] = prev;
            }
            return y;
        }

        static void MixInto(float[] dst, float[] src, float gain)
        {
            int n = Mathf.Min(dst.Length, src.Length);
            for (int i = 0; i < n; i++) dst[i] += src[i] * gain;
        }

        static void Envelope(float[] x, float[] env)
        {
            int n = Mathf.Min(x.Length, env.Length);
            for (int i = 0; i < n; i++) x[i] *= env[i];
        }

        AudioClip Make(string name, float[] sig, float volume = 0.6f)
        {
            float peak = 0f;
            for (int i = 0; i < sig.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(sig[i]));
            float g = peak > 1e-5f ? volume / peak : 0f;

            var data = new float[sig.Length];
            for (int i = 0; i < sig.Length; i++) data[i] = Mathf.Clamp(sig[i] * g, -1f, 1f);
            Attack(data, 2f);

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Put(string key, params AudioClip[] clips) { _clips[key] = clips; }

        // ---------------------------------------------------------- library
        void Build()
        {
            Put("pistol", Gun(0.20f, 16f, 110f, 0.85f));
            Put("smg", Gun(0.13f, 22f, 150f, 0.6f, 0.45f));
            Put("shotgun", Gun(0.42f, 7f, 68f, 1.0f, 0.8f));
            Put("rifle", Gun(0.26f, 12f, 88f, 0.95f, 0.7f));

            Put("hit", Hit(1), Hit(2), Hit(3));
            Put("hurt", Hurt());
            Put("death", Death(0), Death(1));
            Put("growl", Growl(0), Growl(1), Growl(2));
            Put("ui", Click(660f, 0.055f));
            Put("empty", Click(220f, 0.045f));
            Put("reload", Reload());
            Put("pickup", PickupBlip());
            Put("wave", Wave());
            Put("gameover", GameOver());
            Put("dash", Dash());
            Put("spit", Spit());
            Put("explode", Explode());
        }

        AudioClip Gun(float dur, float dk, float thump, float vol, float body = 0.6f)
        {
            int n = N(dur);
            float[] sig = Lowpass(Noise(n), 0.55f);
            Envelope(sig, Decay(n, dk));
            float[] low = Sine(thump, n);
            Envelope(low, Decay(n, dk * 0.7f));
            MixInto(sig, low, body);
            float[] crack = Noise(N(0.012f));
            MixInto(sig, crack, 0.7f);
            return Make("gun", sig, vol * 0.55f);
        }

        AudioClip Hit(int seed)
        {
            int n = N(0.09f);
            float[] sig = Lowpass(Noise(n), 0.75f);
            Envelope(sig, Decay(n, 26f));
            float[] t = Sine(180f + seed * 40f, n);
            Envelope(t, Decay(n, 30f));
            MixInto(sig, t, 0.5f);
            return Make("hit", sig, 0.4f);
        }

        AudioClip Hurt()
        {
            int n = N(0.3f);
            float[] sig = Sine(150f, n);
            float[] s2 = Sine(96f, n);
            MixInto(sig, s2, 0.7f);
            MixInto(sig, Noise(n), 0.35f);
            Envelope(sig, Decay(n, 9f));
            return Make("hurt", sig, 0.5f);
        }

        AudioClip Death(int i)
        {
            int n = N(0.5f + i * 0.1f);
            float[] sig = Saw(90f - i * 18f, n);
            MixInto(sig, Lowpass(Noise(n), 0.2f), 0.8f);
            Envelope(sig, Decay(n, 7f));
            return Make("death", sig, 0.4f);
        }

        AudioClip Growl(int i)
        {
            int n = N(0.75f);
            float[] sig = Saw(58f + i * 11f, n);
            MixInto(sig, Sine(40f + i * 6f, n), 0.9f);
            sig = Lowpass(sig, 0.08f);
            MixInto(sig, Lowpass(Noise(n), 0.05f), 0.5f);
            var env = new float[n];
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n;
                env[k] = Mathf.Sin(Mathf.PI * t) * (0.7f + 0.3f * Mathf.Sin(t * 22f));
            }
            Envelope(sig, env);
            return Make("growl", sig, 0.33f);
        }

        AudioClip Click(float f, float dur)
        {
            int n = N(dur);
            float[] sig = Sine(f, n);
            MixInto(sig, Sine(f * 1.5f, n), 0.35f);
            Envelope(sig, Decay(n, 20f));
            return Make("click", sig, 0.3f);
        }

        AudioClip Reload()
        {
            int n = N(0.42f);
            var sig = new float[n];
            int[] taps = { 0, (int)(0.12f * Rate), (int)(0.28f * Rate) };
            foreach (int t in taps)
            {
                int len = Mathf.Min(N(0.06f), n - t);
                if (len <= 0) continue;
                float[] piece = Lowpass(Noise(len), 0.6f);
                Envelope(piece, Decay(len, 28f));
                for (int i = 0; i < len; i++) sig[t + i] += piece[i];
            }
            return Make("reload", sig, 0.4f);
        }

        AudioClip PickupBlip()
        {
            int n = N(0.22f);
            var sig = new float[n];
            MixInto(sig, Sine(660f, n), 1f);
            MixInto(sig, Sine(990f, n), 0.6f);
            MixInto(sig, Sine(1320f, n), 0.3f);
            Envelope(sig, Decay(n, 11f));
            return Make("pickup", sig, 0.33f);
        }

        AudioClip Wave()
        {
            int n = N(0.9f);
            var sig = new float[n];
            MixInto(sig, Sine(110f, n), 1f);
            MixInto(sig, Sine(165f, n), 0.7f);
            MixInto(sig, Lowpass(Noise(n), 0.06f), 0.5f);
            var env = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                env[i] = Mathf.Min(1f, t * 6f) * Mathf.Exp(-3.2f * t);
            }
            Envelope(sig, env);
            return Make("wave", sig, 0.45f);
        }

        AudioClip GameOver()
        {
            int n = N(1.4f);
            var sig = new float[n];
            MixInto(sig, Sine(140f, n), 1f);
            MixInto(sig, Sine(93f, n), 0.9f);
            MixInto(sig, Saw(70f, n), 0.5f);
            Envelope(sig, Decay(n, 3.4f));
            return Make("gameover", sig, 0.5f);
        }

        AudioClip Dash()
        {
            int n = N(0.22f);
            float[] sig = Lowpass(Noise(n), 0.25f);
            var env = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                env[i] = Mathf.Sin(Mathf.PI * t);
            }
            Envelope(sig, env);
            return Make("dash", sig, 0.3f);
        }

        AudioClip Spit()
        {
            int n = N(0.26f);
            float[] sig = Lowpass(Noise(n), 0.35f);
            MixInto(sig, Sine(300f, n), 0.4f);
            Envelope(sig, Decay(n, 14f));
            return Make("spit", sig, 0.35f);
        }

        AudioClip Explode()
        {
            int n = N(1.1f);
            float[] sig = Lowpass(Noise(n), 0.12f);
            MixInto(sig, Sine(55f, n), 1.1f);
            MixInto(sig, Sine(82f, n), 0.6f);
            Envelope(sig, Decay(n, 4.2f));
            return Make("explode", sig, 0.6f);
        }

        // ------------------------------------------------------------ play
        public void Play(string name, float volume = 1f)
        {
            if (_muted || _sources.Count == 0) return;
            AudioClip[] bank;
            if (!_clips.TryGetValue(name, out bank) || bank.Length == 0) return;

            AudioClip clip = bank.Length == 1 ? bank[0] : bank[(int)(_rng.NextUInt() % (uint)bank.Length)];
            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            src.pitch = 1f + _rng.Range(-0.05f, 0.05f);
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume);
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            foreach (var s in _sources) s.mute = _muted;
        }
    }
}
