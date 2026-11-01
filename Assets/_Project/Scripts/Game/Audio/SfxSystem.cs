using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Pooled 2D sound effects. Gameplay sounds come from Resources/Sfx (several takes each, picked at
    /// random without repeating), interface sounds are synthesized. World sounds fade and pan with the
    /// distance from the camera, and far gunfire is muffled by a low-pass. Footsteps follow the ground
    /// type, and follow-up sounds (shotgun pump, sniper bolt, shell casings) can be scheduled.
    /// Perf: 0 B GC per play.
    /// </summary>
    public sealed class SfxSystem : MonoBehaviour
    {
        private const int Voices = 32;
        private const int MaxPending = 24;

        /// <summary>Resource file names (without the _N take number) for each id; null = synthesized.</summary>
        private static readonly string[] Files = BuildFileTable();

        private AudioSource[] _sources;
        private AudioLowPassFilter[] _lowPass;
        private int _next;
        private AudioClip[][] _clips;
        private int[] _lastTake;
        private float[] _last, _gap, _gain, _range;
        private bool[] _muffle;
        private GameContext _ctx;
        private AudioSource _music;
        private AudioClip _lobbyMusic, _battleMusic;
        private float _musicTarget, _musicLevel;
        private AudioClip _nextClip;

        private struct Pending { public SfxId Id; public Vector2 Pos; public float Volume, At; }
        private readonly Pending[] _pending = new Pending[MaxPending];
        private int _pendingCount;

        /// <summary>Extra volume factor (lower behind the menu).</summary>
        public float Duck { get; set; } = 1f;

        /// <summary>Ground under the fighters; picks the footstep sound.</summary>
        public Surface Ground { get; set; } = Surface.Grass;

        public SfxId StepSound => Ground switch
        {
            Surface.Sand => SfxId.StepSand,
            Surface.Snow => SfxId.StepSnow,
            Surface.Hard => SfxId.StepHard,
            _ => SfxId.Step,
        };

        /// <summary>Footstep surface for a map art theme (T1 forest … T5 camp).</summary>
        public static Surface SurfaceForTheme(int theme) => theme switch
        {
            1 => Surface.Sand,
            2 => Surface.Snow,
            3 => Surface.Hard,
            _ => Surface.Grass,
        };

        /// <summary>The gunshot for a weapon (every gun has its own voice).</summary>
        public static SfxId ShotFor(WeaponStats s) => s.Id switch
        {
            WeaponId.Pistol => SfxId.ShotLight,
            WeaponId.HandCannon => SfxId.ShotCannon,
            WeaponId.Smg => SfxId.ShotSmg,
            WeaponId.Rifle => SfxId.ShotHeavy,
            WeaponId.Lmg => SfxId.ShotLmg,
            WeaponId.Shotgun => SfxId.ShotShotgun,
            WeaponId.Sniper => SfxId.ShotSniper,
            _ => s.Sound,
        };

        private static string[] BuildFileTable()
        {
            var f = new string[(int)SfxId.Count];
            f[(int)SfxId.ShotLight] = "shot_pistol";
            f[(int)SfxId.ShotHeavy] = "shot_rifle";
            f[(int)SfxId.ShotShotgun] = "shot_shotgun";
            f[(int)SfxId.ShotSniper] = "shot_sniper";
            f[(int)SfxId.ShotSmg] = "shot_smg";
            f[(int)SfxId.ShotCannon] = "shot_cannon";
            f[(int)SfxId.ShotLmg] = "shot_lmg";
            f[(int)SfxId.Rocket] = "rocket";
            f[(int)SfxId.Plasma] = "plasma";
            f[(int)SfxId.Tesla] = "tesla";
            f[(int)SfxId.Flame] = "flame";
            f[(int)SfxId.Explosion] = "explosion";
            f[(int)SfxId.HitFlesh] = "hit_flesh";
            f[(int)SfxId.HitWall] = "hit_wall";
            f[(int)SfxId.Ricochet] = "ricochet";
            f[(int)SfxId.Reload] = "reload";
            f[(int)SfxId.Pump] = "pump";
            f[(int)SfxId.Bolt] = "bolt";
            f[(int)SfxId.Empty] = "empty";
            f[(int)SfxId.Click] = "swap";
            f[(int)SfxId.Pickup] = "pickup";
            f[(int)SfxId.Shell] = "shell";
            f[(int)SfxId.ShellPlastic] = "shell_plastic";
            f[(int)SfxId.GrenadeBounce] = "grenade_bounce";
            f[(int)SfxId.Death] = "death";
            f[(int)SfxId.Heartbeat] = "heartbeat";
            f[(int)SfxId.Swing] = "swing";
            f[(int)SfxId.Throw] = "throw";
            f[(int)SfxId.Dash] = "dash";
            f[(int)SfxId.Whiz] = "whiz";
            f[(int)SfxId.Step] = "step_grass";
            f[(int)SfxId.StepSand] = "step_sand";
            f[(int)SfxId.StepSnow] = "step_snow";
            f[(int)SfxId.StepHard] = "step_hard";
            return f;
        }

        public void Init(GameContext ctx)
        {
            _ctx = ctx;
            int n = (int)SfxId.Count;
            _clips = LoadClips(SynthClips.CreateAll(), n);
            _lastTake = new int[n];
            _last = new float[n];
            _gap = new float[n];
            _gain = new float[n];
            _range = new float[n];
            _muffle = new bool[n];
            for (int i = 0; i < n; i++) { _gap[i] = 0.03f; _gain[i] = 0.8f; _last[i] = -1f; _range[i] = 1f; _lastTake[i] = -1; }

            // gunfire: loud, heard far away, muffled with distance
            SetGun(SfxId.ShotLight, 0.82f);
            SetGun(SfxId.ShotSmg, 0.72f);
            SetGun(SfxId.ShotHeavy, 0.85f);
            SetGun(SfxId.ShotLmg, 0.85f);
            SetGun(SfxId.ShotCannon, 0.95f);
            SetGun(SfxId.ShotShotgun, 0.95f);
            SetGun(SfxId.ShotSniper, 1f);
            SetGun(SfxId.Rocket, 0.85f);
            SetGun(SfxId.Plasma, 0.7f);
            SetGun(SfxId.Explosion, 1f);
            _range[(int)SfxId.Explosion] = 2.4f;
            _gap[(int)SfxId.ShotSmg] = 0.02f;
            _gap[(int)SfxId.ShotLmg] = 0.02f;

            _gap[(int)SfxId.Flame] = 0.1f;
            _gain[(int)SfxId.Flame] = 0.55f;
            _gain[(int)SfxId.Tesla] = 0.6f;
            _gap[(int)SfxId.HitFlesh] = 0.05f;
            _gain[(int)SfxId.HitFlesh] = 0.75f;
            _gap[(int)SfxId.HitWall] = 0.04f;
            _gain[(int)SfxId.HitWall] = 0.55f;
            _gap[(int)SfxId.Ricochet] = 0.08f;
            _gain[(int)SfxId.Ricochet] = 0.5f;
            _gain[(int)SfxId.Reload] = 0.75f;
            _gain[(int)SfxId.Pump] = 0.75f;
            _gain[(int)SfxId.Bolt] = 0.75f;
            _gain[(int)SfxId.Empty] = 0.7f;
            _gap[(int)SfxId.Pickup] = 0.08f;
            _gain[(int)SfxId.Pickup] = 0.7f;
            _gain[(int)SfxId.Shell] = 0.3f;
            _gain[(int)SfxId.ShellPlastic] = 0.3f;
            _gap[(int)SfxId.Shell] = 0.05f;
            _gap[(int)SfxId.ShellPlastic] = 0.05f;
            _range[(int)SfxId.Shell] = 0.5f;
            _range[(int)SfxId.ShellPlastic] = 0.5f;
            _gain[(int)SfxId.Whiz] = 0.6f;
            _gap[(int)SfxId.Whiz] = 0.12f;
            _gain[(int)SfxId.GrenadeBounce] = 0.7f;
            _gain[(int)SfxId.Death] = 0.85f;
            _gain[(int)SfxId.HitTick] = 0.5f;
            _gain[(int)SfxId.KillDing] = 0.6f;
            _gain[(int)SfxId.Heartbeat] = 0.9f;
            _gain[(int)SfxId.Dash] = 0.55f;
            _gain[(int)SfxId.Swing] = 0.7f;
            _gain[(int)SfxId.Throw] = 0.6f;
            _gain[(int)SfxId.Click] = 0.6f;
            _gain[(int)SfxId.UiTap] = 0.55f;
            _gain[(int)SfxId.UiBack] = 0.5f;
            _gain[(int)SfxId.UiWhoosh] = 0.35f;
            _gain[(int)SfxId.UiCoin] = 0.5f;
            _gain[(int)SfxId.UiError] = 0.45f;
            _gain[(int)SfxId.UiOpen] = 0.6f;
            _gain[(int)SfxId.UiLevelUp] = 0.7f;
            _gap[(int)SfxId.UiCoin] = 0.06f;
            foreach (var step in new[] { SfxId.Step, SfxId.StepSand, SfxId.StepSnow, SfxId.StepHard })
            {
                _gain[(int)step] = 0.5f;
                _gap[(int)step] = 0.05f;
                _range[(int)step] = 0.55f;
            }
            _gain[(int)SfxId.Crackle] = 0.35f;
            _gap[(int)SfxId.Crackle] = 0.3f;
            _gain[(int)SfxId.Thump] = 0.55f;
            _gap[(int)SfxId.Thump] = 0.06f;

            _sources = new AudioSource[Voices];
            _lowPass = new AudioLowPassFilter[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources[i] = src;
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f;
                lp.lowpassResonanceQ = 1f;
                _lowPass[i] = lp;
            }

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = 0f;
            _lobbyMusic = SynthMusic.Lobby();
            _battleMusic = SynthMusic.Battle();
        }

        private void SetGun(SfxId id, float gain)
        {
            _gain[(int)id] = gain;
            _range[(int)id] = 2f;
            _muffle[(int)id] = true;
        }

        /// <summary>Takes each id's files (name_0, name_1, …) from Resources/Sfx; falls back to the synth clip.</summary>
        private static AudioClip[][] LoadClips(AudioClip[] synth, int n)
        {
            var all = Resources.LoadAll<AudioClip>("Sfx");
            var clips = new AudioClip[n][];
            var takes = new System.Collections.Generic.List<AudioClip>(8);
            for (int i = 0; i < n; i++)
            {
                takes.Clear();
                string f = Files[i];
                if (f != null)
                    for (int k = 0; k < all.Length; k++)
                    {
                        string name = all[k].name;
                        int us = name.LastIndexOf('_');
                        if (us == f.Length && name.StartsWith(f, System.StringComparison.Ordinal) && int.TryParse(name.Substring(us + 1), out _))
                            takes.Add(all[k]);
                    }
                if (takes.Count == 0 && i < synth.Length && synth[i] != null) takes.Add(synth[i]);
                takes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                clips[i] = takes.ToArray();
            }
            return clips;
        }

        /// <summary>Crossfades to the lobby groove (menus) or the battle loop (matches); null fades out.</summary>
        public void PlayMusic(bool battle)
        {
            var clip = battle ? _battleMusic : _lobbyMusic;
            if (_music.clip == clip && _music.isPlaying) { _musicTarget = battle ? 0.4f : 0.7f; return; }
            _nextClip = clip;
            _musicTarget = battle ? 0.4f : 0.7f;
        }

        public void StopMusic() { _nextClip = null; _musicTarget = 0f; }

        private void Update()
        {
            if (_music == null || _ctx == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_nextClip != null && _music.clip != _nextClip)
            {
                _musicLevel = Mathf.MoveTowards(_musicLevel, 0f, dt * 1.6f);
                if (_musicLevel <= 0.001f)
                {
                    _music.clip = _nextClip;
                    _music.Play();
                }
            }
            else _musicLevel = Mathf.MoveTowards(_musicLevel, _musicTarget, dt * 0.8f);
            _music.volume = _musicLevel * _ctx.Prefs.MusicVolume;
            if (_musicLevel <= 0.001f && _musicTarget <= 0f && _music.isPlaying) _music.Stop();

            // scheduled follow-ups (pump, bolt, casings)
            float now = Time.time;
            for (int i = _pendingCount - 1; i >= 0; i--)
            {
                if (_pending[i].At > now) continue;
                var p = _pending[i];
                _pending[i] = _pending[--_pendingCount];
                Play(p.Id, p.Pos, p.Volume);
            }
        }

        /// <summary>Plays a world sound a little later (game time, so slow-mo slows it too).</summary>
        public void PlayLater(SfxId id, Vector2 pos, float delay, float volume = 1f)
        {
            if (_pendingCount >= MaxPending) return;
            _pending[_pendingCount++] = new Pending { Id = id, Pos = pos, Volume = volume, At = Time.time + delay };
        }

        /// <summary>Drops everything scheduled (match end, leaving to the menu).</summary>
        public void ClearPending() => _pendingCount = 0;

        /// <summary>World sound: fades with distance from the camera, pans left/right, far gunfire gets muffled.</summary>
        public void Play(SfxId id, Vector2 pos, float volume = 1f, float pitchVariation = 0.05f)
        {
            if (_ctx == null || _ctx.Camera == null) return;
            int i = (int)id;
            Vector2 cam = _ctx.Camera.transform.position;
            float d = Vector2.Distance(pos, cam);
            float near = 8f * _range[i], far = 26f * _range[i];
            float t = Mathf.Clamp01((d - near) / (far - near));
            float att = (1f - t) * (1f - t * 0.35f);
            if (att <= 0.01f) return;
            float pan = Mathf.Clamp((pos.x - cam.x) / 14f, -0.7f, 0.7f);
            float cutoff = _muffle[i] ? Mathf.Lerp(22000f, 1600f, Mathf.Sqrt(t)) : 22000f;
            PlayInternal(id, volume * att, pan, pitchVariation, true, cutoff);
        }

        /// <summary>Interface sound: no distance fade.</summary>
        public void PlayUi(SfxId id, float volume = 1f) => PlayInternal(id, volume, 0f, 0f, false, 22000f);

        private void PlayInternal(SfxId id, float volume, float pan, float pitchVariation, bool duck, float cutoff)
        {
            int i = (int)id;
            var takes = _clips[i];
            if (takes == null || takes.Length == 0) return;
            float now = Time.unscaledTime;
            if (now - _last[i] < _gap[i]) return;
            _last[i] = now;

            // a random take, never the same one twice in a row
            int take = 0;
            if (takes.Length > 1)
            {
                if (_lastTake[i] < 0) take = Random.Range(0, takes.Length);
                else
                {
                    take = Random.Range(0, takes.Length - 1);
                    if (take >= _lastTake[i]) take++;
                }
                _lastTake[i] = take;
            }

            var src = _sources[_next];
            _lowPass[_next].cutoffFrequency = cutoff;
            _next = (_next + 1) % Voices;
            src.clip = takes[take];
            src.volume = Mathf.Clamp01(volume * _gain[i] * _ctx.Prefs.SfxVolume * (duck ? Duck : 1f));
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.panStereo = pan;
            src.Play();
        }
    }
}
