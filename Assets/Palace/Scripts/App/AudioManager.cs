using System;
using System.Collections.Generic;
using UnityEngine;

namespace Palace
{
    public enum Sfx { CardPlay, CardDraw, Reveal, PickUp, Burn, Skip, Reverse, Reset, Win, Select, Button, YourTurn }

    /// <summary>
    /// Plays sound effects and background music. Every sound is synthesised in code when the
    /// game starts (no audio files, so no licensing issues). Swap in recorded clips later by
    /// replacing the entries in the clips dictionary.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static AudioManager instance;
        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource[] sfxSources;
        private int nextSource;
        private AudioSource musicSource;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioManager>();
                    instance.Init();
                }
                return instance;
            }
        }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (Application.isBatchMode) return;
            AudioManager a = Instance;
            if (!a.clips.TryGetValue(sfx, out AudioClip clip)) return;
            AudioSource src = a.sfxSources[a.nextSource];
            a.nextSource = (a.nextSource + 1) % a.sfxSources.Length;
            src.pitch = 1f + UnityEngine.Random.Range(-0.04f, 0.04f); // tiny variation so repeats don't sound robotic
            src.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static void StartMusic()
        {
            if (Application.isBatchMode) return;
            AudioManager a = Instance;
            if (!a.musicSource.isPlaying) a.musicSource.Play();
            ApplyVolumes();
        }

        public static void ApplyVolumes()
        {
            if (instance == null) return;
            instance.musicSource.volume = GameSettings.MusicVolume * 0.6f;
        }

        private void Init()
        {
            sfxSources = new AudioSource[4];
            for (int i = 0; i < sfxSources.Length; i++)
            {
                sfxSources[i] = gameObject.AddComponent<AudioSource>();
                sfxSources[i].playOnAwake = false;
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;

            var rnd = new System.Random(42);
            clips[Sfx.CardPlay] = Make("card_play", 0.10f, (t, i) => Noise(rnd, 0.35f) * Mathf.Exp(-t * 55f) * 0.8f
                                                                       + Mathf.Sin(Tau * 150f * t) * Mathf.Exp(-t * 40f) * 0.45f);
            clips[Sfx.CardDraw] = Make("card_draw", 0.16f, (t, i) => Noise(rnd, 0.2f) * Window(t, 0.16f) * 0.55f);
            clips[Sfx.Reveal] = Make("reveal", 0.12f, (t, i) => Noise(rnd, 0.5f) * Mathf.Exp(-t * 30f) * 0.6f
                                                               + Mathf.Sin(Tau * 2200f * t) * Mathf.Exp(-t * 120f) * 0.2f);
            clips[Sfx.PickUp] = Make("pick_up", 0.5f, (t, i) =>
            {
                float v = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float lt = t - k * 0.1f;
                    if (lt > 0f && lt < 0.15f) v += Window(lt, 0.15f);
                }
                return Noise(rnd, 0.25f) * v * 0.5f;
            });
            clips[Sfx.Burn] = Make("burn", 0.85f, (t, i) =>
            {
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 3.5f);
                float crackle = rnd.NextDouble() < 0.004 ? (float)(rnd.NextDouble() * 2 - 1) * 1.5f : 0f;
                return (Noise(rnd, 0.15f + 0.3f * Mathf.Exp(-t * 4f)) * 0.7f + crackle + Mathf.Sin(Tau * 60f * t) * 0.35f) * env;
            });
            clips[Sfx.Skip] = Make("skip", 0.22f, (t, i) => Blip(t, 0f, 0.08f, 880f) + Blip(t, 0.11f, 0.08f, 660f));
            clips[Sfx.Reverse] = MakeSweep("reverse", 0.35f, t => 450f + 450f * Mathf.Sin(Mathf.PI * t / 0.35f));
            clips[Sfx.Reset] = MakeSweep("reset", 0.25f, t => 300f + 2000f * t);
            clips[Sfx.Win] = Make("win", 1.1f, (t, i) =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float lt = t - k * 0.11f;
                    if (lt > 0f) v += (Mathf.Sin(Tau * notes[k] * lt) + 0.3f * Mathf.Sin(Tau * notes[k] * 2f * lt)) * Mathf.Exp(-lt * 4f) * 0.22f;
                }
                return v;
            });
            clips[Sfx.Select] = Make("select", 0.04f, (t, i) => Mathf.Sin(Tau * 1800f * t) * Mathf.Exp(-t * 180f) * 0.35f);
            clips[Sfx.Button] = Make("button", 0.06f, (t, i) => (Mathf.Sin(Tau * 1200f * t) + Mathf.Sin(Tau * 600f * t)) * Mathf.Exp(-t * 90f) * 0.25f);
            clips[Sfx.YourTurn] = Make("your_turn", 0.5f, (t, i) => Blip(t, 0f, 0.35f, 784f) * 0.6f + Blip(t, 0.12f, 0.38f, 1046.5f) * 0.6f);

            musicSource.clip = MakeMusic();
            ApplyVolumes();
        }

        // ------------------------------------------------------------------ synthesis helpers

        private const float Tau = Mathf.PI * 2f;
        private float lowpassState;

        private float Noise(System.Random rnd, float smoothing)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lowpassState += (white - lowpassState) * smoothing;
            return lowpassState * 2f;
        }

        private static float Window(float t, float length)
        {
            float x = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
            return x * x;
        }

        private static float Blip(float t, float start, float length, float freq)
        {
            float lt = t - start;
            if (lt < 0f || lt > length) return 0f;
            return Mathf.Sin(Tau * freq * lt) * Mathf.Exp(-lt * 12f) * Mathf.Clamp01(lt / 0.005f) * 0.4f;
        }

        private static AudioClip Make(string name, float seconds, Func<float, int, float> sample, bool fadeEnd = true)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float fade = fadeEnd ? Mathf.Clamp01((count - i) / (SampleRate * 0.01f)) : 1f; // avoid clicks at the end
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, i) * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeSweep(string name, float seconds, Func<float, float> frequency)
        {
            float phase = 0f;
            return Make(name, seconds, (t, i) =>
            {
                phase += Tau * frequency(t) / SampleRate;
                return Mathf.Sin(phase) * Window(t, seconds) * 0.35f;
            });
        }

        /// <summary>A soft, seamless 16-second chord loop: Cmaj7 - Am7 - Fmaj7 - G6.</summary>
        private static AudioClip MakeMusic()
        {
            float[][] chords =
            {
                new[] { 130.81f, 329.63f, 392.00f, 493.88f },
                new[] { 110.00f, 329.63f, 392.00f, 523.25f },
                new[] { 87.31f, 440.00f, 523.25f, 659.25f },
                new[] { 98.00f, 493.88f, 587.33f, 659.25f }
            };
            const float chordLength = 4f, overlap = 0.8f, loop = 16f;

            return Make("music_loop", loop, (t, i) =>
            {
                float v = 0f;
                for (int c = 0; c < chords.Length; c++)
                {
                    float local = Mathf.Repeat(t - c * chordLength, loop);
                    if (local > chordLength + overlap) continue;
                    float env = Mathf.SmoothStep(0f, 1f, local / overlap) * Mathf.SmoothStep(1f, 0f, (local - chordLength) / overlap);
                    float[] notes = chords[c];
                    for (int n = 0; n < notes.Length; n++)
                    {
                        float amp = n == 0 ? 0.30f : 0.12f;
                        float tremolo = 1f + 0.15f * Mathf.Sin(Tau * 0.25f * t + n);
                        v += Mathf.Sin(Tau * notes[n] * local) * amp * tremolo * env; // local time keeps the loop seamless
                    }
                }
                return v * 0.35f;
            }, false);
        }
    }
}
