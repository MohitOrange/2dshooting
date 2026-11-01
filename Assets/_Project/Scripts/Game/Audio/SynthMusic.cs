using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Seamless music loops synthesized at startup (no audio files, ~1 MB in memory each):
    /// a chill campfire groove for the lobby and a faster, punchier loop for matches.
    /// Every note writes with wrap-around, so tails of the last bar ring into the first (no click at the loop).
    /// </summary>
    public static class SynthMusic
    {
        private const int Rate = 22050;

        // chord progressions as note frequencies (one chord per bar)
        private static readonly float[][] LobbyChords =
        {
            new[] { 220f, 261.63f, 329.63f },  // Am
            new[] { 174.61f, 220f, 261.63f },  // F
            new[] { 261.63f, 329.63f, 392f },  // C
            new[] { 196f, 246.94f, 293.66f },  // G
        };

        private static readonly float[][] BattleChords =
        {
            new[] { 220f, 261.63f, 329.63f },  // Am
            new[] { 196f, 246.94f, 293.66f },  // G
            new[] { 174.61f, 220f, 261.63f },  // F
            new[] { 164.81f, 207.65f, 246.94f }, // E
        };

        public static AudioClip Lobby() => Build("music_lobby", 92f, LobbyChords, false);
        public static AudioClip Battle() => Build("music_battle", 126f, BattleChords, true);

        private static AudioClip Build(string name, float bpm, float[][] chords, bool battle)
        {
            float beat = 60f / bpm;
            int bars = chords.Length;
            int n = Mathf.RoundToInt(bars * 4 * beat * Rate);
            var mix = new float[n];
            var echo = new float[n];
            var rng = new System.Random(battle ? 11 : 5);
            float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * 4 * beat;
                var chord = chords[bar];

                // pad: soft detuned chord, swells in and out over the bar
                float padLen = 4 * beat;
                for (int v = 0; v < chord.Length; v++)
                    for (int det = -1; det <= 1; det += 2)
                        Note(mix, n, barStart, padLen + 0.6f, (t, ph) =>
                        {
                            float env = Mathf.Clamp01(t / 0.45f) * Mathf.Clamp01((padLen + 0.6f - t) / 0.8f);
                            float x = Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2f) + 0.12f * Mathf.Sin(ph * 3f);
                            return x * env * (battle ? 0.045f : 0.06f);
                        }, chord[v] * (1f + det * 0.004f));

                // bass: root, plucky eighths
                float root = chord[0] * 0.5f;
                int[] bassSteps = battle ? new[] { 0, 1, 2, 3, 4, 5, 6, 7 } : new[] { 0, 3, 4, 6 };
                foreach (int st in bassSteps)
                {
                    float f = root * (st == 3 && !battle ? 2f : 1f);
                    Note(mix, n, barStart + st * beat * 0.5f, beat * 0.45f, (t, ph) =>
                    {
                        float x = Mathf.Sin(ph) + 0.4f * (float)System.Math.Tanh(3f * Mathf.Sin(ph));
                        return x * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 400f) * (battle ? 0.26f : 0.22f);
                    }, f);
                }

                // arp: chord tones in 16ths an octave up, with an echo
                int[] pattern = { 0, 1, 2, 1, 2, 0, 1, 2 };
                int steps = battle ? 16 : 8;
                float stepLen = 4 * beat / steps;
                for (int s = 0; s < steps; s++)
                {
                    float f = chord[pattern[s % pattern.Length]] * 2f;
                    Note(mix, n, barStart + s * stepLen, stepLen * 2f, (t, ph) =>
                    {
                        float x = Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 2f);
                        return x * Mathf.Exp(-t * (battle ? 16f : 9f)) * Mathf.Clamp01(t * 600f) * (battle ? 0.05f : 0.06f);
                    }, f);
                    Note(echo, n, barStart + s * stepLen + stepLen * 3f, stepLen * 2f, (t, ph) =>
                        Mathf.Sin(ph) * Mathf.Exp(-t * 10f) * Mathf.Clamp01(t * 600f) * 0.025f, f);
                }

                // drums
                for (int b = 0; b < 4; b++)
                {
                    float bt = barStart + b * beat;
                    bool kick = battle || b == 0 || b == 2;
                    if (kick) Kick(mix, n, bt, battle ? 0.55f : 0.45f);
                    if (!battle && b == 2) Kick(mix, n, bt + beat * 0.5f, 0.3f);
                    if (b == 1 || b == 3) Snare(mix, n, bt, battle ? 0.32f : 0.2f, Noise);
                    for (int h = 0; h < 2; h++)
                    {
                        float swing = h == 1 && !battle ? beat * 0.08f : 0f;
                        Hat(mix, n, bt + h * beat * 0.5f + swing, h == 1 && b == 3 && !battle ? 12f : 60f, battle ? 0.07f : 0.05f, Noise);
                    }
                    if (battle) Hat(mix, n, bt + beat * 0.25f, 70f, 0.035f, Noise);
                }
            }

            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                mix[i] = (mix[i] + echo[i]) * 1.2f;
                mix[i] = mix[i] / (1f + Mathf.Abs(mix[i]));
                peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
            }
            float k = 0.85f / peak;
            for (int i = 0; i < n; i++) mix[i] *= k;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(mix, 0);
            return clip;
        }

        private delegate float Voice(float t, float phase);

        /// <summary>Writes one note starting at <paramref name="start"/> seconds, wrapping past the loop end.</summary>
        private static void Note(float[] buf, int n, float start, float dur, Voice voice, float freq)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int len = Mathf.RoundToInt(dur * Rate);
            float w = 2f * Mathf.PI * freq / Rate;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                buf[(s0 + i) % n] += voice(t, w * i);
            }
        }

        private static void Kick(float[] buf, int n, float start, float amp)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int len = (int)(0.32f * Rate);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(130f, 42f, Mathf.Clamp01(t / 0.12f)) / Rate;
                buf[(s0 + i) % n] += Mathf.Sin(phase) * Mathf.Exp(-t * 12f) * amp;
            }
        }

        private static void Snare(float[] buf, int n, float start, float amp, System.Func<float> noise)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int len = (int)(0.25f * Rate);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float x = noise();
                lp += 0.35f * (x - lp);
                buf[(s0 + i) % n] += ((x - lp) * Mathf.Exp(-t * 16f) + Mathf.Sin(2f * Mathf.PI * 185f * t) * 0.5f * Mathf.Exp(-t * 22f)) * amp;
            }
        }

        private static void Hat(float[] buf, int n, float start, float decay, float amp, System.Func<float> noise)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int len = (int)(Mathf.Min(0.4f, 5f / decay) * Rate);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float x = noise();
                lp += 0.7f * (x - lp);
                buf[(s0 + i) % n] += (x - lp) * Mathf.Exp(-t * decay) * amp;
            }
        }
    }
}
