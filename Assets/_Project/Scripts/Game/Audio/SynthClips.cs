using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Placeholder sound effects synthesized at startup (like neon-brawl's audio.js), ~0.6 MB total.
    /// Replaced by real recorded SFX in a later milestone; ids stay the same.
    /// </summary>
    public static class SynthClips
    {
        private const int Rate = 22050;
        private static System.Random _rng = new System.Random(7);

        public static AudioClip[] CreateAll()
        {
            var c = new AudioClip[(int)SfxId.Count];
            c[(int)SfxId.ShotLight] = Gun("shot_light", 0.16f, 0.9f, 2600f, 170f, 70f, 0.45f, 28f, 0.5f);
            c[(int)SfxId.ShotHeavy] = Gun("shot_heavy", 0.24f, 1f, 1800f, 130f, 50f, 0.7f, 19f, 0.6f);
            c[(int)SfxId.ShotShotgun] = Gun("shot_shotgun", 0.4f, 1.15f, 1200f, 110f, 40f, 0.85f, 10f, 0.7f);
            c[(int)SfxId.ShotSniper] = Gun("shot_sniper", 0.6f, 1f, 3200f, 120f, 38f, 0.8f, 8.5f, 1f);
            c[(int)SfxId.Rocket] = Whoosh("rocket", 0.5f, 400f, 1400f, 0.9f, 90f);
            c[(int)SfxId.Plasma] = Zap("plasma", 0.2f, 1300f, 250f);
            c[(int)SfxId.Tesla] = Buzz("tesla", 0.16f);
            c[(int)SfxId.Flame] = Roar("flame", 0.18f);
            c[(int)SfxId.Explosion] = Boom("explosion", 1.25f);
            c[(int)SfxId.HitFlesh] = Thud("hit", 0.1f, 260f, 90f, 40f);
            c[(int)SfxId.HitWall] = Click("hit_wall", 0.06f, 2600f, 80f);
            c[(int)SfxId.Reload] = DoubleClick("reload", 0.42f, 0.22f);
            c[(int)SfxId.Empty] = Click("empty", 0.06f, 1800f, 110f);
            c[(int)SfxId.Pickup] = Arp("pickup", new[] { 660f, 880f, 1320f }, 0.075f);
            c[(int)SfxId.Dash] = Whoosh("dash", 0.22f, 500f, 2600f, 0.7f, 0f);
            c[(int)SfxId.KillDing] = Bell("kill", 0.5f, 1320f);
            c[(int)SfxId.HitTick] = Tone("hit_tick", 0.045f, 3000f, 100f);
            c[(int)SfxId.GrenadeBounce] = Click("bounce", 0.09f, 1700f, 45f);
            c[(int)SfxId.Throw] = Whoosh("throw", 0.18f, 600f, 2200f, 0.6f, 0f);
            c[(int)SfxId.Beep] = Tone("beep", 0.16f, 880f, 14f);
            c[(int)SfxId.Go] = Bell("go", 0.5f, 660f);
            c[(int)SfxId.Swing] = Whoosh("swing", 0.15f, 900f, 3200f, 0.55f, 0f);
            c[(int)SfxId.Death] = Thud("death", 0.38f, 140f, 45f, 9f);
            c[(int)SfxId.Respawn] = Sweep("respawn", 0.32f, 440f, 1100f);
            c[(int)SfxId.Heartbeat] = Heart("heartbeat");
            c[(int)SfxId.Click] = Click("click", 0.04f, 2200f, 120f);
            c[(int)SfxId.UiTap] = Blip("ui_tap", 0.09f, 520f, 880f);
            c[(int)SfxId.UiBack] = Blip("ui_back", 0.1f, 760f, 430f);
            c[(int)SfxId.UiWhoosh] = Whoosh("ui_whoosh", 0.26f, 350f, 3200f, 0.45f, 0f);
            c[(int)SfxId.UiCoin] = Chime("ui_coin", new[] { 1568f, 2093f }, 0.07f, 0.35f);
            c[(int)SfxId.UiError] = Buzzer("ui_error", 0.22f);
            c[(int)SfxId.UiOpen] = Chime("ui_open", new[] { 523f, 659f, 784f, 1047f }, 0.06f, 0.45f);
            c[(int)SfxId.UiLevelUp] = Chime("ui_level", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.09f, 0.7f);
            c[(int)SfxId.Step] = Step("step", 0.12f);
            c[(int)SfxId.Crackle] = Crackle("crackle", 0.5f);
            c[(int)SfxId.Thump] = Thud("thump", 0.18f, 150f, 60f, 20f);
            return c;
        }

        /// <summary>A boot on dirt: a soft low thud with a little gravel on top.</summary>
        private static AudioClip Step(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, a = Alpha(650f), hp = 0f, a2 = Alpha(2500f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float nz = Noise();
                lp += a * (nz - lp);
                hp += a2 * (nz - hp);
                float attack = Mathf.Clamp01(t * 700f);
                float body = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 55f);
                d[i] = attack * (lp * 1.6f * Mathf.Exp(-t * 38f) + (nz - hp) * 0.18f * Mathf.Exp(-t * 70f) + body * 0.45f);
            }
            return Make(name, d);
        }

        /// <summary>Campfire: a few sharp wood pops at random moments.</summary>
        private static AudioClip Crackle(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int k = 0; k < 7; k++)
            {
                int start = (int)(_rng.NextDouble() * (n - 900));
                float amp = 0.35f + (float)_rng.NextDouble() * 0.65f;
                float hp = 0f, a = Alpha(1800f);
                for (int i = 0; i < 900 && start + i < n; i++)
                {
                    float t = (float)i / Rate;
                    float nz = Noise();
                    hp += a * (nz - hp);
                    d[start + i] += (nz - hp) * amp * Mathf.Exp(-t * 260f);
                }
            }
            return Make(name, d);
        }

        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);
        private static float Alpha(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);

        private static AudioClip Make(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float k = 0.9f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        private static AudioClip Gun(string name, float dur, float noiseAmp, float cutoff, float f0, float f1, float thumpAmp, float decay, float crack)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, a = Alpha(cutoff), phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float x = Noise();
                lp += a * (x - lp);
                float hp = x - lp;
                float freq = Mathf.Lerp(f0, f1, Mathf.Clamp01(t / (dur * 0.5f)));
                phase += 2f * Mathf.PI * freq / Rate;
                float s = lp * noiseAmp * Mathf.Exp(-t * decay)
                          + hp * crack * Mathf.Exp(-t * 70f)
                          + Mathf.Sin(phase) * thumpAmp * Mathf.Exp(-t * decay * 0.7f);
                d[i] = SoftClip(s * 1.6f);
            }
            return Make(name, d);
        }

        private static AudioClip Whoosh(string name, float dur, float lowCut, float highCut, float amp, float thumpHz)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lo = 0f, hi = 0f, aLo = Alpha(lowCut), aHi = Alpha(highCut), phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float x = Noise();
                hi += aHi * (x - hi);
                lo += aLo * (x - lo);
                float band = hi - lo;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                phase += 2f * Mathf.PI * thumpHz / Rate;
                d[i] = band * amp * env + (thumpHz > 0f ? Mathf.Sin(phase) * 0.4f * Mathf.Exp(-t * 6f) : 0f);
            }
            return Make(name, d);
        }

        private static AudioClip Zap(string name, float dur, float f0, float f1)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float freq = f0 * Mathf.Pow(f1 / f0, t / dur);
                phase += 2f * Mathf.PI * freq / Rate;
                float sq = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 3f);
                d[i] = (sq * 0.5f + Noise() * 0.08f) * Mathf.Exp(-t * 12f);
            }
            return Make(name, d);
        }

        private static AudioClip Buzz(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float gate = Mathf.Sin(2f * Mathf.PI * 95f * t) > 0f ? 1f : 0.25f;
                phase += 2f * Mathf.PI * (1800f + Noise() * 400f) / Rate;
                d[i] = (Noise() * 0.6f * gate + Mathf.Sin(phase) * 0.3f) * Mathf.Exp(-t * 16f);
            }
            return Make(name, d);
        }

        private static AudioClip Roar(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, a = Alpha(700f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += a * (Noise() - lp);
                d[i] = lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
            }
            return Make(name, d);
        }

        private static AudioClip Boom(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, a = Alpha(600f), a2 = Alpha(140f), phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float x = Noise();
                lp += a * (x - lp);
                lp2 += a2 * (x - lp2);
                phase += 2f * Mathf.PI * Mathf.Lerp(75f, 28f, Mathf.Clamp01(t / 0.6f)) / Rate;
                float s = lp * 1.1f * Mathf.Exp(-t * 3.2f)
                          + lp2 * 2.5f * Mathf.Exp(-t * 2.2f)
                          + Mathf.Sin(phase) * 1f * Mathf.Exp(-t * 4f)
                          + (x - lp) * 0.6f * Mathf.Exp(-t * 40f);
                d[i] = SoftClip(s * 2f);
            }
            return Make(name, d);
        }

        private static AudioClip Thud(string name, float dur, float f0, float f1, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, a = Alpha(1500f), phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += a * (Noise() - lp);
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Clamp01(t / dur)) / Rate;
                d[i] = Mathf.Sin(phase) * 0.8f * Mathf.Exp(-t * decay) + lp * 0.45f * Mathf.Exp(-t * decay * 1.3f);
            }
            return Make(name, d);
        }

        private static AudioClip Click(string name, float dur, float freq, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0f, a = Alpha(3000f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float x = Noise();
                lp += a * (x - lp);
                d[i] = (x - lp) * 0.6f * Mathf.Exp(-t * decay * 1.4f) + Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f * Mathf.Exp(-t * decay);
            }
            return Make(name, d);
        }

        private static AudioClip DoubleClick(string name, float dur, float second)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                float t1 = t, t2 = t - second;
                s += (Noise() * 0.5f + Mathf.Sin(2f * Mathf.PI * 2100f * t1) * 0.4f) * Mathf.Exp(-t1 * 110f);
                if (t2 >= 0f) s += (Noise() * 0.6f + Mathf.Sin(2f * Mathf.PI * 1500f * t2) * 0.5f) * Mathf.Exp(-t2 * 90f);
                d[i] = s;
            }
            return Make(name, d);
        }

        private static AudioClip Arp(string name, float[] notes, float noteLen)
        {
            int n = (int)(notes.Length * noteLen * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                int k = Mathf.Min(notes.Length - 1, (int)(t / noteLen));
                float lt = t - k * noteLen;
                float ph = 2f * Mathf.PI * notes[k] * t;
                d[i] = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 3f)) * Mathf.Exp(-lt * 18f) * Mathf.Clamp01(lt * 400f);
            }
            return Make(name, d);
        }

        private static AudioClip Bell(string name, float dur, float f)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float ph = 2f * Mathf.PI * f * t;
                d[i] = (Mathf.Sin(ph) * 0.55f + Mathf.Sin(ph * 1.5f) * 0.3f + Mathf.Sin(ph * 2f) * 0.12f) * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 600f);
            }
            return Make(name, d);
        }

        private static AudioClip Tone(string name, float dur, float f, float decay)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 800f);
            }
            return Make(name, d);
        }

        private static AudioClip Sweep(string name, float dur, float f0, float f1)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t / dur) / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t / dur) * 0.6f;
            }
            return Make(name, d);
        }

        private static AudioClip Heart(string name)
        {
            const float dur = 0.5f;
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 18f);
                float t2 = t - 0.17f;
                if (t2 > 0f) s += Mathf.Sin(2f * Mathf.PI * 50f * t2) * 0.8f * Mathf.Exp(-t2 * 18f);
                d[i] = s;
            }
            return Make(name, d);
        }

        /// <summary>Soft rounded UI blip: a quick pitch glide with a tiny click on top.</summary>
        private static AudioClip Blip(string name, float dur, float f0, float f1)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / dur;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sqrt(k)) / Rate;
                float env = Mathf.Clamp01(t * 900f) * Mathf.Exp(-t * 38f);
                d[i] = (Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 2f) * 0.15f) * env + Noise() * 0.12f * Mathf.Exp(-t * 400f);
            }
            return Make(name, d);
        }

        /// <summary>Bright bell notes in sequence (coins, unlocks, level up).</summary>
        private static AudioClip Chime(string name, float[] notes, float step, float tail)
        {
            float dur = step * (notes.Length - 1) + tail;
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * step * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float ph = 2f * Mathf.PI * notes[k] * t;
                    d[i] += (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2.01f) * 0.2f + Mathf.Sin(ph * 3f) * 0.08f) * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 900f);
                }
            }
            return Make(name, d);
        }

        /// <summary>Low double buzz for "can't do that".</summary>
        private static AudioClip Buzzer(string name, float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float gate = t < dur * 0.42f || t > dur * 0.55f ? 1f : 0f;
                float sq = Mathf.Sin(2f * Mathf.PI * 150f * t) > 0f ? 1f : -1f;
                d[i] = sq * 0.35f * gate * Mathf.Exp(-((t % (dur * 0.5f))) * 9f);
            }
            return Make(name, d);
        }
    }
}
